using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Infrastructure.Catalog;

/// <summary>
/// Katalogu SQLite'ta tutar. Okuma yolu ikiye ayrılır:
/// <list type="bullet">
/// <item><see cref="All"/>, <see cref="Find"/> ve <see cref="Count"/> bellekteki anlık
/// görüntüden gelir. Eşleştirici her satırda tüm katalogu geziyor ve dizinini
/// <see cref="All"/> örneğine göre önbelleğe alıyor; bunu her çözümlemede veritabanından
/// okumak hem yavaş olur hem de önbelleği bozardı.</item>
/// <item><see cref="Search"/> doğrudan veritabanına sorgu atar (kullanıcı araması, seyrek).</item>
/// </list>
/// Anlık görüntü ilk kullanımda yüklenir ve yalnızca <see cref="Replace"/> ile tazelenir.
/// Tek sunucu varsayımıyla doğrudur; ikinci bir süreç aynı veritabanına yazarsa tazelenmez.
/// </summary>
public sealed class EfCatalogStore : ICatalogStore
{
    private readonly IDbContextFactory<OrderAgentDbContext> _contextFactory;
    private readonly TimeProvider _time;
    private readonly InMemoryCatalogStore _snapshot = new();
    private readonly Lock _gate = new();
    private volatile bool _loaded;

    public EfCatalogStore(IDbContextFactory<OrderAgentDbContext> contextFactory, TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _time = timeProvider ?? TimeProvider.System;
    }

    public int Count
    {
        get
        {
            EnsureLoaded();
            return _snapshot.Count;
        }
    }

    public IReadOnlyList<CatalogItem> All
    {
        get
        {
            EnsureLoaded();
            return _snapshot.All;
        }
    }

    public CatalogItem? Find(string sku)
    {
        EnsureLoaded();
        return _snapshot.Find(sku);
    }

    public IReadOnlyList<CatalogItem> Search(string? query, int limit)
    {
        int take = Math.Clamp(limit, 1, 100);
        using OrderAgentDbContext context = _contextFactory.CreateDbContext();

        IQueryable<CatalogItemEntity> search = context.CatalogItems.AsNoTracking().OrderBy(item => item.Id);

        // Her belirteç ayrı LIKE koşuludur: hepsi geçmelidir ("sunta vidasi" → sunta VE vidasi).
        // Belirteçler TurkishTextNormalizer'dan geliyor; harf ve rakam dışındaki karakterler
        // (LIKE joker karakterleri % ve _ dâhil) orada boşluğa çevrildiği için kaçış gerekmiyor.
        foreach (string token in TurkishTextNormalizer.Tokenize(query))
        {
            string pattern = $"%{token}%";
            search = search.Where(item => EF.Functions.Like(
                item.NormalizedName + " " + item.Sku + " " + (item.Barcode ?? string.Empty),
                pattern));
        }

        return [.. search.Take(take).Select(item => item.ToModel())];
    }

    public void Replace(IEnumerable<CatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        List<CatalogItem> incoming = [.. items];
        DateTimeOffset now = _time.GetUtcNow();

        using OrderAgentDbContext context = _contextFactory.CreateDbContext();
        using IDbContextTransaction transaction = context.Database.BeginTransaction();

        // İçe aktarma katalogun tamamını değiştirir (sözleşme: CatalogImportResultDto).
        context.CatalogItems.ExecuteDelete();
        context.CatalogItems.AddRange(incoming.Select(item => CatalogItemEntity.FromModel(item, now)));
        context.SaveChanges();
        transaction.Commit();

        lock (_gate)
        {
            _snapshot.Replace(incoming);
            _loaded = true;
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            using OrderAgentDbContext context = _contextFactory.CreateDbContext();
            List<CatalogItem> items =
            [
.. context.CatalogItems
.AsNoTracking()
.OrderBy(item => item.Id)
.Select(item => item.ToModel())
            ];

            _snapshot.Replace(items);
            _loaded = true;
        }
    }
}
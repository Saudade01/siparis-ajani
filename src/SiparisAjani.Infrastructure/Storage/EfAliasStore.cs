using Microsoft.EntityFrameworkCore;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Infrastructure.Storage;

/// <summary>
/// Kullanıcı düzeltmelerini SQLite'ta tutar. Anahtar normalleştirilmiş ürün metnidir
/// (benzersiz indeks); ham müşteri mesajı saklanmaz. Aynı metin tekrar kaydedilirse
/// satır güncellenir, yenisi eklenmez.
/// </summary>
public sealed class EfAliasStore(IDbContextFactory<OrderAgentDbContext> contextFactory, TimeProvider? timeProvider = null)
    : IAliasStore
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public string? Resolve(string productText)
    {
        string key = TurkishTextNormalizer.Normalize(productText);
        if (key.Length == 0)
        {
            return null;
        }

        using OrderAgentDbContext context = contextFactory.CreateDbContext();
        return context.ProductAliases
.AsNoTracking()
.Where(alias => alias.NormalizedProductText == key)
.Select(alias => alias.Sku)
.FirstOrDefault();
    }

    public async Task SaveAsync(string productText, string sku, CancellationToken cancellationToken = default)
    {
        string key = TurkishTextNormalizer.Normalize(productText);
        if (key.Length == 0 || string.IsNullOrWhiteSpace(sku))
        {
            return;
        }

        await using OrderAgentDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        ProductAliasEntity? existing = await context.ProductAliases
.FirstOrDefaultAsync(alias => alias.NormalizedProductText == key, cancellationToken);

        if (existing is null)
        {
            context.ProductAliases.Add(new ProductAliasEntity
            {
                NormalizedProductText = key,
                Sku = sku.Trim(),
                UpdatedAt = _time.GetUtcNow()
            });
        }
        else
        {
            existing.Sku = sku.Trim();
            existing.UpdatedAt = _time.GetUtcNow();
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
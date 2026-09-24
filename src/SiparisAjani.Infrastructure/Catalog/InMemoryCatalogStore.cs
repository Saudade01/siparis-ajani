using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Infrastructure.Catalog;

/// <summary>
/// Katalogu bellekte tutar (ADR-0001, karar 4: şimdilik veritabanı yok).
/// <see cref="All"/> her zaman değişmez bir anlık görüntü döner; eşleştirici bu örneğe göre
/// dizinini önbelleğe alabilsin diye katalog değişmedikçe aynı nesne verilir.
/// </summary>
public sealed class InMemoryCatalogStore : ICatalogStore
{
    private readonly Lock _gate = new();
    private IReadOnlyList<CatalogItem> _items = [];
    private Dictionary<string, CatalogItem> _bySku = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _items.Count;

    public IReadOnlyList<CatalogItem> All => _items;

    public CatalogItem? Find(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        return _bySku.TryGetValue(sku.Trim(), out CatalogItem? item) ? item : null;
    }

    public IReadOnlyList<CatalogItem> Search(string? query, int limit)
    {
        int take = Math.Clamp(limit, 1, 100);
        IReadOnlyList<CatalogItem> snapshot = _items;

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. snapshot.Take(take)];
        }

        string[] terms = [.. TurkishTextNormalizer.Tokenize(query)];
        if (terms.Length == 0)
        {
            return [.. snapshot.Take(take)];
        }

        List<CatalogItem> hits = [];
        foreach (CatalogItem item in snapshot)
        {
            string haystack = $"{TurkishTextNormalizer.Normalize(item.Name)} {TurkishTextNormalizer.Normalize(item.Sku)} {item.Barcode}";
            if (terms.All(term => haystack.Contains(term, StringComparison.Ordinal)))
            {
                hits.Add(item);
                if (hits.Count == take)
                {
                    break;
                }
            }
        }

        return hits;
    }

    public void Replace(IEnumerable<CatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        List<CatalogItem> snapshot = [.. items];
        Dictionary<string, CatalogItem> index = new(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogItem item in snapshot)
        {
            index[item.Sku] = item;
        }

        lock (_gate)
        {
            _items = snapshot;
            _bySku = index;
        }
    }
}
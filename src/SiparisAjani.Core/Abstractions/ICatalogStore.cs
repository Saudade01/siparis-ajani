using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>Ürün kataloğunun okunması ve değiştirilmesi. Kalıcılık ayrıntısı Infrastructure'da.</summary>
public interface ICatalogStore
{
    int Count { get; }

    /// <summary>O anki katalogun değişmez anlık görüntüsü.</summary>
    IReadOnlyList<CatalogItem> All { get; }

    /// <summary>Boş sorguda ilk <paramref name="limit"/> ürünü döner.</summary>
    IReadOnlyList<CatalogItem> Search(string? query, int limit);

    CatalogItem? Find(string sku);

    /// <summary>Katalogu tümüyle değiştirir.</summary>
    void Replace(IEnumerable<CatalogItem> items);
}
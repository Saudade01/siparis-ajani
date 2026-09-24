using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>Ürün metnini katalogla eşleştirir ve güven skoruna göre durum üretir.</summary>
public interface IProductMatcher
{
    ProductMatchResult Match(string productText, IReadOnlyList<CatalogItem> catalog);
}
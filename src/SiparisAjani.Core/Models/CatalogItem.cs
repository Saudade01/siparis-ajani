using SiparisAjani.Contracts;

namespace SiparisAjani.Core.Models;

/// <summary>Katalogdaki tek bir ürün. Saf veri; kalıcılık ayrıntısı içermez.</summary>
public sealed record CatalogItem(string Sku, string Name, string Unit, string? Barcode, string? Category)
{
    public CatalogItemDto ToDto() => new(Sku, Name, Unit, Barcode, Category);
}
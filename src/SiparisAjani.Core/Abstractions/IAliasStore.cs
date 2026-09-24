namespace SiparisAjani.Core.Abstractions;

/// <summary>
/// Kullanıcı düzeltmelerini (ürün metni → stok kodu) saklar.
/// Anahtar normalleştirilmiş ürün metnidir; ham müşteri metni saklanmaz.
/// </summary>
public interface IAliasStore
{
    /// <summary>Verilen ürün metni için daha önce öğrenilmiş stok kodunu döner.</summary>
    string? Resolve(string productText);

    Task SaveAsync(string productText, string sku, CancellationToken cancellationToken = default);
}
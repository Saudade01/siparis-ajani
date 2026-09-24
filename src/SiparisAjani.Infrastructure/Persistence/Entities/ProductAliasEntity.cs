namespace SiparisAjani.Infrastructure.Persistence.Entities;

/// <summary>
/// Kullanıcı düzeltmesi: "japon yapıştırıcı" → <c>YP-HZL-20</c>.
/// Anahtar normalleştirilmiş ürün metnidir; ham müşteri mesajı saklanmaz.
/// </summary>
public sealed class ProductAliasEntity
{
    public int Id { get; set; }

    /// <summary>Normalleştirilmiş ürün metni. Benzersiz indeksi vardır (aynı metin tek satır).</summary>
    public string NormalizedProductText { get; set; } = string.Empty;

    /// <summary>
    /// Eşlenen stok kodu. Kataloğa yabancı anahtar BİLİNÇLİ olarak konmadı: katalog içe aktarımı
    /// ürünleri tümüyle değiştiriyor, yabancı anahtar olsaydı her içe aktarım öğrenilen
    /// düzeltmeleri silerdi. Kodu katalogda bulunamayan düzeltme çözümleme sırasında yok sayılır.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
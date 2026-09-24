using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Infrastructure.Persistence.Entities;

/// <summary>
/// Katalog ürününün veritabanı karşılığı. <see cref="CatalogItem"/> saf modeldir;
/// bu tip yalnızca kalıcılık ayrıntılarını (kimlik, normalleştirilmiş ad, zaman damgası) taşır.
/// </summary>
public sealed class CatalogItemEntity
{
    public int Id { get; set; }

    /// <summary>Stok kodu. Benzersiz indeksi vardır.</summary>
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Aramada kullanılan sadeleştirilmiş ad (tr-TR küçük harf, Türkçe harfler ASCII'ye katlanmış).
    /// Üzerinde indeks vardır; "Vidası" yazan kullanıcı "vidasi" ile de bulsun diye tutulur.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;

    public string Unit { get; set; } = "adet";

    public string? Barcode { get; set; }

    public string? Category { get; set; }

    /// <summary>Satırın kataloğa yazıldığı an (içe aktarma zamanı).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public CatalogItem ToModel() => new(Sku, Name, Unit, Barcode, Category);

    public static CatalogItemEntity FromModel(CatalogItem item, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CatalogItemEntity
        {
            Sku = item.Sku,
            Name = item.Name,
            NormalizedName = TurkishTextNormalizer.Normalize(item.Name),
            Unit = item.Unit,
            Barcode = item.Barcode,
            Category = item.Category,
            UpdatedAt = updatedAt
        };
    }
}
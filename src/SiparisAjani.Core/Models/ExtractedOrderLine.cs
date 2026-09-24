namespace SiparisAjani.Core.Models;

/// <summary>Ayrıştırıcının ürettiği ham sipariş satırı (henüz katalogla eşleştirilmemiş).</summary>
/// <param name="RawText">Satırın maskelenmiş kaynak metni.</param>
/// <param name="Quantity">Bulunan miktar; bulunamadıysa null.</param>
/// <param name="Unit">Bulunan birim (adet, kutu, paket...); bulunamadıysa null.</param>
/// <param name="ProductText">Eşleştirmede kullanılacak ürün metni.</param>
public sealed record ExtractedOrderLine(string RawText, decimal? Quantity, string? Unit, string ProductText);

/// <summary>Ayrıştırma sonucu: satırlar ve kullanıcıya gösterilebilir Türkçe uyarılar.</summary>
public sealed record ExtractionResult(IReadOnlyList<ExtractedOrderLine> Lines, IReadOnlyList<string> Warnings)
{
    public static ExtractionResult Empty { get; } = new([], []);
}
using System.Text.RegularExpressions;

using SiparisAjani.Core.Parsing;

namespace SiparisAjani.Core.Text;

/// <summary>Ürün satırı ayıklamasının sonucu.</summary>
/// <param name="Lines">Modele gönderilecek ürün satırı adayları.</param>
/// <param name="DroppedCount">Ayıklanan (atılan) satır sayısı.</param>
public sealed record ProductLineFilterResult(IReadOnlyList<string> Lines, int DroppedCount);

/// <summary>
/// Maskelenmiş metinden selamlaşma, imza, adres ve ödeme satırlarını ayıklar.
/// KVKK gereği modele ham mesaj değil yalnızca bu ürün satırı adayları gönderilir
/// (kimlik ve imza satırları yerelde kalır).
/// </summary>
public static partial class ProductLineFilter
{
    /// <summary>Satırı atmaya yeten kelimeler (normalleştirilmiş hâlleriyle aranır).</summary>
    private static readonly string[] NoiseKeywords =
    [
        "merhaba", "selam", "gunaydin", "iyi gunler", "iyi calismalar", "iyi aksamlar",
        "kolay gelsin", "tesekkur", "saygilar", "rica ederiz", "rica ederim", "sayin",
        "fatura", "odeme", "hesabindan", "hesaba", "iban", "tckn", "vergi dairesi",
        "e posta", "eposta", "irtibat", "adres", "teslimat", "numaram", "santiye sefi",
        "yetisirse", "slm", "hazirlarmisin", "gonderebilir misiniz"
    ];

    /// <summary>
    /// İmza satırlarındaki şirket ekleri. Alt dize olarak aranmaz: "sti" eki
    /// "yapıştırıcı" kelimesinin içinde de geçiyor ve ürün satırını yutuyordu.
    /// </summary>
    private static readonly HashSet<string> CompanySuffixes = new(StringComparer.Ordinal)
    {
        "ltd", "sti", "a.s", "as", "tic", "san", "sanayi"
    };

    /// <summary>Satır başında bulunduğunda tüm satırı atan başlık etiketleri.</summary>
    private static readonly string[] HeaderPrefixes =
    [
        "konu", "kimden", "kime", "tarih", "gonderen", "urun"
    ];

    /// <summary>Bu kelimeleri içeren satırdan sonrası imza bloğu sayılır ve tümüyle atılır.</summary>
    private static readonly string[] SignatureCutKeywords =
    [
        "saygilar", "tesekkur", "iyi calismalar"
    ];

    /// <summary>Metni satırlara böler ve ürün satırı adaylarını döner.</summary>
    public static ProductLineFilterResult Filter(string maskedText)
    {
        if (string.IsNullOrWhiteSpace(maskedText))
        {
            return new ProductLineFilterResult([], 0);
        }

        string[] rawLines = maskedText.Split(['\r', '\n'], StringSplitOptions.None);
        List<string> kept = [];
        int dropped = 0;
        bool inSignature = false;

        foreach (string rawLine in rawLines)
        {
            if (rawLine.Trim().Length == 0)
            {
                continue;
            }

            string cleaned = RemovePlaceholders(rawLine);
            if (inSignature)
            {
                dropped++;
                continue;
            }

            if (IsSignatureLine(cleaned))
            {
                inSignature = true;
                dropped++;
                continue;
            }

            if (IsNoiseSegment(cleaned))
            {
                dropped++;
                continue;
            }

            kept.Add(cleaned);
        }

        return new ProductLineFilterResult(kept, dropped);
    }

    /// <summary>
    /// Yer tutucuları ve onları tanıtan etiketi ("Tel:", "IBAN:", "TCKN") satırdan siler.
    /// Böylece "10 paket dübel. Tel: [telefon]" satırı ürün satırı olarak kalabilir.
    /// </summary>
    public static string RemovePlaceholders(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        string result = PlaceholderWithLabelRegex().Replace(line, " ");
        return CollapseSpacesRegex().Replace(result, " ").Trim();
    }

    /// <summary>Satır bir imza bloğunu başlatıyor mu ("Saygılarımızla", "Teşekkürler")?</summary>
    public static bool IsSignatureLine(string line)
    {
        string normalized = TurkishTextNormalizer.Normalize(line);
        return SignatureCutKeywords.Any(normalized.Contains);
    }

    /// <summary>
    /// Bir satır ya da satır parçası ürün olamayacak kadar "sohbet" mi?
    /// Miktar + birim taşıyan parçalar korunur: "Merhaba abi, 5 kutu vida" satırı atılmamalı.
    /// </summary>
    public static bool IsNoiseSegment(string segment)
    {
        string cleaned = segment.Trim();
        string normalized = TurkishTextNormalizer.Normalize(cleaned);
        if (normalized.Length < 3)
        {
            return true;
        }

        if (SeparatorRegex().IsMatch(cleaned))
        {
            return true;
        }

        // "Ürünler:", "Atölyemiz için:", "... gönderebilir misiniz:" gibi başlık ve soru satırları.
        if (cleaned[^1] is ':' or '?')
        {
            return true;
        }

        if (HeaderPrefixes.Any(prefix => normalized.StartsWith(prefix + " ", StringComparison.Ordinal))
            && cleaned.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        bool looksLikeCompany = normalized
.Split(' ', StringSplitOptions.RemoveEmptyEntries)
.Any(word => CompanySuffixes.Contains(word.Trim('.')));

        if (!looksLikeCompany && !NoiseKeywords.Any(normalized.Contains))
        {
            return false;
        }

        // Sohbet kelimesi var; ama miktar + birim de varsa bu bir sipariş satırıdır.
        return !HasQuantityWithUnit(normalized);
    }

    /// <summary>Metinde "5 kutu", "üç paket" gibi bir miktar + birim ikilisi var mı?</summary>
    public static bool HasQuantityWithUnit(string normalizedText)
    {
        string[] words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < words.Length - 1; index++)
        {
            if (OrderUnits.TryGetQuantity(words[index], out _) && OrderUnits.TryGetUnit(words[index + 1], out _))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"(?:\b[\p{L}]+(?:\s+[\p{L}]+)?\s*:\s*)?\[(?:telefon|e-posta|iban|kimlik|ad)\]", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderWithLabelRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex CollapseSpacesRegex();

    [GeneratedRegex(@"^[\s\-–—_*=<>]{3,}")]
    private static partial Regex SeparatorRegex();
}
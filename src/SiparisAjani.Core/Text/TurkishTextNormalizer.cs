using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SiparisAjani.Core.Text;

/// <summary>
/// Türkçe metin normalleştirme. Küçük harfe çevirme her zaman <c>tr-TR</c> kültürüyle yapılır
/// (aksi hâlde "İ" → "i̇" ve "I" → "i" dönüşümleri bozulur).
/// </summary>
public static partial class TurkishTextNormalizer
{
    /// <summary>Eşleştirmede anlam taşımayan ölçü birimleri; hem sorgudan hem ürün adından atılır.</summary>
    private static readonly HashSet<string> MeasurementTokens = new(StringComparer.Ordinal)
    {
        "mm", "cm", "m", "mt", "gr", "g", "kg", "lt", "l", "ml", "cc", "w", "watt", "v", "volt",
        "inc", "inch", "kum", "parca", "x", "kilo", "kiloluk", "litre", "litrelik",
        "lik", "luk", "li", "lu"
    };

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>tr-TR kurallarıyla küçük harfe çevirir.</summary>
    public static string ToLowerTr(string? value) => value is null ? string.Empty : value.ToLower(Culture);

    /// <summary>tr-TR kurallarıyla büyük harfe çevirir.</summary>
    public static string ToUpperTr(string? value) => value is null ? string.Empty : value.ToUpper(Culture);

    /// <summary>
    /// Karşılaştırma için sadeleştirir: tr-TR küçük harf, Türkçe harfleri ASCII karşılığına indirger,
    /// noktalama yerine boşluk koyar, fazla boşlukları siler.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string lowered = ToLowerTr(value);
        StringBuilder builder = new(lowered.Length);
        foreach (char symbol in lowered)
        {
            char folded = Fold(symbol);
            if (char.IsLetterOrDigit(folded) || folded is '.' or '/' or '\'')
            {
                builder.Append(folded);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>
    /// <see cref="Normalize"/> çıktısını anlamlı belirteçlere böler:
    /// "8'lik" → "8", "3,5x25" → "3.5x25", ölçü birimleri atılır.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string? value)
    {
        string normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return [];
        }

        List<string> tokens = [];
        foreach (string piece in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = NormalizeToken(piece);
            if (token.Length == 0 || MeasurementTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    /// <summary>Belirteçte rakam varsa ölçü/ebat bilgisidir; bu tür belirteçler bire bir karşılaştırılır.</summary>
    public static bool IsNumeric(string token) => token.Any(char.IsDigit);

    private static string NormalizeToken(string piece)
    {
        string token = piece.Trim('.', '/', '\'');
        if (token.Length == 0)
        {
            return string.Empty;
        }

        // "8lik", "10'luk", "200lük", "100'lü" → "8", "10", "200", "100"
        Match ordinal = SizeSuffixRegex().Match(token);
        if (ordinal.Success)
        {
            token = ordinal.Groups[1].Value;
        }

        // "3,5x25" biçimindeki ondalık ayracı noktaya çevrilmiş olur; sondaki noktayı at.
        return token.TrimEnd('.');
    }

    private static char Fold(char symbol) => symbol switch
    {
        'ı' or 'î' or 'í' => 'i',
        'ş' => 's',
        'ç' => 'c',
        'ğ' => 'g',
        'ü' or 'û' => 'u',
        'ö' => 'o',
        'â' or 'á' => 'a',
        ',' => '.',
        '’' or 'ʼ' => '\'',
        _ => symbol
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^([0-9]+(?:[.][0-9]+)?)'?(?:lik|luk|li|lu)$")]
    private static partial Regex SizeSuffixRegex();
}
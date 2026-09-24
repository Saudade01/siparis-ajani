using System.Text.RegularExpressions;

using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Parsing;

/// <summary>
/// Sipariş metnini tek ürünlük parçalara böler.
/// Sahadan gelen metinler tek satırda virgülle ("5 kutu vida, 2 koli silikon"),
/// madde işaretiyle, noktalı virgülle ya da hiç ayraçsız ("8lik dubel 5 paket 10luk dubel 2 paket")
/// yazılabiliyor; bu sınıf yalnızca bölme işini yapar.
/// </summary>
public static partial class OrderTextSegmenter
{
    /// <summary>Bu sözcükten sonrası sipariş değil, ricadır: "... 2 koli daha yolla".</summary>
    private static readonly HashSet<string> TailMarkers = new(StringComparer.Ordinal)
    {
        "lazim", "acil", "olsun", "toplam", "daha", "lutfen", "rica", "yolla", "gonder",
        "gonderin", "hazirla", "istiyorum", "gerek", "gerekli", "ekle", "alalim", "ayarla"
    };

    /// <summary>Satırı ürün parçalarına böler.</summary>
    public static IReadOnlyList<string> Split(string line)
    {
        string prepared = StripBullet(line);
        List<string> segments = [];

        foreach (string piece in SeparatorRegex().Split(prepared))
        {
            string chunk = Clean(piece);
            if (chunk.Length == 0)
            {
                continue;
            }

            segments.AddRange(SplitRepeatedQuantities(chunk));
        }

        return segments;
    }

    /// <summary>Satır başındaki madde işaretini ya da sıra numarasını atar.</summary>
    public static string StripBullet(string line) => BulletRegex().Replace(line.Trim(), string.Empty);

    /// <summary>Parçanın başındaki bağlaçları ve sonundaki rica cümlesini atar.</summary>
    private static string Clean(string piece)
    {
        string chunk = StripBullet(LeadingFillerRegex().Replace(piece.Trim(), string.Empty));
        string[] words = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < words.Length; index++)
        {
            if (TailMarkers.Contains(TurkishTextNormalizer.Normalize(words[index])))
            {
                words = words[..index];
                break;
            }
        }

        return string.Join(' ', words).Trim(' ', ',', '.', ';', ':', '-', '–');
    }

    /// <summary>
    /// Ayraçsız yazılmış çoklu siparişleri böler: "8lik dubel 5 paket 10luk dubel 2 paket".
    /// Bölme yalnızca her parçada ürün adı kalıyorsa uygulanır; yoksa "3 adet şerit metre 5 metre"
    /// satırı ikiye bölünüp ürün bilgisi kaybolur.
    /// </summary>
    private static IReadOnlyList<string> SplitRepeatedQuantities(string chunk)
    {
        string[] words = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<int> pairStarts = [];
        for (int index = 0; index < words.Length - 1; index++)
        {
            if (OrderUnits.TryGetQuantity(words[index], out _) && OrderUnits.TryGetUnit(words[index + 1], out _))
            {
                pairStarts.Add(index);
                index++;
            }
        }

        if (pairStarts.Count < 2)
        {
            return [chunk];
        }

        bool quantityFirst = pairStarts[0] == 0;
        List<int> boundaries = [];
        foreach (int start in pairStarts)
        {
            int boundary = quantityFirst ? start : start + 2;
            if (boundary > 0 && boundary < words.Length)
            {
                boundaries.Add(boundary);
            }
        }

        if (boundaries.Count == 0)
        {
            return [chunk];
        }

        List<string> parts = [];
        int cursor = 0;
        foreach (int boundary in boundaries)
        {
            if (boundary > cursor)
            {
                parts.Add(string.Join(' ', words[cursor..boundary]));
                cursor = boundary;
            }
        }

        if (cursor < words.Length)
        {
            parts.Add(string.Join(' ', words[cursor..]));
        }

        return parts.Count > 1 && parts.All(HasProductWord) ? parts : [chunk];
    }

    /// <summary>Parçada miktar ve birim dışında en az bir sözcük var mı?</summary>
    private static bool HasProductWord(string part)
    {
        foreach (string word in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!OrderUnits.TryGetQuantity(word, out _) && !OrderUnits.TryGetUnit(word, out _))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@",|;|\s+ve\s+|\s+ile\s+|\.\s+|\s+/\s+")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"^(?:[-*•·–—+>]+\s*|\d{1,2}\s*[).\-]\s+)")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(?:bir de|bi de|bide|ayrıca|ayrica|ek olarak|ve|de)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingFillerRegex();
}
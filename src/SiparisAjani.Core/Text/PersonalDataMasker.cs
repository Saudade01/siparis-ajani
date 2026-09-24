using System.Text.RegularExpressions;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Text;

/// <summary>
/// Sipariş metnindeki kişisel verileri yer tutucuyla değiştirir.
/// Sıra önemlidir: e-posta ve IBAN, telefondan önce maskelenir; yoksa IBAN'ın rakam blokları
/// telefon gibi görünür. Maskelenen değerler hiçbir yerde saklanmaz ve döndürülmez.
/// </summary>
public sealed partial class PersonalDataMasker : IPersonalDataMasker
{
    /// <summary>
    /// "Günaydın usta", "Selam abi" gibi kalıplarda ilk kelime kişi adı değildir.
    /// Normalleştirilmiş (tr-TR küçük harf, ASCII'ye indirgenmiş) hâlleriyle tutulur.
    /// </summary>
    private static readonly HashSet<string> NotNames = new(StringComparer.Ordinal)
    {
        "selam", "selamlar", "selamun", "selamun", "merhaba", "merhabalar", "gunaydin",
        "iyi", "kolay", "sayin", "tesekkur", "tesekkurler", "hocam", "ustam", "abi", "abla",
        "bey", "hanim", "usta", "canim", "kardesim", "kolay", "saygilar", "slm", "kanka"
    };

    public MaskingResult Mask(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new MaskingResult(text ?? string.Empty, []);
        }

        List<string> fields = [];
        string masked = text;

        masked = Replace(masked, EmailRegex(), MaskPlaceholders.Email, MaskPlaceholders.Fields.Email, fields);
        masked = Replace(masked, IbanRegex(), MaskPlaceholders.Iban, MaskPlaceholders.Fields.Iban, fields);
        masked = Replace(masked, PhoneRegex(), MaskPlaceholders.Phone, MaskPlaceholders.Fields.Phone, fields);

        // Tek başına duran 11 haneli sayı kimlik numarası kabul edilir. Checksum doğrulaması
        // bilerek yapılmaz: doğrulama kodu yazmak, geçerli numara üretmeye yarayan bir yardımcı
        // bırakır ve yanlış negatifte gerçek kimlik numarası modele gider.
        masked = Replace(masked, NationalIdRegex(), MaskPlaceholders.NationalId, MaskPlaceholders.Fields.NationalId, fields);

        masked = MaskSalutations(masked, fields);

        return new MaskingResult(masked, fields);
    }

    private static string Replace(string input, Regex pattern, string placeholder, string field, List<string> fields)
    {
        if (!pattern.IsMatch(input))
        {
            return input;
        }

        if (!fields.Contains(field))
        {
            fields.Add(field);
        }

        return pattern.Replace(input, placeholder);
    }

    /// <summary>"Hasan Bey", "Ahmet abi", "Ayşe Hanım" gibi hitaplardaki adı maskeler.</summary>
    private static string MaskSalutations(string input, List<string> fields)
    {
        bool found = false;
        string result = SalutationRegex().Replace(input, match =>
        {
            string candidate = TurkishTextNormalizer.Normalize(match.Groups[1].Value);
            if (NotNames.Contains(candidate))
            {
                return match.Value;
            }

            found = true;
            return $"{MaskPlaceholders.Name} {match.Groups[2].Value}";
        });

        if (found && !fields.Contains(MaskPlaceholders.Fields.Name))
        {
            fields.Add(MaskPlaceholders.Fields.Name);
        }

        return result;
    }

    [GeneratedRegex(@"[\p{L}0-9._%+-]+@[\p{L}0-9.-]+\.[\p{L}]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    /// <summary>TR IBAN: TR + 24 rakam, aralarda boşluk/nokta olabilir.</summary>
    [GeneratedRegex(@"\bTR[\s.]?(?:\d[\s.]?){24}", RegexOptions.IgnoreCase)]
    private static partial Regex IbanRegex();

    /// <summary>
    /// TR telefon: isteğe bağlı +90/0 öneki, alan/operatör kodu ve 7 rakam;
    /// aralarda boşluk, nokta, tire veya parantez olabilir.
    /// </summary>
    [GeneratedRegex(@"(?<![\d.])(?:\+?90[\s.\-/]?)?\(?0?(?:2|3|5|8|9)\d{2}\)?[\s.\-]?\d{3}[\s.\-]?\d{2}[\s.\-]?\d{2}(?![\d.])")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<![\d.])\d{11}(?![\d.])")]
    private static partial Regex NationalIdRegex();

    [GeneratedRegex(@"\b(\p{Lu}[\p{Ll}]+)\s+(bey|hanım|hanim|abi|abla|usta|abey)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SalutationRegex();
}
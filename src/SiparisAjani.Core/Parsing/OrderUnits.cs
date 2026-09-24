using System.Globalization;

using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Parsing;

/// <summary>
/// Sipariş birimleri ve Türkçe sayı sözcükleri. Anahtarlar normalleştirilmiş
/// (tr-TR küçük harf, ASCII'ye indirgenmiş) hâlde tutulur.
/// </summary>
public static class OrderUnits
{
    /// <summary>Kısaltma → sözleşmede kullanılacak Türkçe birim adı.</summary>
    private static readonly Dictionary<string, string> Canonical = new(StringComparer.Ordinal)
    {
        ["adet"] = "adet",
        ["ad"] = "adet",
        ["tane"] = "adet",
        ["tn"] = "adet",
        ["kutu"] = "kutu",
        ["paket"] = "paket",
        ["pk"] = "paket",
        ["pkt"] = "paket",
        ["koli"] = "koli",
        ["top"] = "top",
        ["metre"] = "metre",
        ["mt"] = "metre",
        ["rulo"] = "rulo",
        ["cuval"] = "çuval",
        ["torba"] = "torba",
        ["kova"] = "kova",
        ["cift"] = "çift",
        ["takim"] = "takım",
        ["tk"] = "takım",
        ["duzine"] = "düzine",
        ["poset"] = "poşet",
        ["bidon"] = "bidon",
        ["kangal"] = "kangal",
        ["demet"] = "demet",
        ["varil"] = "varil",
        ["sise"] = "şişe",
        ["teneke"] = "teneke",
        ["palet"] = "palet"
    };

    private static readonly Dictionary<string, decimal> NumberWords = new(StringComparer.Ordinal)
    {
        ["bir"] = 1,
        ["iki"] = 2,
        ["uc"] = 3,
        ["dort"] = 4,
        ["bes"] = 5,
        ["alti"] = 6,
        ["yedi"] = 7,
        ["sekiz"] = 8,
        ["dokuz"] = 9,
        ["on"] = 10,
        ["yirmi"] = 20,
        ["otuz"] = 30,
        ["kirk"] = 40,
        ["elli"] = 50,
        ["altmis"] = 60,
        ["yetmis"] = 70,
        ["seksen"] = 80,
        ["doksan"] = 90,
        ["yuz"] = 100
    };

    /// <summary>Verilen sözcük bir sipariş birimi mi? Öyleyse Türkçe karşılığını döner.</summary>
    public static bool TryGetUnit(string word, out string unit)
    {
        return Canonical.TryGetValue(TurkishTextNormalizer.Normalize(word), out unit!);
    }

    /// <summary>"12", "2,5" ya da "üç" gibi bir miktar sözcüğünü sayıya çevirir.</summary>
    public static bool TryGetQuantity(string word, out decimal quantity)
    {
        string normalized = TurkishTextNormalizer.Normalize(word);
        if (NumberWords.TryGetValue(normalized, out quantity))
        {
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
    }
}
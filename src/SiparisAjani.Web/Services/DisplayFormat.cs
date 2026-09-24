using System.Globalization;

namespace SiparisAjani.Web.Services;

/// <summary>Kullanıcıya gösterilen sayıların Türkçe biçimi.</summary>
public static class DisplayFormat
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Number(int value)
    {
        return value.ToString("N0", Turkish);
    }

    /// <summary>0–1 arası oranı "%91" biçiminde yazar.</summary>
    public static string Percent(double ratio)
    {
        double percent = Math.Round(Math.Clamp(ratio, 0, 1) * 100);
        return "%" + percent.ToString("0", Turkish);
    }

    /// <summary>Dosya boyutunu "240 KB" ya da "1,2 MB" biçiminde yazar.</summary>
    public static string FileSize(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = kilobyte * 1024d;
        return bytes >= megabyte
            ? (bytes / megabyte).ToString("0.0", Turkish) + " MB"
            : Math.Ceiling(bytes / kilobyte).ToString("N0", Turkish) + " KB";
    }

    /// <summary>İlk harfi Türkçe kurallarla büyütür (i → İ).</summary>
    public static string CapitalizeFirst(string text)
    {
        return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], Turkish) + text[1..];
    }

    /// <summary>CSS genişliği için oran (ör. "91%"), kültürden bağımsız.</summary>
    public static string CssPercent(double ratio)
    {
        return (Math.Clamp(ratio, 0, 1) * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
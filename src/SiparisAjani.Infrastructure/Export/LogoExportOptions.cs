namespace SiparisAjani.Infrastructure.Export;

/// <summary>Excel sütununda hangi bilginin yazılacağı.</summary>
public enum LogoExportField
{
    LineNo,
    Sku,
    Name,
    Quantity,
    Unit,
    CustomerCode,
    OrderDate,
    Note
}

/// <summary>Tek bir Excel sütunu: başlık metni ve hangi alanı yazdığı.</summary>
public sealed class LogoExportColumn
{
    public string Header { get; set; } = string.Empty;

    public LogoExportField Field { get; set; }
}

/// <summary>
/// Logo Excel aktarımının sütun düzeni (<c>appsettings.json</c> → <c>LogoExport</c>).
///
/// DİKKAT: Aşağıdaki varsayılan sütun başlıkları Logo'nun resmi Excel aktarım şablonuyla
/// HENÜZ DOĞRULANMADI (docs/sozlesmeler/api.md, "Davranış kuralları"). İlk bayi görüşmesinde
/// gerçek şablon alınıp bu varsayılanlar güncellenecek; o zamana kadar sütunlar
/// yapılandırmadan değiştirilebilir olduğu için kod değişikliği gerekmez.
/// </summary>
public sealed class LogoExportOptions
{
    public const string SectionName = "LogoExport";

    public string SheetName { get; set; } = "SIPARIS";

    public List<LogoExportColumn> Columns { get; set; } = [];

    public static LogoExportOptions CreateDefault() => new()
    {
        SheetName = "SIPARIS",
        Columns =
        [
            new LogoExportColumn { Header = "STOK KODU", Field = LogoExportField.Sku },
            new LogoExportColumn { Header = "STOK ADI", Field = LogoExportField.Name },
            new LogoExportColumn { Header = "MIKTAR", Field = LogoExportField.Quantity },
            new LogoExportColumn { Header = "BIRIM", Field = LogoExportField.Unit },
            new LogoExportColumn { Header = "CARI KODU", Field = LogoExportField.CustomerCode },
            new LogoExportColumn { Header = "TARIH", Field = LogoExportField.OrderDate },
            new LogoExportColumn { Header = "ACIKLAMA", Field = LogoExportField.Note }
        ]
    };
}
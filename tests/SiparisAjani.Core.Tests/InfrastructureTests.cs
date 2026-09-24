using ClosedXML.Excel;

using Microsoft.Extensions.Options;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Catalog;
using SiparisAjani.Infrastructure.Export;

namespace SiparisAjani.Core.Tests;

public class CsvCatalogLoaderTests
{
    [Fact]
    public void Load_OrnekKatalogunuOkur()
    {
        string path = Path.Combine(TestData.Root, "data", "katalog", "hirdavat-ornek.csv");
        int dataRows = File.ReadLines(path).Skip(1).Count(line => line.Trim().Length > 0);
        using FileStream stream = File.OpenRead(path);

        CatalogLoadResult result = CsvCatalogLoader.Load(stream);

        // Beklenen sayı dosyadan okunur: kontrol edilen şey hiçbir satırın sessizce atlanmaması.
        Assert.Equal(dataRows, result.Items.Count);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        Assert.Equal("Sunta Vidası 4x40 (500'lü kutu)", result.Items.Single(i => i.Sku == "VD-4040-SNT").Name);
    }

    [Fact]
    public void Load_NoktaliVirguluAyracOlarakTanir()
    {
        CatalogLoadResult result = CsvCatalogLoader.Load(new StringReader(
            "sku;name;unit;barcode;category\nA-1;Vida 4x40;kutu;869;vida"));

        CatalogItem item = Assert.Single(result.Items);
        Assert.Equal("A-1", item.Sku);
        Assert.Equal("kutu", item.Unit);
    }

    [Fact]
    public void Load_TirnakIcindekiAyraciKorur()
    {
        CatalogLoadResult result = CsvCatalogLoader.Load(new StringReader(
            "sku,name,unit,barcode,category\nA-1,\"Vida, özel\",adet,,vida"));

        Assert.Equal("Vida, özel", Assert.Single(result.Items).Name);
    }

    [Fact]
    public void Load_TekrarEdenStokKodunuAtlar()
    {
        CatalogLoadResult result = CsvCatalogLoader.Load(new StringReader(
            "sku,name,unit\nA-1,Vida,adet\nA-1,Vida 2,adet"));

        Assert.Single(result.Items);
        Assert.Equal(1, result.Skipped);
        Assert.Contains(result.Errors, error => error.Contains("tekrar", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_ZorunluSutunYoksaHataDoner()
    {
        CatalogLoadResult result = CsvCatalogLoader.Load(new StringReader("kod,ad\nA-1,Vida"));

        Assert.Empty(result.Items);
        Assert.Contains(result.Errors, error => error.Contains("Zorunlu sütunlar eksik", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_BosDosyadaHataDoner()
    {
        Assert.Contains("boş", Assert.Single(CsvCatalogLoader.Load(new StringReader(string.Empty)).Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_BirimYoksaAdetKabulEder()
    {
        CatalogLoadResult result = CsvCatalogLoader.Load(new StringReader("sku,name,unit\nA-1,Vida,"));

        Assert.Equal("adet", Assert.Single(result.Items).Unit);
    }
}

public class InMemoryCatalogStoreTests
{
    private readonly InMemoryCatalogStore _store = new();

    public InMemoryCatalogStoreTests() => _store.Replace(TestData.Catalog);

    [Fact]
    public void Search_BosSorgudaIlkLimitKadarDoner()
    {
        Assert.Equal(5, _store.Search(null, 5).Count);
    }

    [Fact]
    public void Search_LimitiYuzIleSinirlar()
    {
        Assert.Equal(100, _store.Search(null, 1000).Count);
    }

    [Fact]
    public void Search_TurkceKarakterdenBagimsizBulur()
    {
        Assert.Contains(_store.Search("sunta vidasi", 20), item => item.Sku == "VD-4040-SNT");
    }

    [Fact]
    public void Find_BuyukKucukHarfeDuyarsizdir()
    {
        Assert.NotNull(_store.Find("vd-4040-snt"));
        Assert.Null(_store.Find("YOK-1"));
        Assert.Null(_store.Find("  "));
    }

    [Fact]
    public void All_KatalogDegismedikceAyniOrnegiDoner()
    {
        // Eşleştirici dizinini bu örneğe göre önbelleğe alıyor.
        Assert.Same(_store.All, _store.All);
    }

    [Fact]
    public void Count_UrunSayisiniVerir()
    {
        Assert.Equal(TestData.Catalog.Count, _store.Count);
    }
}

public class ExcelOrderExporterTests
{
    private static ExcelOrderExporter CreateExporter(LogoExportOptions? options = null)
    {
        return new ExcelOrderExporter(Options.Create(options ?? LogoExportOptions.CreateDefault()));
    }

    [Fact]
    public void Export_VarsayilanSutunlariYazar()
    {
        CatalogItem item = TestData.Catalog.Single(i => i.Sku == "VD-4040-SNT");
        ExportOrderRequest request = new(
            [new ExportLineDto("VD-4040-SNT", 5m, "kutu")],
            "CARI-001",
            new DateOnly(2026, 9, 18),
            "demo");

        ExportedFile file = CreateExporter().Export(request, [item]);

        using XLWorkbook workbook = new(new MemoryStream(file.Content));
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("SIPARIS", sheet.Name);
        Assert.Equal("STOK KODU", sheet.Cell(1, 1).GetString());
        Assert.Equal("VD-4040-SNT", sheet.Cell(2, 1).GetString());
        Assert.Equal(item.Name, sheet.Cell(2, 2).GetString());
        Assert.Equal(5, sheet.Cell(2, 3).GetDouble());
        Assert.Equal("kutu", sheet.Cell(2, 4).GetString());
        Assert.Equal("CARI-001", sheet.Cell(2, 5).GetString());
    }

    [Fact]
    public void Export_DosyaAdiVeIcerikTuruSozlesmeyeUyar()
    {
        ExportedFile file = CreateExporter().Export(
            new ExportOrderRequest([new ExportLineDto("A-1", 1m, "adet")]),
            [new CatalogItem("A-1", "Vida", "adet", null, null)]);

        Assert.StartsWith("siparis-", file.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", file.FileName, StringComparison.Ordinal);
        Assert.Equal(ExcelOrderExporter.ExcelContentType, file.ContentType);
    }

    [Fact]
    public void Export_YapilandirilmisSutunlariKullanir()
    {
        LogoExportOptions options = new()
        {
            SheetName = "AKTARIM",
            Columns =
            [
                new LogoExportColumn { Header = "SIRA", Field = LogoExportField.LineNo },
                new LogoExportColumn { Header = "KOD", Field = LogoExportField.Sku }
            ]
        };

        ExportedFile file = CreateExporter(options).Export(
            new ExportOrderRequest([new ExportLineDto("A-1", 2m, "adet")]),
            [new CatalogItem("A-1", "Vida", "adet", null, null)]);

        using XLWorkbook workbook = new(new MemoryStream(file.Content));
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("AKTARIM", sheet.Name);
        Assert.Equal("SIRA", sheet.Cell(1, 1).GetString());
        Assert.Equal("KOD", sheet.Cell(1, 2).GetString());
        Assert.Equal(1, sheet.Cell(2, 1).GetDouble());
        Assert.Equal("A-1", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public void Export_BirimBossaKatalogBirimiKullanilir()
    {
        ExportedFile file = CreateExporter().Export(
            new ExportOrderRequest([new ExportLineDto("A-1", 1m, "  ")]),
            [new CatalogItem("A-1", "Vida", "kutu", null, null)]);

        using XLWorkbook workbook = new(new MemoryStream(file.Content));
        Assert.Equal("kutu", workbook.Worksheet(1).Cell(2, 4).GetString());
    }
}
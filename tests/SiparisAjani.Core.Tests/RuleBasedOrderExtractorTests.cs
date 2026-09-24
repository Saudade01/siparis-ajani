using SiparisAjani.Contracts;

using SiparisAjani.Core.Models;
using SiparisAjani.Core.Parsing;

namespace SiparisAjani.Core.Tests;

public class RuleBasedOrderExtractorTests
{
    [Theory]
    [InlineData("5 kutu 4x40 sunta vidası", 5, "kutu", "4x40 sunta vidası")]
    [InlineData("iki kutu akıllı vida 4.2x16", 2, "kutu", "akıllı vida 4.2x16")]
    [InlineData("Beş kutu 5x50 sunta vidası", 5, "kutu", "5x50 sunta vidası")]
    [InlineData("20 mt ttr kablo 3x2.5", 20, "metre", "ttr kablo 3x2.5")]
    [InlineData("3 ad izole bant siyah", 3, "adet", "izole bant siyah")]
    [InlineData("12 tane beyaz silikon", 12, "adet", "beyaz silikon")]
    public void ParseSegment_MiktarBastaysaOkur(string segment, int quantity, string unit, string product)
    {
        ExtractedOrderLine line = Assert.IsType<ExtractedOrderLine>(RuleBasedOrderExtractor.ParseSegment(segment));

        Assert.Equal(quantity, line.Quantity);
        Assert.Equal(unit, line.Unit);
        Assert.Equal(product, line.ProductText);
    }

    [Theory]
    [InlineData("sunta vidasi 4x50 3 kutu", 3, "kutu", "sunta vidasi 4x50")]
    [InlineData("Alçıpan vidası 3.5x25: 10 kutu", 10, "kutu", "Alçıpan vidası 3.5x25")]
    [InlineData("Buat - 30 adet", 30, "adet", "Buat")]
    [InlineData("Lavabo bataryası 1 adet", 1, "adet", "Lavabo bataryası")]
    public void ParseSegment_MiktarSondaysaOkur(string segment, int quantity, string unit, string product)
    {
        ExtractedOrderLine line = Assert.IsType<ExtractedOrderLine>(RuleBasedOrderExtractor.ParseSegment(segment));

        Assert.Equal(quantity, line.Quantity);
        Assert.Equal(unit, line.Unit);
        Assert.Equal(product, line.ProductText);
    }

    [Theory]
    [InlineData("Sds plus beton ucu 8x160 x 5", 5, "Sds plus beton ucu 8x160")]
    [InlineData("hss 8 mm uç x10", 10, "hss 8 mm uç")]
    public void ParseSegment_CarpiIsaretliAdediOkur(string segment, int quantity, string product)
    {
        ExtractedOrderLine line = Assert.IsType<ExtractedOrderLine>(RuleBasedOrderExtractor.ParseSegment(segment));

        Assert.Equal(quantity, line.Quantity);
        Assert.Null(line.Unit);
        Assert.Equal(product, line.ProductText);
    }

    [Fact]
    public void ParseSegment_OlcuBirimiMiktarSayilmaz()
    {
        // "15 lt" ürünün parçasıdır, sipariş miktarı "4 kova".
        ExtractedOrderLine line = Assert.IsType<ExtractedOrderLine>(
            RuleBasedOrderExtractor.ParseSegment("15 lt iç cephe beyaz boya 4 kova"));

        Assert.Equal(4, line.Quantity);
        Assert.Equal("kova", line.Unit);
        Assert.Equal("15 lt iç cephe beyaz boya", line.ProductText);
    }

    [Fact]
    public void ParseSegment_MiktarYoksaNullDoner()
    {
        ExtractedOrderLine line = Assert.IsType<ExtractedOrderLine>(
            RuleBasedOrderExtractor.ParseSegment("silikon tabancası"));

        Assert.Null(line.Quantity);
        Assert.Null(line.Unit);
        Assert.Equal("silikon tabancası", line.ProductText);
    }

    [Fact]
    public void Extract_VirgulleAyrilmisTekSatiriBoler()
    {
        ExtractionResult result = RuleBasedOrderExtractor.Extract(
            "5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon, 10 paket 8'lik dübel");

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal("4x40 sunta vidası", result.Lines[0].ProductText);
        Assert.Equal("8'lik dübel", result.Lines[2].ProductText);
    }

    [Fact]
    public void Extract_AyracsizCokluSiparisiBoler()
    {
        ExtractionResult result = RuleBasedOrderExtractor.Extract("8lik dubel 5 paket 10luk dubel 2 paket");

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("8lik dubel", result.Lines[0].ProductText);
        Assert.Equal("10luk dubel", result.Lines[1].ProductText);
    }

    [Fact]
    public void Extract_UrunAdiKaybolacaksaBolmez()
    {
        // "5 metre" ayrı satır olsaydı "şerit metre 5 metre" ürün bilgisi bölünürdü.
        ExtractionResult result = RuleBasedOrderExtractor.Extract("3 adet şerit metre 5 metre");

        Assert.Single(result.Lines);
        Assert.Equal("şerit metre 5 metre", result.Lines[0].ProductText);
    }

    [Fact]
    public void Extract_SelamlasmaOrtasindakiSiparisiBulur()
    {
        ExtractionResult result = RuleBasedOrderExtractor.Extract(
            "Hocam selamun aleykum 10 kutu sunta vidası 3.5x30 ve 4 adet köpük 750 lik lazım acil");

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("sunta vidası 3.5x30", result.Lines[0].ProductText);
        Assert.Equal("köpük 750 lik", result.Lines[1].ProductText);
    }

    [Fact]
    public void Extract_NoktaliVirgulluTabloyuOkurBaslikSatiriniAtar()
    {
        ExtractionResult result = RuleBasedOrderExtractor.Extract("Ürün;Miktar;Birim\nPPRC Boru 20 mm;50;adet");

        ExtractedOrderLine line = Assert.Single(result.Lines);
        Assert.Equal(50, line.Quantity);
        Assert.Equal("adet", line.Unit);
        Assert.Equal("PPRC Boru 20 mm", line.ProductText);
    }

    [Fact]
    public void Extract_MaddeIsaretiVeSiraNumarasiniAtar()
    {
        ExtractionResult result = RuleBasedOrderExtractor.Extract("* 50 çift nitril kaplı iş eldiveni\n1) 2 adet çekiç 500 gr");

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("nitril kaplı iş eldiveni", result.Lines[0].ProductText);
        Assert.Equal("çekiç 500 gr", result.Lines[1].ProductText);
    }

    [Fact]
    public void Extract_BosMetindeBosSonucDoner()
    {
        Assert.Empty(RuleBasedOrderExtractor.Extract("   ").Lines);
    }

    [Fact]
    public async Task ExtractAsync_KuralTabanliTuruBildirir()
    {
        RuleBasedOrderExtractor extractor = new();

        ExtractionResult result = await extractor.ExtractAsync("5 kutu 4x40 sunta vidası");

        Assert.Equal(ExtractorKind.RuleBased, extractor.Kind);
        Assert.Single(result.Lines);
    }
}
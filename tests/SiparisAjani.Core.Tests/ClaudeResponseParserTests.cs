using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Claude;

namespace SiparisAjani.Core.Tests;

/// <summary>
/// Claude yanıtının JSON ayrıştırması. Bu testler gerçek API'yi ÇAĞIRMAZ:
/// ayrıştırma bilerek ağdan ayrı, statik bir metotta duruyor.
/// </summary>
public class ClaudeResponseParserTests
{
    [Fact]
    public void Parse_SatirlariOkur()
    {
        const string json = """
            {"lines":[
              {"rawText":"5 kutu 4x40 sunta vidasi","quantity":5,"unit":"kutu","productText":"4x40 sunta vidasi"},
              {"rawText":"silikon tabancasi","quantity":null,"unit":null,"productText":"silikon tabancasi"}
            ]}
            """;

        ExtractionResult result = ClaudeResponseParser.Parse(json);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(5, result.Lines[0].Quantity);
        Assert.Equal("kutu", result.Lines[0].Unit);
        Assert.Equal("4x40 sunta vidasi", result.Lines[0].ProductText);
        Assert.Null(result.Lines[1].Quantity);
        Assert.Null(result.Lines[1].Unit);
    }

    [Fact]
    public void Parse_MetinOlarakGelenMiktariSayiyaCevirir()
    {
        ExtractionResult result = ClaudeResponseParser.Parse(
            """{"lines":[{"rawText":"x","quantity":"3.5","unit":"kutu","productText":"vida"}]}""");

        Assert.Equal(3.5m, Assert.Single(result.Lines).Quantity);
    }

    [Fact]
    public void Parse_UrunMetniBosOlanSatiriAtar()
    {
        ExtractionResult result = ClaudeResponseParser.Parse(
            """{"lines":[{"rawText":"x","quantity":1,"unit":"adet","productText":"  "}]}""");

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Parse_BosBirimiNullSayar()
    {
        ExtractionResult result = ClaudeResponseParser.Parse(
            """{"lines":[{"rawText":"x","quantity":1,"unit":" ","productText":"vida"}]}""");

        Assert.Null(Assert.Single(result.Lines).Unit);
    }

    [Theory]
    [InlineData("{bozuk")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("""{"baska":1}""")]
    [InlineData("""{"lines":"dizi degil"}""")]
    public void Parse_BozukYanittaIstisnaFirlatmaz(string? json)
    {
        Assert.Empty(ClaudeResponseParser.Parse(json).Lines);
    }

    [Fact]
    public void Parse_RawTextYoksaUrunMetniniKullanir()
    {
        ExtractionResult result = ClaudeResponseParser.Parse(
            """{"lines":[{"quantity":2,"unit":"adet","productText":"pense 180 mm"}]}""");

        Assert.Equal("pense 180 mm", Assert.Single(result.Lines).RawText);
    }

    [Fact]
    public void CreateSchema_ModelinBekledigiAnahtarlariIcerir()
    {
        Dictionary<string, System.Text.Json.JsonElement> schema = ClaudeResponseParser.CreateSchema();

        Assert.Contains("type", schema.Keys);
        Assert.Contains("properties", schema.Keys);
        Assert.Contains("required", schema.Keys);
    }

    [Fact]
    public void BuildNarrowingWarning_AyiklananSatirSayisiniBildirir()
    {
        ProductLineFilterResult filtered = ProductLineFilter.Filter(
            new PersonalDataMasker().Mask(TestData.Samples.Single(s => s.Id == "012").Text).MaskedText);

        string warning = ClaudeOrderExtractor.BuildNarrowingWarning(filtered);

        Assert.Contains("6 ürün satırı", warning, StringComparison.Ordinal);
        Assert.Contains("ayıklandı", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPrompt_ModeleYalnizcaVerilenSatirlariKoyar()
    {
        string prompt = ClaudeOrderExtractor.BuildPrompt(["5 kutu vida", "2 koli silikon"]);

        Assert.Contains("5 kutu vida", prompt, StringComparison.Ordinal);
        Assert.Contains("2 koli silikon", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ClaudeOptions_VarsayilanKapalidir()
    {
        ClaudeOptions options = new();

        Assert.False(options.Enabled);
        Assert.False(options.IsUsable);
        Assert.Equal("claude-opus-5", options.Model);
    }

    [Fact]
    public void ClaudeOptions_AnahtarsizAcilamaz()
    {
        ClaudeOptions options = new() { Enabled = true, ApiKey = "   " };

        Assert.False(options.IsUsable);
    }

    [Fact]
    public void ClaudeOptions_AnahtarVeAcikBayrakGerektirir()
    {
        Assert.False(new ClaudeOptions { Enabled = false, ApiKey = "sk-test" }.IsUsable);
        Assert.True(new ClaudeOptions { Enabled = true, ApiKey = "sk-test" }.IsUsable);
    }
}
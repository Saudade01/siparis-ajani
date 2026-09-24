using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Tests;

public class ProductLineFilterTests
{
    private readonly PersonalDataMasker _masker = new();

    [Theory]
    [InlineData("Merhaba,")]
    [InlineData("İyi çalışmalar")]
    [InlineData("Kolay gelsin")]
    [InlineData("Konu: Haftalık sipariş")]
    [InlineData("Ürünler:")]
    [InlineData("Teslimat cuma günü olabilir mi?")]
    [InlineData("---------- İletilen ileti ---------")]
    [InlineData("Örnek Tesisat Ltd.")]
    [InlineData("Adres bilgisini ayrıca ileteceğim.")]
    public void IsNoiseSegment_SohbetVeImzaSatirlariniAyiklar(string line)
    {
        Assert.True(ProductLineFilter.IsNoiseSegment(line));
    }

    [Theory]
    [InlineData("- Alçıpan vidası 3.5x25: 10 kutu")]
    [InlineData("silikon tabancası")]
    [InlineData("Pense 180 mm")]
    [InlineData("8lik dubel 5 paket")]
    public void IsNoiseSegment_UrunSatirlariniKorur(string line)
    {
        Assert.False(ProductLineFilter.IsNoiseSegment(line));
    }

    [Fact]
    public void IsNoiseSegment_MiktarVarsaSelamlasmayaRagmenKorur()
    {
        // "Merhaba" var ama sipariş de var: satır atılırsa bütün sipariş kaybolur.
        Assert.False(ProductLineFilter.IsNoiseSegment("Merhaba abi, 5 kutu 4x40 sunta vidası"));
    }

    [Fact]
    public void IsNoiseSegment_YapistiriciyiSirketEkiSanmaz()
    {
        // "şti" eki "yapıştırıcı" kelimesinin içinde de geçiyor.
        Assert.False(ProductLineFilter.IsNoiseSegment("kontakt yapıştırıcı 850 gr"));
    }

    [Fact]
    public void RemovePlaceholders_EtiketiyleBirlikteSiler()
    {
        string result = ProductLineFilter.RemovePlaceholders("10 paket dübel. Tel: [telefon]");

        Assert.DoesNotContain("[telefon]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Tel:", result, StringComparison.Ordinal);
        Assert.Contains("10 paket dübel", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_ModeleYalnizcaUrunSatirlariniBirakir()
    {
        SampleOrder sample = TestData.Samples.Single(order => order.Id == "012");
        string masked = _masker.Mask(sample.Text).MaskedText;

        ProductLineFilterResult result = ProductLineFilter.Filter(masked);

        Assert.Equal(6, result.Lines.Count);
        Assert.True(result.DroppedCount > 0);
        Assert.All(result.Lines, line => Assert.DoesNotContain("[", line, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Lines, line => line.Contains("Ödeme", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Filter_ImzaBlogunuVeSonrasiniAtar()
    {
        string text = "5 kutu vida\nSaygılarımızla,\nSatınalma Birimi\nÖrnek Yapı Market";

        ProductLineFilterResult result = ProductLineFilter.Filter(text);

        Assert.Single(result.Lines);
        Assert.Equal(3, result.DroppedCount);
    }

    [Fact]
    public void Filter_BosMetindeBosSonucDoner()
    {
        ProductLineFilterResult result = ProductLineFilter.Filter("   ");

        Assert.Empty(result.Lines);
        Assert.Equal(0, result.DroppedCount);
    }
}
using SiparisAjani.Contracts;

using SiparisAjani.Core.Matching;
using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Tests;

public class ProductMatcherTests
{
    private readonly ProductMatcher _matcher = new();

    private static readonly IReadOnlyList<CatalogItem> Small =
    [
        new("A-1", "Sunta Vidası 4x40 (500'lü kutu)", "kutu", "8690000000001", "vida"),
        new("A-2", "Sunta Vidası 4x50 (500'lü kutu)", "kutu", "8690000000002", "vida"),
        new("B-1", "Silikon Şeffaf 280 ml", "adet", "8690000000003", "silikon")
    ];

    [Theory]
    [InlineData("4x40 sunta vidası", "VD-4040-SNT")]
    [InlineData("sunta vidasi 4x50", "VD-4050-SNT")]
    [InlineData("8'lik dübel", "DB-08-PLS")]
    [InlineData("10luk dubel", "DB-10-PLS")]
    [InlineData("kelebek dübel alçıpan için", "DB-KLB-ALC")]
    [InlineData("PPRC Küresel Vana 20 mm", "ST-PPR-V20")]
    [InlineData("Saten alçı 25 kg", "BY-ALC-STN-25")]
    [InlineData("Boya rulosu 25 cm", "BY-RLO-25")]
    [InlineData("şeffaf silikon", "SL-280-SEF")]
    [InlineData("Kontrol kalemi", "EA-TRN-KNT")]
    public void Match_DogruUrunuEslestirir(string productText, string expectedSku)
    {
        ProductMatchResult result = _matcher.Match(productText, TestData.Catalog);

        Assert.Equal(LineStatus.Matched, result.Status);
        Assert.Equal(expectedSku, result.BestMatch!.Item.Sku);
    }

    [Fact]
    public void Match_BarkodaGoreBirebirEslestirir()
    {
        ProductMatchResult result = _matcher.Match("8694020351963", TestData.Catalog);

        Assert.Equal(LineStatus.Matched, result.Status);
        Assert.Equal("ST-FLX-60", result.BestMatch!.Item.Sku);
        Assert.Equal(1.0, result.BestMatch.Confidence);
    }

    [Fact]
    public void Match_EbatFarkliysaKaristirmaz()
    {
        // Sahada en pahalı hata ebat karıştırmaktır .
        ProductMatchResult result = _matcher.Match("4x40 sunta vidası", Small);

        Assert.Equal(LineStatus.Matched, result.Status);
        Assert.Equal("A-1", result.BestMatch!.Item.Sku);
    }

    [Fact]
    public void Match_IkinciAdayYakinsaBelirsizDoner()
    {
        // "Buat" kelimesi dört ayrı ürüne birebir uyuyor; kullanıcı seçmeli.
        ProductMatchResult result = _matcher.Match("Buat", TestData.Catalog);

        Assert.Equal(LineStatus.Ambiguous, result.Status);
        Assert.NotNull(result.BestMatch);
    }

    [Fact]
    public void Match_BelirsizdeEnFazlaUcAlternatifDoner()
    {
        foreach (SampleOrder sample in TestData.Samples)
        {
            ProductMatchResult result = _matcher.Match(sample.Text, TestData.Catalog);
            Assert.True(result.Alternatives.Count <= ProductMatcher.MaxAlternatives);
        }

        ProductMatchResult buat = _matcher.Match("Buat", TestData.Catalog);
        Assert.True(buat.Alternatives.Count <= ProductMatcher.MaxAlternatives);
    }

    [Fact]
    public void Match_EslestidiginDurumdaAlternatifVermez()
    {
        ProductMatchResult result = _matcher.Match("Kelebek Dübel Alçıpan İçin", TestData.Catalog);

        Assert.Equal(LineStatus.Matched, result.Status);
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public void Match_AlakasizMetindeBulunamadiDoner()
    {
        ProductMatchResult result = _matcher.Match("yarın sabah görüşelim", TestData.Catalog);

        Assert.Equal(LineStatus.NotFound, result.Status);
        Assert.Null(result.BestMatch);
    }

    [Fact]
    public void Match_BosKatalogdaBulunamadiDoner()
    {
        Assert.Equal(LineStatus.NotFound, _matcher.Match("vida", []).Status);
        Assert.Equal(LineStatus.NotFound, _matcher.Match("   ", TestData.Catalog).Status);
    }

    [Fact]
    public void Match_EsiklerSozlesmeyeUyar()
    {
        Assert.Equal(0.80, ProductMatcher.MatchedThreshold);
        Assert.Equal(0.10, ProductMatcher.SeparationThreshold);
        Assert.Equal(0.45, ProductMatcher.AmbiguousThreshold);
        Assert.Equal(3, ProductMatcher.MaxAlternatives);
    }

    [Fact]
    public void Match_EslestiDurumuEsikleriSaglar()
    {
        foreach (SampleOrder sample in TestData.Samples)
        {
            foreach (string word in new[] { "dübel", "silikon", "vida", "boya" })
            {
                ProductMatchResult result = _matcher.Match(word, TestData.Catalog);
                if (result.Status == LineStatus.Matched)
                {
                    Assert.True(result.BestMatch!.Confidence >= ProductMatcher.MatchedThreshold);
                }
            }
        }
    }

    [Fact]
    public void Match_TurkceEkiOlanAdlariEslestirir()
    {
        // "yeleği" ~ "yelek", "ucu" ~ "uç": Türkçe iyelik eki ve ünsüz yumuşaması.
        ProductMatchResult result = _matcher.Match("reflektörlü ikaz yelek sarı", TestData.Catalog);

        Assert.Equal("IG-YLK-SRI", result.BestMatch!.Item.Sku);
    }
}
using System.Globalization;
using System.Text.RegularExpressions;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Matching;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Parsing;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Catalog;

namespace SiparisAjani.Core.Tests;

/// <summary>Testlerde kullanılan bellek içi düzeltme deposu.</summary>
internal sealed class FakeAliasStore : IAliasStore
{
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);

    public string? Resolve(string productText)
    {
        return _aliases.TryGetValue(TurkishTextNormalizer.Normalize(productText), out string? sku) ? sku : null;
    }

    public Task SaveAsync(string productText, string sku, CancellationToken cancellationToken = default)
    {
        _aliases[TurkishTextNormalizer.Normalize(productText)] = sku;
        return Task.CompletedTask;
    }
}

public class OrderParsingServiceTests
{
    private readonly FakeAliasStore _aliases = new();
    private readonly InMemoryCatalogStore _catalog = new();
    private readonly OrderParsingService _service;

    public OrderParsingServiceTests()
    {
        _catalog.Replace(TestData.Catalog);
        _service = new OrderParsingService(
            new PersonalDataMasker(),
            new RuleBasedOrderExtractor(),
            new ProductMatcher(),
            _catalog,
            _aliases);
    }

    [Fact]
    public async Task ParseAsync_SozlesmedekiOrnegiUretir()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest(
            "Merhaba, 5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon, 10 adet 8'lik dübel. Tel: 0532 111 22 33"));

        Assert.Equal(ExtractorKind.RuleBased, draft.Extractor);
        Assert.Equal(3, draft.Lines.Count);
        Assert.Contains("phone", draft.MaskedFields);
        Assert.Null(draft.CustomerHint);
        Assert.StartsWith("d_", draft.DraftId, StringComparison.Ordinal);

        OrderLineDto first = draft.Lines[0];
        Assert.Equal(1, first.LineNo);
        Assert.Equal(5, first.Quantity);
        Assert.Equal("kutu", first.Unit);
        Assert.Equal("4x40 sunta vidası", first.ProductText);
        Assert.Equal(LineStatus.Matched, first.Status);
        Assert.Equal("VD-4040-SNT", first.BestMatch!.Sku);
        Assert.Empty(first.Alternatives);
    }

    [Fact]
    public async Task ParseAsync_SatirNumaralariArdisikOlur()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest(TestData.Samples.Single(s => s.Id == "003").Text));

        Assert.Equal(Enumerable.Range(1, draft.Lines.Count), draft.Lines.Select(line => line.LineNo));
    }

    [Fact]
    public async Task ParseAsync_MiktarYoksaBiriKabulEderVeUyarir()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest("silikon tabancası"));

        OrderLineDto line = Assert.Single(draft.Lines);
        Assert.Equal(1, line.Quantity);
        Assert.Null(line.Unit);
        Assert.Contains(draft.Warnings, warning => warning.Contains("miktar bulunamadı", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_KisiselVeriyiYanitinHicbirYerindeDondurmez()
    {
        const string phone = "0532 111 22 33";
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest($"5 kutu 4x40 sunta vidası. Tel: {phone}"));

        string serialized = System.Text.Json.JsonSerializer.Serialize(draft);
        Assert.DoesNotContain("0532", serialized, StringComparison.Ordinal);
        Assert.Contains("phone", draft.MaskedFields);
    }

    [Fact]
    public async Task ParseAsync_DuzeltmeSonrakiCozumlemedeKullanilir()
    {
        OrderDraftDto before = await _service.ParseAsync(new ParseOrderRequest("3 tane japon yapıştırıcı"));
        Assert.NotEqual(LineStatus.Matched, before.Lines[0].Status);

        await _aliases.SaveAsync("japon yapıştırıcı", "YP-HZL-20");

        OrderDraftDto after = await _service.ParseAsync(new ParseOrderRequest("3 tane japon yapıştırıcı"));
        Assert.Equal(LineStatus.Matched, after.Lines[0].Status);
        Assert.Equal("YP-HZL-20", after.Lines[0].BestMatch!.Sku);
        Assert.Equal(1.0, after.Lines[0].BestMatch!.Confidence);
    }

    [Fact]
    public async Task ParseAsync_KatalogdaOlmayanTakmaAdiYoksayar()
    {
        await _aliases.SaveAsync("hayali ürün", "YOK-999");

        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest("2 adet hayali ürün"));

        Assert.NotEqual(LineStatus.Matched, draft.Lines[0].Status);
    }

    [Fact]
    public async Task ParseAsync_SiparisSatiriYoksaUyarir()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest("Merhaba, iyi çalışmalar"));

        Assert.Empty(draft.Lines);
        Assert.Contains(draft.Warnings, warning => warning.Contains("bulunamadı", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_BelirsizSatirdaAdaylariDoner()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest("30 adet Buat"));

        OrderLineDto line = Assert.Single(draft.Lines);
        Assert.Equal(LineStatus.Ambiguous, line.Status);
        Assert.NotNull(line.BestMatch);
        Assert.True(line.Alternatives.Count <= 3);
        Assert.Contains(draft.Warnings, warning => warning.Contains("birden fazla aday", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_SohbetCumlesiniSatirYapmaz()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest(
            "5 kutu 4x40 sunta vidası. Beni 0500 000 00 06 dan ara."));

        OrderLineDto line = Assert.Single(draft.Lines);
        Assert.Equal("4x40 sunta vidası", line.ProductText);
    }

    /// <summary>
    /// Atlanan sohbet cümlesi, kendisinden sonra gelmeyen bir satır numarasıyla uyarı
    /// ürettiriyordu: 5 satırlık çıktıda "6. satırda miktar bulunamadı" yazıyordu.
    /// Uyarıdaki numara her zaman sonuç tablosundaki LineNo olmalı.
    /// </summary>
    [Fact]
    public async Task ParseAsync_AtlananSatirUyariNumarasiniKaydirmaz()
    {
        OrderDraftDto draft = await _service.ParseAsync(new ParseOrderRequest(
            "5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon. Beni 0500 000 00 06 dan ara."));

        Assert.Equal(2, draft.Lines.Count);
        Assert.DoesNotContain(draft.Warnings, warning => warning.Contains("miktar bulunamadı", StringComparison.Ordinal));
        Assert.All(draft.Warnings, warning => Assert.True(
            LineNumberOf(warning) is not int lineNo || draft.Lines.Any(line => line.LineNo == lineNo),
            $"Uyarıdaki satır numarası sonuç tablosunda yok: {warning}"));
    }

    [Theory]
    [MemberData(nameof(SampleIds))]
    public async Task ParseAsync_UyaridakiSatirNumarasiSonucTablosundaVar(string sampleId)
    {
        OrderDraftDto draft = await _service.ParseAsync(
            new ParseOrderRequest(TestData.Samples.Single(sample => sample.Id == sampleId).Text));

        foreach (string warning in draft.Warnings)
        {
            if (LineNumberOf(warning) is int lineNo)
            {
                Assert.True(
                    draft.Lines.Any(line => line.LineNo == lineNo),
                    $"{sampleId}: çıktıda {draft.Lines.Count} satır var ama uyarı '{warning}' diyor.");
            }
        }
    }

    public static TheoryData<string> SampleIds
    {
        get
        {
            TheoryData<string> data = [];
            foreach (SampleOrder sample in TestData.Samples)
            {
                data.Add(sample.Id);
            }

            return data;
        }
    }

    /// <summary>"6. satırda ..." gibi bir uyarının başındaki satır numarası; yoksa null.</summary>
    private static int? LineNumberOf(string warning)
    {
        Match match = Regex.Match(warning, @"^(\d+)\. satır", RegexOptions.None, TimeSpan.FromSeconds(1));
        return match.Success
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
           : null;
    }

    [Fact]
    public async Task ParseAsync_TaslakKimligiBenzersizdir()
    {
        OrderDraftDto first = await _service.ParseAsync(new ParseOrderRequest("5 kutu 4x40 sunta vidası"));
        OrderDraftDto second = await _service.ParseAsync(new ParseOrderRequest("5 kutu 4x40 sunta vidası"));

        Assert.NotEqual(first.DraftId, second.DraftId);
    }
}
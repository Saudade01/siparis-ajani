using System.Globalization;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Matching;
using SiparisAjani.Core.Parsing;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Catalog;

using Xunit.Abstractions;

namespace SiparisAjani.Core.Tests;

/// <summary>
/// 20 örnek sipariş üzerinde kural tabanlı doğruluk ölçümü (özellik 001, kabul kriteri 6).
///
/// Birincil ölçü YANLIŞ EŞLEŞME ORANI'dır: sahada güveni bozan şey,
/// bulunamayan satır değil, yanlış ürünü "Eşleşti" diye göstermektir. Eşik aşılırsa test kırılır.
/// İkincil ölçü satır doğruluğu yalnızca raporlanır.
/// </summary>
public class ExtractionAccuracyTests(ITestOutputHelper output)
{
    /// <summary>Bu oranın üstü kabul edilemez .</summary>
    private const double MaxWrongMatchRate = 0.15;

    /// <summary>Hedef; altına düşünce test kırılmaz, yalnızca rapor edilir.</summary>
    private const double TargetLineAccuracy = 0.70;

    [Fact]
    public async Task KuralTabanliAyristirici_YanlisEslesmeOraniEsigiAsmaz()
    {
        AccuracyReport report = await MeasureAsync();

        foreach (string detail in report.WrongMatches)
        {
            output.WriteLine($"YANLIŞ EŞLEŞME: {detail}");
        }

        output.WriteLine(report.Summary);

        Assert.True(
            report.WrongMatchRate <= MaxWrongMatchRate,
            $"Yanlış eşleşme oranı %{report.WrongMatchRate * 100:F1}, eşik %{MaxWrongMatchRate * 100:F0}. {report.Summary}");
    }

    [Fact]
    public async Task KuralTabanliAyristirici_SatirDogrulugunuRaporlar()
    {
        AccuracyReport report = await MeasureAsync();

        output.WriteLine(report.Summary);
        if (report.LineAccuracy < TargetLineAccuracy)
        {
            // Bilerek kırmıyoruz: ikincil gösterge, Claude modu açılınca yükselmesi bekleniyor.
            output.WriteLine(
                $"UYARI: satır doğruluğu hedefin (%{TargetLineAccuracy * 100:F0}) altında.");
        }

        Assert.True(report.ComparedLines > 0);
    }

    [Fact]
    public async Task KuralTabanliAyristirici_BeklenenMaskeTurleriniBulur()
    {
        AccuracyReport report = await MeasureAsync();

        Assert.Empty(report.MissedMaskedFields);
    }

    private sealed record AccuracyReport(
        int MatchedLines,
        int WrongMatchedLines,
        int ExactLines,
        int ComparedLines,
        IReadOnlyList<string> WrongMatches,
        IReadOnlyList<string> MissedMaskedFields)
    {
        public double WrongMatchRate => MatchedLines == 0 ? 0 : (double)WrongMatchedLines / MatchedLines;

        public double LineAccuracy => ComparedLines == 0 ? 0 : (double)ExactLines / ComparedLines;

        public string Summary =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"Eşleşti satır: {MatchedLines}, yanlış: {WrongMatchedLines} → yanlış eşleşme oranı %{WrongMatchRate * 100:F1} | " +
                $"satır doğruluğu: {ExactLines}/{ComparedLines} = %{LineAccuracy * 100:F1}");
    }

    private static async Task<AccuracyReport> MeasureAsync()
    {
        InMemoryCatalogStore catalog = new();
        catalog.Replace(TestData.Catalog);
        PersonalDataMasker masker = new();
        OrderParsingService service = new(
            masker,
            new RuleBasedOrderExtractor(),
            new ProductMatcher(),
            catalog,
            new FakeAliasStore());

        int matched = 0;
        int wrong = 0;
        int exact = 0;
        int compared = 0;
        List<string> wrongMatches = [];
        List<string> missedMasks = [];

        foreach (SampleOrder sample in TestData.Samples)
        {
            OrderDraftDto draft = await service.ParseAsync(new ParseOrderRequest(sample.Text));

            foreach (string expectedField in sample.MaskedFields)
            {
                if (!draft.MaskedFields.Contains(expectedField))
                {
                    missedMasks.Add($"{sample.Id}: {expectedField}");
                }
            }

            // Beklenen stok kodları çoklu küme olarak tüketilir: satır sayıları farklı olsa da
            // "bu siparişte olmayan bir ürüne Eşleşti dedik mi?" sorusu doğru yanıtlanır.
            Dictionary<string, int> remaining = sample.Lines
              .Where(line => line.Sku is not null)
              .GroupBy(line => line.Sku!, StringComparer.Ordinal)
              .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            foreach (OrderLineDto line in draft.Lines.Where(line => line.Status == LineStatus.Matched))
            {
                matched++;
                string sku = line.BestMatch!.Sku;
                if (remaining.TryGetValue(sku, out int left) && left > 0)
                {
                    remaining[sku] = left - 1;
                }
                else
                {
                    wrong++;
                    wrongMatches.Add($"{sample.Id}: '{line.ProductText}' → {sku} ({line.BestMatch.Confidence:F2})");
                }
            }

            compared += Math.Max(draft.Lines.Count, sample.Lines.Count);
            for (int index = 0; index < Math.Min(draft.Lines.Count, sample.Lines.Count); index++)
            {
                OrderLineDto actual = draft.Lines[index];
                ExpectedLine expected = sample.Lines[index];
                string? actualSku = actual.Status == LineStatus.Matched ? actual.BestMatch!.Sku : null;

                if (actual.Quantity == expected.Quantity
                    && actual.Unit == expected.Unit
                    && string.Equals(actualSku, expected.Sku, StringComparison.Ordinal))
                {
                    exact++;
                }
            }
        }

        return new AccuracyReport(matched, wrong, exact, compared, wrongMatches, missedMasks);
    }
}
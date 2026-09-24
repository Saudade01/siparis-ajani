using System.Globalization;
using System.Text.RegularExpressions;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Parsing;

/// <summary>
/// API anahtarı olmadan çalışan ayrıştırıcı. Demo ve testler bunu kullanır (ADR-0001, karar 3).
/// Saf .NET: ağ ya da dosya erişimi yoktur, aynı girdi her zaman aynı çıktıyı verir.
/// </summary>
public sealed partial class RuleBasedOrderExtractor : IOrderExtractor
{
    public ExtractorKind Kind => ExtractorKind.RuleBased;

    public Task<ExtractionResult> ExtractAsync(string maskedText, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Extract(maskedText));
    }

    /// <summary>Eşzamanlı sürüm: testler ve doğruluk ölçümü bunu çağırır.</summary>
    public static ExtractionResult Extract(string maskedText)
    {
        if (string.IsNullOrWhiteSpace(maskedText))
        {
            return ExtractionResult.Empty;
        }

        List<ExtractedOrderLine> lines = [];

        // Selamlaşma, imza, adres ve ödeme satırları burada elenir; kalanlar ürün satırı adayıdır.
        foreach (string line in ProductLineFilter.Filter(maskedText).Lines)
        {
            if (TryReadDelimitedRow(line, out ExtractedOrderLine? row))
            {
                if (row is not null)
                {
                    lines.Add(row);
                }

                continue;
            }

            foreach (string segment in OrderTextSegmenter.Split(line))
            {
                if (ProductLineFilter.IsNoiseSegment(segment))
                {
                    continue;
                }

                ExtractedOrderLine? parsed = ParseSegment(segment);
                if (parsed is not null)
                {
                    lines.Add(parsed);
                }
            }
        }

        return new ExtractionResult(lines, []);
    }

    /// <summary>
    /// "PPRC Boru 20 mm;50;adet" biçimindeki tablo satırlarını okur.
    /// Ortadaki alan sayı değilse satır başlıktır ("Ürün;Miktar;Birim") ve atılır.
    /// </summary>
    private static bool TryReadDelimitedRow(string line, out ExtractedOrderLine? row)
    {
        row = null;
        string[] fields = line.Split(';', StringSplitOptions.TrimEntries);
        if (fields.Length != 3)
        {
            return false;
        }

        if (!OrderUnits.TryGetQuantity(fields[1], out decimal quantity))
        {
            // Başlık satırı: tüketildi say, ürün üretme.
            return true;
        }

        string product = fields[0].Trim();
        if (product.Length == 0)
        {
            return true;
        }

        string? unit = OrderUnits.TryGetUnit(fields[2], out string canonical) ? canonical : null;
        row = new ExtractedOrderLine(line.Trim(), quantity, unit, product);
        return true;
    }

    /// <summary>Tek ürünlük parçadan miktar, birim ve ürün metnini çıkarır.</summary>
    public static ExtractedOrderLine? ParseSegment(string segment)
    {
        string chunk = segment.Trim();
        if (chunk.Length == 0)
        {
            return null;
        }

        string[] words = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // 1) "5 kutu 4x40 sunta vidası" — miktar ve birim başta.
        if (words.Length >= 3
            && OrderUnits.TryGetQuantity(words[0], out decimal leadingQuantity)
            && OrderUnits.TryGetUnit(words[1], out string leadingUnit))
        {
            return Build(chunk, leadingQuantity, leadingUnit, string.Join(' ', words[2..]));
        }

        // 2) "Sds plus beton ucu 8x160 x 5" — çarpı işaretiyle yazılmış adet.
        Match suffix = QuantitySuffixRegex().Match(chunk);
        if (suffix.Success
            && decimal.TryParse(suffix.Groups["q"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal suffixQuantity))
        {
            return Build(chunk, suffixQuantity, null, suffix.Groups["p"].Value);
        }

        // 3) "sunta vidasi 4x50 3 kutu" / "Buat - 30 adet" — miktar ve birim sonda.
        if (words.Length >= 3
            && OrderUnits.TryGetQuantity(words[^2], out decimal trailingQuantity)
            && OrderUnits.TryGetUnit(words[^1], out string trailingUnit))
        {
            return Build(chunk, trailingQuantity, trailingUnit, string.Join(' ', words[..^2]));
        }

        // 4) "Hocam selamun aleykum 10 kutu sunta vidası 3.5x30" — miktar cümlenin ortasında.
        for (int index = 1; index < words.Length - 1; index++)
        {
            if (!OrderUnits.TryGetQuantity(words[index], out decimal innerQuantity)
                || !OrderUnits.TryGetUnit(words[index + 1], out string innerUnit))
            {
                continue;
            }

            string after = string.Join(' ', words[(index + 2)..]);
            string product = after.Trim().Length > 0 ? after : string.Join(' ', words[..index]);
            return Build(chunk, innerQuantity, innerUnit, product);
        }

        // 5) Miktar yok: "silikon tabancası". Varsayılanı OrderParsingService koyar ve uyarır.
        return Build(chunk, null, null, chunk);
    }

    private static ExtractedOrderLine? Build(string rawText, decimal? quantity, string? unit, string productText)
    {
        string product = productText.Trim(' ', ',', '.', ';', ':', '-', '–', '—');
        return product.Length == 0 ? null : new ExtractedOrderLine(rawText.Trim(), quantity, unit, product);
    }

    [GeneratedRegex(@"^(?<p>.+?)\s+[xX×]\s*(?<q>\d+(?:[.,]\d+)?)\s*$")]
    private static partial Regex QuantitySuffixRegex();
}
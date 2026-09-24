using System.Globalization;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Parsing;

/// <summary>
/// Sipariş çözümlemesinin akışı: maskele → ayrıştır → katalogla eşleştir → taslak üret.
/// Maskeleme her zaman ilk adımdır; ayrıştırıcılar ham metni hiç görmez.
/// </summary>
public sealed class OrderParsingService(
    IPersonalDataMasker masker,
    IOrderExtractor extractor,
    IProductMatcher matcher,
    ICatalogStore catalog,
    IAliasStore aliases,
    TimeProvider? timeProvider = null)
{
    /// <summary>Sözleşme gereği kabul edilen en uzun metin.</summary>
    public const int MaxTextLength = 20_000;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<OrderDraftDto> ParseAsync(ParseOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        MaskingResult masked = masker.Mask(request.Text);
        ExtractionResult extraction = await extractor.ExtractAsync(masked.MaskedText, cancellationToken);

        IReadOnlyList<CatalogItem> items = catalog.All;
        List<OrderLineDto> lines = new(extraction.Lines.Count);
        List<string> warnings = [.. extraction.Warnings];
        int skipped = 0;

        foreach (ExtractedOrderLine source in extraction.Lines)
        {
            ProductMatchResult match = Resolve(source.ProductText, items);

            // Miktarı da eşleşmesi de olmayan parça sipariş satırı değil, sohbet cümlesidir
            // ("Beni ... dan ara"). Satır olarak göstermek listeyi kirletir.
            if (match.Status == LineStatus.NotFound && source.Quantity is null)
            {
                skipped++;
                continue;
            }

            // Numara ancak satırın listeye gireceği kesinleştikten sonra verilir: uyarıdaki
            // numara, sonuç tablosunda görünen LineNo ile birebir aynı olmak zorunda.
            int lineNo = lines.Count + 1;

            decimal quantity = source.Quantity ?? 1m;
            if (source.Quantity is null)
            {
                warnings.Add($"{lineNo}. satırda miktar bulunamadı, 1 kabul edildi.");
            }

            if (match.Status == LineStatus.NotFound)
            {
                warnings.Add($"{lineNo}. satır katalogda bulunamadı, arayıp seçebilirsiniz.");
            }
            else if (match.Status == LineStatus.Ambiguous)
            {
                warnings.Add($"{lineNo}. satır için birden fazla aday var, doğru ürünü seçin.");
            }

            // Birim metinde ne yazıyorsa odur; katalog birimi bestMatch.unit alanında zaten var.
            lines.Add(new OrderLineDto(
                lineNo,
                source.RawText,
                quantity,
                source.Unit,
                source.ProductText,
                match.Status,
                match.BestMatch?.ToDto(),
                [.. match.Alternatives.Select(candidate => candidate.ToDto())]));
        }

        if (skipped > 0)
        {
            warnings.Add($"{skipped} satır sipariş satırı olarak anlaşılmadı ve atlandı.");
        }

        if (lines.Count == 0)
        {
            warnings.Add("Metinde sipariş satırı bulunamadı. Ürünleri alt alta yazmayı deneyin.");
        }

        return new OrderDraftDto(
            CreateDraftId(),
            extractor.Kind,
            CustomerHint: null,
            lines,
            masked.MaskedFields,
            warnings,
            _time.GetLocalNow());
    }

    /// <summary>Önce kullanıcı düzeltmelerine bakar; yoksa katalogda eşleştirir.</summary>
    private ProductMatchResult Resolve(string productText, IReadOnlyList<CatalogItem> items)
    {
        string? learnedSku = aliases.Resolve(productText);
        if (learnedSku is not null)
        {
            CatalogItem? learned = catalog.Find(learnedSku);
            if (learned is not null)
            {
                return new ProductMatchResult(LineStatus.Matched, new ScoredCandidate(learned, 1.0), []);
            }
        }

        return matcher.Match(productText, items);
    }

    private string CreateDraftId()
    {
        string stamp = _time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"d_{stamp}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}
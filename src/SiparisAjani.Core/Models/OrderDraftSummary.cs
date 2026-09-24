using SiparisAjani.Contracts;

namespace SiparisAjani.Core.Models;

/// <summary>
/// Bir çözümlemenin ölçüm özeti. KVKK kararımız gereği METİN TAŞIMAZ: ham sipariş metni,
/// satır metni, ürün metni ve maskelenen değerlerin kendisi burada yer almaz. Yalnızca
/// sayılar, eşleşme durumları, süre ve maskelenen alan TÜRLERİ tutulur; bunlar sahada
/// "kaç satır, ne kadar sürede, ne kadarı eşleşti" sorularını yanıtlamak içindir.
/// </summary>
public sealed record OrderDraftSummary(
    string DraftId,
    DateTimeOffset CreatedAt,
    OrderSource Source,
    ExtractorKind Extractor,
    int LineCount,
    int MatchedCount,
    int AmbiguousCount,
    int NotFoundCount,
    int WarningCount,
    int DurationMs,
    int CharacterCount,
    IReadOnlyList<string> MaskedFields,
    IReadOnlyList<OrderDraftLineSummary> Lines)
{
    /// <summary>
    /// Taslağı ölçüm özetine indirger. Metin alanları (rawText, productText, unit) bilinçli
    /// olarak dışarıda bırakılır; <paramref name="characterCount"/> yalnızca metnin uzunluğudur.
    /// </summary>
    public static OrderDraftSummary FromDraft(
        OrderDraftDto draft,
        OrderSource source,
        int durationMs,
        int characterCount)
    {
        ArgumentNullException.ThrowIfNull(draft);

        List<OrderDraftLineSummary> lines = [.. draft.Lines.Select(line => new OrderDraftLineSummary(
            line.LineNo,
            line.Status,
            line.BestMatch?.Sku,
            line.BestMatch?.Confidence,
            line.Alternatives.Count,
            line.Quantity))];

        return new OrderDraftSummary(
            draft.DraftId,
            draft.CreatedAt,
            source,
            draft.Extractor,
            lines.Count,
            lines.Count(line => line.Status == LineStatus.Matched),
            lines.Count(line => line.Status == LineStatus.Ambiguous),
            lines.Count(line => line.Status == LineStatus.NotFound),
            draft.Warnings.Count,
            durationMs,
            characterCount,
            [.. draft.MaskedFields],
            lines);
    }
}

/// <summary>
/// Tek satırın ölçüm özeti. Satırın metni saklanmaz; hangi stok koduna, hangi güvenle
/// bağlandığı saklanır (eşleştirme doğruluğunu sonradan ölçebilmek için).
/// </summary>
public sealed record OrderDraftLineSummary(
    int LineNo,
    LineStatus Status,
    string? MatchedSku,
    double? Confidence,
    int AlternativeCount,
    decimal? Quantity);
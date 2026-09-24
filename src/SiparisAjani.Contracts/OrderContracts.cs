using System.Text.Json.Serialization;

namespace SiparisAjani.Contracts;

// API sözleşmesi. Sahibi: mimar. Değişiklikler önce docs/sozlesmeler/api.md'ye yazılır,
// sonra burada uygulanır; backend ve frontend yalnızca bu tipleri kullanır.

[JsonConverter(typeof(JsonStringEnumConverter<OrderSource>))]
public enum OrderSource
{
    WhatsApp,
    Email,
    Phone,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter<LineStatus>))]
public enum LineStatus
{
    /// <summary>Güven skoru eşik üstünde, tek aday.</summary>
    Matched,
    /// <summary>Birden fazla makul aday var; kullanıcı seçmeli.</summary>
    Ambiguous,
    /// <summary>Katalogda makul aday bulunamadı.</summary>
    NotFound
}

[JsonConverter(typeof(JsonStringEnumConverter<ExtractorKind>))]
public enum ExtractorKind
{
    /// <summary>Claude API ile yapılandırılmış çıktı.</summary>
    Claude,
    /// <summary>API anahtarı yokken çalışan kural tabanlı ayrıştırıcı.</summary>
    RuleBased
}

public sealed record ParseOrderRequest(string Text, OrderSource Source = OrderSource.WhatsApp);

public sealed record CatalogItemDto(string Sku, string Name, string Unit, string? Barcode, string? Category);

public sealed record MatchCandidateDto(string Sku, string Name, string Unit, double Confidence);

/// <summary>
/// Çözümlenmiş tek sipariş satırı.
/// <c>Unit</c>, müşterinin metinde yazdığı birimdir ("koli", "çuval"); ürünün katalog birimi
/// <c>BestMatch.Unit</c> alanındadır ve ikisi farklı olabilir.
/// </summary>
public sealed record OrderLineDto(
    int LineNo,
    string RawText,
    decimal? Quantity,
    string? Unit,
    string ProductText,
    LineStatus Status,
    MatchCandidateDto? BestMatch,
    IReadOnlyList<MatchCandidateDto> Alternatives);

/// <summary>
/// Bir çözümlemenin sonucu.
/// <c>DraftId</c> yalnızca izleme/teşhis kimliğidir: sunucu taslak durumu tutmaz, bu kimlikle
/// sonradan taslak çağrılamaz ve dışa aktarma isteği onu taşımaz (ADR-0002).
/// <c>MaskedFields</c> maskelenen alan türlerini içerir: phone, email, iban, nationalId, name.
/// Değerlerin kendisi asla dönmez; liste ileride büyüyebilir, bilinmeyen tür genel bir
/// ifadeyle gösterilmelidir.
/// </summary>
public sealed record OrderDraftDto(
    string DraftId,
    ExtractorKind Extractor,
    string? CustomerHint,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<string> MaskedFields,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedAt);

public sealed record CorrectionRequest(string ProductText, string Sku);

/// <summary>
/// Dışa aktarılacak tek satır. Ürün adı TAŞINMAZ: sunucu <c>Sku</c> kodunu katalogda çözüp
/// Excel'deki ad sütununu kendisi doldurur (katalogda yoksa 400).
/// <c>Unit</c> müşterinin yazdığı birimdir; boş bırakılırsa katalog birimi yazılır.
/// </summary>
public sealed record ExportLineDto(string Sku, decimal Quantity, string Unit);

public sealed record ExportOrderRequest(
    IReadOnlyList<ExportLineDto> Lines,
    string? CustomerCode = null,
    DateOnly? OrderDate = null,
    string? Note = null);

public sealed record CatalogImportResultDto(int Imported, int Skipped, IReadOnlyList<string> Errors);

public sealed record HealthDto(string Status, ExtractorKind Extractor, int CatalogCount, string Version);

/// <summary>
/// Sunucunun ürettiği RFC 9457 ProblemDetails gövdesinin istemci tarafı okuma modelidir.
/// Sunucu bu tipi elle üretmez (<c>Results.Problem(...)</c> kullanılır) ve alanların üçü de
/// eksik gelebilir; okuyan taraf boş <c>Detail</c> için kendi Türkçe cümlesini göstermelidir.
/// </summary>
public sealed record ProblemDto(string Title, string Detail, int Status);
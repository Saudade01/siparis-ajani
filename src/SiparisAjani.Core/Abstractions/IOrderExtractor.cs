using SiparisAjani.Contracts;

using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>Maskelenmiş sipariş metnini satırlara ayırır. Uygulamalar: kural tabanlı ve Claude.</summary>
public interface IOrderExtractor
{
    ExtractorKind Kind { get; }

    /// <param name="maskedText">Maskelenmiş metin. Ham metin buraya asla geçmez.</param>
    Task<ExtractionResult> ExtractAsync(string maskedText, CancellationToken cancellationToken = default);
}
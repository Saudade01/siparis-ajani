using SiparisAjani.Contracts;

namespace SiparisAjani.Infrastructure.Persistence.Entities;

/// <summary>
/// Taslak satırının ölçüm kaydı. Satır metni ve ürün metni saklanmaz; hangi stok koduna
/// hangi güvenle bağlandığı saklanır, eşleştirme doğruluğunu sonradan ölçebilmek için.
/// </summary>
public sealed class OrderDraftLineEntity
{
    public int Id { get; set; }

    public int OrderDraftId { get; set; }

    public OrderDraftEntity? OrderDraft { get; set; }

    public int LineNo { get; set; }

    public LineStatus Status { get; set; }

    /// <summary>Eşleşen stok kodu; eşleşme yoksa null.</summary>
    public string? MatchedSku { get; set; }

    public double? Confidence { get; set; }

    public int AlternativeCount { get; set; }

    /// <summary>Satırın miktarı. SQLite decimal'i TEXT olarak saklar; bu sütunda sıralama yapmıyoruz.</summary>
    public decimal? Quantity { get; set; }
}
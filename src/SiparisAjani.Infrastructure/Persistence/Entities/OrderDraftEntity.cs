using SiparisAjani.Contracts;

namespace SiparisAjani.Infrastructure.Persistence.Entities;

/// <summary>
/// Çözümlenen bir taslağın ölçüm kaydı. KVKK kararımız gereği sipariş metni, müşteri bilgisi
/// ve maskelenen değerler bu tabloda YOKTUR; yalnızca sayılar, süre ve maskelenen alan türleri.
/// </summary>
public sealed class OrderDraftEntity
{
    public int Id { get; set; }

    /// <summary>Sözleşmedeki izleme kimliği (<c>d_...</c>). Benzersiz indeksi vardır.</summary>
    public string DraftId { get; set; } = string.Empty;

    /// <summary>Kayıt zamanı. "Son N taslak" sorgusu bu sütunun indeksini kullanır.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public OrderSource Source { get; set; }

    public ExtractorKind Extractor { get; set; }

    public int LineCount { get; set; }

    public int MatchedCount { get; set; }

    public int AmbiguousCount { get; set; }

    public int NotFoundCount { get; set; }

    public int WarningCount { get; set; }

    /// <summary>Çözümlemenin sürdüğü süre (ms). Sahada ölçtüğümüz sayı.</summary>
    public int DurationMs { get; set; }

    /// <summary>Gelen metnin uzunluğu. Metnin kendisi değil, yalnızca karakter sayısı.</summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Maskelenen alan TÜRLERİ (<c>phone</c>, <c>name</c> ...). Değerler değil.
    /// JSON dizi olarak tek sütunda saklanır (EF Core ilkel koleksiyon eşlemesi).
    /// </summary>
    public List<string> MaskedFields { get; set; } = [];

    public List<OrderDraftLineEntity> Lines { get; } = [];
}
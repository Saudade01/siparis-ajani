using SiparisAjani.Contracts;

namespace SiparisAjani.Web.Models;

/// <summary>Satır için seçili katalog ürünü (arayüz durumu, API sözleşmesi değildir).</summary>
public sealed record SelectedProduct(string Sku, string Name, string Unit, double? Confidence, bool IsManual);

/// <summary>Kullanıcının bir satır için yaptığı ürün seçimi.</summary>
public sealed record ProductChoice(OrderLineState Line, string Sku, string Name, string Unit);

/// <summary>
/// Sipariş tablosundaki bir satırın düzenlenebilir hali. API'den gelen <see cref="OrderLineDto"/> değişmez;
/// kullanıcının miktar ve ürün seçimleri burada tutulur.
/// </summary>
public sealed class OrderLineState
{
    public OrderLineState(OrderLineDto source)
    {
        Source = source;
        Quantity = source.Quantity;
        Candidates = BuildCandidates(source);

        if (source.Status == LineStatus.Matched && source.BestMatch is not null)
        {
            Status = LineStatus.Matched;
            Product = new SelectedProduct(
                source.BestMatch.Sku,
                source.BestMatch.Name,
                source.BestMatch.Unit,
                source.BestMatch.Confidence,
                IsManual: false);
        }
        else
        {
            Status = source.Status == LineStatus.Matched ? LineStatus.NotFound : source.Status;
        }

        OriginalStatus = Status;
    }

    /// <summary>HTML kimlikleri için satıra özgü kısa anahtar.</summary>
    public string Key { get; } = Guid.NewGuid().ToString("N")[..10];

    public OrderLineDto Source { get; }

    public int LineNo => Source.LineNo;

    public string RawText => Source.RawText;

    public string ProductText => Source.ProductText;

    public decimal? Quantity { get; set; }

    public LineStatus OriginalStatus { get; }

    public LineStatus Status { get; private set; }

    public SelectedProduct? Product { get; private set; }

    /// <summary>Belirsiz satır için adaylar: en iyi eşleşme ve alternatifler, güvene göre sıralı.</summary>
    public IReadOnlyList<MatchCandidateDto> Candidates { get; }

    /// <summary>Kullanıcı aday listesi yerine katalogda arama yapıyor.</summary>
    public bool IsSearching { get; set; }

    public bool IsSavingCorrection { get; set; }

    public string? CorrectionError { get; set; }

    public bool CorrectionSaved { get; set; }

    public bool IsMatched => Status == LineStatus.Matched && Product is not null;

    public bool HasValidQuantity => Quantity is > 0;

    /// <summary>Müşterinin yazdığı birim; yoksa ürünün katalog birimi.</summary>
    public string? DisplayUnit => string.IsNullOrWhiteSpace(Source.Unit) ? Product?.Unit : Source.Unit;

    /// <summary>Elle çözülen satır yeniden seçime açılabilir.</summary>
    public bool CanReopen => Product?.IsManual == true && OriginalStatus != LineStatus.Matched;

    public void Choose(string sku, string name, string unit)
    {
        Product = new SelectedProduct(sku, name, unit, Confidence: null, IsManual: true);
        Status = LineStatus.Matched;
        IsSearching = false;
        CorrectionError = null;
        CorrectionSaved = false;
    }

    public void Reopen()
    {
        Product = null;
        Status = OriginalStatus;
        IsSearching = false;
        IsSavingCorrection = false;
        CorrectionError = null;
        CorrectionSaved = false;
    }

    public ExportLineDto ToExportLine()
    {
        if (Product is null || Status != LineStatus.Matched || Quantity is not > 0)
        {
            throw new InvalidOperationException($"{LineNo}. satır aktarıma hazır değil.");
        }

        return new ExportLineDto(Product.Sku, Quantity.Value, DisplayUnit ?? Product.Unit);
    }

    private static List<MatchCandidateDto> BuildCandidates(OrderLineDto source)
    {
        List<MatchCandidateDto> candidates = [];
        if (source.BestMatch is not null)
        {
            candidates.Add(source.BestMatch);
        }

        foreach (MatchCandidateDto alternative in source.Alternatives ?? Array.Empty<MatchCandidateDto>())
        {
            bool alreadyListed = candidates.Exists(
                candidate => string.Equals(candidate.Sku, alternative.Sku, StringComparison.OrdinalIgnoreCase));
            if (!alreadyListed)
            {
                candidates.Add(alternative);
            }
        }

        candidates.Sort(static (left, right) => right.Confidence.CompareTo(left.Confidence));
        return candidates;
    }
}
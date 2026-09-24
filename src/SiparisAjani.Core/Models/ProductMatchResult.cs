using SiparisAjani.Contracts;

namespace SiparisAjani.Core.Models;

/// <summary>Tek bir aday ürün ve güven skoru (0–1).</summary>
public sealed record ScoredCandidate(CatalogItem Item, double Confidence)
{
    public MatchCandidateDto ToDto() => new(Item.Sku, Item.Name, Item.Unit, Math.Round(Confidence, 2));
}

/// <summary>Eşleştirme sonucu: durum, en iyi aday ve (belirsizse) alternatifler.</summary>
public sealed record ProductMatchResult(
    LineStatus Status,
    ScoredCandidate? BestMatch,
    IReadOnlyList<ScoredCandidate> Alternatives)
{
    public static ProductMatchResult NotFound { get; } = new(LineStatus.NotFound, null, []);
}
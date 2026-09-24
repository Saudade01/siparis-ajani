using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Matching;

/// <summary>
/// Ürün metnini katalogla eşleştirir.
/// Skor iki parçadan oluşur: sorgu kapsaması (sorgudaki kelimelerin ne kadarı üründe var)
/// ve ürün kapsaması (ürün adındaki ayırt edici kelimelerin ne kadarı sorguda var).
/// İkincisi olmadan "Buat" sorgusu dört farklı buat ürününe %100 uyar.
/// Eşikler sözleşmeden gelir (docs/sozlesmeler/api.md).
/// </summary>
public sealed class ProductMatcher : IProductMatcher
{
    /// <summary>Eşleşti demek için gereken en düşük güven.</summary>
    public const double MatchedThreshold = 0.80;

    /// <summary>Eşleşti demek için birinci ile ikinci aday arasındaki en küçük fark.</summary>
    public const double SeparationThreshold = 0.10;

    /// <summary>Belirsiz aday sayılmak için gereken en düşük güven.</summary>
    public const double AmbiguousThreshold = 0.45;

    /// <summary>Belirsiz satırda gösterilecek en fazla alternatif sayısı.</summary>
    public const int MaxAlternatives = 3;

    private readonly Lock _gate = new();
    private IReadOnlyList<CatalogItem>? _cachedSource;
    private CatalogIndex? _cachedIndex;

    public ProductMatchResult Match(string productText, IReadOnlyList<CatalogItem> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        IReadOnlyList<string> queryTokens = TurkishTextNormalizer.Tokenize(productText);
        if (catalog.Count == 0 || queryTokens.Count == 0)
        {
            return ProductMatchResult.NotFound;
        }

        CatalogIndex index = GetIndex(catalog);
        string normalizedQuery = TurkishTextNormalizer.Normalize(productText);

        List<ScoredCandidate> scored = new(index.Items.Count);
        foreach (IndexedItem indexed in index.Items)
        {
            double confidence = Score(queryTokens, normalizedQuery, indexed, index);
            if (confidence > 0.01)
            {
                scored.Add(new ScoredCandidate(indexed.Item, confidence));
            }
        }

        if (scored.Count == 0)
        {
            return ProductMatchResult.NotFound;
        }

        scored.Sort(static (left, right) => right.Confidence.CompareTo(left.Confidence));

        ScoredCandidate best = scored[0];
        double runnerUp = scored.Count > 1 ? scored[1].Confidence : 0.0;

        if (best.Confidence >= MatchedThreshold && best.Confidence - runnerUp >= SeparationThreshold)
        {
            return new ProductMatchResult(LineStatus.Matched, best, []);
        }

        if (best.Confidence >= AmbiguousThreshold)
        {
            List<ScoredCandidate> alternatives = scored
.Skip(1)
.Where(candidate => candidate.Confidence >= AmbiguousThreshold)
.Take(MaxAlternatives)
.ToList();
            return new ProductMatchResult(LineStatus.Ambiguous, best, alternatives);
        }

        return ProductMatchResult.NotFound;
    }

    private static double Score(
        IReadOnlyList<string> queryTokens,
        string normalizedQuery,
        IndexedItem indexed,
        CatalogIndex index)
    {
        // Barkod, stok kodu ya da tam ürün adı verildiyse tartışma yok.
        if (indexed.NormalizedBarcode is not null
            && string.Equals(indexed.NormalizedBarcode, normalizedQuery, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (string.Equals(indexed.NormalizedSku, normalizedQuery, StringComparison.Ordinal)
            || string.Equals(indexed.NormalizedName, normalizedQuery, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (indexed.Tokens.Count == 0)
        {
            return 0.0;
        }

        double queryCovered = 0.0;
        double queryTotal = 0.0;
        double[] itemBest = new double[indexed.Tokens.Count];

        foreach (string queryToken in queryTokens)
        {
            double weight = index.Weight(queryToken);
            queryTotal += weight;

            double best = 0.0;
            for (int position = 0; position < indexed.Tokens.Count; position++)
            {
                double similarity = TokenSimilarity.Compare(queryToken, indexed.Tokens[position]);
                if (similarity > itemBest[position])
                {
                    itemBest[position] = similarity;
                }

                if (similarity > best)
                {
                    best = similarity;
                }
            }

            queryCovered += best * weight;
        }

        if (queryTotal <= 0.0)
        {
            return 0.0;
        }

        double itemCovered = 0.0;
        double itemTotal = 0.0;
        for (int position = 0; position < indexed.Tokens.Count; position++)
        {
            double weight = index.Weight(indexed.Tokens[position]);
            itemTotal += weight;
            itemCovered += itemBest[position] * weight;
        }

        double queryCoverage = queryCovered / queryTotal;
        double itemCoverage = itemTotal > 0.0 ? itemCovered / itemTotal : 0.0;
        return queryCoverage * (0.5 + (0.5 * itemCoverage));
    }

    private CatalogIndex GetIndex(IReadOnlyList<CatalogItem> catalog)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_cachedSource, catalog) || _cachedIndex is null)
            {
                _cachedIndex = CatalogIndex.Build(catalog);
                _cachedSource = catalog;
            }

            return _cachedIndex;
        }
    }
}
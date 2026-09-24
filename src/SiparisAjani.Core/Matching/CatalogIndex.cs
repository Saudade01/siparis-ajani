using System.Text.RegularExpressions;

using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Matching;

/// <summary>Katalogdaki tek ürünün eşleştirmeye hazırlanmış hâli.</summary>
internal sealed record IndexedItem(
    CatalogItem Item,
    IReadOnlyList<string> Tokens,
    string NormalizedName,
    string NormalizedSku,
    string? NormalizedBarcode);

/// <summary>
/// Katalogun eşleştirme için önceden hesaplanmış dizini.
/// Belirteç ağırlıkları IDF ile verilir: "plastik" gibi sık geçen kelimeler az,
/// "kelebek" gibi ayırt edici kelimeler çok ağırlık taşır.
/// </summary>
internal sealed partial class CatalogIndex
{
    private readonly Dictionary<string, double> _weights;

    private CatalogIndex(IReadOnlyList<IndexedItem> items, Dictionary<string, double> weights, double unknownWeight)
    {
        Items = items;
        _weights = weights;
        UnknownWeight = unknownWeight;
    }

    public IReadOnlyList<IndexedItem> Items { get; }

    /// <summary>Katalogda hiç geçmeyen bir sorgu belirtecinin ağırlığı (en yüksek).</summary>
    public double UnknownWeight { get; }

    public double Weight(string token) => _weights.TryGetValue(token, out double weight) ? weight : UnknownWeight;

    public static CatalogIndex Build(IReadOnlyList<CatalogItem> catalog)
    {
        List<IndexedItem> items = new(catalog.Count);
        Dictionary<string, int> documentFrequency = new(StringComparer.Ordinal);

        foreach (CatalogItem item in catalog)
        {
            // Parantez içi ambalaj bilgisidir ("(500'lü kutu)"); eşleştirmede ayırt edici değil.
            string core = ParentheticalRegex().Replace(item.Name, " ");
            IReadOnlyList<string> tokens = TurkishTextNormalizer.Tokenize(core);
            items.Add(new IndexedItem(
                item,
                tokens,
                TurkishTextNormalizer.Normalize(core),
                TurkishTextNormalizer.Normalize(item.Sku),
                string.IsNullOrWhiteSpace(item.Barcode) ? null : TurkishTextNormalizer.Normalize(item.Barcode)));

            foreach (string token in tokens.Distinct(StringComparer.Ordinal))
            {
                documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
            }
        }

        int total = Math.Max(items.Count, 1);
        double unknownWeight = Math.Log(total + 1.0);
        Dictionary<string, double> weights = new(StringComparer.Ordinal);
        foreach ((string token, int frequency) in documentFrequency)
        {
            weights[token] = Math.Max(Math.Log((double)total / frequency), 0.1);
        }

        return new CatalogIndex(items, weights, unknownWeight);
    }

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex ParentheticalRegex();
}
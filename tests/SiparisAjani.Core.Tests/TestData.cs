using System.Text.Json;

using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Catalog;

namespace SiparisAjani.Core.Tests;

/// <summary>Beklenen sonuç dosyasındaki tek satır.</summary>
public sealed record ExpectedLine(decimal Quantity, string? Unit, string? Sku);

/// <summary>Bir örnek sipariş ve beklenen sonucu.</summary>
public sealed record SampleOrder(string Id, string Text, IReadOnlyList<ExpectedLine> Lines, IReadOnlyList<string> MaskedFields);

/// <summary><c>data/</c> altındaki sentetik veriyi testlere açar.</summary>
public static class TestData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Root { get; } = FindRepositoryRoot();

    public static IReadOnlyList<CatalogItem> Catalog { get; } = LoadCatalog();

    public static IReadOnlyList<SampleOrder> Samples { get; } = LoadSamples();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "data", "ornek-siparisler")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("data/ klasörü bulunamadı.");
    }

    private static IReadOnlyList<CatalogItem> LoadCatalog()
    {
        using FileStream stream = File.OpenRead(Path.Combine(Root, "data", "katalog", "hirdavat-ornek.csv"));
        return CsvCatalogLoader.Load(stream).Items;
    }

    private static IReadOnlyList<SampleOrder> LoadSamples()
    {
        string folder = Path.Combine(Root, "data", "ornek-siparisler");
        List<SampleOrder> samples = [];

        foreach (string path in Directory.GetFiles(folder, "*.txt").OrderBy(name => name, StringComparer.Ordinal))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            string expectedPath = Path.Combine(folder, $"{id}.beklenen.json");
            if (!File.Exists(expectedPath))
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(expectedPath));
            List<ExpectedLine> lines = [];
            foreach (JsonElement element in document.RootElement.GetProperty("lines").EnumerateArray())
            {
                lines.Add(new ExpectedLine(
                    element.GetProperty("quantity").GetDecimal(),
                    ReadNullableString(element, "unit"),
                    ReadNullableString(element, "sku")));
            }

            List<string> maskedFields = [];
            foreach (JsonElement element in document.RootElement.GetProperty("maskedFields").EnumerateArray())
            {
                if (element.GetString() is { } field)
                {
                    maskedFields.Add(field);
                }
            }

            samples.Add(new SampleOrder(id, File.ReadAllText(path), lines, maskedFields));
        }

        return samples;
    }

    private static string? ReadNullableString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
           : null;
    }

    /// <summary>Kullanılmayan seçeneği bastırmak için; testlerde JSON ayarı gerekirse buradan alınır.</summary>
    public static JsonSerializerOptions Json => JsonOptions;
}
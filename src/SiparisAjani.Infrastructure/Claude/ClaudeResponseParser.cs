using System.Globalization;
using System.Text.Json;

using SiparisAjani.Core.Models;

namespace SiparisAjani.Infrastructure.Claude;

/// <summary>
/// Modelin ürettiği JSON'u sipariş satırlarına çevirir.
/// Ağdan ayrı tutuldu ki testler gerçek API çağırmadan doğrulayabilsin.
/// </summary>
public static class ClaudeResponseParser
{
    /// <summary>Modelden istenen JSON şeması.</summary>
    public const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "lines": {
              "type": "array",
              "description": "Siparişteki ürün satırları, metindeki sırayla.",
              "items": {
                "type": "object",
                "properties": {
                  "rawText": { "type": "string", "description": "Satırın kaynak metni." },
                  "quantity": { "type": ["number", "null"], "description": "Miktar; yazılmamışsa null." },
                  "unit": { "type": ["string", "null"], "description": "Birim: adet, kutu, paket, koli, metre, çuval, rulo, kova, çift, takım. Yazılmamışsa null." },
                  "productText": { "type": "string", "description": "Yalnızca ürün adı; miktar ve birim çıkarılmış hâli." }
                },
                "required": ["rawText", "quantity", "unit", "productText"],
                "additionalProperties": false
              }
            }
          },
          "required": ["lines"],
          "additionalProperties": false
        }
        """;

    /// <summary>Şemayı SDK'nın beklediği sözlük biçiminde verir.</summary>
    public static Dictionary<string, JsonElement> CreateSchema()
    {
        using JsonDocument document = JsonDocument.Parse(SchemaJson);
        Dictionary<string, JsonElement> schema = new(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            schema[property.Name] = property.Value.Clone();
        }

        return schema;
    }

    /// <summary>
    /// Modelin JSON çıktısını satırlara çevirir. Bozuk ya da eksik alanlar sessizce atlanır;
    /// ayrıştırma hatası kullanıcıya boş sonuç olarak döner, istisna fırlatılmaz.
    /// </summary>
    public static ExtractionResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ExtractionResult.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("lines", out JsonElement linesElement)
                || linesElement.ValueKind != JsonValueKind.Array)
            {
                return ExtractionResult.Empty;
            }

            List<ExtractedOrderLine> lines = [];
            foreach (JsonElement element in linesElement.EnumerateArray())
            {
                ExtractedOrderLine? line = ReadLine(element);
                if (line is not null)
                {
                    lines.Add(line);
                }
            }

            return new ExtractionResult(lines, []);
        }
        catch (JsonException)
        {
            return ExtractionResult.Empty;
        }
    }

    private static ExtractedOrderLine? ReadLine(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string productText = ReadString(element, "productText") ?? string.Empty;
        if (productText.Trim().Length == 0)
        {
            return null;
        }

        string rawText = ReadString(element, "rawText") ?? productText;
        string? unit = ReadString(element, "unit");
        decimal? quantity = ReadQuantity(element);

        return new ExtractedOrderLine(rawText.Trim(), quantity, string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(), productText.Trim());
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static decimal? ReadQuantity(JsonElement element)
    {
        if (!element.TryGetProperty("quantity", out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal parsed) => parsed,
            _ => null
        };
    }
}
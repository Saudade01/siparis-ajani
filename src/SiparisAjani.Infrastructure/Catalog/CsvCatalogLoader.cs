using System.Text;

using SiparisAjani.Core.Models;

namespace SiparisAjani.Infrastructure.Catalog;

/// <summary>CSV okuma sonucu: alınan ürünler, atlanan satır sayısı ve kullanıcıya gösterilecek hatalar.</summary>
public sealed record CatalogLoadResult(IReadOnlyList<CatalogItem> Items, int Skipped, IReadOnlyList<string> Errors);

/// <summary>
/// Katalog CSV'sini okur. Ayraç ilk satırdan bulunur (";" ya da ","), başlıklar:
/// <c>sku,name,unit,barcode,category</c>. Kodlama UTF-8 (BOM'lu ya da BOM'suz).
/// </summary>
public static class CsvCatalogLoader
{
    private const int MaxErrors = 20;

    private static readonly string[] RequiredHeaders = ["sku", "name", "unit"];

    public static CatalogLoadResult Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Load(reader);
    }

    public static CatalogLoadResult Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        string? headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return new CatalogLoadResult([], 0, ["Dosya boş."]);
        }

        char delimiter = DetectDelimiter(headerLine);
        Dictionary<string, int> columns = MapColumns(ParseFields(headerLine, delimiter));

        string[] missing = [.. RequiredHeaders.Where(header => !columns.ContainsKey(header))];
        if (missing.Length > 0)
        {
            return new CatalogLoadResult([], 0, [$"Zorunlu sütunlar eksik: {string.Join(", ", missing)}."]);
        }

        List<CatalogItem> items = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> errors = [];
        int skipped = 0;
        int lineNumber = 1;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            string[] fields = ParseFields(line, delimiter);
            string sku = Value(fields, columns, "sku");
            string name = Value(fields, columns, "name");

            if (sku.Length == 0 || name.Length == 0)
            {
                skipped++;
                AddError(errors, $"{lineNumber}. satır: stok kodu ya da ürün adı boş.");
                continue;
            }

            if (!seen.Add(sku))
            {
                skipped++;
                AddError(errors, $"{lineNumber}. satır: {sku} stok kodu tekrar ediyor.");
                continue;
            }

            string unit = Value(fields, columns, "unit");
            items.Add(new CatalogItem(
                sku,
                name,
                unit.Length == 0 ? "adet" : unit,
                NullIfEmpty(Value(fields, columns, "barcode")),
                NullIfEmpty(Value(fields, columns, "category"))));
        }

        return new CatalogLoadResult(items, skipped, errors);
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
        {
            errors.Add(message);
        }
        else if (errors.Count == MaxErrors)
        {
            errors.Add("Daha fazla hata var; ilk 20 tanesi gösteriliyor.");
        }
    }

    private static char DetectDelimiter(string headerLine)
    {
        return headerLine.Count(symbol => symbol == ';') > headerLine.Count(symbol => symbol == ',') ? ';' : ',';
    }

    private static Dictionary<string, int> MapColumns(string[] headers)
    {
        Dictionary<string, int> columns = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < headers.Length; index++)
        {
            string header = headers[index].Trim().Trim('﻿');
            if (header.Length > 0)
            {
                columns[header] = index;
            }
        }

        return columns;
    }

    private static string Value(string[] fields, Dictionary<string, int> columns, string header)
    {
        return columns.TryGetValue(header, out int index) && index < fields.Length ? fields[index].Trim() : string.Empty;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>Tırnak içindeki ayraçları koruyan küçük CSV alan ayrıştırıcısı.</summary>
    private static string[] ParseFields(string line, char delimiter)
    {
        List<string> fields = [];
        StringBuilder current = new();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char symbol = line[index];
            if (inQuotes)
            {
                if (symbol == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(symbol);
                }
            }
            else if (symbol == '"')
            {
                inQuotes = true;
            }
            else if (symbol == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(symbol);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
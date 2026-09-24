using System.Globalization;

using ClosedXML.Excel;

using Microsoft.Extensions.Options;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;

namespace SiparisAjani.Infrastructure.Export;

/// <summary>
/// Sipariş satırlarını ClosedXML ile .xlsx dosyasına yazar (ADR-0001, karar 5).
/// Sütun düzeni yapılandırmadan gelir; varsayılanlar için <see cref="LogoExportOptions"/>
/// üzerindeki doğrulama notuna bakın.
/// </summary>
public sealed class ExcelOrderExporter(IOptions<LogoExportOptions> options) : IOrderExporter
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ExportedFile Export(ExportOrderRequest request, IReadOnlyList<CatalogItem> resolvedItems)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolvedItems);

        LogoExportOptions settings = options.Value;
        List<LogoExportColumn> columns = settings.Columns.Count > 0
            ? settings.Columns
            : LogoExportOptions.CreateDefault().Columns;

        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.AddWorksheet(SheetName(settings));

        for (int column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = columns[column].Header;
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }

        DateOnly orderDate = request.OrderDate ?? DateOnly.FromDateTime(DateTime.Now);

        for (int row = 0; row < request.Lines.Count; row++)
        {
            ExportLineDto line = request.Lines[row];
            CatalogItem? item = row < resolvedItems.Count ? resolvedItems[row] : null;

            for (int column = 0; column < columns.Count; column++)
            {
                IXLCell cell = sheet.Cell(row + 2, column + 1);
                switch (columns[column].Field)
                {
                    case LogoExportField.LineNo:
                        cell.Value = row + 1;
                        break;
                    case LogoExportField.Sku:
                        cell.SetValue(line.Sku);
                        break;
                    case LogoExportField.Name:
                        cell.SetValue(item?.Name ?? string.Empty);
                        break;
                    case LogoExportField.Quantity:
                        cell.Value = line.Quantity;
                        break;
                    case LogoExportField.Unit:
                        cell.SetValue(string.IsNullOrWhiteSpace(line.Unit) ? item?.Unit ?? string.Empty : line.Unit);
                        break;
                    case LogoExportField.CustomerCode:
                        cell.SetValue(request.CustomerCode ?? string.Empty);
                        break;
                    case LogoExportField.OrderDate:
                        cell.Value = orderDate.ToDateTime(TimeOnly.MinValue);
                        cell.Style.DateFormat.Format = "dd.MM.yyyy";
                        break;
                    case LogoExportField.Note:
                        cell.SetValue(request.Note ?? string.Empty);
                        break;
                    default:
                        break;
                }
            }
        }

        sheet.Columns().AdjustToContents();

        using MemoryStream buffer = new();
        workbook.SaveAs(buffer);

        return new ExportedFile(buffer.ToArray(), CreateFileName(), ExcelContentType);
    }

    /// <summary>Sözleşmedeki ad: <c>siparis-YYYYMMDD-HHmm.xlsx</c>.</summary>
    public static string CreateFileName()
    {
        return $"siparis-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.xlsx";
    }

    private static string SheetName(LogoExportOptions settings)
    {
        string name = string.IsNullOrWhiteSpace(settings.SheetName) ? "SIPARIS" : settings.SheetName.Trim();
        return name.Length > 31 ? name[..31] : name;
    }
}
using SiparisAjani.Contracts;

using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>Onaylanmış sipariş satırlarını ERP'ye aktarılabilir bir dosyaya yazar.</summary>
public interface IOrderExporter
{
    /// <param name="request">Dışa aktarılacak satırlar.</param>
    /// <param name="resolvedItems">
    /// <paramref name="request"/> satırlarıyla aynı sırada, katalogda çözümlenmiş ürünler.
    /// </param>
    ExportedFile Export(ExportOrderRequest request, IReadOnlyList<CatalogItem> resolvedItems);
}
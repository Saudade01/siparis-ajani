namespace SiparisAjani.Contracts;

// Tek kaynak: Web istemcisi ve API uç noktaları bu sabitleri kullanır.
public static class ApiRoutes
{
    public const string Health = "/api/health";
    public const string Catalog = "/api/catalog";               // GET ?q=&limit=
    public const string CatalogImport = "/api/catalog/import";  // POST multipart/form-data (file)
    public const string ParseOrder = "/api/orders/parse";       // POST ParseOrderRequest -> OrderDraftDto
    public const string Corrections = "/api/orders/corrections"; // POST CorrectionRequest -> 204
    public const string ExportOrder = "/api/orders/export";     // POST ExportOrderRequest -> xlsx
}
using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Catalog;

namespace SiparisAjani.Api.Endpoints;

/// <summary>Katalog arama ve CSV yükleme uç noktaları.</summary>
public static class CatalogEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Catalog, (ICatalogStore catalog, string? q = null, int limit = DefaultLimit) =>
        {
            IReadOnlyList<CatalogItem> items = catalog.Search(q, Math.Clamp(limit, 1, MaxLimit));
            return Results.Ok(items.Select(item => item.ToDto()).ToList());
        })
.WithName("SearchCatalog");

        app.MapPost(ApiRoutes.CatalogImport, async (HttpRequest request, ICatalogStore catalog, ILoggerFactory loggers) =>
        {
            ILogger logger = loggers.CreateLogger("CatalogImport");

            if (!request.HasFormContentType)
            {
                return Problem("Dosya bekleniyordu.", "İsteği multipart/form-data olarak ve 'file' alanıyla gönderin.");
            }

            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync();
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                // Bozuk multipart gövdesi kullanıcı hatasıdır; 500 dönmemeli.
                logger.LogWarning("Katalog dosyası okunamadı: {ExceptionType}", exception.GetType().Name);
                return Problem("Dosya okunamadı.", "Dosyayı tekrar seçip gönderin.");
            }

            IFormFile? file = form.Files["file"];
            if (file is null || file.Length == 0)
            {
                return Problem("Dosya bulunamadı.", "'file' alanında boş olmayan bir CSV dosyası gönderin.");
            }

            await using Stream stream = file.OpenReadStream();
            CatalogLoadResult result = CsvCatalogLoader.Load(stream);

            if (result.Items.Count == 0)
            {
                // Katalogu boşaltmak yerine mevcut hâlini koru: yanlış dosya demoyu bozmasın.
                logger.LogWarning("Katalog yüklenemedi: {ErrorCount} hata.", result.Errors.Count);
                return Problem(
                    "Katalog okunamadı.",
                    result.Errors.Count > 0
                        ? string.Join(" ", result.Errors)
                        : "Dosyada geçerli ürün satırı yok. Başlıklar: sku,name,unit,barcode,category");
            }

            catalog.Replace(result.Items);
            logger.LogInformation(
                "Katalog içe aktarıldı: {Imported} ürün, {Skipped} atlandı.",
                result.Items.Count,
                result.Skipped);

            return Results.Ok(new CatalogImportResultDto(result.Items.Count, result.Skipped, result.Errors));
        })
.WithName("ImportCatalog")
.DisableAntiforgery();
    }

    private static IResult Problem(string title, string detail)
    {
        return Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
    }
}
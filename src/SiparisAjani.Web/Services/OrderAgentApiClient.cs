using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

using SiparisAjani.Contracts;

namespace SiparisAjani.Web.Services;

/// <summary>
/// SiparisAjani API'sine giden tüm HTTP çağrıları bu istemciden geçer.
/// Hiçbir metot istisna fırlatmaz (çağıran iptal etmedikçe); hatalar <see cref="ApiResult{T}"/> içinde döner.
/// Günlüğe istek gövdesi yazılmaz: sipariş metni kişisel veri içerebilir.
/// </summary>
public sealed class OrderAgentApiClient(HttpClient httpClient, ILogger<OrderAgentApiClient> logger)
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string UnreachableMessage = "Sunucuya ulaşılamadı. API çalışıyor mu? (./scripts/dev.sh)";

    private const string TimeoutMessage = "Sunucu zamanında yanıt vermedi. Biraz bekleyip tekrar deneyin.";
    private const string InvalidResponseMessage = "Sunucudan beklenmeyen bir yanıt geldi. Sayfayı yenileyip tekrar deneyin.";
    private const string TextTooLongMessage = "Metin çok uzun. En fazla 20.000 karakter gönderilebilir.";
    private const string FileTooLargeMessage = "Dosya çok büyük. Sunucu bu boyuttaki dosyayı kabul etmedi.";
    private const string RequestTooLargeMessage = "Gönderilen bilgi çok büyük. Sunucu isteği kabul etmedi.";
    private const int MaxCatalogLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<HealthDto>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Health),
            ReadJsonAsync<HealthDto>,
            RequestTooLargeMessage,
            cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<CatalogItemDto>>> SearchCatalogAsync(
        string? query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string?> parameters = new()
        {
            ["q"] = query?.Trim() ?? string.Empty,
            ["limit"] = Math.Clamp(limit, 1, MaxCatalogLimit).ToString(CultureInfo.InvariantCulture)
        };
        string uri = QueryHelpers.AddQueryString(ApiRoutes.Catalog, parameters);

        return SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            ReadJsonAsync<IReadOnlyList<CatalogItemDto>>,
            RequestTooLargeMessage,
            cancellationToken);
    }

    public Task<ApiResult<CatalogImportResultDto>> ImportCatalogAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            () =>
            {
                StreamContent fileContent = new(content);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                MultipartFormDataContent form = new()
                {
                    { fileContent, "file", fileName }
                };
                return new HttpRequestMessage(HttpMethod.Post, ApiRoutes.CatalogImport) { Content = form };
            },
            ReadJsonAsync<CatalogImportResultDto>,
            FileTooLargeMessage,
            cancellationToken);
    }

    public Task<ApiResult<OrderDraftDto>> ParseOrderAsync(
        ParseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            () => CreateJsonRequest(ApiRoutes.ParseOrder, request),
            ReadJsonAsync<OrderDraftDto>,
            TextTooLongMessage,
            cancellationToken);
    }

    /// <summary>Başarılı olduğunda değer her zaman true'dur (API 204 döner).</summary>
    public Task<ApiResult<bool>> SaveCorrectionAsync(
        CorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            () => CreateJsonRequest(ApiRoutes.Corrections, request),
            static (_, _) => Task.FromResult(ApiResult<bool>.Success(true)),
            RequestTooLargeMessage,
            cancellationToken);
    }

    public Task<ApiResult<(byte[] Content, string FileName)>> ExportOrderAsync(
        ExportOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            () => CreateJsonRequest(ApiRoutes.ExportOrder, request),
            ReadFileAsync,
            RequestTooLargeMessage,
            cancellationToken);
    }

    private static HttpRequestMessage CreateJsonRequest<TBody>(string route, TBody body)
    {
        return new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        Func<HttpRequestMessage> createRequest,
        Func<HttpResponseMessage, CancellationToken, Task<ApiResult<T>>> readSuccess,
        string tooLargeMessage,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = createRequest();
        string route = request.RequestUri?.OriginalString.Split('?')[0] ?? "?";

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await readSuccess(response, cancellationToken);
            }

            return await ReadFailureAsync<T>(response, route, tooLargeMessage, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("API'ye ulaşılamadı: {Route} ({Reason})", route, exception.HttpRequestError);
            return ApiResult<T>.Failure(ApiErrorKind.Unreachable, UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("API zaman aşımına uğradı: {Route}", route);
            return ApiResult<T>.Failure(ApiErrorKind.Timeout, TimeoutMessage);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning("API yanıtı okunamadı: {Route} ({ExceptionType})", route, exception.GetType().Name);
            return ApiResult<T>.Failure(ApiErrorKind.InvalidResponse, InvalidResponseMessage);
        }
    }

    private static async Task<ApiResult<T>> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        T? value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value is null
            ? ApiResult<T>.Failure(ApiErrorKind.InvalidResponse, InvalidResponseMessage, (int)response.StatusCode)
            : ApiResult<T>.Success(value);
    }

    private static async Task<ApiResult<(byte[] Content, string FileName)>> ReadFileAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0)
        {
            return ApiResult<(byte[] Content, string FileName)>.Failure(
                ApiErrorKind.InvalidResponse,
                "Sunucu boş bir dosya döndürdü.",
                (int)response.StatusCode);
        }

        string fileName = GetFileName(response.Content.Headers.ContentDisposition)
            ?? $"siparis-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.xlsx";
        return ApiResult<(byte[] Content, string FileName)>.Success((content, fileName));
    }

    private static string? GetFileName(ContentDispositionHeaderValue? disposition)
    {
        string? raw = disposition?.FileNameStar ?? disposition?.FileName;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string fileName = Path.GetFileName(raw.Trim().Trim('"'));
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private async Task<ApiResult<T>> ReadFailureAsync<T>(
        HttpResponseMessage response,
        string route,
        string tooLargeMessage,
        CancellationToken cancellationToken)
    {
        int statusCode = (int)response.StatusCode;
        logger.LogWarning("API isteği başarısız: {Route} {StatusCode}", route, statusCode);

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            return ApiResult<T>.Failure(ApiErrorKind.HttpError, tooLargeMessage, statusCode);
        }

        string? detail = await TryReadProblemDetailAsync(response, cancellationToken);
        string message = string.IsNullOrWhiteSpace(detail) ? DefaultMessageFor(statusCode) : detail;
        return ApiResult<T>.Failure(ApiErrorKind.HttpError, message, statusCode);
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>(JsonOptions, cancellationToken);
            return problem?.Detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string DefaultMessageFor(int statusCode)
    {
        return statusCode switch
        {
            400 => "Gönderilen bilgiler kabul edilmedi. Lütfen kontrol edip tekrar deneyin.",
            404 => "Sunucuda bu işlem bulunamadı. Sunucu güncel mi?",
            >= 500 => "Sunucuda beklenmeyen bir hata oluştu. Biraz sonra tekrar deneyin.",
            _ => $"İşlem tamamlanamadı (hata kodu {statusCode})."
        };
    }
}
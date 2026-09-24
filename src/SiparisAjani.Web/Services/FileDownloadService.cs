using Microsoft.JSInterop;

namespace SiparisAjani.Web.Services;

/// <summary>
/// Sunucudan gelen dosyayı tarayıcıda indirir (wwwroot/js/download.js).
/// </summary>
public sealed class FileDownloadService(IJSRuntime jsRuntime, ILogger<FileDownloadService> logger) : IAsyncDisposable
{
    private const string ModulePath = "./js/download.js";

    private IJSObjectReference? _module;

    /// <returns>İndirme tarayıcıya iletildiyse true.</returns>
    public async Task<bool> DownloadAsync(string fileName, byte[] content, string contentType)
    {
        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            using MemoryStream stream = new(content, writable: false);
            using DotNetStreamReference streamReference = new(stream, leaveOpen: true);
            await _module.InvokeVoidAsync("downloadFileFromStream", fileName, contentType, streamReference);
            return true;
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or TaskCanceledException)
        {
            logger.LogWarning("Dosya indirme tarayıcıya iletilemedi ({ExceptionType})", exception.GetType().Name);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Devre zaten kapandı; tarayıcı tarafı temizlenmiş durumda.
        }
    }
}
namespace SiparisAjani.Web.Services;

/// <summary>
/// Arama kutuları için gecikmeli çalıştırma yardımcısı: her yeni tuş vuruşu önceki bekleyen aramayı iptal eder.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private CancellationTokenSource? _current;

    /// <summary>Önceki bekleyen işi iptal eder ve yeni iş için bir iptal belirteci döndürür.</summary>
    public CancellationToken Next()
    {
        _current?.Cancel();
        _current = new CancellationTokenSource();
        return _current.Token;
    }

    public void Dispose()
    {
        _current?.Cancel();
        _current?.Dispose();
        _current = null;
    }
}
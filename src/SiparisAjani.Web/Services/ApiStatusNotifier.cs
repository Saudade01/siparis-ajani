namespace SiparisAjani.Web.Services;

/// <summary>
/// Devre (oturum) içinde sunucu durum rozetinin yenilenmesini ister.
/// Örneğin katalog yüklendiğinde ya da bir çağrı sunucuya ulaşamadığında kullanılır.
/// </summary>
public sealed class ApiStatusNotifier
{
    public event Action? RefreshRequested;

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke();
    }
}
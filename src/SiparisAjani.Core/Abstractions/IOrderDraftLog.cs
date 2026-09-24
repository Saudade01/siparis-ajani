using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>
/// Çözümlenen taslakların ölçüm geçmişi. Kalıcılık ayrıntısı Infrastructure'dadır.
/// Yazma işlemi bir yan etkidir: başarısız olursa çözümleme isteği başarısız olmamalıdır.
/// </summary>
public interface IOrderDraftLog
{
    /// <summary>Bir çözümlemenin ölçüm özetini kaydeder.</summary>
    Task RecordAsync(OrderDraftSummary summary, CancellationToken cancellationToken = default);

    /// <summary>En yeniden eskiye doğru son <paramref name="limit"/> taslağın özetini döner.</summary>
    Task<IReadOnlyList<OrderDraftSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
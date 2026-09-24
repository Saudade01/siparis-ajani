import { useCallback, useEffect, useState } from 'react';
import { ApiError, errorMessage, getHealth } from '../api/client';
import { formatNumber } from '../format';

interface Badge {
  state: 'checking' | 'ai' | 'rules' | 'down';
  label: string;
  detail: string;
}

const checking: Badge = {
  state: 'checking',
  label: 'Sunucu kontrol ediliyor…',
  detail: 'Sunucu durumunu yenilemek için tıklayın.',
};

/** `refreshKey` değişince durum yeniden sorulur (bir istek başarısız olduğunda). */
export function HealthBadge({ refreshKey }: { refreshKey: number }) {
  const [badge, setBadge] = useState<Badge>(checking);
  const [manualRefresh, setManualRefresh] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    const check = async () => {
      try {
        const health = await getHealth(controller.signal);
        const isAi = health.extractor === 'Claude';
        setBadge({
          state: isAi ? 'ai' : 'rules',
          label: `${isAi ? 'Gelişmiş okuma' : 'Hızlı okuma'} · ${formatNumber(health.catalogCount)} ürün`,
          detail: 'Sunucu çalışıyor. Yenilemek için tıklayın.',
        });
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }
        setBadge({
          state: 'down',
          label: error instanceof ApiError && error.serverDown ? 'Sunucu kapalı' : 'Sunucu hatası',
          detail: errorMessage(error),
        });
      }
    };

    void check();
    const timer = setInterval(() => void check(), 30_000);
    return () => {
      controller.abort();
      clearInterval(timer);
    };
  }, [refreshKey, manualRefresh]);

  const refresh = useCallback(() => setManualRefresh((value) => value + 1), []);

  return (
    <button type="button" className={`health-badge health-badge--${badge.state}`} title={badge.detail} onClick={refresh}>
      <span className="health-dot" aria-hidden="true" />
      <span role="status">{badge.label}</span>
      <span className="visually-hidden">(durumu yenile)</span>
    </button>
  );
}

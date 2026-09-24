import {
  apiRoutes,
  type CatalogItem,
  type CorrectionRequest,
  type ExportOrderRequest,
  type Health,
  type OrderDraft,
  type ParseOrderRequest,
  type Problem,
} from './types';

/** Boşsa istekler Vite vekiline (/api → localhost:5080) gider. */
const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '');

export const unreachableMessage = 'Sunucuya ulaşılamadı. API çalışıyor mu? (./scripts/dev.sh)';

/** Mesajı doğrudan kullanıcıya gösterilebilir; teknik ayrıntı içermez. */
export class ApiError extends Error {
  readonly status: number | undefined;
  /** Sunucuya hiç ulaşılamadı. */
  readonly serverDown: boolean;

  constructor(message: string, status?: number, serverDown = false) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.serverDown = serverDown;
  }
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  return 'Beklenmeyen bir hata oluştu. Sayfayı yenileyip tekrar deneyin.';
}

export function getHealth(signal?: AbortSignal): Promise<Health> {
  return json<Health>(request(apiRoutes.health, { signal }));
}

export function searchCatalog(query: string, limit = 8, signal?: AbortSignal): Promise<CatalogItem[]> {
  const params = new URLSearchParams({ q: query.trim(), limit: String(Math.min(Math.max(limit, 1), 100)) });
  return json<CatalogItem[]>(request(`${apiRoutes.catalog}?${params}`, { signal }));
}

export function parseOrder(body: ParseOrderRequest, signal?: AbortSignal): Promise<OrderDraft> {
  return json<OrderDraft>(
    request(apiRoutes.parseOrder, postJson(body, signal), 'Metin çok uzun. En fazla 20.000 karakter gönderilebilir.'),
  );
}

export async function saveCorrection(body: CorrectionRequest): Promise<void> {
  await request(apiRoutes.corrections, postJson(body));
}

export async function exportOrder(body: ExportOrderRequest): Promise<{ blob: Blob; fileName: string }> {
  const response = await request(apiRoutes.exportOrder, postJson(body));
  const blob = await response.blob();
  if (blob.size === 0) {
    throw new ApiError('Sunucu boş bir dosya döndürdü. Tekrar deneyin.');
  }
  return { blob, fileName: fileNameFrom(response.headers.get('content-disposition')) };
}

function postJson(body: unknown, signal?: AbortSignal): RequestInit {
  return {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  };
}

async function request(path: string, init?: RequestInit, tooLargeMessage?: string): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(baseUrl + path, init);
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error; // Çağıran iptal etti; kullanıcıya hata gösterilmez.
    }
    throw new ApiError(unreachableMessage, undefined, true);
  }

  if (response.ok) {
    return response;
  }
  if (response.status === 413) {
    throw new ApiError(tooLargeMessage ?? 'Gönderilen bilgi çok büyük. Sunucu isteği kabul etmedi.', 413);
  }

  const detail = await problemDetail(response);
  throw new ApiError(detail ?? defaultMessage(response.status), response.status);
}

async function json<T>(pending: Promise<Response>): Promise<T> {
  const response = await pending;
  try {
    return (await response.json()) as T;
  } catch {
    throw new ApiError('Sunucudan beklenmeyen bir yanıt geldi. Sayfayı yenileyip tekrar deneyin.');
  }
}

/** ProblemDetails gövdesindeki `detail` Türkçe ve gösterilebilir; yoksa null döner. */
async function problemDetail(response: Response): Promise<string | null> {
  if (!response.headers.get('content-type')?.includes('json')) {
    return null;
  }
  try {
    const problem = (await response.json()) as Problem;
    return problem.detail?.trim() || null;
  } catch {
    return null;
  }
}

function defaultMessage(status: number): string {
  if (status === 400) {
    return 'Gönderilen bilgiler kabul edilmedi. Lütfen kontrol edip tekrar deneyin.';
  }
  if (status === 404) {
    return 'Sunucuda bu işlem bulunamadı. Sunucu güncel mi?';
  }
  if (status >= 500) {
    return 'Sunucuda beklenmeyen bir hata oluştu. Biraz sonra tekrar deneyin.';
  }
  return `İşlem tamamlanamadı (hata kodu ${status}).`;
}

function fileNameFrom(disposition: string | null): string {
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  const name = match?.[1]?.trim();
  if (name) {
    return decodeURIComponent(name);
  }
  const now = new Date();
  const pad = (value: number) => String(value).padStart(2, '0');
  return `siparis-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}.xlsx`;
}

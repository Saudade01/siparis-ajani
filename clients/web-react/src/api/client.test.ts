import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ApiError, exportOrder, parseOrder } from './client';

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

it('uzun metin için 413 yanıtını gövdeye bakmadan açıklar', async () => {
  fetchMock.mockResolvedValue(new Response('', { status: 413 }));

  await expect(parseOrder({ text: 'x'.repeat(30_000), source: 'WhatsApp' })).rejects.toThrow(
    'Metin çok uzun. En fazla 20.000 karakter gönderilebilir.',
  );
});

it('hata gövdesindeki açıklamayı kullanıcıya gösterir', async () => {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify({ title: 'Geçersiz istek', detail: '2. satırdaki stok kodu katalogda yok.', status: 400 }), {
      status: 400,
      headers: { 'content-type': 'application/problem+json' },
    }),
  );

  await expect(
    exportOrder({ lines: [{ sku: 'YOK-1', quantity: 1, unit: 'adet' }], customerCode: null, orderDate: null, note: null }),
  ).rejects.toThrow('2. satırdaki stok kodu katalogda yok.');
});

it('bağlantı kurulamadığında sunucunun kapalı olduğunu işaretler', async () => {
  fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

  const error = await parseOrder({ text: 'merhaba', source: 'WhatsApp' }).catch((failure: unknown) => failure);

  expect(error).toBeInstanceOf(ApiError);
  expect((error as ApiError).serverDown).toBe(true);
});

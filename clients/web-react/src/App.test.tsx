import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from './App';
import type { Health, OrderDraft } from './api/types';

const health: Health = { status: 'ok', extractor: 'RuleBased', catalogCount: 312, version: '0.1.0' };

const draft: OrderDraft = {
  draftId: 'd_test',
  extractor: 'RuleBased',
  customerHint: null,
  lines: [
    {
      lineNo: 1,
      rawText: '5 kutu 4x40 sunta vidası',
      quantity: 5,
      unit: 'kutu',
      productText: '4x40 sunta vidası',
      status: 'Matched',
      bestMatch: { sku: 'VD-4040-SNT', name: 'Sunta Vidası 4x40', unit: 'kutu', confidence: 0.91 },
      alternatives: [],
    },
    {
      lineNo: 2,
      rawText: '2 koli silikon',
      quantity: 2,
      unit: 'koli',
      productText: 'silikon',
      status: 'Ambiguous',
      bestMatch: { sku: 'SL-SFF-280', name: 'Şeffaf Silikon 280 ml', unit: 'adet', confidence: 0.62 },
      alternatives: [{ sku: 'SL-BYZ-280', name: 'Beyaz Silikon 280 ml', unit: 'adet', confidence: 0.55 }],
    },
    {
      lineNo: 3,
      rawText: '1 adet zımba teli',
      quantity: 1,
      unit: 'adet',
      productText: 'zımba teli',
      status: 'NotFound',
      bestMatch: null,
      alternatives: [],
    },
  ],
  maskedFields: ['phone'],
  warnings: [],
  createdAt: '2026-09-24T10:00:00+03:00',
};

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

/** Sağlık ve çözümleme yanıtlarını verir; çözümleme yerine hata da döndürülebilir. */
function mockApi(parse: () => Promise<Response> = () => Promise.resolve(json(draft))) {
  fetchMock.mockImplementation((input) => {
    const url = String(input);
    if (url.includes('/api/health')) {
      return Promise.resolve(json(health));
    }
    if (url.includes('/api/orders/parse')) {
      return parse();
    }
    if (url.includes('/api/orders/corrections')) {
      return Promise.resolve(new Response(null, { status: 204 }));
    }
    if (url.includes('/api/catalog')) {
      return Promise.resolve(json([{ sku: 'ZMB-TEL-8', name: 'Zımba Teli 8 mm', unit: 'kutu', barcode: null, category: null }]));
    }
    return Promise.reject(new Error(`Testte beklenmeyen istek: ${url}`));
  });
}

function callsTo(path: string) {
  return fetchMock.mock.calls.filter((call) => String(call[0]).includes(path));
}

function lastCorrection(): unknown {
  const call = callsTo('/api/orders/corrections').at(-1);
  return JSON.parse(String(call?.[1]?.body));
}

async function parseSampleOrder() {
  const user = userEvent.setup();
  render(<App />);
  await user.click(screen.getByRole('button', { name: 'Örnek sipariş doldur' }));
  await user.click(screen.getByRole('button', { name: 'Çözümle' }));
  return user;
}

it('her satırı kendi durum rozetiyle listeler', async () => {
  mockApi();

  await parseSampleOrder();

  expect(await screen.findByText('Eşleşti')).toBeInTheDocument();
  expect(screen.getByText('Seçim gerekli')).toBeInTheDocument();
  expect(screen.getByText('Bulunamadı')).toBeInTheDocument();
  expect(screen.getByText('3 satır · 1 eşleşti · 1 seçim gerekli · 1 bulunamadı')).toBeInTheDocument();
});

it('maskelenen alanı sade Türkçe ile anlatır', async () => {
  mockApi();

  await parseSampleOrder();

  expect(await screen.findByText('Telefon numarası gizlendi, modele gönderilmedi.')).toBeInTheDocument();
});

it('belirsiz satırda aday seçilince düzeltmeyi kaydeder', async () => {
  mockApi();

  const user = await parseSampleOrder();
  const choice = await screen.findByLabelText('2. satır için ürün seçin');
  await user.selectOptions(choice, 'SL-SFF-280');

  await waitFor(() => {
    expect(lastCorrection()).toEqual({ productText: 'silikon', sku: 'SL-SFF-280' });
  });
  expect(await screen.findByText('Hatırlandı')).toBeInTheDocument();
  expect(screen.getByText('Şeffaf Silikon 280 ml')).toBeInTheDocument();
});

it('sunucuya ulaşılamazsa ne yapılacağını söyler', async () => {
  mockApi(() => Promise.reject(new TypeError('Failed to fetch')));

  await parseSampleOrder();

  expect(await screen.findByText('Sunucuya ulaşılamadı. API çalışıyor mu? (./scripts/dev.sh)')).toBeInTheDocument();
});

it('bulunamayan satırda katalogda arayıp seçileni kaydeder', async () => {
  mockApi();

  const user = await parseSampleOrder();
  await user.type(await screen.findByLabelText('3. satır için katalogda ara'), 'zımba');
  await user.click(await screen.findByRole('button', { name: /Zımba Teli 8 mm/ }));

  await waitFor(() => {
    expect(lastCorrection()).toEqual({ productText: 'zımba teli', sku: 'ZMB-TEL-8' });
  });
  // Beş tuş vuruşu tek arama: istek duraklamada gidiyor.
  expect(callsTo('/api/catalog')).toHaveLength(1);
});

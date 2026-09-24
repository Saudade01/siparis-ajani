import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, errorMessage, parseOrder, saveCorrection } from './api/client';
import type { OrderDraft, ParseOrderRequest } from './api/types';
import { ExportPanel } from './components/ExportPanel';
import { HealthBadge } from './components/HealthBadge';
import { LineRow } from './components/LineRow';
import { OrderForm } from './components/OrderForm';
import { extractorLabel, formatNumber, maskedFieldsText } from './format';
import { chooseProduct, reopen, toLineStates, type LineState, type ProductPick } from './lines';

export function App() {
  const [draft, setDraft] = useState<OrderDraft | null>(null);
  const [lines, setLines] = useState<LineState[]>([]);
  const [parsing, setParsing] = useState(false);
  const [parseError, setParseError] = useState<string | null>(null);
  const [healthKey, setHealthKey] = useState(0);
  const resultHeading = useRef<HTMLHeadingElement>(null);

  const refreshHealth = useCallback(() => setHealthKey((value) => value + 1), []);

  useEffect(() => {
    if (draft) {
      resultHeading.current?.focus();
    }
  }, [draft]);

  const updateLine = useCallback((key: string, update: (state: LineState) => LineState) => {
    setLines((previous) => previous.map((state) => (state.key === key ? update(state) : state)));
  }, []);

  const handleParse = async (request: ParseOrderRequest) => {
    setParsing(true);
    setParseError(null);
    try {
      const result = await parseOrder(request);
      setDraft(result);
      setLines(toLineStates(result));
    } catch (error) {
      setParseError(errorMessage(error));
      if (error instanceof ApiError && error.serverDown) {
        refreshHealth();
      }
    } finally {
      setParsing(false);
    }
  };

  const handleChoose = async (key: string, product: ProductPick) => {
    const productText = lines.find((state) => state.key === key)?.line.productText.trim() ?? '';
    updateLine(key, (state) => chooseProduct(state, product));
    if (productText.length === 0) {
      updateLine(key, (state) => ({ ...state, savingCorrection: false }));
      return;
    }

    try {
      await saveCorrection({ productText, sku: product.sku });
      // Kullanıcı bu arada seçimi değiştirdiyse eski sonucu yazma.
      updateLine(key, (state) =>
        state.product?.sku === product.sku ? { ...state, savingCorrection: false, correctionSaved: true } : state,
      );
    } catch (error) {
      updateLine(key, (state) =>
        state.product?.sku === product.sku
          ? {
              ...state,
              savingCorrection: false,
              correctionError: `Bu siparişte kullanıldı ama hatırlanamadı. ${errorMessage(error)}`,
            }
          : state,
      );
      if (error instanceof ApiError && error.serverDown) {
        refreshHealth();
      }
    }
  };

  const matched = lines.filter((state) => state.status === 'Matched').length;
  const ambiguous = lines.filter((state) => state.status === 'Ambiguous').length;
  const notFound = lines.filter((state) => state.status === 'NotFound').length;
  const counts = [`${formatNumber(lines.length)} satır`, `${formatNumber(matched)} eşleşti`];
  if (ambiguous > 0) {
    counts.push(`${formatNumber(ambiguous)} seçim gerekli`);
  }
  if (notFound > 0) {
    counts.push(`${formatNumber(notFound)} bulunamadı`);
  }
  const maskedText = draft ? maskedFieldsText(draft.maskedFields ?? []) : null;
  const warnings = draft?.warnings ?? [];

  return (
    <>
      <header className="topbar">
        <div className="topbar-inner">
          <span className="brand">SiparisAjani</span>
          <span className="brand-note">React istemcisi</span>
          <HealthBadge refreshKey={healthKey} />
        </div>
      </header>

      <main className="page">
        <div className="stack">
          <div className="page-header">
            <h1>Sipariş</h1>
            <p className="lead">
              Müşterinin mesajını yapıştırın, satırları kontrol edin, gerekirse ürünü seçin ve Excel'e aktarın.
            </p>
          </div>

          <OrderForm busy={parsing} error={parseError} onSubmit={(request) => void handleParse(request)} />

          {draft && (
            <section className="stack" aria-labelledby="result-heading">
              <div className="stack stack--tight">
                <h2 id="result-heading" tabIndex={-1} ref={resultHeading}>
                  Çözümleme sonucu
                </h2>
                <div className="summary">
                  <p className="summary-counts">{counts.join(' · ')}</p>
                  <p className="meta">
                    {extractorLabel(draft.extractor)}
                    {draft.customerHint && <span> · Müşteri: {draft.customerHint}</span>}
                  </p>
                  {maskedText && (
                    <div className="notice notice--info">
                      <p>{maskedText}</p>
                    </div>
                  )}
                  {warnings.length > 0 && (
                    <div className="notice notice--warning">
                      <p className="notice-title">Kontrol edin</p>
                      <ul>
                        {warnings.map((warning) => (
                          <li key={warning}>{warning}</li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              </div>

              {lines.length > 0 ? (
                <>
                  <div className="table-wrap">
                    <table className="data-table order-table">
                      <caption className="visually-hidden">Sipariş satırları</caption>
                      <thead>
                        <tr>
                          <th scope="col" className="num">
                            No
                          </th>
                          <th scope="col">Müşterinin yazdığı</th>
                          <th scope="col">Miktar</th>
                          <th scope="col">Birim</th>
                          <th scope="col">Eşleşen ürün</th>
                          <th scope="col">Güven</th>
                          <th scope="col">Durum</th>
                          <th scope="col">
                            <span className="visually-hidden">İşlem</span>
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {lines.map((state) => (
                          <LineRow
                            key={state.key}
                            state={state}
                            onQuantityChange={(key, value) => updateLine(key, (line) => ({ ...line, quantity: value }))}
                            onChoose={(key, product) => void handleChoose(key, product)}
                            onSearch={(key, searching) => updateLine(key, (line) => ({ ...line, searching }))}
                            onReopen={(key) => updateLine(key, reopen)}
                            onRemove={(key) => setLines((previous) => previous.filter((line) => line.key !== key))}
                          />
                        ))}
                      </tbody>
                    </table>
                  </div>

                  <ExportPanel lines={lines} onServerDown={refreshHealth} />
                </>
              ) : (
                <div className="notice notice--warning">
                  <p>
                    {draft.lines.length === 0
                      ? 'Metinde sipariş satırı bulunamadı. Her ürünü ayrı satıra yazıp tekrar deneyin.'
                      : 'Tüm satırlar çıkarıldı. Yeniden başlamak için “Çözümle”ye basın.'}
                  </p>
                </div>
              )}
            </section>
          )}
        </div>
      </main>
    </>
  );
}

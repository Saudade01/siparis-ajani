import { useState } from 'react';
import { ApiError, errorMessage, exportOrder } from '../api/client';
import type { ExportLine } from '../api/types';
import { formatNumber } from '../format';
import { displayUnit, quantityOf, type LineState } from '../lines';

interface Props {
  lines: LineState[];
  onServerDown: () => void;
}

export function ExportPanel({ lines, onServerDown }: Props) {
  const [customerCode, setCustomerCode] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const pending = lines.filter((state) => state.status !== 'Matched' || state.product === null).length;
  const missingQuantity = lines.filter((state) => state.status === 'Matched' && quantityOf(state) === null).length;
  const blocked = lines.length === 0 || pending > 0 || missingQuantity > 0;

  const hint = () => {
    if (lines.length === 0) {
      return 'Aktarılacak satır yok.';
    }
    const reasons: string[] = [];
    if (pending > 0) {
      reasons.push(`${formatNumber(pending)} satırda seçim gerekli`);
    }
    if (missingQuantity > 0) {
      reasons.push(`${formatNumber(missingQuantity)} satırda miktar eksik`);
    }
    return reasons.length > 0 ? reasons.join(' · ') : `${formatNumber(lines.length)} satır aktarılacak.`;
  };

  const exportToExcel = async () => {
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      const exportLines: ExportLine[] = lines.flatMap((state) => {
        const quantity = quantityOf(state);
        if (state.product === null || quantity === null) {
          return [];
        }
        return [{ sku: state.product.sku, quantity, unit: displayUnit(state) ?? state.product.unit }];
      });

      const { blob, fileName } = await exportOrder({
        lines: exportLines,
        customerCode: customerCode.trim() || null,
        orderDate: null,
        note: note.trim() || null,
      });

      saveFile(blob, fileName);
      setSuccess(`“${fileName}” indirildi.`);
    } catch (failure) {
      setError(errorMessage(failure));
      if (failure instanceof ApiError && failure.serverDown) {
        onServerDown();
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card" aria-labelledby="export-heading">
      <h2 id="export-heading">Excel'e aktar</h2>

      <div className="export-grid">
        <div className="field">
          <label className="field-label" htmlFor="export-customer-code">
            Cari kodu <span className="optional">(isteğe bağlı)</span>
          </label>
          <input
            id="export-customer-code"
            className="input"
            maxLength={50}
            autoComplete="off"
            value={customerCode}
            onChange={(event) => setCustomerCode(event.target.value)}
          />
        </div>
        <div className="field">
          <label className="field-label" htmlFor="export-note">
            Not <span className="optional">(isteğe bağlı)</span>
          </label>
          <input
            id="export-note"
            className="input"
            maxLength={500}
            autoComplete="off"
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </div>
      </div>

      <div className="actions">
        <button
          type="button"
          className="btn btn-primary btn-lg"
          disabled={blocked || busy}
          aria-describedby="export-hint"
          onClick={() => void exportToExcel()}
        >
          {busy ? 'Hazırlanıyor…' : "Excel'e aktar"}
        </button>
        <span id="export-hint" className={blocked ? 'hint hint--warn' : 'hint'}>
          {hint()}
        </span>
      </div>

      {error && (
        <div className="notice notice--error" role="alert">
          <p>{error}</p>
        </div>
      )}
      {success && (
        <div className="notice notice--success" role="status">
          <p>{success}</p>
        </div>
      )}
    </section>
  );
}

function saveFile(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

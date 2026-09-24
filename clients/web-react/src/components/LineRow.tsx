import { confidenceOf, displayUnit, quantityOf, type LineState, type ProductPick } from '../lines';
import { formatPercent, statusLabel } from '../format';
import { CatalogSearch } from './CatalogSearch';

const searchOptionValue = '__katalogda-ara__';

interface Props {
  state: LineState;
  onQuantityChange: (key: string, value: string) => void;
  onChoose: (key: string, product: ProductPick) => void;
  onSearch: (key: string, searching: boolean) => void;
  onReopen: (key: string) => void;
  onRemove: (key: string) => void;
}

export function LineRow({ state, onQuantityChange, onChoose, onSearch, onReopen, onRemove }: Props) {
  const { line, product } = state;
  const quantityValid = quantityOf(state) !== null;
  const showCandidates = state.status === 'Ambiguous' && state.candidates.length > 0 && !state.searching;
  const canReopen = product?.manual === true && state.originalStatus !== 'Matched';

  return (
    <tr className={`line-row line-row--${state.status.toLowerCase()}`}>
      <td className="col-no num">{line.lineNo}</td>
      <td className="col-raw">
        <span className="raw-text">{line.rawText}</span>
      </td>
      <td className="col-qty">
        <label className="visually-hidden" htmlFor={`qty-${state.key}`}>
          {line.lineNo}. satırın miktarı
        </label>
        <input
          id={`qty-${state.key}`}
          type="number"
          min="0"
          step="any"
          inputMode="decimal"
          className={quantityValid ? 'input input-sm input-qty' : 'input input-sm input-qty is-invalid'}
          aria-invalid={!quantityValid}
          value={state.quantity}
          onChange={(event) => onQuantityChange(state.key, event.target.value)}
        />
        {!quantityValid && <span className="row-note row-note--error">Miktar girin</span>}
      </td>
      <td className="col-unit">{displayUnit(state) ?? '—'}</td>
      <td className="col-product">
        {state.status === 'Matched' && product ? (
          <>
            <span className="product-name">{product.name}</span>
            <span className="product-sku">{product.sku}</span>
            {state.savingCorrection && <span className="row-note">Seçim kaydediliyor…</span>}
            {state.correctionError && <span className="row-note row-note--error">{state.correctionError}</span>}
            {state.correctionSaved && <span className="row-note row-note--ok">Hatırlandı</span>}
            {canReopen && !state.savingCorrection && (
              <button type="button" className="link-button" onClick={() => onReopen(state.key)}>
                Değiştir<span className="visually-hidden"> ({line.lineNo}. satır)</span>
              </button>
            )}
          </>
        ) : showCandidates ? (
          <>
            <label className="visually-hidden" htmlFor={`choice-${state.key}`}>
              {line.lineNo}. satır için ürün seçin
            </label>
            <select
              id={`choice-${state.key}`}
              className="select select-sm"
              value=""
              onChange={(event) => {
                const value = event.target.value;
                if (value === searchOptionValue) {
                  onSearch(state.key, true);
                  return;
                }
                const candidate = state.candidates.find((item) => item.sku === value);
                if (candidate) {
                  onChoose(state.key, candidate);
                }
              }}
            >
              <option value="">Ürün seçin…</option>
              {state.candidates.map((candidate) => (
                <option value={candidate.sku} key={candidate.sku}>
                  {candidate.name} ({candidate.sku}) · {formatPercent(candidate.confidence)}
                </option>
              ))}
              <option value={searchOptionValue}>Listede yok, katalogda ara…</option>
            </select>
          </>
        ) : (
          <>
            <CatalogSearch
              label={`${line.lineNo}. satır için katalogda ara`}
              onSelect={(item) => onChoose(state.key, item)}
            />
            {state.candidates.length > 0 && (
              <button type="button" className="link-button" onClick={() => onSearch(state.key, false)}>
                Önerilen ürünlere dön
              </button>
            )}
          </>
        )}
      </td>
      <td className="col-confidence">
        <Confidence value={confidenceOf(state)} manual={product?.manual === true} />
      </td>
      <td className="col-status">
        <span className={`status-badge status-badge--${state.status.toLowerCase()}`}>
          <span className="status-dot" aria-hidden="true" />
          {statusLabel(state.status)}
        </span>
      </td>
      <td className="col-actions">
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => onRemove(state.key)}>
          Satırı çıkar<span className="visually-hidden"> ({line.lineNo}. satır)</span>
        </button>
      </td>
    </tr>
  );
}

function Confidence({ value, manual }: { value: number | null; manual: boolean }) {
  if (value === null) {
    return <span className="confidence-manual">{manual ? 'Elle seçildi' : '—'}</span>;
  }

  const level = value >= 0.8 ? 'high' : value >= 0.45 ? 'medium' : 'low';
  return (
    <div className={`confidence confidence--${level}`}>
      <span className="confidence-value">{formatPercent(value)}</span>
      <span className="confidence-bar" aria-hidden="true">
        <span className="confidence-fill" style={{ width: `${Math.round(value * 100)}%` }} />
      </span>
    </div>
  );
}

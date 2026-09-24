import { useEffect, useId, useState } from 'react';
import { errorMessage, searchCatalog } from '../api/client';
import type { CatalogItem } from '../api/types';
import { formatNumber } from '../format';

interface Props {
  /** Ekran okuyucu için arama kutusunun adı. */
  label: string;
  onSelect: (item: CatalogItem) => void;
}

interface SearchState {
  results: CatalogItem[];
  status: string;
  failed: boolean;
}

const idle: SearchState = { results: [], status: '', failed: false };

export function CatalogSearch({ label, onSelect }: Props) {
  const inputId = useId();
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState<SearchState>(idle);

  const term = query.trim();
  const searchable = term.length >= 2;

  useEffect(() => {
    if (!searchable) {
      return;
    }

    const controller = new AbortController();
    // Her tuş vuruşu bekleyen aramayı iptal eder; sunucuya yalnızca duraklamada gidilir.
    const timer = setTimeout(() => {
      setSearch({ results: [], status: 'Aranıyor…', failed: false });
      void searchCatalog(term, 8, controller.signal)
        .then((items) => {
          setSearch({
            results: items,
            status:
              items.length === 0
                ? 'Sonuç yok. Başka bir kelime deneyin.'
                : `${formatNumber(items.length)} sonuç. Doğru ürünü seçin.`,
            failed: false,
          });
        })
        .catch((error: unknown) => {
          if (controller.signal.aborted) {
            return;
          }
          setSearch({ results: [], status: errorMessage(error), failed: true });
        });
    }, 300);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [term, searchable]);

  const results = searchable ? search.results : [];
  const status = searchable ? search.status : term.length === 0 ? '' : 'En az 2 harf yazın.';

  return (
    <div className="picker">
      <label className="visually-hidden" htmlFor={inputId}>
        {label}
      </label>
      <input
        id={inputId}
        type="search"
        className="input input-sm"
        placeholder="Katalogda ara (ör. silikon)"
        autoComplete="off"
        aria-describedby={`${inputId}-status`}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <span
        id={`${inputId}-status`}
        className={search.failed ? 'picker-status picker-status--error' : 'picker-status'}
        role="status"
      >
        {status}
      </span>

      {results.length > 0 && (
        <ul className="picker-results" aria-label="Arama sonuçları">
          {results.map((item) => (
            <li key={item.sku}>
              <button type="button" className="picker-option" onClick={() => onSelect(item)}>
                <span className="picker-option-name">{item.name}</span>
                <span className="picker-option-meta">
                  {item.sku} · {item.unit}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

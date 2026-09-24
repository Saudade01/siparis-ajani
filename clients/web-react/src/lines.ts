import type { LineStatus, MatchCandidate, OrderDraft, OrderLine } from './api/types';

export interface SelectedProduct {
  sku: string;
  name: string;
  unit: string;
  confidence: number | null;
  /** Kullanıcı elle seçti. */
  manual: boolean;
}

/**
 * Tablodaki bir satırın düzenlenebilir hali. `line` API'den geldiği gibi kalır;
 * kullanıcının miktar ve ürün seçimleri burada tutulur.
 */
export interface LineState {
  key: string;
  line: OrderLine;
  /** Metin olarak tutulur ki kullanıcı alanı boşaltabilsin. */
  quantity: string;
  status: LineStatus;
  originalStatus: LineStatus;
  product: SelectedProduct | null;
  candidates: MatchCandidate[];
  /** Aday listesi yerine katalogda arama açık. */
  searching: boolean;
  savingCorrection: boolean;
  correctionSaved: boolean;
  correctionError: string | null;
}

export function toLineStates(draft: OrderDraft): LineState[] {
  return (draft.lines ?? []).map((line, index) => {
    // "Matched" geldiği halde eşleşme yoksa satır seçim bekliyor demektir.
    const best = line.status === 'Matched' ? line.bestMatch : null;
    const status: LineStatus = best ? 'Matched' : line.status === 'Matched' ? 'NotFound' : line.status;
    return {
      key: `${draft.draftId}-${line.lineNo}-${index}`,
      line,
      quantity: line.quantity === null ? '' : String(line.quantity),
      status,
      originalStatus: status,
      product: best
        ? { sku: best.sku, name: best.name, unit: best.unit, confidence: best.confidence, manual: false }
        : null,
      candidates: candidatesOf(line),
      searching: false,
      savingCorrection: false,
      correctionSaved: false,
      correctionError: null,
    };
  });
}

/** Kullanıcının seçtiği ürün: aday listesinden ya da katalog aramasından gelir. */
export interface ProductPick {
  sku: string;
  name: string;
  unit: string;
}

export function chooseProduct(state: LineState, product: ProductPick): LineState {
  return {
    ...state,
    status: 'Matched',
    product: { ...product, confidence: null, manual: true },
    searching: false,
    savingCorrection: true,
    correctionSaved: false,
    correctionError: null,
  };
}

export function reopen(state: LineState): LineState {
  return {
    ...state,
    status: state.originalStatus,
    product: null,
    searching: false,
    savingCorrection: false,
    correctionSaved: false,
    correctionError: null,
  };
}

/** "2,5" ve "2.5" kabul edilir; geçersizse null. */
export function quantityOf(state: LineState): number | null {
  const value = Number(state.quantity.replace(',', '.'));
  return Number.isFinite(value) && value > 0 ? value : null;
}

/** Müşterinin yazdığı birim; metinde yoksa ürünün katalog birimi. */
export function displayUnit(state: LineState): string | null {
  return state.line.unit?.trim() || state.product?.unit || null;
}

/** Belirsiz satırda gösterilecek güven: seçili ürünün, yoksa en iyi adayın. */
export function confidenceOf(state: LineState): number | null {
  if (state.product) {
    return state.product.confidence;
  }
  return state.status === 'Ambiguous' ? (state.line.bestMatch?.confidence ?? null) : null;
}

function candidatesOf(line: OrderLine): MatchCandidate[] {
  const all = [...(line.bestMatch ? [line.bestMatch] : []), ...(line.alternatives ?? [])];
  const unique = new Map(all.map((candidate) => [candidate.sku, candidate]));
  return [...unique.values()].sort((left, right) => right.confidence - left.confidence);
}

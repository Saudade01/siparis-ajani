// Kaynak: docs/sozlesmeler/api.md (v0.1.2) ve src/SiparisAjani.Contracts/OrderContracts.cs.
// Sözleşme değişirse bu dosya elle güncellenir; üretim aracı yok.
// JSON alan adları camelCase, enum'lar metin, tarihler ISO 8601.

export type OrderSource = 'WhatsApp' | 'Email' | 'Phone' | 'Other';

export type LineStatus = 'Matched' | 'Ambiguous' | 'NotFound';

export type ExtractorKind = 'Claude' | 'RuleBased';

export interface ParseOrderRequest {
  text: string;
  source: OrderSource;
}

export interface CatalogItem {
  sku: string;
  name: string;
  unit: string;
  barcode: string | null;
  category: string | null;
}

export interface MatchCandidate {
  sku: string;
  name: string;
  unit: string;
  /** 0–1 arası. */
  confidence: number;
}

/** `unit` müşterinin metinde yazdığı birimdir; ürünün katalog birimi `bestMatch.unit` alanındadır. */
export interface OrderLine {
  lineNo: number;
  rawText: string;
  quantity: number | null;
  unit: string | null;
  productText: string;
  status: LineStatus;
  bestMatch: MatchCandidate | null;
  alternatives: MatchCandidate[];
}

/** `draftId` yalnızca izleme kimliğidir; sunucu taslak durumu tutmaz (ADR-0002). */
export interface OrderDraft {
  draftId: string;
  extractor: ExtractorKind;
  customerHint: string | null;
  lines: OrderLine[];
  /** phone, email, iban, nationalId, name — liste büyüyebilir. */
  maskedFields: string[];
  warnings: string[];
  createdAt: string;
}

export interface CorrectionRequest {
  productText: string;
  sku: string;
}

/** Ürün adı taşınmaz: sunucu adı sku'dan çözer. `unit` boşsa katalog birimi yazılır. */
export interface ExportLine {
  sku: string;
  quantity: number;
  unit: string;
}

export interface ExportOrderRequest {
  lines: ExportLine[];
  customerCode: string | null;
  orderDate: string | null;
  note: string | null;
}

export interface Health {
  status: string;
  extractor: ExtractorKind;
  catalogCount: number;
  version: string;
}

/** RFC 9457 ProblemDetails gövdesi; alanların hepsi eksik gelebilir. */
export interface Problem {
  title?: string;
  detail?: string;
  status?: number;
}

/** Karşılığı: src/SiparisAjani.Contracts/ApiRoutes.cs */
export const apiRoutes = {
  health: '/api/health',
  catalog: '/api/catalog',
  parseOrder: '/api/orders/parse',
  corrections: '/api/orders/corrections',
  exportOrder: '/api/orders/export',
} as const;

export const maxOrderTextLength = 20_000;

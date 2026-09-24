import type { ExtractorKind, LineStatus } from './api/types';

const turkish = new Intl.NumberFormat('tr-TR');

export function formatNumber(value: number): string {
  return turkish.format(value);
}

/** 0–1 arası oranı "%91" biçiminde yazar. */
export function formatPercent(ratio: number): string {
  return `%${Math.round(Math.min(Math.max(ratio, 0), 1) * 100)}`;
}

export function statusLabel(status: LineStatus): string {
  if (status === 'Matched') {
    return 'Eşleşti';
  }
  return status === 'Ambiguous' ? 'Seçim gerekli' : 'Bulunamadı';
}

export function extractorLabel(extractor: ExtractorKind): string {
  return extractor === 'Claude' ? 'Yapay zekâ ile çözümlendi' : 'Kural modu ile çözümlendi';
}

const maskedFieldNames: Record<string, string> = {
  phone: 'telefon numarası',
  email: 'e-posta adresi',
  iban: 'IBAN',
  nationalId: 'TC kimlik numarası',
  name: 'isim',
};

/** Tanınmayan tür genel bir ifadeyle gösterilir (sözleşme v0.1.2). */
export function maskedFieldsText(fields: string[]): string | null {
  const names = [...new Set(fields.map((field) => maskedFieldNames[field] ?? 'kişisel bilgi'))];
  if (names.length === 0) {
    return null;
  }
  const joined = names.length === 1 ? names[0] : `${names.slice(0, -1).join(', ')} ve ${names[names.length - 1]}`;
  return `${joined.charAt(0).toLocaleUpperCase('tr-TR')}${joined.slice(1)} gizlendi, modele gönderilmedi.`;
}

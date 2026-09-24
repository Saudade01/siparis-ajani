import { useState } from 'react';
import { maxOrderTextLength, type OrderSource, type ParseOrderRequest } from '../api/types';
import { formatNumber } from '../format';

const sourceOptions: { value: OrderSource; label: string }[] = [
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'Email', label: 'E-posta' },
  { value: 'Phone', label: 'Telefon notu' },
  { value: 'Other', label: 'Diğer' },
];

interface Props {
  busy: boolean;
  /** Sunucudan gelen hata. */
  error: string | null;
  onSubmit: (request: ParseOrderRequest) => void;
}

export function OrderForm({ busy, error, onSubmit }: Props) {
  const [text, setText] = useState('');
  const [source, setSource] = useState<OrderSource>('WhatsApp');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [sampleIndex, setSampleIndex] = useState(0);

  const tooLong = text.length > maxOrderTextLength;
  const shownError = validationError ?? error;

  const submit = () => {
    if (text.trim().length === 0) {
      setValidationError('Önce sipariş metnini yapıştırın.');
      return;
    }
    if (tooLong) {
      setValidationError(
        `Metin çok uzun. En fazla ${formatNumber(maxOrderTextLength)} karakter olabilir, şu an ${formatNumber(text.length)} karakter.`,
      );
      return;
    }
    setValidationError(null);
    onSubmit({ text, source });
  };

  const fillSample = () => {
    const sample = sampleOrders[sampleIndex % sampleOrders.length];
    setText(sample.text);
    setSource(sample.source);
    setSampleIndex(sampleIndex + 1);
    setValidationError(null);
  };

  return (
    <section className="card" aria-labelledby="order-input-heading">
      <h2 id="order-input-heading" className="visually-hidden">
        Sipariş metni
      </h2>

      <fieldset className="segmented">
        <legend className="field-label">Sipariş nereden geldi?</legend>
        <div className="segmented-options">
          {sourceOptions.map((option) => (
            <label className="segmented-option" key={option.value}>
              <input
                type="radio"
                name="order-source"
                value={option.value}
                checked={source === option.value}
                onChange={() => setSource(option.value)}
              />
              <span>{option.label}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <div className="field">
        <div className="field-row">
          <label className="field-label" htmlFor="order-text">
            Sipariş metni
          </label>
          <span className={tooLong ? 'hint hint--error' : 'hint'}>
            {formatNumber(text.length)} / {formatNumber(maxOrderTextLength)} karakter
          </span>
        </div>
        <textarea
          id="order-text"
          className="textarea"
          rows={10}
          spellCheck={false}
          placeholder="Müşterinin mesajını buraya yapıştırın…"
          aria-describedby="order-text-help"
          aria-invalid={shownError !== null}
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            setValidationError(null);
          }}
        />
        <p className="hint" id="order-text-help">
          WhatsApp mesajını, e-postayı ya da telefon notunu olduğu gibi yapıştırın. Telefon, e-posta, IBAN, TC kimlik
          numarası ve isimler çözümlemeden önce gizlenir.
        </p>
      </div>

      {shownError && (
        <div className="notice notice--error" role="alert">
          <p>{shownError}</p>
        </div>
      )}

      <div className="actions">
        <button type="button" className="btn btn-primary btn-lg" disabled={busy} onClick={submit}>
          {busy ? 'Çözümleniyor…' : 'Çözümle'}
        </button>
        <button type="button" className="btn btn-secondary" disabled={busy} onClick={fillSample}>
          Örnek sipariş doldur
        </button>
        {text.length > 0 && (
          <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setText('')}>
            Temizle
          </button>
        )}
      </div>
    </section>
  );
}

// Demo için sentetik örnekler: telefon ve e-posta uydurmadır, gerçek müşteri verisi değildir.
// Maskeleme bilgisinin ekranda görünmesi için bilerek iletişim bilgisi içerirler.
const sampleOrders: { source: OrderSource; text: string }[] = [
  {
    source: 'WhatsApp',
    text: `Merhaba abi, yarın sabah şunları hazırlar mısın:
5 kutu 4x40 sunta vidası
10 adet şeffaf silikon
3 paket 8'lik dübel
2 paket kablo bağı 200'lük
4 adet 6'lık beton matkap ucu
6 rulo maskeleme bandı
1 koli kırmızı sprey boya
Akşama kadar gelirse süper. Tel: 0532 111 22 33`,
  },
  {
    source: 'Email',
    text: `Konu: Haftalık sipariş

İyi günler,
Aşağıdaki ürünlerin bu hafta sevkini rica ederiz:

- 20 adet teflon bant
- 4 kutu alçıpan vidası 3,5x35
- 2 adet poliüretan köpük 750 ml
- 10 adet su zımparası 100 kum
- 3 adet şerit metre 5 m

Fatura bilgileri için: satinalma@example.com
Saygılarımızla,
Satın alma birimi`,
  },
];

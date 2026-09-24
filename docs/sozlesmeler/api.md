# API sözleşmesi v0.1.2

Kod karşılığı: `src/SiparisAjani.Contracts` (`OrderContracts.cs`, `ApiRoutes.cs`).
JSON: camelCase alan adları, enum'lar metin (`"Matched"`), tarih ISO 8601. Hata yanıtları RFC 9457 ProblemDetails.

| Yöntem | Rota | İstek | Yanıt |
|---|---|---|---|
| GET | `/api/health` | — | `HealthDto` |
| GET | `/api/catalog?q=&limit=20` | — | `CatalogItemDto[]` (q boşsa ilk `limit` ürün; limit en fazla 100) |
| POST | `/api/catalog/import` | multipart `file` (CSV, UTF-8, `;` veya `,` ayraçlı; başlıklar: `sku,name,unit,barcode,category`) | `CatalogImportResultDto` (çok büyük dosya → 413) |
| POST | `/api/orders/parse` | `ParseOrderRequest` | `OrderDraftDto` |
| POST | `/api/orders/corrections` | `CorrectionRequest` | 204. Aynı `productText` bir sonraki çözümlemede bu `sku` ile eşleşir |
| POST | `/api/orders/export` | `ExportOrderRequest` | 200 `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `Content-Disposition: attachment; filename="siparis-YYYYMMDD-HHmm.xlsx"` |

## Davranış kuralları
- `ParseOrderRequest.text`: boş → 400; 20.000 karakterden uzun → 413.
- Güven eşikleri: `confidence ≥ 0.80` ve ikinci aday ile fark ≥ 0.10 → `Matched`; `0.45 ≤ confidence` → `Ambiguous` (en fazla 3 `alternatives`); altı → `NotFound` (`bestMatch` null).
- `maskedFields`: metinden maskelenen alan türleri (`phone`, `email`, `iban`, `nationalId`, `name`). Değerlerin kendisi asla dönmez. `name`, "Hasan Bey" gibi hitap/kişi adı kalıplarını kapsar (KVKK gereği); arayüz tanımadığı bir tür gelirse genel bir ifadeyle ("kişisel bilgi") göstermelidir.
- `warnings`: kullanıcıya gösterilebilir Türkçe cümleler (ör. "3. satırda miktar bulunamadı, 1 kabul edildi").
- `export`: bilinmeyen `sku` → 400 (hangi satır olduğu `detail` içinde). Sütun eşlemesi `appsettings.json` → `LogoExport:Columns` ile yapılandırılır. **Varsayılan sütunlar Logo'nun resmi Excel aktarım şablonuyla henüz doğrulanmadı**; ilk bayi görüşmesinde gerçek şablon alınıp güncellenecek.
- `413 Payload Too Large` iki yerde çıkabilir: `/api/orders/parse` (metin 20.000 karakteri aşarsa, uygulama kontrolü) ve `/api/catalog/import` (dosya sunucunun istek gövdesi sınırını aşarsa, Kestrel varsayılanı 30 MB — gövde ProblemDetails olmayabilir). İstemci 413'ü gövdeye bakmadan, isteğin türüne uygun Türkçe bir cümleyle karşılamalıdır.

## Alan anlamları
- `orderLine.unit`: **müşterinin metinde yazdığı birimdir** ("koli", "çuval"), katalog birimi değildir. Ürünün katalogdaki birimi `bestMatch.unit` (ve `/api/catalog` içindeki `unit`) alanındadır. İkisi farklı olabilir; arayüz metindeki birimi gösterir, metinde birim yoksa katalog birimine düşebilir.
- `exportLine`: yalnızca `sku`, `quantity`, `unit` taşır. **Ürün adını istemci göndermez; sunucu `sku`'yu katalogda çözüp Excel'deki "STOK ADI" sütununu kendisi doldurur.** Katalogda olmayan `sku` → 400. Böylece Excel'deki ad her zaman kataloğun kendi adıdır, ekrandaki serbest metin değildir.
- `exportLine.unit` boş gelirse sunucu katalog birimini yazar.
- `orderDraft.draftId`: yalnızca **izleme/teşhis kimliğidir** (`d_<UTC zaman damgası>_<8 karakter>`). Sunucu taslak durumu tutmaz; bu kimlikle sonradan taslak çağrılamaz, `export` isteği de onu taşımaz. Günlük satırlarını tek bir çözümlemeyle eşleştirmek için vardır. Arayüzün kullanma zorunluluğu yoktur. Kalıcılık (ADR-0001 karar 4) gelmezse alan kaldırılacaktır — bkz. ADR-0002.

## Hata yanıtları
- Hata gövdesi ASP.NET Core'un ürettiği **RFC 9457 ProblemDetails**'tir: `application/problem+json`, camelCase `type`, `title`, `status`, `detail`, `instance`.
- `ProblemDto(Title, Detail, Status)` bu gövdenin **istemci tarafı okuma modelidir**; sunucu bu tipi elle üretmez (`Results.Problem(...)` kullanılır). Alanların üçü de eksik gelebilir (ör. `UseStatusCodePages` ile üretilen 404/405 yanıtlarında `detail` yoktur); istemci `detail` boşsa duruma göre kendi Türkçe cümlesini gösterir.
- `title` ve `detail` Türkçe ve kullanıcıya gösterilebilir; `detail` içinde kişisel veri veya sipariş metni yer almaz.

## Örnek
```http
POST /api/orders/parse
{ "text": "Merhaba, 5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon, 10 adet 8'lik dübel. Tel: 0532 111 22 33", "source": "WhatsApp" }
```
```json
{
  "draftId": "d_01J...",
  "extractor": "RuleBased",
  "customerHint": null,
  "lines": [
    { "lineNo": 1, "rawText": "5 kutu 4x40 sunta vidası", "quantity": 5, "unit": "kutu", "productText": "4x40 sunta vidası",
      "status": "Matched", "bestMatch": { "sku": "VD-4040-SNT", "name": "Sunta Vidası 4x40 (500'lü kutu)", "unit": "kutu", "confidence": 0.91 }, "alternatives": [] }
  ],
  "maskedFields": ["phone"],
  "warnings": [],
  "createdAt": "2026-09-21T10:15:00+03:00"
}
```

## Sürüm notları
- v0.1 (2026-09-17): ilk sözleşme.
- v0.1.1 (2026-09-17): örnekten kişi adı çıkarıldı (KVKK notu).
- v0.1.2 (2026-09-18): `maskedFields` artık `name` da dönebilir — "Hasan Bey" gibi hitaplar maskeleniyor (kırıcı değil, listeye yeni değer). Ayrıca arayüz tarafında açık kalan dört soru karara bağlandı: `exportLine` ürün adı taşımaz, adı sunucu SKU'dan çözer; `orderLine.unit` metindeki birimdir, katalog birimi `bestMatch.unit`'tedir; `draftId` yalnızca izleme kimliğidir (sunucuda taslak durumu yok, bkz. ADR-0002); hata gövdesi ProblemDetails'tir ve `ProblemDto` onun istemci okuma modelidir; 413 katalog yüklemede de görülebilir.

# ADR-0002: Sözleşme netleştirmeleri (durumsuz taslak, ad çözümü, hata gövdesi)

- Durum: Kabul edildi · 2026-09-18
- İlgili: `docs/sozlesmeler/api.md` v0.1.2, ADR-0001

## Bağlam
İlk geliştirme turunda arayüz tarafında sözleşmede karşılıksız kalan dört nokta çıktı; API'yi
yazarken bunlar için örtük kararlar verdim. Kırıcı bir değişiklik yok, ama bu varsayımları yazılı
hale getirmezsem bir sonraki turda sessiz hatalar çıkar. Kararlar burada yazılı hale getirildi.

## Kararlar
1. **Ürün adını sunucu çözer.** `ExportLineDto` yalnızca `sku`, `quantity`, `unit` taşır. Excel'deki
   ad sütununu sunucu, `sku`'yu katalogda bularak doldurur; bilinmeyen `sku` → 400. Gerekçe:
   Excel'e giden ad her zaman kataloğun kendi adı olmalı; ekranda gösterilen serbest metin ya da
   eski bir katalog sürümünden kalan ad ERP'ye sızmamalı. İstemciye ad göndertmek aynı bilgiyi iki
   yerde doğru tutmayı gerektirirdi.
2. **`draftId` izleme kimliğidir, durum tutmaz.** ADR-0001 karar 4 gereği kalıcılık yok; sunucu
   taslağı hatırlamaz. Alan, tek bir çözümlemenin günlük satırlarını eşleştirmek için kalıyor.
   Şimdilik KALDIRILMIYOR (sözleşme değişikliği ve hem API hem arayüzde düzenleme maliyeti, demo
   öncesi kazanca değmez). SQLite kararı alınmazsa v0.2'de kaldırılması gerekiyor.
3. **Hata gövdesi ProblemDetails'tir.** Sunucu `Results.Problem(...)` kullanır; gövde RFC 9457,
   `application/problem+json`, camelCase. `ProblemDto` sunucunun ürettiği bir tip değil, istemcinin
   okuma modelidir. Alanlar eksik gelebilir (durum kodu sayfalarının ürettiği 404/405 yanıtlarında
   `detail` yoktur); istemci boş `detail` için kendi Türkçe cümlesini gösterir.
4. **413 iki uçta da olabilir.** `/api/orders/parse` uygulama sınırıyla (20.000 karakter),
   `/api/catalog/import` sunucunun istek gövdesi sınırıyla (Kestrel varsayılanı 30 MB) 413 döner.
   İkincisinde gövde ProblemDetails olmayabilir; istemci 413'ü gövdeye bakmadan karşılamalıdır.
5. **Birim alanı ikiye ayrılır.** `orderLine.unit` müşterinin yazdığı birimdir, `bestMatch.unit`
   katalog birimidir. Birbirinin yerine geçmezler; aktarımda `exportLine.unit` boşsa katalog birimi
   yazılır. Gerekçe: "2 koli" ile kataloğun "kutu" birimi arasındaki fark sahada konuşulacak bir
   bulgudur, sessizce ezilmemeli.

## Sonuçlar
- Olumlu: API ve arayüz aynı varsayımla çalışır; Excel'deki ad tek kaynaktan gelir.
- Olumsuz: `draftId` şimdilik kullanılmayan bir alan olarak duruyor (sözleşme gürültüsü).
- Takip: Frontend `maskedFields` içindeki `name` türü için Türkçe karşılık eklemeli; aksi halde
  "kişisel bilgi" genel ifadesi gösterilir (yanlış değil, belirsiz).

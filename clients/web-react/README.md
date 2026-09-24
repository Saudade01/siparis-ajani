# SiparisAjani — React istemcisi

Blazor arayüzüyle (`src/SiparisAjani.Web`) aynı API'yi kullanan ikinci istemci: React + TypeScript + Vite.
Blazor Server sürekli açık bir bağlantı ister; saha demosunda telefon/tabletten ve zayıf internette çalışsın diye
bu istemci statik dosya olarak dağıtılabiliyor. Ekran akışı aynı: metni yapıştır, satırları kontrol et, gerekirse
ürünü seç, Excel'e aktar.

## Çalıştırma

```bash
npm install
npm run dev      # http://localhost:5173 · /api istekleri http://localhost:5080 adresine vekillenir
npm test         # Vitest + Testing Library
npm run build    # dist/
```

API'nin ayakta olması için depo kökünde `./scripts/dev.sh` çalıştırın. API başka bir adresteyse `.env.example`
dosyasını `.env` olarak kopyalayıp `VITE_API_BASE_URL` değerini yazın.

`src/api/types.ts` sözleşmenin (`docs/sozlesmeler/api.md`) elle yazılmış karşılığıdır; sözleşme değişirse
bu dosya da güncellenmelidir.

Katalog CSV yükleme ekranı yalnızca Blazor arayüzünde var; bu istemci sipariş akışına odaklanır.

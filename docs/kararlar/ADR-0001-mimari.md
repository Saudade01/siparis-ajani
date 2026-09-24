# ADR-0001: MVP mimarisi

- Durum: Kabul edildi · 2026-09-17

## Bağlam
Ürün sahada demo ve 1–2 pilot için gerekiyor. Kurucu günde 3 saat çalışıyor, junior .NET geliştiricisi. İleride aynı sunucu WhatsApp Business webhook'u ve bayi entegrasyonları tarafından da çağrılacak. Müşteri verisi KVKK kapsamında.

## Karar
1. **Ayrı API + Blazor Server arayüzü**, ortak `Contracts` projesi. Tek uygulama daha basit olurdu, ama API'yi başka kanalların (webhook, bayi) da kullanacağı için sınır baştan çiziliyor.
2. **Core / Infrastructure ayrımı:** Ayrıştırma, eşleştirme ve maskeleme saf ve test edilebilir. Claude, Excel ve dosya işleri değiştirilebilir adaptörler.
3. **İki ayrıştırıcı:** `ClaudeOrderExtractor` (resmi `Anthropic` SDK, `claude-opus-5`, JSON şemalı yapılandırılmış çıktı, reddedilmeye karşı sunucu tarafı yedek model) ve `RuleBasedOrderExtractor` (anahtarsız demo, testler ve CI için). Seçim: `Anthropic:ApiKey` tanımlıysa Claude.
4. **Kalıcılık yok (şimdilik):** Katalog açılışta CSV'den belleğe yüklenir, düzeltmeler (takma adlar) `App_Data/aliases.json` dosyasında tutulur. SQLite, ilk ücretli pilotta değerlendirilecek (ADR açılacak).
5. **Logo entegrasyonu dosya ile:** Excel (ClosedXML) çıktısı, sütunlar yapılandırmadan. Logo REST erişimi çözüm ortaklığı gerektirdiği için sonraya kaldı.
6. **Aspire, Docker ve bulut dağıtımı yok.** `scripts/dev.sh` iki süreci başlatır. Pilot sunucuya çıkarken yeniden değerlendirilecek.

## Sonuçlar
- Olumlu: Anahtarsız çalışan demo, deterministik testler, rol bazlı paralel geliştirme.
- Olumsuz: İki süreç çalıştırmak gerekiyor. Bellek içi katalog yeniden başlatmada CSV'den tekrar yükleniyor. Kural tabanlı ayrıştırıcının doğruluğu sınırlı, bu yüzden demo kalitesi anahtara bağlı.
- Maliyet notu: `claude-opus-5` için 1M token başına $5 girdi / $25 çıktı. Kısa bir sipariş yüzlerce token tutar. Pilotta gerçek sipariş başına maliyet ölçülecek.

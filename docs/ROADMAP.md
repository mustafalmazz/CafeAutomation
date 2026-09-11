# Güncel kapsam · 0.3.0

Personel/PIN/rol, yönetici onayı, vardiya ve kasa, gerekçeli adisyon düzeltmeleri ve manuel iadeler, ürün bazında bölme/birleştirme, temel ve seçenek reçeteleri, fire/sayım, sadakat kazanımı/kullanımı, kuponlar, tarih ve maliyet raporları, yerel QR istekleri, HTTPS personel terminali, Windows yazıcı ayarları ve yedekten dönüş uygulanmıştır.

Çalışan akışlar ve sınırlamalar README.md ile OPERATIONS.md içindedir.

SQL Server'a geçiş tamamlanmıştır: 19 ilişkisel tablo, EF Core migration, eski verinin doğrulamalı aktarımı, transaction, çakışma kontrolü, bağlantı ayarları ve doğrulanan SQL yedeği. Kurulum ve işletim ayrıntıları SQL_SERVER.md içindedir.

## Harici bağımlılık nedeniyle saha aşamasında

- Gerçek banka POS ve mali fiş/ÖKC/e-belge: cihaz ve sağlayıcı belirlenmedi; canlı adaptör yok.
- Fiziksel yazıcı, tablet, dokunmatik POS, ağ kesintisi ve tesis içi TLS kurulumu: saha donanımı üzerinde doğrulanmalı.

## Bu sürümün dışındaki ürün geliştirmeleri

Çoklu şube, merkezi bulut yönetimi, yüksek erişilebilir veritabanı, otomatik güncelleme/lisanslama, tedarikçi siparişleri ve mal kabul, birim dönüşümleri, özelleştirilebilir vergi/ürün seçenek grupları, otomatik termal yazıcı kuyruğu, mesajlaşma/yemek platformu entegrasyonları ve işletme giderleri dahil net kâr.

Mevcut yerel pilot; banka, mali cihaz ve çoklu şube entegrasyonları tamamlanmış bir ticari paket olarak sunulmamalıdır.

# Ürün yol haritası

## 0.1 — Tasarım ve çalışan satış çekirdeği (mevcut)

Espresso/krem/bakır tasarım sistemi; uyarlanabilir WPF ekranları; örnek ürünler; masa, sipariş, hazırlık ve tutar bazında bölünmüş tahsilat; menü yönetimi; günlük rapor; tek cihazda kalıcı kayıt ve yedek.

## 0.2 — İşletme operasyonu

1. Kimlik doğrulama, rol/işlem yetkileri ve kritik işlem onayı.
2. Kalıcı veritabanı ve sürümlü veri geçişi. Mevcut örnek veriler ayrı kalır; gerçek işletme kurulumu açılır.
3. Nedenli iptal, ikram, indirim, kısmi/tam iade ve tutarlı ters kayıtlar.
4. Vardiya açılışı, kasa giriş/çıkışları, beklenen/sayılan nakit ve kapanış.
5. Ürün bazında özelleştirilebilir seçenek grupları, vergi kuralları ve fiyat listeleri.
6. Bar/mutfak istasyon yönlendirmesi, ayrı hazırlık takibi ve yazıcı kuyruğu.
7. İlk pilot: gerçek kasa, dokunmatik ekran, yazıcı, elektrik/kesinti ve geri yükleme kontrolleri.

Kabul: aynı işlemin tekrarı mükerrer tahsilat/stok hareketi üretmez; iptal/iade izlenebilir; vardiya tutarları ödeme kayıtlarıyla uzlaşır.

## 0.3 — Stok ve maliyet

Malzeme/birim dönüşümleri, reçete ve seçenek tüketimleri, alış maliyeti, tedarikçi/mal kabul, fire ve sayım, kritik stok uyarısı, ürün katkı payı. Net kâr için işletme giderleri ayrıca dahil edilir.

Kabul: satış, iptal ve iadelerde malzeme hareketleri tutarlı; stok sayımı ve maliyet hesapları doğrulanmış örneklerle uzlaşır.

## 0.4 — Müşteri ve büyüme

Sadakat kartı/puan/damga, izin ve iletişim tercihleri, süre/kullanım sınırı olan kampanyalar ve kullanım raporu. Ortak ürün kaynağıyla mobil QR menü; daha sonra masa doğrulamalı sipariş ve servis çağrısı.

## 0.5 — Ağ, entegrasyonlar ve ticari paket

Yerel API ve çoklu kasa/garson terminali; bağlantı kaybı ve eşzamanlı işlem senaryoları. Sağlayıcı erişimi ve gerekli doğrulamalarla banka POS'u, ÖKC/e-belge, yemek platformları ve mesajlaşma. Kurulum/güncelleme/geri alma, lisanslama, destek, veri taşıma ve tanıtım materyalleri.

## 1.0 — Pilot sonrası

Gerçek işletmede kabul edilen çekirdek; şube yönetimi, merkezi menü, kampanya ölçümü. Tahmin/öneri özellikleri yeterli veri oluştuktan sonra eklenir. Kullanıcı ve donanım geri bildirimlerine göre ekran akışları iyileştirilir.

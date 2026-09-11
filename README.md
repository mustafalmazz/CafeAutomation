# Atelier · Kafe Yönetimi 0.3.0

Windows/WPF masaüstü uygulaması; espresso, krem ve bakır tasarımı, 18 gömülü gerçek ürün fotoğrafı, yerel QR menü ve HTTPS personel terminali.

## İlk açılış

`artifacts/Atelier-win-x64/PremiumKafeOtomasyon.exe` dosyasını çalıştırın. Klasörün tamamını birlikte taşıyın; .NET kurulumu gerekmez. **SQL Server gereklidir**; varsayılan bağlantı Windows kimliğiyle `.\SQLEXPRESS`, veritabanı `AtelierCafe` olur. [SQL Server kurulumu ve geçiş](docs/SQL_SERVER.md).

Boş kurulumda kendi yönetici adınızı ve 6–12 rakamlı PIN'inizi belirleyin. Hazır yönetici şifresi yoktur. Önceki sürümden geçişte mevcut veriler ve personel PIN'leri korunur; yeniden yönetici oluşturulmaz.

1. **İşletme & ayarlar → Personel**: kasiyer/garson oluşturun.
2. **Kasa & vardiya**: açılış nakdini girin. Tahsilat için açık vardiya gerekir.
3. **Stok & reçete**: içerikleri, giriş miktarlarını ve porsiyon reçetelerini tanımlayın.
4. **Masalar → Satış**: adisyon oluşturun, hazırlığa gönderin, ödeme alın.
5. Gün sonunda sayılan nakitle vardiyayı kapatın; dış yedeğinizi alın.

## Çalışan modüller

| Alan | Kapsam |
|---|---|
| Personel | Tuzlu PBKDF2 PIN, yönetici/kasiyer/garson, 5 hatalı denemede kilit, personel devre dışı bırakma ve PIN sıfırlama, indirim/iade için yönetici onayı |
| Kasa | Vardiya açılışı, gerekçeli nakit giriş/çıkışı, beklenen/sayılan nakit ve fark, kapanış geçmişi |
| Adisyon | Masa taşıma, ürün/adet bazında hesap bölme, adisyon birleştirme, kişi adıyla kısmi/karma tahsilat, gerekçeli indirim/ikram/iptal, kısmi/tam manuel iade |
| Hazırlık | Bar/mutfak filtreleri, sipariş sırası, gecikme göstergesi, ürün bazında hazırlama/hazır/teslim |
| Stok | g/ml/adet, giriş/fire/sayım, kritik stok, temel ve boyut/süt/shot seçeneğine özel reçete, hazırlıkta tek seferlik düşüm ve tarihsel maliyet |
| Sadakat | Müşteri geçmişi, puan ve damga kazanımı/kullanımı, iadede kazanım düzeltmesi |
| Kampanya | Sabit tutarlı kupon, minimum sepet, son kullanım ve toplam kullanım limiti |
| Rapor | Günlük/haftalık/aylık/yıllık seçim, yıllık hasılat, haftanın/ayın/yılın ürünü ve geçmiş dönemler, yöntem dağılımı, iade göstergesi, tarih aralığı, saatlik satış, indirim/ikram/iptal, ürün katkı payı, CSV ve metin dışa aktarım |
| QR menü | Masaya özel QR kartları, gerçek fotoğraflar, alerjen/seçenekler, sipariş isteği, garson çağrısı, personel onayı |
| Çoklu terminal | HTTPS tarayıcı girişi, ortak masa/adisyon görünümü, ürün ekleme, hazırlığa gönderme, role göre manuel tahsilat, kalıcı tekrar işlem engeli |
| Cihaz/yedek | Windows yazıcı seçimi ve 58/80 mm/A4 hesap dökümü, POS test merkezi, dış yedek, doğrulamalı geri yükleme |

## Önemli iş kuralları

- Tahsilat başlayan hesapta ürün değişikliği yapılmaz. İndirimli/kuponlu hesaplar bölünmez/birleştirilmez. Kupon ve sadakat indirimi üst üste uygulanmaz.
- Reçete stoğu hazırlığa gönderimde tüketilir. Seçenek reçetesi tanımlıysa temel reçetenin yerine geçer; tüm bileşenleri içermelidir. Yetersiz stokta işlem bütünüyle reddedilir.
- Hazırlanmış ürünün iptal/iadesi stoğu otomatik geri eklemez. Geri kullanılabilir malzeme için gerekçeli stok hareketi gerekir.
- Her 10 ₺ net tamamlanan satış 1 puan; 1 puan 1 ₺ indirimdir. Her pozitif ödenmiş hesap 1 damga; 10 damga en düşük fiyatlı bir ürün tutarında indirimdir. İade sonrası harcanmış puan/damga nedeniyle negatif bakiye oluşabilir; sonraki kazanımlar bunu kapatır.
- Maliyet raporu yalnızca kayıtlı reçete maliyetini içerir. Reçetesiz ürün maliyeti sıfırdır; gösterge net işletme kârı değildir.
- Kart/iade düğmeleri manuel kayıt oluşturur. Bankadan para çekme/iade etme işlemi yapmaz. POS simülatörü de gerçek tahsilat veya adisyon kapanışı oluşturmaz.

## Ağ ve cihaz kurulumu

[Yerel ağ / QR / terminal kurulumu](docs/OPERATIONS.md) · [POS test merkezi](docs/POS.md)

Masaüstü minimum 800 × 600 kullanılabilir DIP; Windows ölçeklendirmesi desteklenir. Telefon/tabletler tarayıcı üzerinden yerel menü/terminale erişir. Ana bilgisayar açık kalmalıdır. Çevrimdışı terminaller yeni işlem kaydetmez; belirsiz isteklerin tekrarında işlem numarası korunur.

Gerçek banka POS'u, mali fiş/ÖKC/e-belge adaptörü ve fiziksel cihaz kabul testleri **tamamlanmış değildir**. Sağlayıcı, cihaz modeli, resmi SDK/test erişimi ve sahadaki donanım gereklidir. Termal baskı Windows sürücüsü üzerinden hazırlanır; doğrudan ESC/POS sürücüsü yoktur.

## Veri ve güvenilirlik

Canlı veri kaynağı **Microsoft SQL Server** ve Entity Framework Core 8'dir. Ürünler, adisyonlar, adisyon satırları, ödemeler, personel, stok ve diğer kayıtlar 19 ilişkisel tabloda tutulur. İşlem ve terminal tekrar kontrol anahtarı aynı SQL transaction içinde kaydedilir; hatada işlem geri alınır. Sürüm kontrolü, eski veriye sahip ikinci uygulamanın yeni kayıtların üzerine yazmasını engeller. Merkezi WPF işlem kuyruğu istemci değişikliklerini sıraya alır.

Bağlantı ayarları `%LOCALAPPDATA%/AtelierCafe/database.json` içindedir. Eski `demo-state.json` ilk geçişte ayrıca yedeklenir, aktarım SQL'den okunarak karşılaştırılır ve kaynak dosya korunur. SQL bağlantısı kesildiğinde JSON'a otomatik dönüş yapılmaz. JSON yalnızca taşınabilir dış yedek ve test senaryolarında kullanılır.

İşletme & ayarlar → Yedek bölümünden SQL `.bak` yedeği veya taşınabilir JSON dış yedeği alınabilir. SQL yedeği sunucunun yedek dizinine yazılır ve `RESTORE VERIFYONLY` ile kontrol edilir. Bu kontrol, ayrı bir veritabanına gerçek geri yükleme tatbikatının yerine geçmez. Yedekleme otomatik zamanlanmaz.

Bu sürüm tek merkezi uygulama üzerinden yerel pilot içindir; çoklu şube ve otomatik failover yoktur. Uygulama başlangıçta işletme durumunu belleğe alır; büyüyen veri hacmi için sorgu ve yük testleri gerekir. Windows/SQL erişimi ve yedek dosyaları korunmalıdır. Dış yedekler işletme bilgilerini ve PIN hashlerini içerir.

Geri yükleme mevcut işletme verilerini değiştirir, personel hesaplarını korur ve aktif vardiya yokken yapılır. Önce ayrı yedek alın. Bozuk dosya otomatik sıfırlanmaz. Geri yükleme sonrası QR kartlarını yeniden üretin ve terminalleri yenileyin.

## Geliştirme ve doğrulama

```powershell
dotnet run --project PremiumKafeOtomasyon
dotnet run --project PremiumKafeOtomasyon.Verification -c Release -- artifacts/verification-operations
dotnet run --project PremiumKafeOtomasyon.Verification -c Release -- --sql
dotnet publish PremiumKafeOtomasyon -c Release -r win-x64 --self-contained true -o artifacts/Atelier-win-x64
```

132 uygulama kontrolü; iş kuralları, kayıt hatası, stok atomikliği, PIN/rol, sadakat, iade/kasa, HTTPS giriş, HTTP istek tekrarı, çıkış ve farklı WPF ekran ölçülerini kapsar. Ayrıca 21 gerçek SQL Server kontrolü; aktarım eşitliği, ilişkisel kayıtlar, yeniden açılış, çakışma, transaction geri alma, ondalık hassasiyet, dış yedek ve SQL yedeğini doğrular. SQL testleri ayrı `AtelierCafe_Verification_*` veritabanları oluşturur. Fiziksel terminal ve telefon üzerinde saha kabulünün yerine geçmez.

Fotoğraf kaynakları: [PHOTO_CREDITS](docs/PHOTO_CREDITS.md). QR üretimi [QRCoder 1.8.0](https://www.nuget.org/packages/QRCoder/1.8.0) ile yapılır; MIT lisansı `docs/QRCoder-LICENSE.txt` içindedir.

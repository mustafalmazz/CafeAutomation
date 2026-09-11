# Yerel ağ, QR ve terminal kurulumu

## Masaüstü

SQL Server hizmeti ve veritabanı bağlantısı gereklidir. [SQL Server kurulumu ve yedekleme](SQL_SERVER.md). Canlı veriler SQL'dedir; eski JSON dosyasını başka bilgisayara taşımak güncel işletmeyi taşımaz.

İlk girişte kendi yönetici PIN'inizi oluşturun. Personel ve vardiya yönetimi İşletme & ayarlar ekranındadır. Menü ve mevcut siparişler sıfırlanmaz. Uygulama örnek işletme verileriyle devam eder.

## Yerel menü ve personel terminali

1. Ana bilgisayarın yerel IPv4 adresini öğrenin (Windows ağ özellikleri). Örnek `192.168.1.20`.
2. İşletme & ayarlar → QR menü & istekler bölümünde `https://192.168.1.20:5088` gibi bir adresle sunucuyu başlatın. IP bilgisayara ait olmalıdır; 1024 üstü boş bir port seçin. `127.0.0.1` yalnızca aynı bilgisayara erişim içindir.
3. İlk HTTPS başlangıcında yalnızca bu IP için yerel TLS sertifikası üretilir. Özel anahtar Windows kullanıcısının kişisel sertifika deposunda saklanır. Sertifika güvenilir köklere otomatik eklenmez.
4. `.cer` dosyasını uygulamadan dışa aktarın. Yönetiminizdeki istemci cihazlarda bu sertifikayı güvenilir olacak şekilde kurun veya uygun bir kurum sertifikası altyapısı kullanın. Tarayıcı güvenlik uyarılarını atlayarak personel PIN'i girmeyin. IP değişirse yeni sertifika ve QR kartları gerekir.
5. Windows güvenlik duvarında yalnızca işletmenin özel yerel ağı için seçilen TCP portuna izin verin. Uygulama güvenlik duvarını veya yönlendiriciyi değiştirmez. İnternete port yönlendirmesi yapmayın.
6. Masa QR kartlarını bir klasöre kaydedin. `masa-qr.html` dosyasını tarayıcıda açıp yazdırın. Her masa ayrı bir token kullanır. Menüdeki istekler personel onayına düşer.
7. Personel cihazında `https://192.168.1.20:5088/terminal` açın. Kendi personel hesabı/PIN'iyle girin. Garson sipariş alabilir; kasiyer/yönetici açık vardiya üzerinde manuel tahsilat kaydedebilir.

HTTP sunucusu yalnızca misafir menüsünü destekler; personel API'si HTTPS ister. Misafirlerin kendi telefonlarında yerel sertifika yönetimi pratik değilse QR menü için HTTP yerel önizleme kullanılabilir; personel terminali için ayrıca HTTPS kullanımı gerekir. Tek sunucu bir anda tek adresle çalışır. Genel internet yayını bu sürümün kapsamı değildir.

## Eşzamanlı işlemler

Merkezi masaüstü uygulaması kayıt işlemlerini tek kuyrukta yürütür. Terminal verileri 5 saniyede bir yenilenir. İstek yeniden gönderilirse aynı kimlik tek işlem sayılır; işlem ve kimlik atomik kaydedilir. Telefon çevrimdışı kaldığında yeni ödeme kaydetmeyin; sonucu belirsiz işlemi aynı düğmeyle yeniden deneyin. İşlem bilgisi o sekmenin oturum deposunda korunur. Sekme/cihaz verilerini temizlemek bu tekrar anahtarını kaybettirir; önce merkezi adisyonu kontrol edin.

Terminal oturumları 8 saattir; çıkış ve sunucu durdurma oturumu iptal eder. Devre dışı bırakılmış personelin işlemleri reddedilir. Ana uygulama kapalıysa menü ve terminaller kullanılamaz. Çoklu ana sunucu/failover ve çevrimdışı ödeme kuyruğu yoktur.

## Saha kabulünde gerekenler

- Gerçek ekran ölçeklendirmesi, dokunmatik kullanım ve Wi-Fi erişimi.
- TLS güven zinciri ve her istemcinin bağlantısı.
- Windows yazıcı sürücüsü, 58/80 mm baskı ölçüsü ve fiş kesici davranışı.
- Ana bilgisayar yeniden başlatma, ağ kopması ve harici yedekten dönüş.
- Canlı banka POS/ÖKC için cihaz modeli, sağlayıcı SDK'sı, test hesabı ve gerçek cihaz kabulü.

Yazılım içi kontroller bu fiziksel doğrulamaların yerine geçmez.

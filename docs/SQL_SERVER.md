# SQL Server kurulumu ve veri geçişi · 0.3.0

Atelier'in canlı veri deposu Microsoft SQL Server'dır. JSON dosyası canlı veritabanı olarak kullanılmaz. Uygulama Entity Framework Core 8 ve sürümlenmiş migration kullanır.

## Yerel kurulum

1. SQL Server Express kurun veya mevcut SQL Server örneğini kullanın. Geliştirme bilgisayarında SQL Server 2022 Express üzerinde doğrulanmıştır.
2. SQL Server hizmetini başlatın. Varsayılan sunucu `.\SQLEXPRESS`, veritabanı `AtelierCafe`, kimlik doğrulama Windows hesabıdır.
3. Uygulamayı açın. Bağlantı kurulamıyorsa sunucu/veritabanı ayarları ekranı açılır. Bağlantı testi sunucuya erişimi doğrular; ilk açılış ayrıca veritabanı ve şema izinlerini kontrol eder.
4. İlk kurulum için veritabanı oluşturma ve şema değiştirme yetkisi gerekir. Önceden hazırlanmış bir veritabanı da kullanılabilir. Sistem veritabanları hedef olarak kabul edilmez.
5. Boş kurulumda örnek menü oluşturulur ve yönetici PIN'i tanımlanır. Mevcut veriden geçişte personel ve PIN hashleri korunur.

Bağlantı ayarları `%LOCALAPPDATA%\AtelierCafe\database.json` içindedir. SQL kullanıcı adı/şifre seçilirse şifre Windows DPAPI ile mevcut Windows kullanıcısına bağlı şifrelenir. Yapılandırma başka kullanıcıya kopyalandığında şifre yeniden girilmelidir. Bağlantı bilgileri repoya eklenmez.

Yerel varsayılan bağlantı şifreli iletişim kullanır ve sunucu sertifikasına güvenir (`TrustServerCertificate=true`). Uzak sunucuda geçerli TLS sertifikası kurun ve bu seçeneği kapatın. Telefon ve tabletler SQL'e doğrudan bağlanmaz; merkezi uygulamanın HTTPS terminalini kullanır.

## Eski verinin korunması

İlk geçişte `%LOCALAPPDATA%\AtelierCafe\demo-state.json`, aynı dizinin `migration-backups` klasörüne benzersiz adla kopyalanır. Kaynak dosya değiştirilmez. Aktarılan tüm alanlar ve liste sırası SQL'den yeniden okunarak karşılaştırılır; uyuşmazlıkta aktarım transaction'ı geri alınır.

Başarılı geçişten sonra `LegacyImportAllowed=false` kaydedilir. Dolu veritabanı eski JSON ile tekrar doldurulmaz. Bağlantı hatasında eski dosyaya dönülmez ve boş işletme oluşturulmaz. Bağlantı ayarını değiştirmek verileri başka sunucuya taşımaz; önce hedef veritabanına SQL yedeğini geri yükleyin, sonra yeni bağlantıyla uygulamayı yeniden başlatın.

## Veri yapısı ve işlemler

19 uygulama tablosu vardır: Settings, Products, CafeTables, Orders, OrderLines, Payments, Employees, CashShifts, CashMovements, Ingredients, RecipeParts, StockEntries, Customers, Coupons, Refunds, GuestRequests, PosTransactions, AuditEntries, TerminalOperations. EF migration geçmişi ayrıca tutulur.

Adisyon/satır/ödeme ve stok/reçete ilişkileri foreign key ile korunur. Ödeme tutarı ve ürün miktarı gibi alanlar CHECK kısıtları içerir. Adisyon numarası ve masa token'ı gibi alanlarda benzersiz indeksler vardır. Parasal/miktarsal alanlar `decimal(28,8)` kullanır; daha hassas değerler sessiz yuvarlama yerine reddedilir.

Her iş işlemi tek SQL transaction'ıdır. Kayıt sürümü çakışırsa üzerine yazma engellenir ve uygulamayı yeniden açma bildirimi verilir. Önerilen kullanım tek merkezi WPF uygulaması ve ona bağlı tarayıcı terminalleridir; birbirinden bağımsız çoklu WPF sunucuları arasında anlık senkronizasyon uygulanmamıştır.

## Yedek ve geri dönüş

- İşletme & ayarlar → Yedek → SQL yedeği: `COPY_ONLY, CHECKSUM` ile `.bak` üretir ve `RESTORE VERIFYONLY` çalıştırır. Dosya istemciye değil SQL sunucusunun varsayılan yedek dizinine yazılır. SQL hesabının yedek alma, SQL hizmetinin dizine yazma izni olmalıdır.
- Taşınabilir JSON dış yedeği uygulamadan alınabilir. Geri yükleme işletme verilerini değiştirir, personel hesaplarını korur; açık vardiya varken yapılamaz. Geri yükleme SQL transaction'ıyla uygulanır.
- `.bak` geri yüklemesi SQL Server yönetim araçlarıyla yapılır. Önce ayrı bir veritabanında geri yükleyip sayım ve uygulama akışlarını kontrol edin. Canlı hedefi değiştirmeden önce güncel yedek alın.
- Yedek zamanlama ve başka cihaza kopyalama bu sürümde otomatik değildir. Geçiş öncesi JSON yedeği yalnızca geçiş anını içerir; sonrasındaki işlemler SQL'dedir.

## Geliştirme ve şema dağıtımı

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project PremiumKafeOtomasyon --output artifacts/atelier-schema.sql
dotnet run --project PremiumKafeOtomasyon.Verification -c Release -- --sql
```

Migration kaynakları `PremiumKafeOtomasyon/Data/Migrations` içindedir. Uygulama açılışta bekleyen migration'ları uygular; güncellemelerden önce yedek alın. Şema yönetimini ayrı hesapla yapıyorsanız betiği yetkili hesapla önceden uygulayın ve çalışma hesabına gereken okuma/yazma izinlerini verin. SQL testleri gerçek sunucuda benzersiz `AtelierCafe_Verification_*` veritabanları oluşturur; işletme veritabanını kullanmaz.

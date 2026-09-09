# Atelier · Kafe Yönetimi

Espresso, krem ve bakır renkleriyle tasarlanmış, Windows/WPF tabanlı çalışan kafe otomasyonu önizlemesi. Sürüm **0.1.0**. İlk açılışta örnek işletme, 18 ürün, 12 masa, bir gel-al alanı ve üç örnek adisyon oluşturulur. Ekrandaki **DEMO İŞLETME** etiketi bu kapsamı belirtir.

## Çalıştırma

Hazır Windows x64 paketi: `artifacts/Atelier-win-x64/PremiumKafeOtomasyon.exe`. Bu paketin tüm klasörünü birlikte taşıyın; .NET kurulumu gerektirmez.

Kaynak koddan (.NET 8 veya sonraki SDK ile):

```powershell
dotnet run --project PremiumKafeOtomasyon
```

Visual Studio'da `PremiumKafeOtomasyon.sln` dosyasını açıp başlangıç projesini `PremiumKafeOtomasyon` seçebilirsiniz.

## Bu sürümde çalışanlar

- Masa/alan seçimi, müsait/açık/hazır durumları, boş masaya adisyon taşıma ve gel-al hesabı.
- Kategori filtreleri, Türkçe ürün/içerik arama, ürün ekleme ve düzenleme, satışa kapatma.
- Kahvelerde boyut, süt ve shot seçenekleri; fiyat farkları ve hazırlık notları.
- Taslak ürünlerde miktar artırma/azaltma; son ürün kaldırılınca masanın boşalması.
- Hazırlığa gönderme; yeni → hazırlanıyor → hazır → teslim edildi akışı.
- Tutar bazında bölünmüş tahsilat; nakit, kart kaydı ve diğer yöntemlerin aynı hesapta kullanılması.
- Tam ödeme sonrası adisyon kapanışı ve masanın serbest kalması.
- Günlük tamamlanan hesaplar, bugünkü tahsilatlar, ödeme dağılımı, ortalama hesap ve ürün satış sıralaması.
- CSV raporu, Windows yazdırma penceresiyle mali olmayan hesap dökümü.
- İşletme adı düzenleme, yerel işlem geçmişi, otomatik önceki sürüm yedeği ve dışarıya yedek kopyası.

Siparişe eklenen ürünün adı ve fiyatı o adisyonda korunur; menüde yapılan değişiklik geçmiş hesabı değiştirmez. Tahsilat başlayan hesaplarda ürün değişikliği kapatılır. Hazırlığa gönderilen ürünler sessizce silinemez. İptal/iade için neden, yetki ve stok ters hareketleri sonraki operasyon aşamasında ele alınacaktır.

## Hızlı deneme

1. **Masalar** ekranında boş bir masayı seçin.
2. Ürünü seçip seçeneklerle adisyona ekleyin.
3. **Hazırlığa gönder** düğmesine basın. **Hazırlık** ekranından siparişi ilerletin.
4. Satış ekranına dönüp **Ödeme al** seçin. Örneğin 100,00 ₺ nakit kaydedin.
5. Yeniden ödeme ekranını açıp kalanı kart olarak kaydedin. Hesap kapanır.
6. **Raporlar** ekranında satış ve iki tahsilatı görün. Uygulamayı kapatıp açarak kayıtların korunduğunu kontrol edin.

Kart seçimi yalnızca tahsilat kaydı oluşturur; banka cihazından ödeme çekmez. Döküm mali fiş değildir.

## Ekran ve cihaz kapsamı

- Windows masaüstü, Windows tablet ve Windows tabanlı dokunmatik POS için WPF arayüzü.
- En az **800 × 600 DIP kullanılabilir çalışma alanı**. DIP fiziksel piksel değildir; Windows ölçeklendirmesi kullanılabilir alanı değiştirir.
- Örneğin 1280 × 800 ekranın %125 ölçeklemesi yaklaşık 1024 × 640 DIP sağlar; görev çubuğu ve pencere başlığı ayrıca alan kullanır.
- Dar ekranda simgeli gezinme; kısa ekranda kompakt ürün görselleri; kullanılabilir genişliğe göre ürün sütunları; sabit ödeme alanı.
- PerMonitorV2 manifesti, vektörel ürün çizimleri, dokunarak kaydırma ve büyük işlem düğmeleri.
- F2 satış, F3 masalar, F4 hazırlık, F9 tahsilat, Ctrl+F ürün arama, Escape pencere kapatma.
- Android, iPad, tarayıcı ve ağ üzerinden çoklu kasa henüz desteklenmiyor.
- Fiziksel POS terminali, banka cihazı veya fiş yazıcısı üzerinde saha testi yapılmadı. Windows yazıcı sürücüsüyle hesap dökümü için baskı penceresi kullanılır; otomatik termal fiş/ESC-POS entegrasyonu yoktur.

## Veriler

Asıl dosya: `%LOCALAPPDATA%/AtelierCafe/demo-state.json`

Önceki kayıt: `%LOCALAPPDATA%/AtelierCafe/demo-state.json.bak`

Her değişiklik yeni bir durum kopyasına uygulanır. Geçici dosya diske yazılıp flush edilir; ardından asıl dosya atomik olarak değiştirilir. Başarısız kayıtta çalışan uygulama önceki durumunu korur. Aynı oturumda ikinci uygulama açılışı engellenir. Bu yapı tek kullanıcılı/tek cihazlı önizleme içindir; çoklu cihaz için servis ve veritabanı katmanına geçilmelidir.

Bozuk dosya otomatik olarak sıfırlanmaz. Kurtarma için uygulamayı kapatın, asıl dosyayı ayrı bir konumda koruyun, doğrulanmış `.bak` veya dış yedeğin **kopyasını** `demo-state.json` olarak yerleştirin. Önceki sürüm yedeği yalnızca bir kayıt öncesini kapsar; harici disk/bulut yedekleme yerine geçmez.

## Doğrulama

```powershell
dotnet build PremiumKafeOtomasyon.sln -c Release
dotnet run --project PremiumKafeOtomasyon.Verification -- artifacts/verification
```

Doğrulama ayrı geçici örnek verilerle çalışır; kullanıcının işletme kayıtlarına dokunmaz. Ödeme sınırları, ek seçenekler, hazırlık, masa taşıma, disk yazma hatası, bozuk dosya, yeniden açılış, fiyat geçmişi ve arayüz düğmelerini kapsar. 1480, 1280, 1024, 980 ve 800 DIP genişlikte WPF ekranları üretir; veri bağlama hatalarını kaydeder. Bunlar uygulama içi yazılım/yerleşim kontrolleridir, fiziksel donanım sertifikasyonu değildir.

Görüntüler, `result.txt` ve `binding-errors.txt` dosyaları `artifacts/verification` içinde oluşur.

## Mimari ve devam eden kapsam

`Domain`: ürün, masa, sipariş ve ödeme modeli. `Services`: kalıcı kayıt ve iş kuralları. `ViewModels`: ekran durumları ve komutlar. `Themes`: ortak tasarım sistemi. `Views`: sayfalar ve işlem pencereleri. `Controls`: çözünürlükten bağımsız çizimler ve uyarlanabilir kart yerleşimi.

Bu önizleme ilk tasarım ve satış çekirdeğini sunar. Canlı ticari kullanım öncesinde yetkilendirme/PIN, nedenli iptal ve iade, vardiya/kasa kapanışı, stok-reçete, veritabanı geçişi, cihaz/ÖKC entegrasyonları ve pilot işletme kabul testleri tamamlanmalıdır. Sadakat, QR menü, kampanyalar ve çoklu şube sonraki ürün aşamalarındadır. Ayrıntılı sıra: `docs/ROADMAP.md`.

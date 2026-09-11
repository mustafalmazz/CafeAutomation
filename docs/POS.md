# POS hazırlığı · 0.1.2

Bu sürüm yalnızca yerel test simülatörü içerir. Banka veya fiziksel terminal bağlantısı, kart verisi işleme ve gerçek tahsilat yoktur. Mevcut manuel kart kaydı kullanılabilir; simülasyonlar manuel tahsilatı engellemez ve raporlara girmez.

## Kullanım

1. Ayarlar → Cihazlar → POS ayarları ve test merkezi.
2. Test simülatörünü etkinleştirin ve ayarları kaydedin.
3. Simülatör bağlantısını test edin. Bu düğme girilen IP/COM adresine bağlanmaz.
4. Tutar ve Onay / Ret / Zaman aşımı / Bağlantı kesintisi senaryosunu seçin.
5. Testi başlatın. İşlem numarası, tutar, tarih, durum ve açıklama geçmişe kaydedilir.
6. Zaman aşımı sonucu belirsizdir; geçmişten test durumunu sorgulayın. Bu senaryoda simülatör sorgusu onay döndürür; gerçek sağlayıcı davranışı değildir.

Bağlantı kesintisi senaryosu isteğin terminale ulaşmadığını varsayar. Gerçek entegrasyonda istek gönderildikten sonra bağlantı kaybı belirsiz olarak ele alınmalı ve sağlayıcıdan sorgulanmalıdır.

## Kayıt ve tekrar deneme

İstek simülatöre gönderilmeden önce tekil kimlikle kalıcı kaydedilir. Bekleyen veya belirsiz bir test varken ikinci test reddedilir. Sonuç kalıcı yazılır; tekrar gelen sonuç tamamlanmış kaydı değiştirmez. Uygulama bekleyen işlemle kapanırsa sonraki açılışta belirsiz duruma geçirilir. Sonuç kaydedilemezse kayıt korunur; SQL bağlantısını kontrol edip uygulamayı yeniden açarak sorgulayın. Son 30 kayıt arayüzde gösterilir; önceki kayıtlar SQL Server üzerinde korunur.

## Gerçek cihaz seçildiğinde

Üreticinin resmi SDK/protokolü, test erişimi ve desteklediği taşıma türü doğrulanmalı. Canlı sağlayıcı adaptörü, terminal durum sorgusu, sağlayıcı işlem kimliğiyle mükerrer tahsilat önleme, uzlaştırma ve iptal/iade akışları ayrıca geliştirilmeli ve cihaz üzerinde denenmeli. Buradaki IPosTestTerminal yalnızca test sözleşmesidir; canlı entegrasyon hazır olduğu anlamına gelmez.

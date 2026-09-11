# Ürün fotoğrafları

Atelier 0.1.1'de çizimlerin yerine kullanılan 18 fotoğrafın kaynak kaydı. Bu görüntüler örnek menü için seçilmiş **gerçek stok fotoğraflarıdır**; belirli bir işletmenin kendi ürün çekimleri veya porsiyon/servis garantisi değildir.

Kaynak: **Pexels**. Kaynak ve lisans sayfalarının kontrol tarihi: **9 Eylül 2026**.

[Pexels lisansı](https://www.pexels.com/license/) uygulama içerisinde fotoğraf kullanımına izin verir. Fotoğrafların hak sahipliği fotoğrafçılarda kalır; uygulamanın kaynak kodunun lisansı fotoğraflara aktarılmaz. Fotoğraflar bağımsız stok fotoğraf ürünü olarak sunulmamalı, fotoğrafçıların veya görüntülerdeki markaların uygulamayı desteklediği izlenimi verilmemelidir. Geçerli koşullar için kaynak lisansı esas alınır.

| Dosya (`Assets/Products/`) | Menü ürünü | Fotoğrafçı | Kaynak |
|---|---|---|---|
| latte.jpg | Caffè Latte | Victor Freitas | [Pexels 832820](https://www.pexels.com/photo/close-up-photography-of-clear-glass-cup-with-latte-and-tablespoon-832820/) |
| flat.jpg | Flat White | Dicky Agustian | [Pexels 34187295](https://www.pexels.com/photo/flat-white-coffee-next-to-laptop-on-wooden-table-34187295/) |
| americano.jpg | Americano | Olena Bohovyk | [Pexels 12821519](https://www.pexels.com/photo/close-up-shot-of-a-black-coffee-in-black-ceramic-cup-12821519/) |
| cappuccino.jpg | Cappuccino | Enes can Acikgoz | [Pexels 16615598](https://www.pexels.com/photo/latte-art-on-the-top-of-cappuccino-16615598/) |
| mocha.jpg | Caffè Mocha | Porapak Apichodilok | [Pexels 362572](https://www.pexels.com/photo/brown-ceramic-coffee-mug-on-saucer-362572/) |
| turkish.jpg | Türk Kahvesi | Şeyda Nur Yüce | [Pexels 11376038](https://www.pexels.com/photo/coffee-in-a-traditional-turkish-coffee-cup-11376038/) |
| coldbrew.jpg | Cold Brew | shu'kai chen | [Pexels 18997241](https://www.pexels.com/photo/cold-brew-coffee-in-glass-18997241/) |
| icedlatte.jpg | Iced Latte | Su La Pyae | [Pexels 20220698](https://www.pexels.com/photo/glass-of-iced-latte-with-ice-cubes-20220698/) |
| lemon.jpg | Ev Yapımı Limonata | Denys Gromov | [Pexels 18142613](https://www.pexels.com/photo/glass-of-lemonade-18142613/) |
| berry.jpg | Berry Hibiscus | Mohamed Olwy | [Pexels 36630822](https://www.pexels.com/photo/refreshing-hibiscus-drink-with-lime-garnish-36630822/) |
| tea.jpg | Demleme Çay | Kubra YETER | [Pexels 14721995](https://www.pexels.com/photo/close-up-of-turkish-tea-in-glasses-on-a-table-in-a-cafe-14721995/) |
| herbal.jpg | Yasemin Çayı | Julia Filirovska | [Pexels 7138780](https://www.pexels.com/photo/cup-of-aromatic-jasmine-tea-served-on-table-in-garden-7138780/) |
| san.jpg | San Sebastian | Kezia Lynn | [Pexels 6168429](https://www.pexels.com/photo/baked-cheesecake-on-white-pasty-paper-6168429/) |
| brownie.jpg | Belçika Brownie | Fernando Capetillo | [Pexels 36500587](https://www.pexels.com/photo/delicious-chocolate-brownies-on-plate-36500587/) |
| cookie.jpg | Çikolatalı Cookie | Rawan Ali | [Pexels 28122562](https://www.pexels.com/photo/chocolate-chip-cookies-28122562/) |
| croissant.jpg | Tereyağlı Kruvasan | Szymon Shields | [Pexels 35974903](https://www.pexels.com/photo/delicious-freshly-baked-croissants-in-tokyo-35974903/) |
| sandwich.jpg | Mozzarella Sandviç | Andres Alaniz | [Pexels 23996600](https://www.pexels.com/photo/italian-caprese-sandwich-23996600/) |
| water.jpg | Doğal Kaynak Suyu | Maria Orlova | [Pexels 4946707](https://www.pexels.com/photo/glass-bottle-with-clear-water-4946707/) |

## Teknik kayıt

Fotoğraflar Pexels görüntü sunucusundan `https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=960` biçimindeki adreslerle alınmıştır. Oranları korunur; sunucu tarafından 960 piksel genişlikte sunulan JPEG kopyaları uygulamaya gömülür. Çalışma anında ağ isteği yapılmaz.

Arayüzde oranı bozmayan, ürünün odak noktasını esas alan kırpma uygulanır. Kırpma yalnızca gösterim sırasında yapılır; paketteki JPEG dosyaları ayrıca düzenlenmez. Görseller 800 piksel genişlikte çözülüp önbelleğe alınır. Satış ekranı, menü listesi ve ürün ayrıntısı aynı fotoğraf anahtarını kullanır.

Eski kayıtlarda fotoğraf anahtarı bulunmazsa ürün kimliği kullanılır. Kullanıcının yeni seçimi `PhotoKey` alanına kaydedilir; adisyonlar ve diğer işletme verileri sıfırlanmaz. Yeni ürünler için paket içindeki fotoğraflardan seçim yapılabilir; kullanıcı dosyası yükleme bu değişikliğin kapsamında değildir.

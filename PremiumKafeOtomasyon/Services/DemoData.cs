using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public static class DemoData
{
    public static CafeState Create()
    {
        var state = new CafeState();
        void Add(string id, string name, string category, decimal price, string description, string art, string color, bool featured = false) =>
            state.Products.Add(new Product { Id = id, PhotoKey = id, Name = name, Category = category, Price = price, Description = description, Art = art, Color = color, Featured = featured });
        Add("latte", "Caffè Latte", "Kahveler", 145, "Espresso, kadifemsi süt köpüğü", "coffee", "#EADACA", true);
        Add("flat", "Flat White", "Kahveler", 150, "Çift shot, ince dokulu süt", "coffee", "#DDDCCF", true);
        Add("americano", "Americano", "Kahveler", 110, "Dengeli ve yoğun espresso", "dark", "#D8D0C3");
        Add("cappuccino", "Cappuccino", "Kahveler", 140, "Espresso ve yoğun süt köpüğü", "coffee", "#E9D9C4");
        Add("mocha", "Caffè Mocha", "Kahveler", 165, "Bitter çikolata, espresso ve süt", "coffee", "#DCC8BF");
        Add("turkish", "Türk Kahvesi", "Kahveler", 95, "Geleneksel, taze öğütülmüş", "dark", "#DFD9CE");
        Add("coldbrew", "Cold Brew", "Soğuk İçecekler", 155, "Uzun demleme, yumuşak içim", "cold", "#E1C9AD", true);
        Add("icedlatte", "Iced Latte", "Soğuk İçecekler", 155, "Espresso, soğuk süt ve buz", "cold", "#DFDCD1");
        Add("lemon", "Ev Yapımı Limonata", "Soğuk İçecekler", 125, "Taze limon ve nane", "lemon", "#E6E6BE");
        Add("berry", "Berry Hibiscus", "Soğuk İçecekler", 140, "Orman meyveleri, hibiskus", "berry", "#E8D3D3");
        Add("tea", "Demleme Çay", "Çaylar", 45, "Taze demlenmiş siyah çay", "dark", "#E0DCCF");
        Add("herbal", "Yasemin Çayı", "Çaylar", 95, "Çiçeksi ve hafif yeşil çay", "dark", "#DDE3D0");
        Add("san", "San Sebastian", "Tatlılar", 195, "Kremamsı doku, karamelize yüzey", "cake", "#EDDFCC", true);
        Add("brownie", "Belçika Brownie", "Tatlılar", 165, "Yoğun bitter çikolata", "brownie", "#E1CFC3");
        Add("cookie", "Çikolatalı Cookie", "Tatlılar", 90, "Bol çikolatalı, fırından taze", "cookie", "#E9DBC4");
        Add("croissant", "Tereyağlı Kruvasan", "Atıştırmalıklar", 115, "Kat kat, günlük fırınlanmış", "pastry", "#E8D5BA");
        Add("sandwich", "Mozzarella Sandviç", "Atıştırmalıklar", 210, "Mozzarella, domates ve pesto", "pastry", "#DDE1CF");
        Add("water", "Doğal Kaynak Suyu", "Soğuk İçecekler", 35, "330 ml cam şişe", "cold", "#D9E4E1");
        for (var i = 1; i <= 12; i++) state.Tables.Add(new CafeTable { Id = "t" + i, Name = "Masa " + i.ToString("00"), Area = i <= 6 ? "Salon" : i <= 10 ? "Bahçe" : "Teras", Seats = i % 3 == 0 ? 4 : 2 });
        state.Tables.Add(new CafeTable { Id = "takeaway", Name = "Gel-al", Area = "Gel-al", Seats = 0 });
        void SeedOrder(int n, string table, int minutes, string status, params string[] products)
        {
            var order = new Order { Number = n, TableId = table, OpenedAt = DateTimeOffset.Now.AddMinutes(-minutes) };
            foreach (var id in products)
            {
                var p = state.Products.Single(p => p.Id == id);
                order.Lines.Add(new OrderLine { ProductId = id, Name = p.Name, UnitPrice = p.Price, Status = status, Station = p.Category is "Tatlılar" or "Atıştırmalıklar" ? "Mutfak" : "Bar" });
            }
            state.Orders.Add(order);
        }
        SeedOrder(1001, "t2", 12, "Hazırlanıyor", "latte", "flat", "san");
        SeedOrder(1002, "t7", 6, "Yeni", "coldbrew", "cookie");
        SeedOrder(1003, "t4", 25, "Hazır", "americano", "croissant");
        state.Audit.Add(new AuditEntry { Message = "Örnek işletme oluşturuldu. Tüm başlangıç verileri demodur." });
        return state;
    }
}

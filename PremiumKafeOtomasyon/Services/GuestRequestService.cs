using PremiumKafeOtomasyon.Domain;
namespace PremiumKafeOtomasyon.Services;
public sealed partial class CafeService
{
    public string SubmitGuestRequest(string token, string id, string? productId, int quantity, string note, string size = "Standart", string milk = "Normal süt", bool extraShot = false)
    {
        if(size is not "Standart" and not "Büyük" || milk is not "Normal süt" and not "Yulaf sütü") throw new InvalidOperationException("Ürün seçeneği geçersiz.");
        var table = State.Tables.SingleOrDefault(t => t.MenuToken == token) ?? throw new InvalidOperationException("Masa bağlantısı geçersiz.");
        if (!Guid.TryParseExact(id, "N", out _) || quantity < 1 || quantity > 20 || note is null || note.Length > 200) throw new InvalidOperationException("İstek bilgilerini kontrol edin.");
        if (State.GuestRequests.Any(r => r.Id == id && r.TableId == table.Id)) return "İsteğiniz zaten alındı. Personel kontrol edecek.";
        if (State.GuestRequests.Any(r => r.Id == id)) throw new InvalidOperationException("İstek numarası geçersiz.");
        if (State.GuestRequests.Count(r => r.TableId == table.Id && (r.Status == "Bekliyor" || r.At > DateTimeOffset.Now.AddMinutes(-1))) >= 5) throw new InvalidOperationException("Bekleyen istekleriniz var. Personeli bekleyin.");
        if (productId != null && !State.Products.Any(p => p.Id == productId && p.Available)) throw new InvalidOperationException("Ürün satışa kapalı.");
        Commit("Masadan istek: " + table.Name, s => s.GuestRequests.Add(new GuestRequest { Id = id, TableId = table.Id, ProductId = productId, Quantity = quantity, Note = note, Size = size, Milk = milk, ExtraShot = extraShot }));
        return "İsteğiniz alındı. Personel onayını bekleyin.";
    }
    public void HandleGuestRequest(string id, bool approve)
    {
        Commit(approve ? "Masa isteği onaylandı" : "Masa isteği reddedildi", s =>
        {
            var request = s.GuestRequests.Single(r => r.Id == id);
            if (request.Status != "Bekliyor") throw new InvalidOperationException("İstek zaten işlendi.");
            if (approve && request.ProductId != null)
            {
                var product = s.Products.Single(p => p.Id == request.ProductId && p.Available);
                var order = s.Orders.SingleOrDefault(o => o.TableId == request.TableId && o.IsOpen);
                if (order != null) EnsureEditable(order);
                else { order = new Order { TableId = request.TableId, Number = s.Orders.Select(o=>o.Number).DefaultIfEmpty(1000).Max()+1 }; s.Orders.Add(order); }
                var coffee=product.Category=="Kahveler"; var surcharge=coffee ? (request.Size=="Büyük"?25m:0)+(request.Milk=="Yulaf sütü"?30m:0)+(request.ExtraShot?25m:0):0;
                var options=coffee?request.Size+" · "+request.Milk+(request.ExtraShot?" · Ekstra shot":""):"";
                order.Lines.Add(new OrderLine { ProductId = product.Id, Name = product.Name, Options = options, UnitPrice = product.Price+surcharge, Quantity = request.Quantity, Note = request.Note, Station = product.Category is "Tatlılar" or "Atıştırmalıklar" ? "Mutfak" : "Bar" });
            }
            request.Status = approve ? "Onaylandı" : "Reddedildi";
        });
    }
}

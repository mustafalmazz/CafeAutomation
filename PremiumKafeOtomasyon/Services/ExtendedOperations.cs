using PremiumKafeOtomasyon.Domain;
namespace PremiumKafeOtomasyon.Services;
public sealed partial class CafeService
{
    private bool _suppressNotifications;
    private void RaiseChanged() { if (!_suppressNotifications && !_terminalBatch) Changed?.Invoke(); }
    public void UpdateIngredient(string id,decimal minimum,decimal cost)
    {
        if(!CanManage) throw new InvalidOperationException("Yönetici girişi gerekli.");
        if(minimum<0 || cost<0) throw new InvalidOperationException("Miktar ve maliyet negatif olamaz.");
        Commit("Stok maliyeti/eşiği güncellendi",s=>{var item=s.Ingredients.Single(i=>i.Id==id);item.Minimum=minimum;item.UnitCost=cost;});
    }
    public void UpdateEmployee(string id,string role,bool active,string pin)
    {
        if(role is not "Garson" and not "Kasiyer" and not "Yönetici") throw new InvalidOperationException("Geçersiz rol.");
        Commit("Personel yetkisi güncellendi",s=>
        {
            var employee=s.Employees.Single(e=>e.Id==id);
            if(employee.Role=="Yönetici" && (!active || role!="Yönetici") && s.Employees.Count(e=>e.Active && e.Role=="Yönetici")==1) throw new InvalidOperationException("Son yönetici devre dışı bırakılamaz.");
            employee.Role=role;employee.Active=active;
            if(!string.IsNullOrEmpty(pin)) {SetPin(employee,pin);employee.FailedAttempts=0;employee.LockedUntil=null;}
        });
    }
    public void RedeemLoyalty(string orderId, int points, bool stamps)
    {
        Commit(stamps ? "10 damga ödülü kullanıldı" : "Sadakat puanı kullanıldı", s =>
        {
            var o = s.Orders.Single(o=>o.Id==orderId); EnsureEditable(o);
            if (o.Discount > 0 || o.CustomerId is null || o.Lines.Any(l=>l.Status=="Taslak")) throw new InvalidOperationException("Müşteri bağlı, hazırlığa gönderilmiş ve indirimsiz hesap gerekli.");
            var c = s.Customers.Single(c=>c.Id==o.CustomerId);
            if (stamps)
            {
                if(c.Stamps < 10) throw new InvalidOperationException("En az 10 damga gerekli.");
                var line = o.Lines.Where(l=>l.Total>0).MinBy(l=>l.UnitPrice) ?? throw new InvalidOperationException("Ödüle uygun ürün yok.");
                o.Discount = line.UnitPrice; c.SpentStamps += 10;
            }
            else { if(points < 1 || points > c.Points || points > o.Total) throw new InvalidOperationException("Puan veya hesap bakiyesi yetersiz."); o.Discount=points; c.SpentPoints+=points; }
            o.DiscountReason=stamps?"10 damga · en düşük fiyatlı 1 ürün":"Sadakat puanı"; o.LoyaltyRedeemed=true;
            if(o.Remaining==0) o.ClosedAt=DateTimeOffset.Now;
            RecalculateLoyalty(s,c.Id);
        });
    }
    public void AdvanceLine(string lineId)
    {
        Commit("Ürün hazırlık durumu güncellendi", s => { var line=s.Orders.SelectMany(o=>o.Lines).Single(l=>l.Id==lineId); line.Status=line.Status switch { "Yeni"=>"Hazırlanıyor", "Hazırlanıyor"=>"Hazır", "Hazır"=>"Teslim edildi", _=>throw new InvalidOperationException("Bu ürün ilerletilemez.") }; });
    }
    public string AuthenticateTerminal(string employeeId,string pin)
    {
        var previous=_employeeId;
        var suppressed=_suppressNotifications; _suppressNotifications=true;
        try { Login(employeeId,pin); return CurrentEmployee!.Id; }
        finally { _employeeId=previous; _suppressNotifications=suppressed; RaiseChanged(); }
    }
    public T AsEmployee<T>(string id, Func<T> action)
    {
        var previous=_employeeId; _employeeId=id;
        var suppressed=_suppressNotifications; _suppressNotifications=true;
        try { if(CurrentEmployee is null) throw new InvalidOperationException("Oturum geçersiz."); return action(); }
        finally { _employeeId=previous; _suppressNotifications=suppressed; RaiseChanged(); }
    }
    public void TerminalAction(string id,string operation,string tableId,string? productId,decimal amount,string? orderId)
    {
        if(!Guid.TryParseExact(id,"N",out _)) throw new InvalidOperationException("İşlem numarası geçersiz.");
        if(State.TerminalOperations.Contains(id)) return;
        // A single persisted snapshot includes both the action and its replay key.
        var storeBefore=State;
        _terminalBatch=true;
        try
        {
            switch(operation)
            {
                case "add": AddProduct(tableId,productId??"","Standart","Normal süt",false,""); break;
                case "send": SendToKitchen(orderId??""); break;
                case "cash": Pay(orderId??"",amount,"Nakit"); break;
                case "card": Pay(orderId??"",amount,"Kart"); break;
                default: throw new InvalidOperationException("Terminal işlemi geçersiz.");
            }
            State.TerminalOperations.Add(id); _store.Save(State);
        }
        catch { State=storeBefore; throw; }
        finally { _terminalBatch=false; RaiseChanged(); }
    }
    private bool _terminalBatch;
}

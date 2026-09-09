using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed class CafeService
{
    private readonly StateStore _store;
    public CafeState State { get; private set; }
    public string DataPath => _store.FilePath;
    public event Action? Changed;

    public CafeService(StateStore store) { _store = store; State = store.Load(); }

    private void Commit(string message, Action<CafeState> change)
    {
        var next = StateStore.Copy(State);
        change(next);
        next.Audit.Add(new AuditEntry { Message = message });
        _store.Save(next);
        State = next;
        Changed?.Invoke();
    }

    public Order? OpenOrder(string tableId) => State.Orders.FirstOrDefault(o => o.TableId == tableId && o.IsOpen);

    public void AddProduct(string tableId, string productId, string size, string milk, bool extraShot, string note)
    {
        Commit("Ürün adisyona eklendi", state =>
        {
            if (!state.Tables.Any(t => t.Id == tableId)) throw new InvalidOperationException("Önce bir masa seçin.");
            var product = state.Products.Single(p => p.Id == productId);
            if (!product.Available) throw new InvalidOperationException("Bu ürün şu anda satışa kapalı.");
            var order = state.Orders.FirstOrDefault(o => o.TableId == tableId && o.IsOpen);
            if (order is null)
            {
                order = new Order { TableId = tableId, Number = state.Orders.Select(o => o.Number).DefaultIfEmpty(1000).Max() + 1 };
                state.Orders.Add(order);
            }
            EnsureEditable(order);
            var coffee = product.Category == "Kahveler";
            var surcharge = coffee ? (size == "Büyük" ? 25m : 0) + (milk == "Yulaf sütü" ? 30m : 0) + (extraShot ? 25m : 0) : 0;
            var options = coffee ? string.Join(" · ", new[] { size, milk, extraShot ? "Ekstra shot" : "" }.Where(s => s.Length > 0)) : "";
            var line = order.Lines.FirstOrDefault(l => l.ProductId == productId && l.Options == options && l.Note == note && l.Status == "Taslak" && l.UnitPrice == product.Price + surcharge);
            if (line is not null) line.Quantity++;
            else order.Lines.Add(new OrderLine { ProductId = product.Id, Name = product.Name, UnitPrice = product.Price + surcharge, Options = options, Note = note.Trim(), Station = product.Category is "Tatlılar" or "Atıştırmalıklar" ? "Mutfak" : "Bar" });
        });
    }

    private static void EnsureEditable(Order order)
    {
        if (!order.IsOpen || order.Paid > 0) throw new InvalidOperationException("Tahsilat başlayan hesapta ürün değiştirilemez. Kalan tutarı tahsil edin.");
    }

    public void ChangeQuantity(string orderId, string lineId, int delta)
    {
        Commit("Adisyon miktarı güncellendi", state =>
        {
            var order = state.Orders.Single(o => o.Id == orderId);
            EnsureEditable(order);
            var line = order.Lines.Single(l => l.Id == lineId);
            if (line.Status != "Taslak") throw new InvalidOperationException("Hazırlığa gönderilen ürünün miktarı değiştirilemez.");
            line.Quantity += delta;
            if (line.Quantity <= 0) order.Lines.Remove(line);
            if (order.Lines.Count == 0) state.Orders.Remove(order);
        });
    }

    public void SendToKitchen(string orderId)
    {
        Commit("Yeni ürünler hazırlığa gönderildi", state =>
        {
            var order = state.Orders.Single(o => o.Id == orderId && o.IsOpen);
            foreach (var line in order.Lines.Where(l => l.Status == "Taslak")) line.Status = "Yeni";
        });
    }

    public void AdvanceKitchen(string orderId)
    {
        Commit("Hazırlık durumu güncellendi", state =>
        {
            var order = state.Orders.Single(o => o.Id == orderId);
            var pending = order.Lines.Where(l => l.Status is not "Taslak" and not "Teslim edildi").ToList();
            var source = pending.Any(l => l.Status == "Yeni") ? "Yeni" : pending.Any(l => l.Status == "Hazırlanıyor") ? "Hazırlanıyor" : "Hazır";
            var target = source == "Yeni" ? "Hazırlanıyor" : source == "Hazırlanıyor" ? "Hazır" : "Teslim edildi";
            foreach (var line in pending.Where(l => l.Status == source)) line.Status = target;
        });
    }

    public void Pay(string orderId, decimal amount, string method)
    {
        Commit($"{method} tahsilat: {Money.Format(amount)}", state =>
        {
            var order = state.Orders.Single(o => o.Id == orderId);
            if (!order.IsOpen || amount <= 0 || amount != Money.Round(amount) || amount > order.Remaining)
                throw new InvalidOperationException("Tahsilat tutarı sıfırdan büyük ve kalan hesaptan küçük veya eşit olmalı.");
            if (method is not "Nakit" and not "Kart" and not "Diğer") throw new InvalidOperationException("Geçersiz ödeme yöntemi.");
            if (order.Lines.Any(l => l.Status == "Taslak")) throw new InvalidOperationException("Önce ürünleri hazırlığa gönderin.");
            order.Payments.Add(new Payment { Amount = amount, Method = method });
            if (order.Remaining == 0) order.ClosedAt = DateTimeOffset.Now;
        });
    }

    public void MoveOrder(string orderId, string targetTableId)
    {
        Commit("Adisyon başka masaya taşındı", state =>
        {
            var order = state.Orders.Single(o => o.Id == orderId && o.IsOpen);
            if (!state.Tables.Any(t => t.Id == targetTableId) || state.Orders.Any(o => o.IsOpen && o.TableId == targetTableId))
                throw new InvalidOperationException("Hedef masa boş olmalı.");
            order.TableId = targetTableId;
        });
    }

    public void SaveProduct(Product product)
    {
        Commit("Menü ürünü kaydedildi", state =>
        {
            if (string.IsNullOrWhiteSpace(product.Name) || product.Price <= 0 || product.Price != Money.Round(product.Price))
                throw new InvalidOperationException("Ürün adı ve geçerli bir fiyat girin.");
            var index = state.Products.FindIndex(p => p.Id == product.Id);
            if (index < 0) state.Products.Add(product); else state.Products[index] = product;
        });
    }

    public void RenameBusiness(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("İşletme adı boş olamaz.");
        Commit("İşletme adı güncellendi", state => state.BusinessName = name.Trim());
    }
}

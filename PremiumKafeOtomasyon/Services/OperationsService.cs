using System.Security.Cryptography;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed partial class CafeService
{
    private string? _employeeId;
    public Employee? CurrentEmployee => State.Employees.FirstOrDefault(e => e.Id == _employeeId && e.Active);
    public bool CanManage => State.Employees.Count == 0 || CurrentEmployee?.Role == "Yönetici";
    public void Logout() { _employeeId = null; RaiseChanged(); }
    private void Authorize(string operation)
    {
        if (operation is ".ctor" or nameof(Login) or nameof(CreateFirstManager) or nameof(SubmitGuestRequest)) return;
        if (State.Employees.Count == 0) return;
        var employee = CurrentEmployee ?? throw new InvalidOperationException("Personel girişi gerekli.");
        var managerOnly = new[] { nameof(SaveReceiptSettings), nameof(SaveEmployee), nameof(UpdateEmployee), nameof(SaveProduct), nameof(RenameBusiness), nameof(SavePosSettings), nameof(AdjustOrder), nameof(Refund), nameof(SaveIngredient), nameof(AdjustStock), nameof(SaveRecipe), nameof(SaveCoupon), nameof(RestoreBackup) };
        if (managerOnly.Contains(operation) && employee.Role != "Yönetici") throw new InvalidOperationException("Bu işlem için yönetici girişi gerekli.");
        if (new[] { nameof(Pay), nameof(OpenShift), nameof(CloseShift), nameof(CashEntry), nameof(BeginPosTest) }.Contains(operation) && employee.Role == "Garson") throw new InvalidOperationException("Bu işlem için kasiyer veya yönetici girişi gerekli.");
    }
    private static void ValidMoney(decimal amount, bool zero = false)
    { if (amount < 0 || (!zero && amount == 0) || amount != Money.Round(amount) || amount > 99999999) throw new InvalidOperationException("Geçerli bir tutar girin."); }
    private static string Required(string text)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > 200) throw new InvalidOperationException("Açıklama/ad gerekli (en çok 200 karakter)."); return text.Trim(); }
    private static void SetPin(Employee employee, string pin)
    {
        if (pin.Length is < 6 or > 12 || !pin.All(char.IsAsciiDigit)) throw new InvalidOperationException("PIN 6–12 rakam olmalı.");
        var salt = RandomNumberGenerator.GetBytes(16); employee.Salt = Convert.ToBase64String(salt);
        employee.PinHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(pin, salt, 100000, HashAlgorithmName.SHA256, 32));
    }
    public void CreateFirstManager(string name, string pin)
    {
        if (State.Employees.Count > 0) throw new InvalidOperationException("Yönetici zaten oluşturuldu.");
        var employee = new Employee { Name = Required(name), Role = "Yönetici" }; SetPin(employee, pin);
        Commit("İlk yönetici oluşturuldu", s => s.Employees.Add(employee)); _employeeId = employee.Id; RaiseChanged();
    }
    public void SaveEmployee(string name, string role, string pin)
    {
        if (role is not "Yönetici" and not "Kasiyer" and not "Garson") throw new InvalidOperationException("Geçersiz rol.");
        var employee = new Employee { Name = Required(name), Role = role }; SetPin(employee, pin);
        Commit("Personel eklendi: " + employee.Name, s => { if (s.Employees.Any(e => e.Name.Equals(employee.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Personel adı zaten var."); s.Employees.Add(employee); });
    }
    public void Login(string employeeId, string pin)
    {
        var employee = State.Employees.Single(e => e.Id == employeeId && e.Active);
        if (employee.LockedUntil > DateTimeOffset.Now) throw new InvalidOperationException("Çok fazla deneme. Beş dakika sonra tekrar deneyin.");
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, Convert.FromBase64String(employee.Salt), 100000, HashAlgorithmName.SHA256, 32);
        var success = CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(employee.PinHash));
        Commit(success ? "Personel girişi: " + employee.Name : "Başarısız giriş: " + employee.Name, s =>
        {
            var e = s.Employees.Single(e => e.Id == employeeId);
            e.FailedAttempts = success ? 0 : e.FailedAttempts + 1;
            e.LockedUntil = !success && e.FailedAttempts >= 5 ? DateTimeOffset.Now.AddMinutes(5) : null;
        });
        if (!success) throw new InvalidOperationException("PIN hatalı.");
        _employeeId = employeeId; RaiseChanged();
    }
    public CashShift? ActiveShift => State.Shifts.SingleOrDefault(s => s.ClosedAt is null);
    public decimal ExpectedCash(CashShift shift)
    {
        var end = shift.ClosedAt ?? DateTimeOffset.Now;
        return shift.Opening + shift.Movements.Sum(m => m.Amount)
            + State.Orders.SelectMany(o => o.Payments).Where(p => p.Method == "Nakit" && p.At >= shift.OpenedAt && p.At <= end).Sum(p => p.Amount)
            - State.Refunds.Where(r => r.Method == "Nakit" && r.At >= shift.OpenedAt && r.At <= end).Sum(r => r.Amount);
    }
    public void OpenShift(decimal opening) { ValidMoney(opening, true); Commit("Kasa vardiyası açıldı", s => { if (s.Shifts.Any(x => x.ClosedAt is null)) throw new InvalidOperationException("Açık vardiya var."); s.Shifts.Add(new CashShift { Employee = CurrentEmployee?.Name ?? "Demo", Opening = opening }); }); }
    public void CashEntry(decimal amount, string reason)
    { ValidMoney(Math.Abs(amount)); reason = Required(reason); Commit("Kasa hareketi: " + reason, s => { var shift = s.Shifts.SingleOrDefault(x => x.ClosedAt is null) ?? throw new InvalidOperationException("Vardiya açın."); if (amount < 0 && ExpectedCash(ActiveShift!) + amount < 0) throw new InvalidOperationException("Kasa bakiyesi yetersiz."); shift.Movements.Add(new CashMovement { Amount = amount, Reason = reason }); }); }
    public void CloseShift(decimal counted)
    { ValidMoney(counted, true); var shift = ActiveShift ?? throw new InvalidOperationException("Açık vardiya yok."); var expected = ExpectedCash(shift); Commit("Kasa kapandı · Fark: " + Money.Format(counted - expected), s => { var x = s.Shifts.Single(x => x.Id == shift.Id); x.ClosedAt = DateTimeOffset.Now; x.Counted = counted; x.Expected = expected; }); }
    public void AdjustOrder(string orderId, string? lineId, string kind, decimal amount, string reason)
    {
        reason = Required(reason);
        Commit(kind + ": " + reason, s =>
        {
            var order = s.Orders.Single(o => o.Id == orderId); EnsureEditable(order);
            if (order.CouponId is not null) throw new InvalidOperationException("Kuponlu hesap değiştirilemez.");
            if (kind == "İndirim") { ValidMoney(amount, true); if (amount > order.Lines.Sum(l => l.Total)) throw new InvalidOperationException("İndirim toplamı aşamaz."); order.Discount = amount; order.DiscountReason = reason; }
            else
            {
                var line = order.Lines.Single(l => l.Id == lineId);
                if (kind == "İkram") line.Complimentary = true;
                else if (kind == "İptal") line.Status = "İptal";
                else throw new InvalidOperationException("Geçersiz işlem.");
                line.AdjustmentReason = reason; order.Discount = Math.Min(order.Discount, order.Lines.Sum(l => l.Total));
            }
            if (order.Total == 0) { if (order.Lines.Any(l => l.Status == "Taslak")) throw new InvalidOperationException("Sıfır hesap için önce ürünleri hazırlığa gönderin."); order.ClosedAt = DateTimeOffset.Now; }
        });
    }
    public void TransferLines(string sourceId, string targetTableId, string? lineId, int quantity)
    {
        Commit(lineId is null ? "Adisyonlar birleştirildi" : "Ürün ayrı hesaba taşındı", s =>
        {
            var source = s.Orders.Single(o => o.Id == sourceId); EnsureEditable(source);
            if (targetTableId == source.TableId || !s.Tables.Any(t => t.Id == targetTableId)) throw new InvalidOperationException("Farklı bir masa seçin.");
            var target = s.Orders.SingleOrDefault(o => o.TableId == targetTableId && o.IsOpen);
            if (target is not null) EnsureEditable(target);
            if (source.Discount != 0 || source.CouponId != null || target?.Discount > 0 || target?.CouponId != null) throw new InvalidOperationException("İndirimli/kuponlu hesaplar taşınamaz.");
            target ??= new Order { Number = s.Orders.Max(o => o.Number) + 1, TableId = targetTableId };
            if (!s.Orders.Contains(target)) s.Orders.Add(target);
            if (lineId is null) { target.Lines.AddRange(source.Lines); source.Lines.Clear(); }
            else
            {
                var line = source.Lines.Single(l => l.Id == lineId);
                if (quantity < 1 || quantity > line.Quantity || line.Status == "İptal") throw new InvalidOperationException("Geçersiz ürün miktarı.");
                var cost = line.Cost * quantity / line.Quantity;
                target.Lines.Add(new OrderLine { ProductId = line.ProductId, Name = line.Name, Options = line.Options, Note = line.Note, UnitPrice = line.UnitPrice, Station = line.Station, Status = line.Status, SentAt = line.SentAt, Quantity = quantity, Cost = cost, Complimentary = line.Complimentary, AdjustmentReason = line.AdjustmentReason });
                line.Quantity -= quantity; line.Cost -= cost; if (line.Quantity == 0) source.Lines.Remove(line);
            }
            if (source.Lines.Count == 0) s.Orders.Remove(source);
        });
    }
    public void Refund(string orderId, decimal amount, string method, string reason)
    {
        ValidMoney(amount); reason = Required(reason);
        Commit("İade kaydı: " + reason, s =>
        {
            var order = s.Orders.Single(o => o.Id == orderId && !o.IsOpen);
            if (method is not "Nakit" and not "Kart" and not "Diğer") throw new InvalidOperationException("Geçersiz yöntem.");
            if (s.Employees.Count > 0 && !s.Shifts.Any(x => x.ClosedAt is null)) throw new InvalidOperationException("İade için vardiya açın.");
            if (amount > order.Paid - s.Refunds.Where(r => r.OrderId == orderId).Sum(r => r.Amount)) throw new InvalidOperationException("İade kalan tahsilatı aşamaz.");
            s.Refunds.Add(new RefundEntry { OrderId = orderId, Amount = amount, Method = method, Reason = reason });
            RecalculateLoyalty(s, order.CustomerId);
        });
    }
    public void SaveIngredient(string name, string unit, decimal minimum, decimal unitCost)
    {
        name = Required(name); if (unit is not "g" and not "ml" and not "adet" || minimum < 0 || unitCost < 0) throw new InvalidOperationException("Birim, eşik veya maliyet geçersiz.");
        Commit("Stok kalemi eklendi", s => { if (s.Ingredients.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Bu stok kalemi var."); s.Ingredients.Add(new Ingredient { Name = name, Unit = unit, Minimum = minimum, UnitCost = unitCost }); });
    }
    public void AdjustStock(string ingredientId, decimal quantity, string kind, string reason)
    {
        if (quantity < 0 || quantity > 100000000) throw new InvalidOperationException("Geçersiz miktar."); reason = Required(reason);
        Commit("Stok " + kind + ": " + reason, s => { var i = s.Ingredients.Single(i => i.Id == ingredientId); var delta = kind switch { "Giriş" => quantity, "Fire" => -quantity, "Sayım" => quantity - i.Quantity, _ => throw new InvalidOperationException("Geçersiz hareket.") }; if (i.Quantity + delta < 0) throw new InvalidOperationException("Stok yetersiz."); i.Quantity += delta; s.StockEntries.Add(new StockEntry { IngredientId = i.Id, Delta = delta, Reason = kind + " · " + reason }); });
    }
    public void SaveRecipe(string productId, string ingredientId, decimal quantity, string variant = "")
    {
        if (quantity < 0 || quantity > 1000000) throw new InvalidOperationException("Geçersiz reçete miktarı.");
        Commit("Ürün reçetesi güncellendi", s => { if (!s.Products.Any(p => p.Id == productId) || !s.Ingredients.Any(i => i.Id == ingredientId)) throw new InvalidOperationException("Ürün/stok seçin."); s.Recipes.RemoveAll(r => r.ProductId == productId && r.IngredientId == ingredientId && r.Variant == variant); if (quantity > 0) s.Recipes.Add(new RecipePart { Variant = variant, ProductId = productId, IngredientId = ingredientId, Quantity = quantity }); });
    }
    private static void ConsumeRecipe(CafeState s, OrderLine line)
    {
        var variant = s.Recipes.Any(r => r.ProductId == line.ProductId && r.Variant == line.Options) ? line.Options : "";
        foreach (var recipe in s.Recipes.Where(r => r.ProductId == line.ProductId && r.Variant == variant))
        {
            var ingredient = s.Ingredients.Single(i => i.Id == recipe.IngredientId);
            var quantity = recipe.Quantity * line.Quantity;
            if (ingredient.Quantity < quantity) throw new InvalidOperationException(ingredient.Name + " stoku yetersiz.");
            ingredient.Quantity -= quantity; line.Cost += quantity * ingredient.UnitCost;
            s.StockEntries.Add(new StockEntry { IngredientId = ingredient.Id, Delta = -quantity, Reason = "Hazırlık · " + line.Name });
        }
    }
    public void SaveCustomer(string name, string contact) { name = Required(name); if (contact.Length > 100) throw new InvalidOperationException("İletişim çok uzun."); Commit("Müşteri eklendi", s => s.Customers.Add(new Customer { Name = name, Contact = contact.Trim() })); }
    public void AssignCustomer(string orderId, string customerId)
    { Commit("Adisyona müşteri bağlandı", s => { var o = s.Orders.Single(o => o.Id == orderId); EnsureEditable(o); if (!s.Customers.Any(c => c.Id == customerId)) throw new InvalidOperationException("Müşteri seçin."); o.CustomerId = customerId; }); }
    private static void AwardLoyalty(CafeState s, Order order) => RecalculateLoyalty(s, order.CustomerId);
    private static void RecalculateLoyalty(CafeState s, string? customerId)
    {
        var c = s.Customers.SingleOrDefault(c => c.Id == customerId); if (c is null) return;
        var orders = s.Orders.Where(o => !o.IsOpen && o.CustomerId == customerId).ToList();
        c.Points = orders.Sum(o => (int)Math.Floor((o.Total - s.Refunds.Where(r => r.OrderId == o.Id).Sum(r => r.Amount)) / 10)) - c.SpentPoints;
        c.Stamps = orders.Count(o => o.Total - s.Refunds.Where(r => r.OrderId == o.Id).Sum(r => r.Amount) > 0) - c.SpentStamps;
    }
    public void SaveCoupon(string code, decimal amount, decimal minimum, DateTime expires, int uses)
    { code = Required(code).ToUpperInvariant(); ValidMoney(amount); ValidMoney(minimum, true); if (expires.Date < DateTime.Today || uses < 1) throw new InvalidOperationException("Tarih/kullanım limiti geçersiz."); Commit("Kupon oluşturuldu", s => { if (s.Coupons.Any(c => c.Code == code)) throw new InvalidOperationException("Kupon kodu mevcut."); s.Coupons.Add(new Coupon { Code = code, Amount = amount, MinimumSpend = minimum, MaximumUses = uses, ExpiresAt = new DateTimeOffset(expires.Date.AddDays(1).AddTicks(-1)) }); }); }
    public void ApplyCoupon(string orderId, string code)
    { Commit("Kupon uygulandı", s => { var o = s.Orders.Single(o => o.Id == orderId); EnsureEditable(o); var c = s.Coupons.SingleOrDefault(c => c.Code == code.Trim().ToUpperInvariant()) ?? throw new InvalidOperationException("Kupon bulunamadı."); if (o.CouponId != null || o.Discount > 0 || c.ExpiresAt < DateTimeOffset.Now || c.Uses >= c.MaximumUses || o.Total < c.MinimumSpend) throw new InvalidOperationException("Kupon koşulları sağlanmıyor."); o.Discount = Math.Min(c.Amount, o.Total); o.DiscountReason = "Kupon: " + c.Code; o.CouponId = c.Id; c.Uses++; if (o.Remaining == 0) { if (o.Lines.Any(l => l.Status == "Taslak")) throw new InvalidOperationException("Önce ürünleri hazırlığa gönderin."); o.ClosedAt = DateTimeOffset.Now; AwardLoyalty(s,o); } }); }
    public void RestoreBackup(string path)
    {
        Authorize(nameof(RestoreBackup));
        if (ActiveShift is not null) throw new InvalidOperationException("Önce vardiyayı kapatın.");
        if (!System.IO.File.Exists(path)) throw new InvalidOperationException("Yedek bulunamadı.");
        var restored = new StateStore(path).Load();
        Commit("Yedekten geri yükleme", s => { s.Products = restored.Products; s.Tables = restored.Tables; s.Orders = restored.Orders; s.Ingredients = restored.Ingredients; s.Recipes = restored.Recipes; s.StockEntries = restored.StockEntries; s.Customers = restored.Customers; s.Coupons = restored.Coupons; s.Refunds = restored.Refunds; s.Shifts = restored.Shifts; s.GuestRequests = restored.GuestRequests; s.BusinessName = restored.BusinessName; });
    }
}

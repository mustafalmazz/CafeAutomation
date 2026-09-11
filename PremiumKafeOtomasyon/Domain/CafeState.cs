using System.Globalization;

namespace PremiumKafeOtomasyon.Domain;

public static class Money
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static string Format(decimal amount) => amount.ToString("N2", Turkish) + " ₺";
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public sealed class CafeState
{
    public int Version { get; set; } = 1;
    public string BusinessName { get; set; } = "Atelier Coffee";
    public bool IsDemo { get; set; } = true;
    public List<Product> Products { get; set; } = [];
    public List<CafeTable> Tables { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<AuditEntry> Audit { get; set; } = [];
    public PosSettings Pos { get; set; } = new();
    public List<PosTransaction> PosTransactions { get; set; } = [];
    public List<Employee> Employees { get; set; } = [];
    public List<CashShift> Shifts { get; set; } = [];
    public List<Ingredient> Ingredients { get; set; } = [];
    public List<RecipePart> Recipes { get; set; } = [];
    public List<StockEntry> StockEntries { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public List<Coupon> Coupons { get; set; } = [];
    public List<RefundEntry> Refunds { get; set; } = [];
    public List<GuestRequest> GuestRequests { get; set; } = [];
    public List<string> TerminalOperations { get; set; } = [];
    public string ReceiptPrinter { get; set; } = "";
    public string ReceiptPaper { get; set; } = "80 mm";
}

public sealed class PosSettings
{
    public bool TestEnabled { get; set; }
    public string DeviceName { get; set; } = "Atelier test terminali";
    public string ConnectionType { get; set; } = "Simülatör";
    public string Endpoint { get; set; } = "";
}

public enum PosStatus { Pending, Approved, Declined, Unknown, Disconnected }
public enum PosScenario { Approve, Decline, Timeout, Disconnect }
public sealed class PosTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public decimal Amount { get; set; }
    public PosScenario Scenario { get; set; }
    public PosStatus Status { get; set; } = PosStatus.Pending;
    public string Detail { get; set; } = "Test terminalinden yanıt bekleniyor.";
    public string Reference => "TEST-" + Id;
    public string Summary => $"{At.LocalDateTime:dd.MM HH:mm} · {Money.Format(Amount)} · {StatusLabel}";
    public string StatusLabel => Status switch { PosStatus.Pending => "Bekliyor", PosStatus.Approved => "Test onaylandı", PosStatus.Declined => "Reddedildi", PosStatus.Unknown => "Sonuç belirsiz", _ => "Bağlantı kesildi" };
}

public sealed class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Kahveler";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string Art { get; set; } = "coffee";
    public string PhotoKey { get; set; } = "";
    public string Color { get; set; } = "#EDE0D1";
    public bool Available { get; set; } = true;
    public bool Featured { get; set; }
    public string Allergens { get; set; } = "";
}

public sealed class CafeTable
{
    public string MenuToken { get; set; } = Guid.NewGuid().ToString("N");
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "Salon";
    public int Seats { get; set; } = 2;
}

public sealed class OrderLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Options { get; set; } = "";
    public string Note { get; set; } = "";
    public string Station { get; set; } = "Bar";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string Status { get; set; } = "Taslak";
    public DateTimeOffset? SentAt { get; set; }
    public bool Complimentary { get; set; }
    public string AdjustmentReason { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal Total => Status == "İptal" || Complimentary ? 0 : Money.Round(UnitPrice * Quantity);
}

public sealed class Payment
{
    public string Payer { get; set; } = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Method { get; set; } = "Nakit";
    public decimal Amount { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
}

public sealed class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Number { get; set; }
    public string TableId { get; set; } = "";
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ClosedAt { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public decimal Discount { get; set; }
    public string DiscountReason { get; set; } = "";
    public string? CustomerId { get; set; }
    public string? CouponId { get; set; }
    public bool LoyaltyRedeemed { get; set; }
    public decimal Total => Math.Max(0, Lines.Sum(x => x.Total) - Discount);
    public decimal Paid => Payments.Sum(x => x.Amount);
    public decimal Remaining => Money.Round(Total - Paid);
    public bool IsOpen => ClosedAt is null;
}

public sealed class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public string Message { get; set; } = "";
}

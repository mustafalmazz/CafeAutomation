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
}

public sealed class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Kahveler";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string Art { get; set; } = "coffee";
    public string Color { get; set; } = "#EDE0D1";
    public bool Available { get; set; } = true;
    public bool Featured { get; set; }
}

public sealed class CafeTable
{
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
    public decimal Total => Money.Round(UnitPrice * Quantity);
}

public sealed class Payment
{
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
    public decimal Total => Lines.Sum(x => x.Total);
    public decimal Paid => Payments.Sum(x => x.Amount);
    public decimal Remaining => Money.Round(Total - Paid);
    public bool IsOpen => ClosedAt is null;
}

public sealed class AuditEntry
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public string Message { get; set; } = "";
}

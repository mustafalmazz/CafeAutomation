namespace PremiumKafeOtomasyon.Domain;

public sealed class GuestRequest
{
    public string Id { get; set; } = "";
    public string TableId { get; set; } = "";
    public string? ProductId { get; set; }
    public int Quantity { get; set; }
    public string Note { get; set; } = "";
    public string Size { get; set; } = "Standart";
    public string Milk { get; set; } = "Normal süt";
    public bool ExtraShot { get; set; }
    public string Status { get; set; } = "Bekliyor";
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
}

public sealed class Employee
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Role { get; set; } = "Garson";
    public string Salt { get; set; } = "";
    public string PinHash { get; set; } = "";
    public bool Active { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
public sealed class CashShift
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Employee { get; set; } = "";
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal Opening { get; set; }
    public decimal? Counted { get; set; }
    public decimal? Expected { get; set; }
    public List<CashMovement> Movements { get; set; } = [];
}
public sealed class CashMovement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class Ingredient
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "g";
    public decimal Quantity { get; set; }
    public decimal Minimum { get; set; }
    public decimal UnitCost { get; set; }
}
public sealed class RecipePart
{
    public string Variant { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string IngredientId { get; set; } = "";
    public decimal Quantity { get; set; }
}
public sealed class StockEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public string IngredientId { get; set; } = "";
    public decimal Delta { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public int Points { get; set; }
    public int Stamps { get; set; }
    public int SpentPoints { get; set; }
    public int SpentStamps { get; set; }
}
public sealed class Coupon
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Code { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal MinimumSpend { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaximumUses { get; set; } = 1;
    public int Uses { get; set; }
}
public sealed class RefundEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OrderId { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Nakit";
    public string Reason { get; set; } = "";
}

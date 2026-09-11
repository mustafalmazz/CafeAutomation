using System.Text.Json;
using System.IO;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public interface IStateStore
{
    string FilePath { get; }
    CafeState Load();
    void Save(CafeState state);
}

// JSON remains an explicit import/export format and a test fixture store.
public sealed class StateStore(string path) : IStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath { get; } = path;
    public static CafeState Copy(CafeState state) => JsonSerializer.Deserialize<CafeState>(JsonSerializer.Serialize(state, Options))!;

    public CafeState Load()
    {
        if (!File.Exists(FilePath))
        {
            var seed = DemoData.Create();
            Save(seed);
            return seed;
        }
        var state = JsonSerializer.Deserialize<CafeState>(File.ReadAllText(FilePath), Options)
            ?? throw new InvalidDataException("Kayıt dosyası boş veya okunamıyor.");
        Validate(state);
        return state;
    }

    public void Save(CafeState state)
    {
        Validate(state);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temp = FilePath + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            file.Write(bytes);
            file.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }

    public static void Validate(CafeState state)
    {
        if (state.Version != 1) throw new InvalidDataException("Bu kayıt sürümü desteklenmiyor.");
        if (state.Employees.Any(e => e.Role is not "Yönetici" and not "Kasiyer" and not "Garson" || string.IsNullOrWhiteSpace(e.PinHash))
            || state.Employees.Select(e => e.Id).Distinct().Count() != state.Employees.Count
            || state.Shifts.Count(s => s.ClosedAt is null) > 1
            || state.Ingredients.Any(i => i.Quantity < 0 || i.UnitCost < 0)
            || state.Recipes.Any(r => r.Quantity <= 0 || !state.Products.Any(p => p.Id == r.ProductId) || !state.Ingredients.Any(i => i.Id == r.IngredientId))
            || state.Refunds.Any(r => r.Amount <= 0 || !state.Orders.Any(o => o.Id == r.OrderId && !o.IsOpen))
            || state.Refunds.GroupBy(r => r.OrderId).Any(g => g.Sum(r => r.Amount) > state.Orders.Single(o => o.Id == g.Key).Paid)
            || state.GuestRequests.Any(r => !state.Tables.Any(t => t.Id == r.TableId))
            || state.GuestRequests.Select(r => r.Id).Distinct().Count() != state.GuestRequests.Count)
            throw new InvalidDataException("İşletme kayıtları tutarsız.");
        if (state.Pos is null || state.PosTransactions is null || state.PosTransactions.Any(t => t.Amount <= 0 || t.Amount != Money.Round(t.Amount) || !Enum.IsDefined(t.Status) || !Enum.IsDefined(t.Scenario))
            || state.PosTransactions.Select(t => t.Id).Distinct().Count() != state.PosTransactions.Count
            || state.PosTransactions.Count(t => t.Status is PosStatus.Pending or PosStatus.Unknown) > 1)
            throw new InvalidDataException("POS test kayıtları geçersiz.");
        if (state.Products.Any(p => p.Price < 0) || state.Products.Select(p => p.Id).Distinct().Count() != state.Products.Count)
            throw new InvalidDataException("Ürün kayıtları geçersiz.");
        if (state.Tables.Select(t => t.Id).Distinct().Count() != state.Tables.Count)
            throw new InvalidDataException("Masa kayıtları geçersiz.");
        if (state.Orders.Where(o => o.IsOpen).GroupBy(o => o.TableId).Any(g => g.Count() > 1))
            throw new InvalidDataException("Bir masada birden fazla açık hesap var.");
        foreach (var order in state.Orders)
        {
            if (!state.Tables.Any(t => t.Id == order.TableId) || order.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0)
                || order.Payments.Any(p => p.Amount <= 0) || order.Paid > order.Total || order.Discount < 0 || order.Discount > order.Lines.Sum(l => l.Total)
                || (!order.IsOpen && order.Remaining != 0))
                throw new InvalidDataException("Adisyon veya ödeme kayıtları tutarsız.");
        }
    }
}

using System.Text.Json;
using System.IO;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed class StateStore(string path)
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
        if (state.Products.Any(p => p.Price < 0) || state.Products.Select(p => p.Id).Distinct().Count() != state.Products.Count)
            throw new InvalidDataException("Ürün kayıtları geçersiz.");
        if (state.Tables.Select(t => t.Id).Distinct().Count() != state.Tables.Count)
            throw new InvalidDataException("Masa kayıtları geçersiz.");
        if (state.Orders.Where(o => o.IsOpen).GroupBy(o => o.TableId).Any(g => g.Count() > 1))
            throw new InvalidDataException("Bir masada birden fazla açık hesap var.");
        foreach (var order in state.Orders)
        {
            if (!state.Tables.Any(t => t.Id == order.TableId) || order.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0)
                || order.Payments.Any(p => p.Amount <= 0) || order.Paid > order.Total
                || (!order.IsOpen && order.Remaining != 0))
                throw new InvalidDataException("Adisyon veya ödeme kayıtları tutarsız.");
        }
    }
}

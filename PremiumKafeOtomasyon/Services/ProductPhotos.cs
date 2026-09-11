using System.Windows.Media.Imaging;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed record PhotoChoice(string Key, string Name, double FocusX = 0.5, double FocusY = 0.5);

/// <summary>Bundled photographs. Legacy products resolve by stable product ID without changing saved orders.</summary>
public static class ProductPhotos
{
    public static IReadOnlyList<PhotoChoice> Choices { get; } = Array.AsReadOnly(new[]
    {
        new PhotoChoice("latte", "Caffè Latte", 0.58, 0.5),
        new PhotoChoice("flat", "Flat White", 0.62, 0.63),
        new PhotoChoice("americano", "Americano", 0.5, 0.56),
        new PhotoChoice("cappuccino", "Cappuccino", 0.5, 0.62),
        new PhotoChoice("mocha", "Caffè Mocha", 0.43, 0.5),
        new PhotoChoice("turkish", "Türk Kahvesi", 0.56, 0.57),
        new PhotoChoice("coldbrew", "Cold Brew"),
        new PhotoChoice("icedlatte", "Iced Latte", 0.5, 0.53),
        new PhotoChoice("lemon", "Limonata"),
        new PhotoChoice("berry", "Hibiskus", 0.5, 0.49),
        new PhotoChoice("tea", "Demleme Çay", 0.5, 0.59),
        new PhotoChoice("herbal", "Yasemin Çayı"),
        new PhotoChoice("san", "San Sebastian", 0.51, 0.56),
        new PhotoChoice("brownie", "Çikolatalı Brownie"),
        new PhotoChoice("cookie", "Çikolatalı Cookie", 0.5, 0.64),
        new PhotoChoice("croissant", "Tereyağlı Kruvasan"),
        new PhotoChoice("sandwich", "Mozzarella Sandviç", 0.5, 0.55),
        new PhotoChoice("water", "Cam Şişe Su", 0.70, 0.55)
    });
    private static readonly Dictionary<string, PhotoChoice> Catalog = Choices.ToDictionary(p => p.Key, StringComparer.Ordinal);
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public static PhotoChoice Find(string? key) => key is not null && Catalog.TryGetValue(key, out var choice) ? choice : Catalog["latte"];

    public static string ResolveKey(Product product)
    {
        if (!string.IsNullOrWhiteSpace(product.PhotoKey) && Catalog.ContainsKey(product.PhotoKey)) return product.PhotoKey;
        if (Catalog.ContainsKey(product.Id)) return product.Id;
        return product.Art switch { "dark" => "americano", "cold" => "icedlatte", "lemon" => "lemon", "berry" => "berry", "cake" => "san", "brownie" => "brownie", "cookie" => "cookie", "pastry" => "croissant", _ => "latte" };
    }

    public static BitmapImage Load(string? key)
    {
        var resolved = Find(key).Key;
        lock (Cache)
        {
            if (Cache.TryGetValue(resolved, out var cached)) return cached;
            var image = new BitmapImage(); image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 800;
            image.UriSource = new Uri($"pack://application:,,,/PremiumKafeOtomasyon;component/Assets/Products/{resolved}.jpg");
            image.EndInit(); image.Freeze(); Cache.Add(resolved, image); return image;
        }
    }
}

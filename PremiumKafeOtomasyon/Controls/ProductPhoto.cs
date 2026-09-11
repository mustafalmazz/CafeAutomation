using System.Windows;
using System.Windows.Media;
using PremiumKafeOtomasyon.Services;

namespace PremiumKafeOtomasyon.Controls;

/// <summary>Displays a packaged photo with an aspect-preserving crop around the product's focal point.</summary>
public sealed class ProductPhoto : FrameworkElement
{
    public static readonly DependencyProperty PhotoKeyProperty = DependencyProperty.Register(nameof(PhotoKey), typeof(string), typeof(ProductPhoto), new FrameworkPropertyMetadata("latte", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(double), typeof(ProductPhoto), new FrameworkPropertyMetadata(9d, FrameworkPropertyMetadataOptions.AffectsRender));
    public string PhotoKey { get => (string)GetValue(PhotoKeyProperty); set => SetValue(PhotoKeyProperty, value); }
    public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    public ProductPhoto() => RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var photo = ProductPhotos.Load(PhotoKey); var choice = ProductPhotos.Find(PhotoKey);
        var scale = Math.Max(ActualWidth / photo.PixelWidth, ActualHeight / photo.PixelHeight);
        var width = photo.PixelWidth * scale; var height = photo.PixelHeight * scale;
        var x = Math.Clamp(ActualWidth / 2 - choice.FocusX * width, Math.Min(ActualWidth - width, 0), 0);
        var y = Math.Clamp(ActualHeight / 2 - choice.FocusY * height, Math.Min(ActualHeight - height, 0), 0);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), Radius, Radius));
        dc.DrawImage(photo, new Rect(x, y, width, height)); dc.Pop();
    }
}

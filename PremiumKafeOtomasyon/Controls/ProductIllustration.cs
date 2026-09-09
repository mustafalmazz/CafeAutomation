using System.Windows;
using System.Windows.Media;

namespace PremiumKafeOtomasyon.Controls;

/// <summary>Resolution-independent menu artwork; no network or external image files required.</summary>
public sealed class ProductIllustration : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(ProductIllustration), new FrameworkPropertyMetadata("coffee", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    private static SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static LinearGradientBrush G(string from, string to) => new((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), new Point(0, 0), new Point(1, 1));
    private static void Path(DrawingContext dc, string data, Brush fill, Pen? pen = null) => dc.DrawGeometry(fill, pen, Geometry.Parse(data));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = Math.Min(ActualWidth / 220, ActualHeight / 145);
        dc.PushTransform(new TranslateTransform((ActualWidth - 220 * scale) / 2, (ActualHeight - 145 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawEllipse(B("#14000000"), null, new Point(115, 126), 66, 9);
        switch (Kind)
        {
            case "cold": case "lemon": case "berry": DrawCold(dc); break;
            case "cake": DrawCake(dc); break;
            case "brownie": DrawBrownie(dc); break;
            case "cookie": DrawCookie(dc); break;
            case "pastry": DrawPastry(dc); break;
            default: DrawCoffee(dc); break;
        }
        dc.Pop(); dc.Pop();
    }

    private void DrawCoffee(DrawingContext dc)
    {
        dc.DrawEllipse(G("#FDFBF7", "#CEC6BB"), null, new Point(108, 119), 65, 16);
        dc.DrawEllipse(null, new Pen(B("#C6BDB1"), 1), new Point(108, 119), 49, 10);
        dc.DrawEllipse(null, new Pen(B("#F5F1E8"), 10), new Point(164, 80), 17, 20);
        dc.DrawEllipse(null, new Pen(B("#CFC7B9"), 2), new Point(164, 80), 12, 16);
        Path(dc, "M 53,58 C 53,113 69,125 107,126 C 144,125 157,110 157,58 Z", G("#FFFDF8", "#D8CEBF"));
        dc.DrawEllipse(B("#FEFCF7"), null, new Point(105, 58), 52, 21);
        dc.DrawEllipse(G("#B17A49", "#56321F"), null, new Point(105, 58), 46, 16);
        if (Kind != "dark")
        {
            dc.DrawEllipse(B("#C78F57"), null, new Point(105, 58), 39, 13);
            Path(dc, "M 105,69 C 98,65 79,57 89,50 C 96,46 105,52 105,56 C 106,50 116,46 122,51 C 132,60 112,67 105,69 Z", B("#FFF0D3"));
            Path(dc, "M 105,68 C 103,60 102,53 99,48", Brushes.Transparent, new Pen(B("#D4A776"), 1.2));
        }
        Path(dc, "M 78,26 C 66,14 89,14 80,1 M 106,25 C 94,12 116,11 110,-1", Brushes.Transparent, new Pen(B("#65FFFFFF"), 2));
        Path(dc, "M 65,77 C 67,97 73,106 79,108", Brushes.Transparent, new Pen(B("#90FFFFFF"), 4));
    }

    private void DrawCold(DrawingContext dc)
    {
        Path(dc, "M 145,1 L 133,53", Brushes.Transparent, new Pen(B("#705647"), 5));
        Path(dc, "M 75,26 L 85,122 Q 110,132 139,122 L 150,26 Z", G("#DDFDFBF5", "#99D7E1DB"), new Pen(B("#70FFFFFF"), 2));
        var top = Kind == "lemon" ? "#E8D477" : Kind == "berry" ? "#B95470" : "#C39769";
        var bottom = Kind == "lemon" ? "#CDB742" : Kind == "berry" ? "#792C48" : "#805335";
        Path(dc, "M 80,43 L 88,119 Q 113,127 136,119 L 145,43 Z", G(top, bottom));
        dc.DrawEllipse(B(top), null, new Point(112, 43), 32, 8);
        foreach (var r in new[] { new Rect(88, 43, 20, 18), new Rect(115, 48, 19, 19), new Rect(101, 70, 22, 19) }) dc.DrawRoundedRectangle(B("#65FFFDF6"), new Pen(B("#66FFFFFF"), 1), r, 4, 4);
        Path(dc, "M 86,36 L 92,112", Brushes.Transparent, new Pen(B("#70FFFFFF"), 3));
        dc.DrawEllipse(null, new Pen(B("#BFFFFFFF"), 2), new Point(112, 26), 37, 9);
        if (Kind == "lemon") { dc.DrawEllipse(B("#ECDD74"), new Pen(B("#FFF5BB"), 3), new Point(149, 43), 19, 19); dc.DrawEllipse(null, new Pen(B("#FFF5BB"), 1), new Point(149, 43), 13, 13); }
        if (Kind == "berry") { dc.DrawEllipse(B("#777D45"), null, new Point(82, 31), 14, 6); dc.DrawEllipse(B("#586735"), null, new Point(91, 22), 6, 12); }
    }

    private static void DrawCake(DrawingContext dc)
    {
        dc.DrawEllipse(G("#FEFCF8", "#D4CCC0"), null, new Point(112, 117), 76, 19);
        Path(dc, "M 60,64 L 60,112 Q 110,130 164,103 L 164,54 L 113,38 Z", G("#F9E7B5", "#EBCF91"));
        Path(dc, "M 60,64 L 113,38 L 164,54 Q 117,86 60,64", G("#B6753C", "#6F432B"));
        Path(dc, "M 64,108 Q 118,127 160,100 L 160,106 Q 110,134 61,115 Z", B("#BA8C50"));
        Path(dc, "M 65,77 Q 111,95 160,70 M 65,91 Q 110,110 160,86", Brushes.Transparent, new Pen(B("#60FFFAE4"), 2));
    }
    private static void DrawBrownie(DrawingContext dc)
    {
        dc.DrawEllipse(B("#F7F1E7"), null, new Point(112, 119), 73, 16);
        Path(dc, "M 59,65 L 121,43 L 166,69 L 165,110 L 101,128 L 59,106 Z", G("#77452E", "#41271F"));
        Path(dc, "M 59,65 L 121,43 L 166,69 L 101,90 Z", G("#8D573B", "#5F382A"));
        Path(dc, "M 74,62 L 101,78 L 112,61 L 132,73 L 149,62", Brushes.Transparent, new Pen(B("#C08B5D"), 3));
        foreach (var p in new[] { new Point(77, 87), new Point(117, 105), new Point(148, 88) }) dc.DrawEllipse(B("#B27A50"), null, p, 3, 2);
    }
    private static void DrawCookie(DrawingContext dc)
    {
        dc.DrawEllipse(G("#C88A48", "#986337"), null, new Point(117, 96), 57, 31);
        dc.DrawEllipse(G("#E8B66F", "#C28743"), new Pen(B("#BD854C"), 2), new Point(104, 76), 57, 35);
        foreach (var p in new[] { new Point(78, 66), new Point(109, 55), new Point(127, 78), new Point(92, 90), new Point(142, 64), new Point(65, 83), new Point(116, 101) }) dc.DrawRoundedRectangle(B("#593928"), null, new Rect(p, new Size(9, 7)), 2, 2);
    }
    private static void DrawPastry(DrawingContext dc)
    {
        dc.DrawEllipse(B("#F7F2E9"), null, new Point(113, 119), 74, 16);
        Path(dc, "M 40,108 C 43,66 73,40 111,42 C 152,41 182,74 182,108 L 155,99 C 139,75 86,75 66,99 Z", G("#E9B469", "#AD682F"), new Pen(B("#BB803D"), 1));
        foreach (var data in new[] { "M 71,56 Q 71,79 81,89", "M 94,44 Q 89,68 99,81", "M 120,44 Q 112,64 120,82", "M 145,56 Q 134,73 139,88", "M 162,74 Q 147,87 155,99" }) Path(dc, data, Brushes.Transparent, new Pen(B("#FFE0A0"), 5));
    }
}

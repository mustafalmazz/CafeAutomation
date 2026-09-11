using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PremiumKafeOtomasyon.Controls;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.ViewModels;
using PremiumKafeOtomasyon.Services;

namespace PremiumKafeOtomasyon.Views;

public class AtelierDialog : Window
{
    protected readonly StackPanel Body = new() { Margin = new Thickness(28, 8, 28, 18) };
    protected readonly StackPanel Footer = new() { Margin = new Thickness(28, 10, 28, 22) };
    protected readonly TextBlock Error = new() { Foreground = new SolidColorBrush(Color.FromRgb(174, 57, 43)), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
    protected AtelierDialog(string title, string subtitle)
    {
        Title = title + " · Atelier"; Width = 480; MaxHeight = SystemParameters.WorkArea.Height - 24;
        SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        var root = new Grid { Background = (Brush)Application.Current.FindResource("Canvas") }; root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(28, 24, 28, 16) };
        heading.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Georgia"), FontSize = 28 });
        heading.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        root.Children.Add(heading);
        var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(220, SystemParameters.WorkArea.Height - 265) };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll); Grid.SetRow(Footer, 2); root.Children.Add(Footer); Footer.Children.Add(Error); Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
    protected static TextBlock Label(string text) => new() { Text = text, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 12, 0, 7) };
    protected static Button Button(string text, bool primary = false) => new() { Content = text, Style = (Style)Application.Current.FindResource(primary ? "PrimaryButton" : typeof(Button)) };
    protected static ComboBox Choice(string[] items, string selected) => new() { ItemsSource = items, SelectedItem = selected };
    protected void FinishActions(Button confirm)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(110) }); row.ColumnDefinitions.Add(new());
        var cancel = Button("Vazgeç"); cancel.Margin = new Thickness(0, 0, 10, 0); cancel.Click += (_, _) => Close(); row.Children.Add(cancel); Grid.SetColumn(confirm, 1); row.Children.Add(confirm); Footer.Children.Add(row);
    }
    public static bool TryAmount(string text, out decimal amount) => decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, Money.Turkish, out amount) && amount > 0 && amount == Money.Round(amount);
}

public sealed class ProductDialog : AtelierDialog
{
    public ProductDialog(MainViewModel vm, Product product, bool edit) : base(edit ? "Menünün bir parçası." : product.Name, edit ? "Ürün bilgileri ve satışa uygunluk" : product.Description)
    {
        if (edit) { BuildEditor(vm, product); return; }
        var art = new ProductPhoto { PhotoKey = ProductPhotos.ResolveKey(product), Radius = 12, Height = 135 };
        Body.Children.Add(art);
        var size = Choice(["Standart", "Büyük"], "Standart"); var milk = Choice(["Normal süt", "Yulaf sütü"], "Normal süt"); var shot = new CheckBox { Content = "Ekstra shot  +25,00 ₺" };
        var coffee = product.Category == "Kahveler";
        if (coffee)
        {
            Body.Children.Add(Label("Boyut · Büyük +25,00 ₺")); Body.Children.Add(size);
            Body.Children.Add(Label("Süt tercihi · Yulaf sütü +30,00 ₺")); Body.Children.Add(milk); Body.Children.Add(shot);
        }
        var note = new TextBox { MaxLength = 180, MinHeight = 52, TextWrapping = TextWrapping.Wrap };
        System.Windows.Automation.AutomationProperties.SetName(size, "Boyut");
        System.Windows.Automation.AutomationProperties.SetName(milk, "Süt tercihi");
        System.Windows.Automation.AutomationProperties.SetName(note, "Hazırlık notu");
        Body.Children.Add(Label("Hazırlık notu (isteğe bağlı)")); Body.Children.Add(note);
        var add = Button("", true);
        void Update() { var total = product.Price + (coffee ? ((string)size.SelectedItem == "Büyük" ? 25 : 0) + ((string)milk.SelectedItem == "Yulaf sütü" ? 30 : 0) + (shot.IsChecked == true ? 25 : 0) : 0); add.Content = "Ekle  ·  " + Money.Format(total); }
        size.SelectionChanged += (_, _) => Update(); milk.SelectionChanged += (_, _) => Update(); shot.Checked += (_, _) => Update(); shot.Unchecked += (_, _) => Update(); Update();
        add.Click += (_, _) => { if (vm.Run(() => vm.Service.AddProduct(vm.TableId, product.Id, (string)size.SelectedItem, (string)milk.SelectedItem, shot.IsChecked == true, note.Text), product.Name + " adisyona eklendi.")) Close(); else Error.Text = vm.Notice; };
        FinishActions(add);
    }

    private void BuildEditor(MainViewModel vm, Product product)
    {
        var name = new TextBox { Tag = "ProductName", Text = product.Name, MaxLength = 60 }; var description = new TextBox { Text = product.Description, MaxLength = 160 }; var price = new TextBox { Tag = "ProductPrice", Text = product.Price.ToString("0.00", Money.Turkish) };
        var category = Choice(["Kahveler", "Soğuk İçecekler", "Çaylar", "Tatlılar", "Atıştırmalıklar"], product.Category);
        var allergens = new TextBox { Text = product.Allergens, MaxLength = 200 }; Body.Children.Add(Label("Alerjen bilgisi (QR menüde görünür)")); Body.Children.Add(allergens);
        var artwork = new ComboBox { ItemsSource = ProductPhotos.Choices, DisplayMemberPath = nameof(PhotoChoice.Name), SelectedValuePath = nameof(PhotoChoice.Key), SelectedValue = ProductPhotos.ResolveKey(product) };
        System.Windows.Automation.AutomationProperties.SetName(artwork, "Ürün fotoğrafı");
        var available = new CheckBox { Content = "Satışa açık", IsChecked = product.Available }; var featured = new CheckBox { Content = "Çok sevilen etiketi", IsChecked = product.Featured };
        foreach (var item in new (string Label, FrameworkElement Field)[] { ("Ürün adı", name), ("Kısa açıklama", description), ("Kategori", category), ("Satış fiyatı (₺)", price), ("Ürün fotoğrafı", artwork) }) { Body.Children.Add(Label(item.Label)); Body.Children.Add(item.Field); }
        var photoPreview = new ProductPhoto { PhotoKey = ProductPhotos.ResolveKey(product), Height = 100, Margin = new Thickness(0, 10, 0, 0) };
        artwork.SelectionChanged += (_, _) => photoPreview.PhotoKey = (string?)artwork.SelectedValue ?? "latte";
        Body.Children.Add(photoPreview);
        Body.Children.Add(available); Body.Children.Add(featured);
        var save = Button("Ürünü kaydet", true); save.Click += (_, _) =>
        {
            if (!TryAmount(price.Text, out var amount)) { Error.Text = "Geçerli bir fiyat girin. Örnek: 145,50"; return; }
            var changed = new Product { Id = product.Id, Allergens = allergens.Text.Trim(), Name = name.Text.Trim(), Description = description.Text.Trim(), Category = (string)category.SelectedItem, Price = amount, Art = product.Art, PhotoKey = (string?)artwork.SelectedValue ?? "latte", Color = product.Color, Available = available.IsChecked == true, Featured = featured.IsChecked == true };
            if (vm.Run(() => vm.Service.SaveProduct(changed), "Ürün kaydedildi.")) Close(); else Error.Text = vm.Notice;
        };
        FinishActions(save);
    }
}

public sealed class PaymentDialog : AtelierDialog
{
    public PaymentDialog(MainViewModel vm) : base("Hesabı tamamlayalım.", vm.TableName + " · " + vm.OrderNumber)
    {
        var order = vm.CurrentOrder!; var remaining = order.Remaining;
        var total = new StackPanel { Margin = new Thickness(18) }; total.Children.Add(new TextBlock { Text = "KALAN HESAP", FontSize = 10, Foreground = Brushes.DimGray }); total.Children.Add(new TextBlock { Text = Money.Format(remaining), FontSize = 36, Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold });
        Body.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(237, 229, 216)), CornerRadius = new CornerRadius(12), Child = total });
        var posTest = Button("POS test merkezi · Gerçek tahsilat yapmaz"); posTest.Margin = new Thickness(0,10,0,8); posTest.Click += (_, _) => new PosDialog(vm, remaining) { Owner = this }.ShowDialog();
        var method = Choice(["Nakit", "Kart", "Diğer"], "Nakit"); Body.Children.Add(Label("Tahsilat yöntemi")); Body.Children.Add(method);
        Body.Children.Add(Label("Alınacak tutar (₺)")); var amount = new TextBox { Text = remaining.ToString("0.00", Money.Turkish), FontSize = 23, MaxLength = 12 }; Body.Children.Add(amount);
        System.Windows.Automation.AutomationProperties.SetName(method, "Tahsilat yöntemi");
        System.Windows.Automation.AutomationProperties.SetName(amount, "Alınacak tutar");
        var shortcuts = new UniformGrid { Columns = 3, Margin = new Thickness(0, 9, 0, 9) };
        foreach (var part in new[] { 1, 2, 3 }) { var b = Button(part == 1 ? "Tamamı" : "1 / " + part); b.Margin = new Thickness(0, 0, part == 3 ? 0 : 7, 0); b.Click += (_, _) => amount.Text = Money.Round(remaining / part).ToString("0.00", Money.Turkish); shortcuts.Children.Add(b); }
        Body.Children.Add(shortcuts);
        var keypad = new UniformGrid { Columns = 3 };
        var replace = true;
        foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", ",", "0", "⌫" })
        {
            var b = Button(key); b.FontSize = 19; b.Margin = new Thickness(0, 0, 6, 6); b.MinHeight = 47;
            b.Click += (_, _) => { if (replace && key != "⌫") { amount.Text = ""; replace = false; } if (key == "⌫") { amount.Text = amount.Text.Length > 0 ? amount.Text[..^1] : ""; replace = false; } else if (key != "," || !amount.Text.Contains(',')) amount.Text += key; };
            keypad.Children.Add(b);
        }
        amount.GotKeyboardFocus += (_, _) => { amount.SelectAll(); replace = true; };
        Body.Children.Add(keypad);
        Body.Children.Add(new TextBlock { Text = "Kısmi tutar alarak hesabı bölebilirsiniz. Kart seçimi yalnızca tahsilat kaydı oluşturur; cihazdan ödeme çekmez.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 10, 0, 0) });
        if (order.Payments.Count > 0) Body.Children.Add(new TextBlock { Text = "Önceki tahsilatlar: " + string.Join(" · ", order.Payments.Select(p => (p.Payer==""?"":p.Payer+" · ") + p.Method + " " + Money.Format(p.Amount))), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        var payer=new TextBox {MaxLength=80};
        var pay = Button("Tahsilatı kaydet", true); pay.Click += (_, _) =>
        {
            if (!TryAmount(amount.Text, out var value) || value > remaining) { Error.Text = "Tutar 0'dan büyük, kalan hesaba eşit veya daha küçük olmalı."; return; }
            pay.IsEnabled = false;
            if (vm.Run(() => vm.Service.Pay(order.Id, value, (string)method.SelectedItem, payer.Text), value == remaining ? "Hesap kapatıldı. Masa yeni misafirler için hazır." : "Kısmi tahsilat kaydedildi. Kalan tutar adisyonda.")) Close();
            else { Error.Text = vm.Notice; pay.IsEnabled = true; }
        };
        FinishActions(pay);
        Width = 660;
        // A two-column keypad keeps every digit and the confirmation visible on POS screens.
        var fields = Body.Children.Cast<UIElement>().ToList(); Body.Children.Clear();
        var paymentGrid = new Grid(); paymentGrid.ColumnDefinitions.Add(new() { Width = new GridLength(268) }); paymentGrid.ColumnDefinitions.Add(new() { Width = new GridLength(18) }); paymentGrid.ColumnDefinitions.Add(new());
        var details = new StackPanel(); foreach (var field in fields.Take(6)) details.Children.Add(field);
        paymentGrid.Children.Add(details); Grid.SetColumn(keypad, 2); paymentGrid.Children.Add(keypad);
        Body.Children.Add(paymentGrid); foreach (var field in fields.Skip(7)) Body.Children.Add(field); Body.Children.Add(posTest); Body.Children.Add(Label("Ödeyen kişi (isteğe bağlı)")); Body.Children.Add(payer);
    }
}

public sealed class MoveDialog : AtelierDialog
{
    public MoveDialog(MainViewModel vm) : base("Misafirler yer değiştirdi.", "Adisyonu boş bir masaya aktarın.")
    {
        var tables = vm.Service.State.Tables.Where(t => vm.Service.OpenOrder(t.Id) is null).ToList();
        var choice = new ComboBox { ItemsSource = tables, DisplayMemberPath = "Name", SelectedIndex = tables.Count > 0 ? 0 : -1 }; Body.Children.Add(Label("Yeni masa")); Body.Children.Add(choice);
        var move = Button("Adisyonu taşı", true); move.IsEnabled = tables.Count > 0; move.Click += (_, _) => { if (choice.SelectedItem is CafeTable table && vm.Run(() => vm.Service.MoveOrder(vm.CurrentOrder!.Id, table.Id), "Adisyon " + table.Name + " masasına taşındı.")) { vm.SelectMovedTable(table.Id); Close(); } else Error.Text = vm.Notice; }; FinishActions(move);
    }
}

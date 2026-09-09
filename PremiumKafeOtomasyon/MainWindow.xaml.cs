using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;
using PremiumKafeOtomasyon.ViewModels;
using PremiumKafeOtomasyon.Views;

namespace PremiumKafeOtomasyon;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    public MainWindow(CafeService service)
    {
        InitializeComponent();
        ViewModel = new MainViewModel(service);
        DataContext = ViewModel;
        ViewModel.ProductRequested = (p, edit) => ShowDialog(new ProductDialog(ViewModel, p, edit));
        ViewModel.PaymentRequested = () => ShowDialog(new PaymentDialog(ViewModel));
        ViewModel.MoveRequested = () => ShowDialog(new MoveDialog(ViewModel));
        ViewModel.BackupRequested = SaveBackup;
        ViewModel.ExportRequested = ExportSales;
        ViewModel.PrintRequested = PrintOrder;
        ViewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ViewModel.Page)) AdaptLayout(); };
        _clock.Tick += (_, _) => ViewModel.Refresh(); _clock.Start(); Closed += (_, _) => _clock.Stop();
        PreviewKeyDown += OnKeyDown;
        Loaded += (_, _) => AdaptLayout();
    }

    private void ShowDialog(Window dialog) { dialog.Owner = this; dialog.ShowDialog(); }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => AdaptLayout();
    public void AdaptLayout(double? viewportWidth = null, double? viewportHeight = null)
    {
        if (NavColumn is null) return;
        var width = viewportWidth ?? (ActualWidth > 0 ? ActualWidth : Width);
        var compact = width < 1280;
        Resources["NavLabelVisibility"] = compact ? Visibility.Collapsed : Visibility.Visible;
        Resources["NavPadding"] = new Thickness(compact ? 10 : 16, 11, compact ? 10 : 16, 11);
        NavColumn.Width = new GridLength(compact ? 76 : 188);
        var sales = ViewModel is null || ViewModel.Page == "Satış";
        CartColumn.Width = new GridLength(sales ? (width < 1000 ? 300 : compact ? 320 : 350) : 0);
        CartPanel.Visibility = sales ? Visibility.Visible : Visibility.Collapsed;
        if (ViewModel is not null) ViewModel.ShortViewport = (viewportHeight ?? (ActualHeight > 0 ? ActualHeight : Height)) < 800 || width < 1150;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var page = e.Key switch { Key.F2 => "Satış", Key.F3 => "Masalar", Key.F4 => "Hazırlık", _ => null };
        if (page is not null) { ViewModel.NavigateCommand.Execute(page); e.Handled = true; }
        else if (e.Key == Key.F9 && ViewModel.PayCommand.CanExecute(null)) { ViewModel.PayCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.NavigateCommand.Execute("Satış"); UpdateLayout();
            FindChildren<TextBox>(this).FirstOrDefault(t => Equals(t.Tag, "ProductSearch"))?.Focus(); e.Handled = true;
        }
    }
    public static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) yield return found;
            foreach (var next in FindChildren<T>(child)) yield return next;
        }
    }
    private void SaveBackup()
    {
        var dialog = new SaveFileDialog { Title = "Yedek kopyasını kaydet", Filter = "Atelier kayıt dosyası (*.json)|*.json", FileName = "atelier-yedek-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json" };
        if (dialog.ShowDialog(this) == true) ViewModel.Run(() =>
        {
            if (Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(ViewModel.DataPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Yedeği asıl kayıt dosyasından farklı bir konuma kaydedin.");
            File.Copy(ViewModel.DataPath, dialog.FileName, true);
        }, "Yedek kopyası kaydedildi.");
    }
    private void ExportSales()
    {
        var dialog = new SaveFileDialog { Title = "Günlük adisyon raporu", Filter = "CSV (*.csv)|*.csv", FileName = "atelier-satis-" + DateTime.Now.ToString("yyyyMMdd") + ".csv" };
        if (dialog.ShowDialog(this) != true) return;
        ViewModel.Run(() =>
        {
            static string Cell(string value) { if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value; return "\"" + value.Replace("\"", "\"\"") + "\""; }
            var rows = new List<string> { "Adisyon;Masa;Kapanış;Ödeme yöntemleri;Toplam" };
            rows.AddRange(ViewModel.Sales.Select(s => string.Join(";", new[] { s.Number, s.Table, s.Time, s.Methods, s.Total }.Select(Cell))));
            File.WriteAllLines(dialog.FileName, rows, new UTF8Encoding(true));
        }, "Günlük adisyon raporu dışa aktarıldı.");
    }
    private void PrintOrder()
    {
        if (ViewModel.CurrentOrder is not { } order) return;
        ViewModel.Run(() =>
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 11, PagePadding = new Thickness(12), ColumnWidth = double.PositiveInfinity };
            document.Blocks.Add(new Paragraph(new Run(ViewModel.BusinessName)) { FontSize = 19, FontWeight = FontWeights.SemiBold });
            document.Blocks.Add(new Paragraph(new Run($"{ViewModel.TableName} · #{order.Number}\n{DateTime.Now:dd.MM.yyyy HH:mm}\nHESAP DÖKÜMÜ — MALİ FİŞ DEĞİLDİR")));
            foreach (var line in order.Lines) document.Blocks.Add(new Paragraph(new Run($"{line.Quantity} × {line.Name}    {Money.Format(line.Total)}\n{line.Options}")));
            document.Blocks.Add(new Paragraph(new Run($"Toplam: {Money.Format(order.Total)}\nTahsil edilen: {Money.Format(order.Paid)}\nKalan: {Money.Format(order.Remaining)}")) { FontWeight = FontWeights.SemiBold });
            document.PageWidth = dialog.PrintableAreaWidth; document.PageHeight = dialog.PrintableAreaHeight;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Atelier hesap dökümü #" + order.Number);
        }, "Yazdırma penceresi kapatıldı.");
    }
}

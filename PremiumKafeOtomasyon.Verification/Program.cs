using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PremiumKafeOtomasyon;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;
using PremiumKafeOtomasyon.ViewModels;
using PremiumKafeOtomasyon.Views;

internal static class Program
{
    private static int _checks;
    private static void Check(bool passed, string label) { if (!passed) throw new Exception("FAIL: " + label); _checks++; Console.WriteLine("PASS: " + label); }
    private static void Reject(Action action, string label) { try { action(); } catch (InvalidOperationException) { Check(true, label); return; } throw new Exception("FAIL: " + label); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/verification"); Directory.CreateDirectory(output);
            var session = Path.Combine(output, "run-" + DateTime.Now.ToString("yyyyMMddHHmmssfff")); Directory.CreateDirectory(session);
            var store = new StateStore(Path.Combine(session, "state.json")); var service = new CafeService(store);
            Check(service.State.Products.Count == 18 && service.State.Tables.Count == 13, "Demo catalog and seating created");
            service.AddProduct("t1", "latte", "Büyük", "Yulaf sütü", true, "Az köpük");
            var order = service.OpenOrder("t1")!; var id = order.Id;
            Check(order.Total == 225m, "Modifiers included exactly once");
            service.AddProduct("t1", "latte", "Büyük", "Yulaf sütü", true, "Az köpük");
            Check(service.OpenOrder("t1")!.Lines.Single().Quantity == 2, "Identical draft products merge");
            service.ChangeQuantity(id, order.Lines[0].Id, -1);
            Reject(() => service.Pay(id, 100m, "Nakit"), "Draft order cannot be paid");
            service.SendToKitchen(id); service.SendToKitchen(id);
            Check(service.OpenOrder("t1")!.Lines.Single().Status == "Yeni", "Kitchen send is idempotent");
            Reject(() => service.ChangeQuantity(id, order.Lines[0].Id, -1), "Sent line cannot silently disappear");
            service.Pay(id, 100m, "Nakit");
            Check(service.OpenOrder("t1")!.Remaining == 125m, "Partial payment leaves correct balance");
            Reject(() => service.Pay(id, 125.01m, "Kart"), "Overpayment is rejected");
            Reject(() => service.Pay(id, -1, "Kart"), "Negative payment is rejected");
            Reject(() => service.Pay(id, 0.001m, "Kart"), "Sub-cent payment is rejected");
            Reject(() => service.AddProduct("t1", "cookie", "", "", false, ""), "Paid order is protected from edits");
            service.MoveOrder(id, "t3"); Check(service.OpenOrder("t1") is null && service.OpenOrder("t3")!.Paid == 100, "Moving preserves payments");
            Reject(() => service.MoveOrder(id, "t2"), "Moving to occupied table is rejected");
            service.Pay(id, 125m, "Kart"); Check(service.OpenOrder("t3") is null && service.State.Orders.Single(o => o.Id == id).Paid == 225, "Mixed payment closes and frees table");
            Reject(() => service.Pay(id, 1m, "Nakit"), "Closed order rejects repeat payment");
            service.AdvanceKitchen(id); service.AdvanceKitchen(id); service.AdvanceKitchen(id);
            Check(service.State.Orders.Single(o => o.Id == id).Lines.All(l => l.Status == "Teslim edildi"), "Paid takeaway can still finish preparation");
            var reopened = new CafeService(store); Check(reopened.State.Orders.Single(o => o.Id == id).Paid == 225m, "Payments survive restart");
            Check(File.Exists(store.FilePath + ".bak"), "Prior snapshot exists");
            var prior = new StateStore(store.FilePath + ".bak").Load(); Check(prior.Orders.Single(o => o.Id == id).Paid == 225m, "Backup is independently readable");
            var before = service.State.BusinessName;
            using (var locked = new FileStream(store.FilePath + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                try { service.RenameBusiness("Should not persist"); throw new Exception("Expected disk failure"); } catch (IOException) { Check(service.State.BusinessName == before, "Failed save leaves in-memory state unchanged"); }
            }
            var corruptPath = Path.Combine(session, "corrupt.json"); File.WriteAllText(corruptPath, "{broken");
            try { new StateStore(corruptPath).Load(); throw new Exception("Expected corrupt data error"); } catch (System.Text.Json.JsonException) { Check(File.ReadAllText(corruptPath) == "{broken", "Corrupt data is not overwritten"); }
            Check(AtelierDialog.TryAmount("145,50", out var parsed) && parsed == 145.50m && !AtelierDialog.TryAmount("145.50", out _) && !AtelierDialog.TryAmount("1,005", out _), "Turkish monetary input has unambiguous precision");
            service.AddProduct("t5", "latte", "Standart", "Normal süt", false, "");
            var kitchenOrderId = service.OpenOrder("t5")!.Id;
            service.SendToKitchen(kitchenOrderId); service.AdvanceKitchen(kitchenOrderId); service.AdvanceKitchen(kitchenOrderId);
            service.AddProduct("t5", "cookie", "", "", false, ""); service.SendToKitchen(kitchenOrderId); service.AdvanceKitchen(kitchenOrderId);
            Check(service.OpenOrder("t5")!.Lines.First().Status == "Hazır" && service.OpenOrder("t5")!.Lines.Last().Status == "Hazırlanıyor", "Late additions never regress already-ready food");

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PremiumKafeOtomasyon;component/Themes/Theme.xaml", UriKind.Relative) });
            var trace = new StringWriter(); PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(trace)); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var uiService = new CafeService(new StateStore(Path.Combine(session, "ui-state.json")));
            var window = new MainWindow(uiService);
            window.ViewModel.SelectTableCommand.Execute(window.ViewModel.Tables.First(t => t.Id == "t2"));
            foreach (var width in new[] { 1480, 1280, 1024, 980, 800 })
            {
                Render(window, width, width == 1480 ? 900 : width == 800 ? 600 : 720, Path.Combine(output, "sales-" + width + ".png"));
                var root = (FrameworkElement)window.Content;
                Check(root.ActualWidth == width && MainWindow.FindChildren<Button>(root).Any(b => Equals(b.Content, "Ödeme al   →") && b.ActualWidth > 240), "Sales layout " + width + " keeps payment available");
            }
            foreach (var page in new[] { "Masalar", "Hazırlık", "Menü", "Raporlar", "Ayarlar" })
            {
                window.ViewModel.NavigateCommand.Execute(page); Render(window, 1280, 800, Path.Combine(output, "page-" + page + ".png")); Check(MainWindow.FindChildren<TextBlock>((FrameworkElement)window.Content).Any(t => t.Text == window.ViewModel.PageTitle), page + " page renders");
            }
            window.ViewModel.NavigateCommand.Execute("Satış"); window.ViewModel.Search = "TÜRK"; Check(window.ViewModel.Products.Count == 1, "Turkish search finds accented uppercase products"); window.ViewModel.Search = "zzzz"; Check(window.ViewModel.NoProducts, "Empty search state works"); window.ViewModel.Search = "";
            var productDialog = new ProductDialog(window.ViewModel, uiService.State.Products.First(), false); RenderDialog(productDialog, Path.Combine(output, "product-dialog.png"));
            MainWindow.FindChildren<Button>((FrameworkElement)productDialog.Content).Single(b => b.Content is string s && s.StartsWith("Ekle")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.OpenOrder("t2")!.Total == 635, "Product dialog button writes to selected table");
            window.ViewModel.SendCommand.Execute(null);
            var paymentDialog = new PaymentDialog(window.ViewModel); RenderDialog(paymentDialog, Path.Combine(output, "payment-dialog.png"));
            MainWindow.FindChildren<TextBox>((FrameworkElement)paymentDialog.Content).Single().Text = "100,00";
            MainWindow.FindChildren<Button>((FrameworkElement)paymentDialog.Content).Single(b => Equals(b.Content, "Tahsilatı kaydet")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.OpenOrder("t2")!.Remaining == 535, "Payment dialog records partial cash payment");
            var editor = new ProductDialog(window.ViewModel, uiService.State.Products.First(), true); RenderDialog(editor, Path.Combine(output, "product-editor.png"));
            var editorInputs = MainWindow.FindChildren<TextBox>((FrameworkElement)editor.Content).ToList(); editorInputs[0].Text = "Signature Latte"; editorInputs[2].Text = "187,50";
            MainWindow.FindChildren<Button>((FrameworkElement)editor.Content).Single(b => Equals(b.Content, "Ürünü kaydet")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.State.Products.First().Name == "Signature Latte" && uiService.State.Products.First().Price == 187.50m, "Product editor saves name and decimal price");
            Check(uiService.OpenOrder("t2")!.Total == 635, "Menu edits preserve existing order prices");
            uiService.Pay(uiService.OpenOrder("t2")!.Id, 535, "Kart");
            window.ViewModel.NavigateCommand.Execute("Raporlar"); Render(window, 1280, 800, Path.Combine(output, "reports-populated.png"));
            Check(window.ViewModel.Sales.Count == 1 && window.ViewModel.Cash == Money.Format(100) && window.ViewModel.Card == Money.Format(535), "Reports reconcile completed sale and mixed payment");
            PresentationTraceSources.DataBindingSource.Flush(); File.WriteAllText(Path.Combine(output, "binding-errors.txt"), trace.ToString()); Check(string.IsNullOrWhiteSpace(trace.ToString()), "No WPF data binding errors");
            window.Close(); app.Shutdown();
            File.WriteAllText(Path.Combine(output, "result.txt"), $"{_checks} checks passed.\nData: {session}\n"); Console.WriteLine($"SUCCESS: {_checks} checks passed. Artifacts: {output}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Render(MainWindow window, int width, int height, string path)
    {
        window.Width = width; window.Height = height; window.AdaptLayout(width, height); var root = (FrameworkElement)window.Content; root.Width = width; root.Height = height;
        Layout(root, width, height); SaveImage(root, width, height, path);
    }
    private static void RenderDialog(Window window, string path)
    {
        var root = (FrameworkElement)window.Content; var width = (int)window.Width; root.Width = width; Layout(root, width, 640); SaveImage(root, width, 640, path);
    }
    private static void Layout(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); root.UpdateLayout();
    }
    private static void SaveImage(FrameworkElement root, int width, int height, string path)
    {
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var output = File.Create(path); encoder.Save(output);
    }
}

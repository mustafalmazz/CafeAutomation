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
using PremiumKafeOtomasyon.Controls;

internal static partial class Program
{
    private static int _checks;
    private static void Check(bool passed, string label) { if (!passed) throw new Exception("FAIL: " + label); _checks++; Console.WriteLine("PASS: " + label); }
    private static void Reject(Action action, string label) { try { action(); } catch (InvalidOperationException) { Check(true, label); return; } throw new Exception("FAIL: " + label); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--sql")) return VerifySql();
            if (args.Contains("--migrate-sql")) return MigrateProductionSql();
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

            var posStore = new StateStore(Path.Combine(session, "pos.json"));
            var posService = new CafeService(posStore);
            Reject(() => posService.BeginPosTest(100, PosScenario.Approve), "POS tests require explicit test mode");
            posService.SavePosSettings(new PosSettings { TestEnabled = true, DeviceName = "Test terminal", ConnectionType = "TCP/IP (hazırlık)", Endpoint = "127.0.0.1:9000" });
            Check(new CafeService(posStore).State.Pos.Endpoint == "127.0.0.1:9000", "POS preparation settings survive reload");
            Reject(() => posService.BeginPosTest(0, PosScenario.Approve), "Invalid POS amount rejected");
            Reject(() => posService.BeginPosTest(1.001m, PosScenario.Approve), "POS sub-cent amount rejected");
            var paymentsBefore = posService.State.Orders.Sum(o => o.Paid);
            var simulator = new PosSimulator();
            foreach (var scenario in Enum.GetValues<PosScenario>())
            {
                var testId = posService.BeginPosTest(100, scenario);
                Reject(() => posService.BeginPosTest(100, scenario), "Pending POS test blocks duplicate start");
                var result = simulator.ExecuteAsync(scenario).GetAwaiter().GetResult();
                posService.CompletePosTest(testId, result);
                var expected = scenario switch { PosScenario.Approve => PosStatus.Approved, PosScenario.Decline => PosStatus.Declined, PosScenario.Timeout => PosStatus.Unknown, _ => PosStatus.Disconnected };
                Check(posService.State.PosTransactions.Last().Status == expected, "POS scenario outcome: " + scenario);
                if (scenario == PosScenario.Timeout)
                {
                    Reject(() => posService.BeginPosTest(100, scenario), "Unknown result blocks retry after timeout");
                    posService = new CafeService(posStore);
                    Reject(() => posService.BeginPosTest(100, scenario), "Unknown result remains blocked after restart");
                    posService.QueryPosTest(testId);
                    Check(posService.State.PosTransactions.Last().Status == PosStatus.Approved, "Query resolves simulated terminal approval");
                    posService.CompletePosTest(testId, new(PosStatus.Declined, "Duplicate stale response"));
                    Check(posService.State.PosTransactions.Last().Status == PosStatus.Approved, "Late duplicate response does not overwrite terminal result");
                }
            }
            var interruptedId = posService.BeginPosTest(50, PosScenario.Approve);
            posService = new CafeService(posStore);
            Check(posService.State.PosTransactions.Last().Status == PosStatus.Unknown, "Interrupted test recovers as unknown on startup");
            posService.QueryPosTest(interruptedId);
            Check(posService.State.Orders.Sum(o => o.Paid) == paymentsBefore && posService.State.PosTransactions.Count == 5, "POS simulator never changes real payments and keeps one record per attempt");
            var ops = new CafeService(new StateStore(Path.Combine(session, "operations.json")));
            ops.CreateFirstManager("Manager", "816204");
            Check(ops.CurrentEmployee?.Role == "Yönetici" && ops.State.Employees[0].PinHash != "816204", "Manager PIN is salted and hashed");
            ops.SaveEmployee("Waiter", "Garson", "627419"); ops.SaveEmployee("Cashier", "Kasiyer", "518306");
            var managerId = ops.State.Employees[0].Id;
            ops.Logout(); Reject(() => ops.RenameBusiness("Forbidden"), "Logged-out service rejects mutations");
            ops.Login(ops.State.Employees[1].Id, "627419");
            Reject(() => ops.SaveIngredient("Milk", "ml", 100, 0.1m), "Waiter cannot change inventory");
            Reject(() => ops.Pay(ops.OpenOrder("t2")!.Id, 10, "Nakit"), "Waiter cannot collect payments");
            ops.Login(managerId, "816204");
            ops.SaveIngredient("Milk", "ml", 50, 0.1m); var milkId = ops.State.Ingredients[0].Id;
            ops.AdjustStock(milkId, 1000, "Giriş", "Delivery"); ops.SaveRecipe("latte", milkId, 200);
            ops.SaveCustomer("Guest", ""); var customerId = ops.State.Customers[0].Id;
            ops.AddProduct("t1", "latte", "Standart", "Normal süt", false, ""); var opId = ops.OpenOrder("t1")!.Id;
            ops.AssignCustomer(opId, customerId); ops.SendToKitchen(opId); ops.SendToKitchen(opId);
            Check(ops.State.Ingredients[0].Quantity == 800 && ops.OpenOrder("t1")!.Lines[0].Cost == 20, "Recipe consumed once with historical cost");
            Reject(() => ops.Pay(opId,145,"Nakit"), "Staff payment requires cash shift");
            ops.OpenShift(500); ops.AdjustOrder(opId,null,"İndirim",5,"Courtesy"); ops.Pay(opId,140,"Nakit");
            Check(ops.State.Customers[0].Points == 14 && ops.State.Customers[0].Stamps == 1 && ops.ExpectedCash(ops.ActiveShift!) == 640, "Sale updates shift and customer loyalty");
            ops.Refund(opId,40,"Nakit","Partial refund");
            Check(ops.State.Customers[0].Points == 10 && ops.ExpectedCash(ops.ActiveShift!) == 600, "Refund reverses cash and earned loyalty");
            Reject(()=>ops.Refund(opId,101,"Nakit","Too much"), "Refund cannot exceed unrefunded payment");
            ops.CashEntry(-50,"Supplies"); ops.CloseShift(549);
            Check(ops.State.Shifts[0].Expected == 550 && ops.State.Shifts[0].Counted == 549, "Shift stores cash discrepancy");
            ops.AddProduct("t1","latte","Standart","Normal süt",false,""); ops.AddProduct("t1","latte","Standart","Normal süt",false,"");
            var split = ops.OpenOrder("t1")!; ops.TransferLines(split.Id,"t3",split.Lines[0].Id,1);
            Check(ops.OpenOrder("t1")!.Total == 145 && ops.OpenOrder("t3")!.Total == 145,"Product split preserves amount");
            ops.TransferLines(ops.OpenOrder("t1")!.Id,"t3",null,0);
            Check(ops.OpenOrder("t1") == null && ops.OpenOrder("t3")!.Total == 290,"Merge preserves totals and frees source table");
            ops.SaveCoupon("WELCOME",20,200,DateTime.Today,1); ops.ApplyCoupon(ops.OpenOrder("t3")!.Id,"WELCOME");
            Check(ops.OpenOrder("t3")!.Total == 270 && ops.State.Coupons[0].Uses == 1,"Coupon enforces fixed discount and usage count");
            Reject(()=>ops.AddProduct("t3","latte","Standart","Normal süt",false,""),"Coupon order cannot be edited after conditions checked");
            ops.AdjustStock(milkId,0,"Sayım","Count");
            var stockBefore = ops.State.Ingredients[0].Quantity;
            Reject(()=>ops.SendToKitchen(ops.OpenOrder("t3")!.Id),"Insufficient stock blocks whole kitchen send");
            Check(ops.State.Ingredients[0].Quantity == stockBefore && ops.OpenOrder("t3")!.Lines.All(l=>l.Status=="Taslak"),"Failed stock consumption is atomic");
            var token=ops.State.Tables.Single(t=>t.Id=="t1").MenuToken;
            var guestId=Guid.NewGuid().ToString("N"); ops.SubmitGuestRequest(token,guestId,"latte",2,"No sugar"); ops.SubmitGuestRequest(token,guestId,"latte",2,"No sugar");
            Check(ops.State.GuestRequests.Count==1 && ops.OpenOrder("t1")==null,"QR request is idempotent and awaits staff approval");
            Reject(()=>ops.SubmitGuestRequest("bad",Guid.NewGuid().ToString("N"),"latte",1,""),"Invalid QR token rejected");
            ops.HandleGuestRequest(guestId,true); Check(ops.OpenOrder("t1")!.Total==290,"Approved QR order uses current catalog price");
            Reject(()=>ops.HandleGuestRequest(guestId,true),"QR approval cannot be replayed");
            var reloadedOps = new CafeService(new StateStore(ops.DataPath));
            Check(reloadedOps.CurrentEmployee==null && reloadedOps.State.Customers[0].Points==10 && reloadedOps.State.Tables.Single(t=>t.Id=="t1").MenuToken==token,"Operations and QR tokens survive restart without retaining login");
            ops.Login(managerId,"816204");
            ops.AdjustStock(milkId,2000,"Giriş","Restock");
            ops.SaveRecipe("latte",milkId,300,"Büyük · Yulaf sütü · Ekstra shot");
            var variantTable=ops.State.Tables.First(t=>ops.OpenOrder(t.Id)==null).Id;
            ops.AddProduct(variantTable,"latte","Büyük","Yulaf sütü",true,""); var variantOrder=ops.OpenOrder(variantTable)!;
            ops.SendToKitchen(variantOrder.Id);
            Check(ops.State.Ingredients[0].Quantity==1700,"Exact modifier recipe replaces base consumption");
            var vline=ops.State.Orders.Single(o=>o.Id==variantOrder.Id).Lines[0];
            ops.AdvanceLine(vline.Id); ops.AdvanceLine(vline.Id); ops.AdvanceLine(vline.Id);
            Check(ops.State.Orders.Single(o=>o.Id==variantOrder.Id).Lines[0].Status=="Teslim edildi","Kitchen line can be completed independently");
            ops.AssignCustomer(variantOrder.Id,customerId); ops.RedeemLoyalty(variantOrder.Id,5,false);
            Check(ops.State.Customers[0].Points==5 && ops.State.Orders.Single(o=>o.Id==variantOrder.Id).Discount==5,"Loyalty redemption debits points atomically");
            Reject(()=>ops.RedeemLoyalty(variantOrder.Id,5,false),"Loyalty discount cannot be replayed");
            var terminalBefore=ops.CurrentEmployee!.Id;
            ops.AuthenticateTerminal(ops.State.Employees[1].Id,"627419");
            Check(ops.CurrentEmployee!.Id==terminalBefore,"Remote login leaves desktop session unchanged");
            Reject(()=>ops.UpdateEmployee(managerId,"Garson",false,""),"Last manager cannot be disabled");
            ops.SaveEmployee("Lockout test","Garson","327618");var lockId=ops.State.Employees.Last().Id;
            for(var attempt=0;attempt<5;attempt++) Reject(()=>ops.Login(lockId,"000000"),"Incorrect PIN rejected");
            Reject(()=>ops.Login(lockId,"327618"),"PIN lockout survives even a correct PIN during lock interval");
            ops.UpdateEmployee(lockId,"Garson",false,"");
            var receiptBefore=ops.State.ReceiptPaper;ops.SaveReceiptSettings("","58 mm");Check(ops.State.ReceiptPaper=="58 mm","Receipt paper setting saved");
            var rollbackCopy=Path.Combine(session,"restore-source.json");File.Copy(ops.DataPath,rollbackCopy);
            var quantityBeforeRestore=ops.State.Ingredients[0].Quantity;ops.AdjustStock(milkId,1,"Giriş","Temporary");ops.RestoreBackup(rollbackCopy);
            Check(ops.State.Ingredients[0].Quantity==quantityBeforeRestore&&ops.State.Employees.Any(e=>e.Id==managerId),"Restore recovers operations while retaining employee security");
            var replayId=Guid.NewGuid().ToString("N");var replayTable=ops.State.Tables.First(t=>ops.OpenOrder(t.Id)==null).Id;
            using(var locked=new FileStream(ops.DataPath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                try {ops.TerminalAction(replayId,"add",replayTable,"tea",0,null);throw new Exception("Expected terminal disk failure");}
                catch(IOException){Check(ops.OpenOrder(replayTable)==null&&!ops.State.TerminalOperations.Contains(replayId),"Failed terminal write rolls back action and replay marker");}
            }
            ops.TerminalAction(replayId,"add",replayTable,"tea",0,null);
            var terminalReload=new CafeService(new StateStore(ops.DataPath));terminalReload.Login(managerId,"816204");terminalReload.TerminalAction(replayId,"add",replayTable,"tea",0,null);
            Check(terminalReload.OpenOrder(replayTable)!.Lines.Sum(l=>l.Quantity)==1,"Terminal replay remains idempotent after restart");
            var loyalty=new CafeService(new StateStore(Path.Combine(session,"loyalty.json")));loyalty.SaveCustomer("Loyal guest","");var loyalId=loyalty.State.Customers[0].Id;
            for(var visit=0;visit<10;visit++){loyalty.AddProduct("t1","tea","Standart","Normal süt",false,"");var visitOrder=loyalty.OpenOrder("t1")!;loyalty.AssignCustomer(visitOrder.Id,loyalId);loyalty.SendToKitchen(visitOrder.Id);loyalty.Pay(visitOrder.Id,visitOrder.Total,"Nakit","Misafir");}
            loyalty.AddProduct("t1","tea","Standart","Normal süt",false,"");var reward=loyalty.OpenOrder("t1")!;loyalty.AssignCustomer(reward.Id,loyalId);loyalty.SendToKitchen(reward.Id);loyalty.RedeemLoyalty(reward.Id,0,true);
            Check(loyalty.OpenOrder("t1")==null&&loyalty.State.Customers[0].Stamps==0,"Ten stamps redeem one product without generating a new paid stamp");
            Check(loyalty.State.Orders.First(o=>o.CustomerId==loyalId).Payments[0].Payer=="Misafir","Named payer is retained on payment record");
            Check(ops.BuildReport(DateTime.Today,DateTime.Today).Contains("ÜRÜN KATKI PAYI"),"Date report includes product contribution after discounts");
            Reject(()=>ops.BuildReport(DateTime.Today,DateTime.Today.AddDays(-1)),"Inverted report date range is rejected");
            var roleVm=new MainViewModel(ops);roleVm.NavigateCommand.Execute("Raporlar");ops.AuthenticateTerminal(ops.State.Employees[1].Id,"627419");Check(roleVm.Page=="Raporlar","Remote role does not transiently change desktop page");ops.Login(ops.State.Employees[1].Id,"627419");Check(roleVm.Page=="Satış","Switching to waiter leaves restricted report page");ops.Login(managerId,"816204");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PremiumKafeOtomasyon;component/Themes/Theme.xaml", UriKind.Relative) });
            Check(ProductPhotos.Choices.Count == 18 && ProductPhotos.Choices.All(p => ProductPhotos.Load(p.Key).PixelWidth == 800 && ProductPhotos.Load(p.Key).IsFrozen), "All 18 bundled photographs decode offline and are cached");
            Check(ProductPhotos.ResolveKey(new Product { Id = "turkish" }) == "turkish" && ProductPhotos.ResolveKey(new Product { Art = "cake" }) == "san" && ProductPhotos.ResolveKey(new Product { Id = "latte", PhotoKey = "mocha" }) == "mocha", "Legacy product IDs and explicit photo choices resolve without data reset");
            RenderPhotoCatalog(Path.Combine(output, "photo-catalog.png"));
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
            RenderDialog(new OperationsDialog(new MainViewModel(ops), "Kasa & vardiya"), Path.Combine(output,"operations-cash.png"));
            RenderDialog(new OperationsDialog(new MainViewModel(ops), "Stok & reçete"), Path.Combine(output,"operations-stock.png"));
            foreach(var section in new[]{"Personel","Adisyon işlemleri","Müşteri & sadakat","Kampanyalar","İleri raporlar","Yedek & cihazlar"}) RenderDialog(new OperationsDialog(new MainViewModel(ops),section),Path.Combine(output,"module-"+section.Replace(" & ","-")+".png"));
            RenderDialog(new GuestMenuDialog(new MainViewModel(ops)),Path.Combine(output,"qr-settings.png"));
            RenderDialog(new LoginDialog(reloadedOps), Path.Combine(output,"login.png"));
            var touchService=new CafeService(new StateStore(Path.Combine(session,"touch-login.json")));touchService.CreateFirstManager("Dokunmatik test","916203");
            var touchLogin=new LoginDialog(touchService);var touchRoot=(FrameworkElement)touchLogin.Content;
            Layout(touchRoot,520,540);
            void Tap(string label)=>MainWindow.FindChildren<Button>(touchRoot).Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var touchPin=MainWindow.FindChildren<PasswordBox>(touchRoot).Single();
            for(var digit=0;digit<14;digit++) Tap("1");
            Check(touchPin.Password.Length==12,"Touch keypad respects PIN length limit");
            Tap("⌫");Check(touchPin.Password.Length==11,"Touch backspace removes one digit");
            Tap("Temizle");Check(touchPin.Password.Length==0,"Touch clear removes the PIN");
            foreach(var digit in "916203") Tap(digit.ToString());
            Layout(touchRoot,520,540);SaveImage(touchRoot,520,540,Path.Combine(output,"login-touch-compact.png"));
            Check(MainWindow.FindChildren<Button>(touchRoot).Where(b=>b.Content is string s && (s.Length==1 && char.IsDigit(s[0]) || s=="Giriş yap")).All(b=>b.ActualHeight>=48 && b.TransformToAncestor(touchRoot).Transform(new Point(0,b.ActualHeight)).Y<=540),"Touch digits and login fit compact layout with 48 DIP targets");
            Tap("Giriş yap");Check(touchLogin.Authenticated,"Touch keypad authenticates without physical keyboard");
            var guestServer = new GuestMenuServer(ops); File.WriteAllText(Path.Combine(output,"guest-menu.html"),guestServer.RenderMenu(token));
            Check(guestServer.RenderMenu("invalid")==null,"QR page requires table token");
            RenderDialog(new PosDialog(new MainViewModel(posService)), Path.Combine(output, "pos-settings.png"));
            var networkTask = VerifyNetwork(ops, output); Pump(networkTask);
            var productDialog = new ProductDialog(window.ViewModel, uiService.State.Products.First(), false); RenderDialog(productDialog, Path.Combine(output, "product-dialog.png"));
            MainWindow.FindChildren<Button>((FrameworkElement)productDialog.Content).Single(b => b.Content is string s && s.StartsWith("Ekle")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.OpenOrder("t2")!.Total == 635, "Product dialog button writes to selected table");
            window.ViewModel.SendCommand.Execute(null);
            var paymentDialog = new PaymentDialog(window.ViewModel); RenderDialog(paymentDialog, Path.Combine(output, "payment-dialog.png"));
            MainWindow.FindChildren<TextBox>((FrameworkElement)paymentDialog.Content).Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Alınacak tutar").Text = "100,00";
            MainWindow.FindChildren<Button>((FrameworkElement)paymentDialog.Content).Single(b => Equals(b.Content, "Tahsilatı kaydet")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.OpenOrder("t2")!.Remaining == 535, "Payment dialog records partial cash payment");
            var editor = new ProductDialog(window.ViewModel, uiService.State.Products.First(), true); RenderDialog(editor, Path.Combine(output, "product-editor.png"));
            var editorInputs = MainWindow.FindChildren<TextBox>((FrameworkElement)editor.Content).ToList(); editorInputs.Single(x => Equals(x.Tag, "ProductName")).Text = "Signature Latte"; editorInputs.Single(x => Equals(x.Tag, "ProductPrice")).Text = "187,50";
            MainWindow.FindChildren<ComboBox>((FrameworkElement)editor.Content).Single(c => c.ItemsSource == ProductPhotos.Choices).SelectedValue = "mocha";
            MainWindow.FindChildren<Button>((FrameworkElement)editor.Content).Single(b => Equals(b.Content, "Ürünü kaydet")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(uiService.State.Products.First().Name == "Signature Latte" && uiService.State.Products.First().Price == 187.50m, "Product editor saves name and decimal price");
            Check(new StateStore(uiService.DataPath).Load().Products.First().PhotoKey == "mocha", "Selected photograph survives save and reload");
            Check(uiService.OpenOrder("t2")!.Total == 635, "Menu edits preserve existing order prices");
            uiService.Pay(uiService.OpenOrder("t2")!.Id, 535, "Kart");
            window.ViewModel.NavigateCommand.Execute("Raporlar"); Render(window, 1280, 800, Path.Combine(output, "reports-populated.png"));
            Check(window.ViewModel.Sales.Count == 1 && window.ViewModel.Cash == Money.Format(100) && window.ViewModel.Card == Money.Format(535), "Reports reconcile completed sale and mixed payment");
            Check(Math.Abs(window.ViewModel.CashShare - 100d / 635 * 100) < 0.001 && Math.Abs(window.ViewModel.CashShare + window.ViewModel.CardShare + window.ViewModel.OtherShare - 100) < 0.001, "Payment shares reconcile with recorded mixed tender");
            Render(window, 800, 600, Path.Combine(output, "reports-800.png"));
            var datedOrder=uiService.State.Orders.Single(o=>o.ClosedAt is not null);
            datedOrder.ClosedAt=new DateTimeOffset(new DateTime(2024,2,29,23,59,0));
            datedOrder.Payments[0].At=new DateTimeOffset(new DateTime(2024,2,29,12,0,0));
            datedOrder.Payments[1].At=new DateTimeOffset(new DateTime(2024,3,1,0,0,0));
            window.ViewModel.ReportDate=new DateTime(2024,2,12); window.ViewModel.ReportPeriod="Aylık";
            Check(window.ViewModel.ClosedCount==1 && window.ViewModel.Collected==Money.Format(100),"Monthly report includes leap day sale and excludes next-month payment");
            Check(window.ViewModel.Sales[0].Time.Contains("29.02.2024"),"Period report and CSV rows include full closing date");
            Render(window,800,600,Path.Combine(output,"reports-monthly-800.png"));
            datedOrder.ClosedAt=new DateTimeOffset(new DateTime(2025,12,31,18,0,0));
            datedOrder.Payments[0].At=new DateTimeOffset(new DateTime(2026,1,4,23,59,0));
            datedOrder.Payments[1].At=new DateTimeOffset(new DateTime(2026,1,5,0,0,0));
            window.ViewModel.ReportDate=new DateTime(2026,1,4);window.ViewModel.ReportPeriod="Haftalık";
            Check(window.ViewModel.ReportStart==new DateTime(2025,12,29) && window.ViewModel.ClosedCount==1 && window.ViewModel.Collected==Money.Format(100),"Weekly report spans year boundary from Monday through Sunday");
            window.ViewModel.ReportPeriod="Günlük";
            Check(window.ViewModel.ClosedCount==0 && window.ViewModel.Collected==Money.Format(100),"Daily view separates payment date from order closing date");
            window.ViewModel.ReportDate=new DateTime(2023,1,1);window.ViewModel.ReportPeriod="Aylık";
            Check(window.ViewModel.NoSales && window.ViewModel.Collected==Money.Format(0) && window.ViewModel.CashShare==0,"Empty historical period clears totals and payment shares");
            Order ReportFixture(DateTime day,string product,string name,int quantity) => new() { TableId="t1", Number=9000+quantity, ClosedAt=new DateTimeOffset(day), Lines=[new() {ProductId=product,Name=name,Quantity=quantity,UnitPrice=20,Status="Teslim edildi"}] };
            uiService.State.Orders = [ReportFixture(new(2025,12,31),"tea","Çay",5), ReportFixture(new(2026,1,4),"latte","Latte",3), ReportFixture(new(2026,1,31),"cookie","Cookie",7), ReportFixture(new(2026,12,31,23,59,59),"mocha","Mocha",10), ReportFixture(new(2027,1,1),"latte","Latte",100)];
            uiService.State.Orders[1].Lines.Add(new() {ProductId="tea",Name="Çay",Quantity=500,UnitPrice=20,Status="İptal"});
            uiService.State.Orders[1].Lines.Add(new() {ProductId="tea",Name="Çay",Quantity=500,UnitPrice=20,Complimentary=true,Status="Teslim edildi"});
            window.ViewModel.ReportDate=new DateTime(2026,1,4);window.ViewModel.ReportPeriod="Yıllık";
            Check(window.ViewModel.Revenue==Money.Format(400) && window.ViewModel.YearlyRevenue==Money.Format(400) && window.ViewModel.ClosedCount==3,"Annual revenue includes December end and excludes adjacent years");
            Check(window.ViewModel.ProductHighlights.Select(p=>p.Name).SequenceEqual(new[]{"Çay","Cookie","Mocha"}),"Week month and year winners use their own periods and exclude cancelled or complimentary items");
            Render(window,1280,1100,Path.Combine(output,"reports-yearly.png"));
            Render(window,800,1000,Path.Combine(output,"reports-yearly-800.png"));
            window.ViewModel.ReportDate=new DateTime(2024,1,1);
            Check(window.ViewModel.ProductHighlights.All(p=>p.Name=="Henüz satış yok") && window.ViewModel.YearlyRevenue==Money.Format(0),"Empty year clears all product highlights and annual revenue");
            PresentationTraceSources.DataBindingSource.Flush(); File.WriteAllText(Path.Combine(output, "binding-errors.txt"), trace.ToString()); Check(string.IsNullOrWhiteSpace(trace.ToString()), "No WPF data binding errors");
            window.Close(); app.Shutdown();
            File.WriteAllText(Path.Combine(output, "result.txt"), $"{_checks} checks passed.\nData: {session}\n"); Console.WriteLine($"SUCCESS: {_checks} checks passed. Artifacts: {output}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Pump(Task task)
    {
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(!task.IsCompleted)
        {
            if(DateTime.UtcNow>deadline) throw new TimeoutException("Network verification timed out");
            Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            Thread.Sleep(5);
        }
        task.GetAwaiter().GetResult();
    }
    private static async Task VerifyNetwork(CafeService service,string output)
    {
        var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        await using var server=new GuestMenuServer(service);
        await server.StartAsync("https://127.0.0.1:"+port);
        using var handler=new System.Net.Http.HttpClientHandler { CookieContainer=new System.Net.CookieContainer(), ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert?.Thumbprint==server.Certificate?.Thumbprint };
        using var client=new System.Net.Http.HttpClient(handler) {BaseAddress=new Uri(server.Address!),Timeout=TimeSpan.FromSeconds(10)};
        var home=await client.GetStringAsync("/");
        Check(home.Contains("data-product=") && home.Contains("masanızdaki QR"),"Menu server base address serves the product menu");
        Check(!home.Contains("Sipariş isteği gönder") && !home.Contains(">Garson çağır</button>"),"Base menu does not submit requests without a table");
        Check((await client.GetAsync("/menu/invalid")).StatusCode==System.Net.HttpStatusCode.NotFound,"Unknown table link stays invalid");
        Check((await client.GetAsync("/staff/state")).StatusCode==System.Net.HttpStatusCode.Unauthorized,"Terminal data rejects unauthenticated requests");
        var employee=service.State.Employees.First(e=>e.Role=="Yönetici");
        async Task<System.Net.Http.HttpResponseMessage> Post(string route,object body)=>await client.PostAsync(route,new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body),Encoding.UTF8,"application/json"));
        var login=await Post("/staff/login",new {EmployeeId=employee.Id,Pin="816204"});Check(login.IsSuccessStatusCode,"HTTPS terminal authenticates with PIN");
        Check((await client.GetAsync("/staff/state")).IsSuccessStatusCode,"Authenticated terminal can load current tables");
        var table=service.State.Tables.First(t=>service.OpenOrder(t.Id)==null);
        var request=new {Id=Guid.NewGuid().ToString("N"),Operation="add",TableId=table.Id,ProductId="latte",Amount=0,OrderId=(string?)null};
        var first=await Post("/staff/action",request);var second=await Post("/staff/action",request);
        Check(first.IsSuccessStatusCode&&second.IsSuccessStatusCode&&service.OpenOrder(table.Id)!.Lines.Sum(l=>l.Quantity)==1,"HTTPS terminal replay writes product only once");
        var waiter=service.State.Employees.First(e=>e.Role=="Garson");await Post("/staff/login",new {EmployeeId=waiter.Id,Pin="627419"});
        var pay=await Post("/staff/action",new {Id=Guid.NewGuid().ToString("N"),Operation="cash",TableId=table.Id,ProductId=(string?)null,Amount=145,OrderId=service.OpenOrder(table.Id)!.Id});
        Check(!pay.IsSuccessStatusCode&&service.OpenOrder(table.Id)!.Paid==0,"Remote waiter cannot bypass cashier permissions");
        var page=await client.GetStringAsync("/menu/"+table.MenuToken);File.WriteAllText(Path.Combine(output,"guest-live.html"),page);
        Check(page.Contains("Sipariş isteği gönder")&&(await client.GetAsync("/photo/latte")).IsSuccessStatusCode,"QR menu and embedded photos served over HTTPS");
        var guest=await Post("/request/"+table.MenuToken,new {Id=Guid.NewGuid().ToString("N"),ProductId=(string?)null,Quantity=1,Note="Garson çağrısı"});
        Check(guest.IsSuccessStatusCode,"Guest call reaches central queue over HTTP");
        server.ExportTableCodes(Path.Combine(output,"qr"));
        Check(File.ReadAllText(Path.Combine(output,"qr","masa-qr.html")).Contains("data:image/png;base64"),"Printable table QR codes generated");
        await client.PostAsync("/staff/logout",null);
        Check((await client.GetAsync("/staff/state")).StatusCode==System.Net.HttpStatusCode.Unauthorized,"Terminal logout revokes session");
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
    private static void RenderPhotoCatalog(string path)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 6, Margin = new Thickness(16) };
        foreach (var photo in ProductPhotos.Choices)
        {
            var item = new StackPanel { Margin = new Thickness(8) };
            item.Children.Add(new ProductPhoto { PhotoKey = photo.Key, Height = 164 });
            item.Children.Add(new TextBlock { Text = photo.Name, FontFamily = new FontFamily("Segoe UI"), FontSize = 14, Margin = new Thickness(2, 10, 0, 0) });
            grid.Children.Add(item);
        }
        var root = new Border { Background = (Brush)Application.Current.FindResource("Canvas"), Child = grid };
        Layout(root, 1440, 690); SaveImage(root, 1440, 690, path);
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

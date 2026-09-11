using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;
using PremiumKafeOtomasyon.ViewModels;
using Microsoft.Win32;

namespace PremiumKafeOtomasyon.Views;

public sealed class LoginDialog : AtelierDialog
{
    public bool Authenticated { get; private set; }
    public LoginDialog(CafeService service) : base(service.State.Employees.Count == 0 ? "İlk yönetici hesabı" : "Çalışma alanına giriş", "Kişisel PIN ile oturum açın. PIN en az 6 rakam olmalıdır.")
    {
        var setup = service.State.Employees.Count == 0;
        Width = Math.Min(520, SystemParameters.WorkArea.Width - 24);
        Body.Margin = new Thickness(20,0,20,4); Footer.Margin = new Thickness(20,6,20,12);
        var root = (Grid)Content;
        ((StackPanel)root.Children[0]).Margin = new Thickness(20,14,20,8);
        var scroll = (ScrollViewer)root.Children[1];
        scroll.PanningMode = PanningMode.VerticalOnly;
        scroll.MaxHeight = Math.Max(220, SystemParameters.WorkArea.Height - 180);
        TextBlock TouchLabel(string text) => new() { Text=text, FontSize=12, Foreground=Brushes.DimGray, Margin=new Thickness(0,4,0,4) };
        var name = new TextBox { MaxLength = 80 };
        var users = new ComboBox { ItemsSource = service.State.Employees.Where(e => e.Active).ToList(), DisplayMemberPath = "Name", SelectedIndex = 0 };
        users.MinHeight=48; name.MinHeight=48;
        Body.Children.Add(TouchLabel(setup ? "Yönetici adı" : "Personel")); Body.Children.Add(setup ? name : users);
        var pin = new PasswordBox { Padding = new Thickness(12,8,12,8), FontSize=20, MinHeight=48, MaxLength = 12 }; Body.Children.Add(TouchLabel("PIN")); Body.Children.Add(pin);
        var confirm = new PasswordBox { Padding = new Thickness(12,8,12,8), FontSize=20, MinHeight=48, MaxLength = 12 }; if (setup) { Body.Children.Add(TouchLabel("PIN tekrar")); Body.Children.Add(confirm); }
        System.Windows.Automation.AutomationProperties.SetName(pin,"Giriş PIN");
        System.Windows.Automation.AutomationProperties.SetName(confirm,"PIN tekrar");
        var activePin=pin;
        var target=TouchLabel("PIN giriliyor"); if(setup) Body.Children.Add(target);
        pin.GotKeyboardFocus+=(_,_)=>{activePin=pin;target.Text="PIN giriliyor";};
        confirm.GotKeyboardFocus+=(_,_)=>{activePin=confirm;target.Text="PIN tekrarı giriliyor";};
        users.SelectionChanged+=(_,_)=>{pin.Clear();confirm.Clear();Error.Text="";};
        var keypad=new System.Windows.Controls.Primitives.UniformGrid {Columns=3,Rows=4,Margin=new Thickness(-3,8,-3,0)};
        foreach(var key in new[]{"1","2","3","4","5","6","7","8","9","Temizle","0","⌫"})
        {
            var button=Button(key);button.MinHeight=48;button.Margin=new Thickness(3);button.Padding=new Thickness(6);
            button.FontSize=key=="Temizle"?13:22;button.Focusable=false;
            System.Windows.Automation.AutomationProperties.SetName(button,key=="⌫"?"Son rakamı sil":key);
            button.Click+=(_,_)=>
            {
                if(key=="Temizle") activePin.Clear();
                else if(key=="⌫") { if(activePin.Password.Length>0) activePin.Password=activePin.Password[..^1]; }
                else if(activePin.Password.Length<activePin.MaxLength) activePin.Password+=key;
                Error.Text="";
            };
            keypad.Children.Add(button);
        }
        Body.Children.Add(keypad);
        var login = Button(setup ? "Yönetici oluştur" : "Giriş yap", true);
        login.MinHeight=48; login.IsDefault=true;
        login.Click += (_, _) => { try { if (setup) { if (pin.Password != confirm.Password) throw new InvalidOperationException("PIN alanları eşleşmiyor."); service.CreateFirstManager(name.Text, pin.Password); } else service.Login(((Employee)users.SelectedItem).Id, pin.Password); Authenticated = true; Close(); } catch (Exception ex) { Error.Text = ex.Message; pin.Clear(); } }; FinishActions(login);
    }
}

public sealed class OperationsDialog : AtelierDialog
{
    private readonly MainViewModel _vm;
    private CafeService S => _vm.Service;
    public OperationsDialog(MainViewModel vm, string section) : base(section, "Atelier · İşletme çalışma alanı")
    {
        _vm = vm; Width = 640;
        if (new[] { "Stok & reçete", "İleri raporlar", "Yedek & cihazlar" }.Contains(section) && !S.CanManage) throw new InvalidOperationException("Bu alan için yönetici girişi gerekli.");
        var close = Button("Kapat"); close.Click += (_, _) => Close(); Footer.Children.Add(close);
        switch (section)
        {
            case "Personel": Staff(); break;
            case "Kasa & vardiya": Cash(); break;
            case "Adisyon işlemleri": Orders(); break;
            case "Stok & reçete": Stock(); break;
            case "Müşteri & sadakat": Customers(); break;
            case "Kampanyalar": Campaigns(); break;
            case "İleri raporlar": Reports(); break;
            case "Yedek & cihazlar": Backup(); break;
        }
        Tabify();
    }
    private void Tabify()
    {
        var elements=Body.Children.Cast<UIElement>().ToList();
        if(elements.OfType<TextBlock>().Count(t=>Equals(t.Tag,"SectionHeading"))<2) return;
        Body.Children.Clear();
        var tabs=new TabControl {Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,8,0,0)};
        StackPanel? group=null;
        foreach(var element in elements)
        {
            if(group==null || element is TextBlock {Tag:"SectionHeading"})
            {
                group=new StackPanel();
                System.Windows.Documents.TextElement.SetForeground(group, new SolidColorBrush(Color.FromRgb(48,41,32)));
                var title=element is TextBlock {Tag:"SectionHeading"} heading?heading.Text:"Genel";
                tabs.Items.Add(new TabItem {Header=title,Content=group,Padding=new Thickness(10,9,10,9),FontSize=11});
            }
            group.Children.Add(element);
        }
        Body.Children.Add(tabs);
    }
    private TextBox Input(string label, string value = "") { Body.Children.Add(Label(label)); var field = new TextBox { Text = value, MaxLength = 200 }; Body.Children.Add(field); return field; }
    private ComboBox Select<T>(string label, IEnumerable<T> values, string member = "") { Body.Children.Add(Label(label)); var c = new ComboBox { ItemsSource = values.ToList(), DisplayMemberPath = member, SelectedIndex = 0 }; Body.Children.Add(c); return c; }
    private void Info(string text) => Body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0,10,0,12) });
    private void Heading(string text) => Body.Children.Add(new TextBlock { Text = text, FontFamily = new FontFamily("Georgia"), FontSize = 23, Tag = "SectionHeading", Margin = new Thickness(0,20,0,10) });
    private void Action(string label, Action run, bool close = true)
    {
        var button = Button(label, true); button.Margin = new Thickness(0,14,0,8); Body.Children.Add(button);
        button.Click += (_, _) => { button.IsEnabled = false; try { if (!S.CanManage && (label == "Düzeltmeyi uygula" || label == "Yapılan iadeyi kaydet")) { var approval=new ManagerApprovalDialog(S,run) {Owner=this}; approval.ShowDialog(); if(!approval.Approved) return; } else run(); Error.Text = "İşlem kaydedildi."; if (close) Close(); } catch (Exception ex) { Error.Text = ex.Message; } finally { button.IsEnabled = true; } };
    }
    private static decimal Number(TextBox field) { if (!decimal.TryParse(field.Text, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, Money.Turkish, out var n)) throw new InvalidOperationException("Sayıları virgülle girin (ör. 12,50)."); return n; }
    private static T Selected<T>(ComboBox field) => field.SelectedItem is T item ? item : throw new InvalidOperationException("Listeden seçim yapın.");
    private void Staff()
    {
        Info("Oturum: " + (S.CurrentEmployee?.Name ?? "Kurulum") + " · " + (S.CurrentEmployee?.Role ?? ""));
        foreach (var e in S.State.Employees) Info(e.Name + " · " + e.Role);
        Action("Personel değiştir / kilitle", () => { S.Logout(); var login = new LoginDialog(S) { Owner = Owner }; Close(); login.ShowDialog(); if (!login.Authenticated) Owner?.Close(); }, false);
        Heading("Personel yetkisi / PIN sıfırlama"); var employee=Select("Personel",S.State.Employees,"Name"); var editRole=Select("Yeni rol",new[]{"Garson","Kasiyer","Yönetici"}); var active=new CheckBox {Content="Aktif",IsChecked=true};Body.Children.Add(active);Body.Children.Add(Label("Yeni PIN (değişmeyecekse boş bırakın)"));var resetPin=new PasswordBox {Padding=new Thickness(12),MaxLength=12};Body.Children.Add(resetPin);Action("Personel yetkisini güncelle",()=>S.UpdateEmployee(Selected<Employee>(employee).Id,Selected<string>(editRole),active.IsChecked==true,resetPin.Password));
        Heading("Yeni personel"); var name = Input("Ad soyad"); var role = Select("Rol", new[] { "Garson", "Kasiyer", "Yönetici" });
        Body.Children.Add(Label("6–12 rakamlı PIN")); var pin = new PasswordBox { Padding = new Thickness(12), MaxLength = 12 }; Body.Children.Add(pin);
        Action("Personeli ekle", () => S.SaveEmployee(name.Text, Selected<string>(role), pin.Password));
    }
    private void Cash()
    {
        if (S.ActiveShift is { } shift)
        {
            Heading("Açık vardiya"); Info($"{shift.Employee} · {shift.OpenedAt.LocalDateTime:g}\nAçılış {Money.Format(shift.Opening)} · Beklenen nakit {Money.Format(S.ExpectedCash(shift))}");
            var kind = Select("Kasa hareketi", new[] { "Giriş", "Çıkış" }); var amount = Input("Tutar"); var reason = Input("Hareket gerekçesi");
            Action("Kasa hareketini kaydet", () => { var n = Number(amount); if (n <= 0) throw new InvalidOperationException("Pozitif tutar girin."); S.CashEntry(Selected<string>(kind) == "Giriş" ? n : -n, reason.Text); });
            var counted = Input("Kapanışta sayılan nakit"); Action("Sayımı kaydet ve vardiyayı kapat", () => S.CloseShift(Number(counted)));
        }
        else { var opening = Input("Açılış nakit bakiyesi", "0"); Action("Vardiyayı aç", () => S.OpenShift(Number(opening))); }
        Heading("Vardiya geçmişi"); foreach (var closedShift in S.State.Shifts.Where(s => s.ClosedAt != null).TakeLast(10).Reverse()) Info($"{closedShift.OpenedAt.LocalDateTime:g} · {closedShift.Employee}\nBeklenen {Money.Format(closedShift.Expected ?? 0)} · Sayılan {Money.Format(closedShift.Counted ?? 0)} · Fark {Money.Format((closedShift.Counted ?? 0) - (closedShift.Expected ?? 0))}");
    }
    private void Orders()
    {
        var order = _vm.CurrentOrder;
        if (order is not null)
        {
            Heading(_vm.TableName + " · #" + order.Number); Info("Kalan " + Money.Format(order.Remaining));
            var lines = Select("Ürün", order.Lines.Where(l => l.Status != "İptal"), "Name"); var kind = Select("Yönetici işlemi", new[] { "İndirim", "İkram", "İptal" }); var amount = Input("İndirim tutarı (ikram/iptalde kullanılmaz)", "0"); var reason = Input("Zorunlu gerekçe");
            Action("Düzeltmeyi uygula", () => S.AdjustOrder(order.Id, (lines.SelectedItem as OrderLine)?.Id, Selected<string>(kind), Number(amount), reason.Text));
            Info("Hazırlanmış ürünün iptali stoğu geri eklemez. İkram da stok tüketir. Tahsilat başlayan hesap değiştirilemez.");
            Heading("Hesabı böl / birleştir"); var target = Select("Hedef masa", S.State.Tables.Where(t => t.Id != order.TableId), "Name"); var qty = Input("Taşınacak ürün adedi", "1");
            Action("Seçili ürünü hedef hesaba taşı", () => { var n = Number(qty); if (n != decimal.Truncate(n)) throw new InvalidOperationException("Tam adet girin."); S.TransferLines(order.Id, Selected<CafeTable>(target).Id, Selected<OrderLine>(lines).Id, checked((int)n)); });
            Action("Tüm adisyonu hedef hesapla birleştir", () => S.TransferLines(order.Id, Selected<CafeTable>(target).Id, null, 0));
        }
        Heading("İade kaydı"); Info("Bu işlem manuel iade kaydıdır; banka/POS üzerinden para iade etmez.");
        var closed = Select("Tamamlanmış adisyon numarası", S.State.Orders.Where(o => !o.IsOpen && o.Paid > 0).OrderByDescending(o => o.ClosedAt), "Number"); var refund = Input("İade tutarı"); var method = Select("İade yöntemi", new[] { "Nakit", "Kart", "Diğer" }); var why = Input("İade gerekçesi");
        Action("Yapılan iadeyi kaydet", () => S.Refund(Selected<Order>(closed).Id, Number(refund), Selected<string>(method), why.Text));
    }
    private void Stock()
    {
        Heading("Stok görünümü"); foreach (var i in S.State.Ingredients) Info($"{(i.Quantity <= i.Minimum ? "● KRİTİK · " : "")}{i.Name} · {i.Quantity:N2} {i.Unit} · Birim maliyet {i.UnitCost:N4} ₺");
        Heading("Stok hareketi"); var item = Select("Stok kalemi", S.State.Ingredients, "Name"); var kind = Select("İşlem", new[] { "Giriş", "Fire", "Sayım" }); var qty = Input("Miktar (sayımda sayılan toplam)"); var why = Input("Gerekçe"); Action("Stok hareketini kaydet", () => S.AdjustStock(Selected<Ingredient>(item).Id, Number(qty), Selected<string>(kind), why.Text));
        Heading("Ürün reçetesi"); Info("Reçete hazırlığa gönderimde bir kez düşer. Seçenek reçetesi varsa temel reçetenin yerine kullanılır; o seçenek için tüm içerikleri girin.");
        var product = Select("Ürün", S.State.Products, "Name"); var ingredient = Select("İçerik", S.State.Ingredients, "Name"); var use = Input("Porsiyon başına miktar · 0 ile bileşeni kaldır");
        var variants = new List<string> { "Temel reçete" }; foreach(var size in new[]{"Standart","Büyük"}) foreach(var milk in new[]{"Normal süt","Yulaf sütü"}) foreach(var shot in new[]{false,true}) variants.Add(size+" · "+milk+(shot?" · Ekstra shot":"")); var variant=Select("Reçete seçeneği",variants);
        Action("Reçete bileşenini kaydet", () => S.SaveRecipe(Selected<Product>(product).Id, Selected<Ingredient>(ingredient).Id, Number(use),variant.SelectedIndex==0?"":Selected<string>(variant)));
        foreach (var r in S.State.Recipes) Info($"{S.State.Products.Single(p => p.Id == r.ProductId).Name} [{(r.Variant==""?"Temel":r.Variant)}] → {S.State.Ingredients.Single(i => i.Id == r.IngredientId).Name} · {r.Quantity}");
        Heading("Maliyet & eşik");var editItem=Select("Stok kartı",S.State.Ingredients,"Name");var editMinimum=Input("Kritik stok eşiği","0");var editCost=Input("Güncel birim maliyet","0");void LoadStockCard(){if(editItem.SelectedItem is Ingredient i){editMinimum.Text=i.Minimum.ToString(Money.Turkish);editCost.Text=i.UnitCost.ToString(Money.Turkish);}}editItem.SelectionChanged+=(_,_)=>LoadStockCard();LoadStockCard();Action("Stok kartını güncelle",()=>S.UpdateIngredient(Selected<Ingredient>(editItem).Id,Number(editMinimum),Number(editCost)));
        Heading("Yeni stok kalemi"); var name = Input("İçerik adı"); var unit = Select("Birim", new[] { "g", "ml", "adet" }); var minimum = Input("Kritik stok eşiği", "0"); var cost = Input("Birim maliyet (örn. gram başına)", "0"); Action("Stok kalemini oluştur", () => S.SaveIngredient(name.Text, Selected<string>(unit), Number(minimum), Number(cost)));
    }
    private void Customers()
    {
        Info("Her 10 ₺ net tamamlanmış satış için 1 puan, her ödenmiş hesap için 1 damga. İadeler kazanımlardan düşer. 1 puan = 1 ₺ indirim; 10 damga = en düşük fiyatlı bir ürün. İade sonrası puan borcu oluşabilir; yeni kazanımlar bu bakiyeyi kapatır.");
        foreach (var c in S.State.Customers) Info($"{c.Name} · {c.Points} puan · {c.Stamps} damga · {S.State.Orders.Count(o => o.CustomerId == c.Id && !o.IsOpen)} ziyaret");
        if (_vm.CurrentOrder is { } order) { var customer = Select("Seçili adisyona müşteri bağla", S.State.Customers, "Name"); Action("Müşteriyi bağla", () => S.AssignCustomer(order.Id, Selected<Customer>(customer).Id)); var points=Input("Kullanılacak puan", "1"); Action("Puan ile indirim uygula",()=>{if(!int.TryParse(points.Text,out var n)) throw new InvalidOperationException("Tam puan girin."); S.RedeemLoyalty(order.Id,n,false);}); Action("10 damga ödülünü kullan",()=>S.RedeemLoyalty(order.Id,0,true)); }
        Heading("Müşteri işlem geçmişi"); var historyCustomer=Select("Müşteri",S.State.Customers,"Name"); var historyText=new TextBlock {TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,12,0,12)};Body.Children.Add(historyText);void CustomerHistory(){if(historyCustomer.SelectedItem is Customer c)historyText.Text=string.Join("\n",S.State.Orders.Where(o=>o.CustomerId==c.Id&&!o.IsOpen).OrderByDescending(o=>o.ClosedAt).Take(20).Select(o=>$"#{o.Number} · {o.ClosedAt!.Value.LocalDateTime:g} · {Money.Format(o.Total)} · İade {Money.Format(S.State.Refunds.Where(r=>r.OrderId==o.Id).Sum(r=>r.Amount))}"));}historyCustomer.SelectionChanged+=(_,_)=>CustomerHistory();CustomerHistory();
        Heading("Yeni müşteri"); var name = Input("Ad soyad"); var contact = Input("İletişim (isteğe bağlı)"); Action("Müşteriyi kaydet", () => S.SaveCustomer(name.Text, contact.Text));
    }
    private void Campaigns()
    {
        foreach (var c in S.State.Coupons) Info($"{c.Code} · {Money.Format(c.Amount)} indirim · Alt limit {Money.Format(c.MinimumSpend)}\nSon gün {c.ExpiresAt.LocalDateTime:d} · {c.Uses}/{c.MaximumUses} kullanım");
        if (_vm.CurrentOrder is { } order) { var apply = Input("Seçili adisyona kupon kodu"); Action("Kuponu uygula", () => S.ApplyCoupon(order.Id, apply.Text)); }
        Heading("Yeni kupon"); var code = Input("Kod"); var amount = Input("Sabit indirim tutarı"); var minimum = Input("Minimum hesap tutarı", "0"); var uses = Input("Toplam kullanım sınırı", "1"); Body.Children.Add(Label("Son kullanım günü")); var date = new DatePicker { SelectedDate = DateTime.Today.AddDays(30) }; Body.Children.Add(date);
        Action("Kuponu oluştur", () => { if (!int.TryParse(uses.Text, out var count)) throw new InvalidOperationException("Tam kullanım sayısı girin."); S.SaveCoupon(code.Text, Number(amount), Number(minimum), date.SelectedDate ?? DateTime.Today, count); });
    }
    private void Reports()
    {
        Body.Children.Add(Label("Başlangıç")); var from = new DatePicker { SelectedDate = _vm.ReportStart }; Body.Children.Add(from);
        Body.Children.Add(Label("Bitiş")); var to = new DatePicker { SelectedDate = _vm.ReportEndExclusive.AddDays(-1) }; Body.Children.Add(to);
        var output = new TextBlock { Text = S.BuildReport(_vm.ReportStart, _vm.ReportEndExclusive.AddDays(-1)), TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 24, Margin = new Thickness(0,16,0,0) };
        Action("Raporu hesapla", () => output.Text=S.BuildReport(from.SelectedDate??DateTime.Today,to.SelectedDate??DateTime.Today), false);
        Action("Bu raporu dışa aktar",()=>{var report=S.BuildReport(from.SelectedDate??DateTime.Today,to.SelectedDate??DateTime.Today);var file=new SaveFileDialog {Filter="Metin raporu|*.txt",FileName="atelier-rapor.txt"};if(file.ShowDialog()==true)System.IO.File.WriteAllText(file.FileName,report);},false);
        Body.Children.Add(output); Info("Reçetesi tanımlanmayan ürünlerin maliyeti sıfırdır. Bu gösterge net kâr değildir; işletme giderlerini içermez.");
    }
    private void Backup()
    {
        Info("Yedek geri yükleme mevcut işletme verilerini değiştirir. Mevcut personel hesapları korunur. Önce güncel bir yedek alın ve vardiyayı kapatın.");
        Action("Yedek kopyası kaydet", () => _vm.BackupRequested?.Invoke(), false);
        Action("Yedekten geri yükle…", () => { var file = new OpenFileDialog { Filter = "Atelier JSON|*.json" }; if (file.ShowDialog() != true) return; if (MessageBox.Show("Seçilen yedekle işletme verileri değiştirilecek. Devam edilsin mi?", "Yedeği geri yükle", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) S.RestoreBackup(file.FileName); });
        Heading("SQL Server"); Info(S.DataPath); Info("JSON dosyası yalnızca taşınabilir dış yedektir. SQL .bak yedeği sunucunun yedek dizinine yazılır ve SQL Server tarafından doğrulanır.");
        var sqlBackup=Button("SQL .bak yedeği al",true); Body.Children.Add(sqlBackup); sqlBackup.Click+=async(_,_)=>{sqlBackup.IsEnabled=false;try{var path=await Task.Run(S.CreateSqlBackup);Error.Text="Doğrulanan SQL yedeği: "+path;}catch(Exception ex){Error.Text="SQL yedeği alınamadı: "+ex.Message;}finally{sqlBackup.IsEnabled=true;}};
        Action("SQL bağlantı ayarları",()=>{var connection=new DatabaseConnectionDialog(PremiumKafeOtomasyon.Data.DatabaseConfiguration.Load()) {Owner=this};connection.ShowDialog();if(connection.Saved)Error.Text="Bağlantı ayarları kaydedildi. Yeni bağlantı uygulama yeniden açıldığında kullanılır.";},false);
        Heading("Hesap dökümü yazıcısı"); var printers=new List<string>{"Windows varsayılanı"};try{using var printServer=new System.Printing.LocalPrintServer();printers.AddRange(printServer.GetPrintQueues().Select(q=>q.FullName));}catch{Info("Yazıcı listesi okunamadı; Windows varsayılanı kullanılabilir.");}var printer=Select("Yazıcı",printers);var paper=Select("Kâğıt",new[]{"58 mm","80 mm","A4"});paper.SelectedItem=S.State.ReceiptPaper;printer.SelectedItem=S.State.ReceiptPrinter==""?"Windows varsayılanı":S.State.ReceiptPrinter;Action("Yazdırma ayarlarını kaydet",()=>S.SaveReceiptSettings(printer.SelectedIndex==0?"":Selected<string>(printer),Selected<string>(paper)));
        Heading("Cihaz bağlantıları"); Info("Windows hesap dökümü yazdırması mevcut. Mali fiş ve canlı banka POS için cihaz/sağlayıcı entegrasyonu gerekir. Çoklu terminal, QR çalışma alanındaki HTTPS sunucusunun /terminal adresinden kullanılır.");
        Action("POS test merkezini aç", () => new PosDialog(_vm) { Owner = this }.ShowDialog(), false);
    }
}

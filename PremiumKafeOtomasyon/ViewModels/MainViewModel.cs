using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;

namespace PremiumKafeOtomasyon.ViewModels;

public sealed record CategoryItem(string Name, int Count, bool Selected = false);
public sealed record ProductCard(Product Product)
{
    public string Name => Product.Name;
    public string Category => Product.Category;
    public string Description => Product.Description;
    public string Price => Money.Format(Product.Price);
    public string Art => Product.Art;
    public string Color => Product.Color;
    public bool Available => Product.Available;
    public string Badge => !Available ? "Tükendi" : Product.Featured ? "ÇOK SEVİLEN" : Category;
}
public sealed record TableCard(CafeTable Table, Order? Order, bool Selected)
{
    public string Id => Table.Id;
    public string Name => Table.Name;
    public string Area => Table.Area;
    public string Seats => Table.Seats == 0 ? "Paket servis" : Table.Seats + " kişilik";
    public bool Occupied => Order is not null;
    public string StatusColor => Order is null ? "#41634E" : Order.Lines.Any(l => l.Status == "Hazır") ? "#38666A" : "#93602E";
    public string Status => Order is null ? "Müsait" : Order.Paid > 0 ? "Kısmi ödeme" : Order.Lines.Any(l => l.Status == "Hazır") ? "Servise hazır" : "Açık hesap";
    public string Amount => Order is null ? "—" : Money.Format(Order.Remaining);
    public string Time => Order is null ? "Yeni misafirler için hazır" : $"{Math.Max(1, (int)(DateTimeOffset.Now - Order.OpenedAt).TotalMinutes)} dk · {Order.Lines.Sum(l => l.Quantity)} ürün";
}
public sealed record LineCard(OrderLine Line)
{
    public string Name => Line.Name;
    public string Detail => string.Join(" · ", new[] { Line.Options, Line.Note }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Total => Money.Format(Line.Total);
    public int Quantity => Line.Quantity;
    public string Status => Line.Status;
    public bool Editable => Line.Status == "Taslak";
}
public sealed record KitchenCard(Order Order, string Table)
{
    public string Number => "#" + Order.Number;
    public string Time => $"{Math.Max(1, (int)(DateTimeOffset.Now - Order.OpenedAt).TotalMinutes)} dk önce";
    public List<OrderLine> Lines => Order.Lines.Where(l => l.Status is not "Taslak" and not "Teslim edildi").ToList();
    public string Status => Lines.Any(l => l.Status == "Yeni") ? "Yeni sipariş" : Lines.Any(l => l.Status == "Hazırlanıyor") ? "Hazırlanıyor" : "Servise hazır";
    public string StatusBackground => Lines.Any(l => l.Status == "Yeni") ? "#F4E6D9" : Lines.Any(l => l.Status == "Hazırlanıyor") ? "#EEE9DB" : "#E4EDE5";
    public string StatusColor => Lines.Any(l => l.Status == "Yeni") ? "#995A38" : Lines.Any(l => l.Status == "Hazırlanıyor") ? "#866A34" : "#41634E";
    public string NextAction => Lines.Any(l => l.Status == "Yeni") ? "Hazırlamaya başla" : Lines.Any(l => l.Status == "Hazırlanıyor") ? "Hazır olarak işaretle" : "Teslim edildi";
}
public sealed record SaleRow(string Number, string Table, string Time, string Total, string Methods);
public sealed record RankedProduct(string Name, string Category, int Quantity, string Total, double BarWidth);

public sealed class MainViewModel : ObservableObject
{
    public CafeService Service { get; }
    public Action<Product, bool>? ProductRequested { get; set; }
    public Action? PaymentRequested { get; set; }
    public Action? MoveRequested { get; set; }
    public Action? BackupRequested { get; set; }
    public Action? ExportRequested { get; set; }
    public Action? PrintRequested { get; set; }

    private string _page = "Satış";
    private string _search = "";
    private string _category = "Tümü";
    private string _area = "Tüm alanlar";
    private string _tableId = "t1";
    private string _notice = "Hoş geldiniz. Örnek işletmeniz servise hazır.";
    private bool _error;
    private string _businessDraft = "";
    private bool _shortViewport;
    public bool ShortViewport { get => _shortViewport; set => Set(ref _shortViewport, value); }

    public MainViewModel(CafeService service)
    {
        Service = service;
        _businessDraft = service.State.BusinessName;
        NavigateCommand = new RelayCommand(p => { Page = (string)p!; _category = "Tümü"; Search = ""; Refresh(); });
        CategoryCommand = new RelayCommand(p => { _category = p is CategoryItem c ? c.Name : (string)p!; Refresh(); });
        AreaCommand = new RelayCommand(p => { Area = (string)p!; });
        SelectTableCommand = new RelayCommand(p => { _tableId = ((TableCard)p!).Id; Page = "Satış"; Refresh(); });
        ProductCommand = new RelayCommand(p => { if (p is ProductCard card) ProductRequested?.Invoke(card.Product, false); });
        EditProductCommand = new RelayCommand(p => { if (p is ProductCard card) ProductRequested?.Invoke(card.Product, true); });
        AddProductCommand = new RelayCommand(_ => ProductRequested?.Invoke(new Product { Color = "#EADACA", Art = "coffee", Price = 100 }, true));
        PlusCommand = new RelayCommand(p => ChangeQty(p, 1), p => CanEdit(p));
        MinusCommand = new RelayCommand(p => ChangeQty(p, -1), p => CanEdit(p));
        SendCommand = new RelayCommand(_ => Run(() => Service.SendToKitchen(CurrentOrder!.Id), "Sipariş hazırlık ekranına gönderildi."), _ => CurrentOrder?.Lines.Any(l => l.Status == "Taslak") == true);
        PayCommand = new RelayCommand(_ => PaymentRequested?.Invoke(), _ => CurrentOrder?.Remaining > 0 && !CurrentOrder.Lines.Any(l => l.Status == "Taslak"));
        MoveCommand = new RelayCommand(_ => MoveRequested?.Invoke(), _ => CurrentOrder is not null);
        AdvanceKitchenCommand = new RelayCommand(p => Run(() => Service.AdvanceKitchen(((KitchenCard)p!).Order.Id), "Hazırlık durumu güncellendi."));
        BackupCommand = new RelayCommand(_ => BackupRequested?.Invoke());
        ExportCommand = new RelayCommand(_ => ExportRequested?.Invoke());
        PrintCommand = new RelayCommand(_ => PrintRequested?.Invoke(), _ => CurrentOrder is not null);
        SaveBusinessCommand = new RelayCommand(_ => Run(() => Service.RenameBusiness(BusinessDraft), "İşletme bilgileri kaydedildi."));
        Service.Changed += Refresh;
        Refresh();
    }

    public string Page { get => _page; set { Set(ref _page, value); Raise(nameof(PageTitle)); Raise(nameof(PageSubtitle)); } }
    public string PageTitle => Page switch { "Masalar" => "Her masa, yeni bir hikâye.", "Hazırlık" => "İyi servis, iyi bir ritim.", "Raporlar" => "İşletmenizin nabzı.", "Menü" => "Menünüzün karakteri.", "Ayarlar" => "Her şey yerli yerinde.", _ => "Güzel bir servis başlasın." };
    public string PageSubtitle => Page switch { "Masalar" => "Alanlarınızı ve açık hesaplarınızı tek bakışta yönetin.", "Hazırlık" => "Bar ve mutfak siparişlerini hazırlıktan teslime takip edin.", "Raporlar" => "Bugünün gerçekleşen satışları ve tahsilatları.", "Menü" => "Ürünleri, fiyatları ve satışa uygunluk durumunu düzenleyin.", "Ayarlar" => "İşletme bilgileri, yerel kayıtlar ve cihaz seçenekleri.", _ => "Misafirlerinize odaklanın. Detaylar burada." };
    public string Search { get => _search; set { if (Set(ref _search, value)) RefreshProducts(); } }
    public string SelectedCategory => _category;
    public string Area { get => _area; set { Set(ref _area, value); Refresh(); } }
    public string BusinessDraft { get => _businessDraft; set => Set(ref _businessDraft, value); }
    public string BusinessName => Service.State.BusinessName;
    public string DateLabel => DateTime.Now.ToString("dd MMMM yyyy · dddd", Money.Turkish);
    public string Clock => DateTime.Now.ToString("HH:mm");
    public string TableId => _tableId;
    public string TableName => Service.State.Tables.First(t => t.Id == _tableId).Name;
    public Order? CurrentOrder => Service.OpenOrder(_tableId);
    public string OrderNumber => CurrentOrder is null ? "YENİ ADİSYON" : "ADİSYON #" + CurrentOrder.Number;
    public string OrderMeta => Service.State.Tables.First(t => t.Id == _tableId).Area + " · " + (CurrentOrder is null ? "Henüz sipariş yok" : CurrentOrder.OpenedAt.ToString("HH:mm") + " açılış");
    public string Subtotal => Money.Format(CurrentOrder?.Total ?? 0);
    public string Paid => Money.Format(CurrentOrder?.Paid ?? 0);
    public string Remaining => Money.Format(CurrentOrder?.Remaining ?? 0);
    public bool HasOrder => CurrentOrder?.Lines.Count > 0;
    public bool HasPayment => CurrentOrder?.Paid > 0;
    public bool HasDraft => CurrentOrder?.Lines.Any(l => l.Status == "Taslak") == true;
    public string CartHint => HasPayment ? "Kısmi tahsilat alındı. Ürünler değiştirilemez." : HasDraft ? "Ödeme için yeni ürünleri hazırlığa gönderin." : HasOrder ? "Adisyon tahsilata hazır." : "Menüden bir ürün seçerek başlayın.";
    public string Notice => _notice;
    public bool IsError => _error;
    public int OccupiedTables => Service.State.Orders.Count(o => o.IsOpen && o.TableId != "takeaway");
    public int EmptyTables => Service.State.Tables.Count(t => t.Id != "takeaway") - OccupiedTables;
    public int PendingCount => Service.State.Orders.Count(o => o.Lines.Any(l => l.Status is "Yeni" or "Hazırlanıyor" or "Hazır"));
    public int ClosedCount => TodayClosed.Count;
    private List<Order> TodayClosed => Service.State.Orders.Where(o => o.ClosedAt?.LocalDateTime.Date == DateTime.Today).ToList();
    private List<Payment> TodayPayments => Service.State.Orders.SelectMany(o => o.Payments).Where(p => p.At.LocalDateTime.Date == DateTime.Today).ToList();
    public string Revenue => Money.Format(TodayClosed.Sum(o => o.Total));
    public string Collected => Money.Format(TodayPayments.Sum(p => p.Amount));
    public string Cash => Money.Format(TodayPayments.Where(p => p.Method == "Nakit").Sum(p => p.Amount));
    public string Card => Money.Format(TodayPayments.Where(p => p.Method == "Kart").Sum(p => p.Amount));
    public string Other => Money.Format(TodayPayments.Where(p => p.Method == "Diğer").Sum(p => p.Amount));
    public string Average => Money.Format(ClosedCount == 0 ? 0 : TodayClosed.Sum(o => o.Total) / ClosedCount);
    public string OpenTotal => Money.Format(Service.State.Orders.Where(o => o.IsOpen).Sum(o => o.Remaining));
    public string ProductCount => Products.Count + " ürün";
    public string DataPath => Service.DataPath;
    public bool NoProducts => Products.Count == 0;
    public bool NoKitchen => Kitchen.Count == 0;
    public bool NoSales => Sales.Count == 0;

    public ObservableCollection<ProductCard> Products { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public ObservableCollection<TableCard> Tables { get; } = [];
    public ObservableCollection<LineCard> Lines { get; } = [];
    public ObservableCollection<KitchenCard> Kitchen { get; } = [];
    public ObservableCollection<SaleRow> Sales { get; } = [];
    public ObservableCollection<RankedProduct> Ranking { get; } = [];
    public ObservableCollection<AuditEntry> Activity { get; } = [];
    public string[] Areas { get; } = ["Tüm alanlar", "Salon", "Bahçe", "Teras", "Gel-al"];

    public ICommand NavigateCommand { get; }
    public ICommand CategoryCommand { get; }
    public ICommand AreaCommand { get; }
    public ICommand SelectTableCommand { get; }
    public ICommand ProductCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand PlusCommand { get; }
    public ICommand MinusCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand PayCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand AdvanceKitchenCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SaveBusinessCommand { get; }

    public void Notify(string message, bool error = false) { _notice = message; _error = error; Raise(nameof(Notice)); Raise(nameof(IsError)); }
    public bool Run(Action action, string message)
    {
        try { action(); Notify(message); return true; }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        { Notify(ex.Message, true); return false; }
    }
    private bool CanEdit(object? p) => p is LineCard card && card.Editable && CurrentOrder?.Paid == 0;
    private void ChangeQty(object? p, int delta) { if (p is LineCard c && CurrentOrder is { } o) Run(() => Service.ChangeQuantity(o.Id, c.Line.Id, delta), "Adisyon güncellendi."); }
    public void SelectMovedTable(string id) { _tableId = id; Refresh(); }

    private void RefreshProducts()
    {
        Products.Clear();
        foreach (var p in Service.State.Products.Where(p => (_category == "Tümü" || p.Category == _category) &&
            (string.IsNullOrWhiteSpace(Search) || Money.Turkish.CompareInfo.IndexOf(p.Name + " " + p.Description, Search.Trim(), System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0))) Products.Add(new ProductCard(p));
        Raise(nameof(ProductCount)); Raise(nameof(NoProducts));
    }

    public void Refresh()
    {
        Categories.Clear(); Categories.Add(new("Tümü", Service.State.Products.Count, _category == "Tümü"));
        foreach (var g in Service.State.Products.GroupBy(p => p.Category)) Categories.Add(new(g.Key, g.Count(), _category == g.Key));
        RefreshProducts();
        Tables.Clear();
        foreach (var t in Service.State.Tables.Where(t => Area == "Tüm alanlar" || t.Area == Area)) Tables.Add(new(t, Service.OpenOrder(t.Id), t.Id == _tableId));
        Lines.Clear(); foreach (var l in CurrentOrder?.Lines ?? []) Lines.Add(new(l));
        Kitchen.Clear(); foreach (var o in Service.State.Orders.Where(o => o.Lines.Any(l => l.Status is "Yeni" or "Hazırlanıyor" or "Hazır")).OrderBy(o => o.OpenedAt)) Kitchen.Add(new(o, Service.State.Tables.First(t => t.Id == o.TableId).Name));
        Sales.Clear(); foreach (var o in TodayClosed.OrderByDescending(o => o.ClosedAt)) Sales.Add(new("#" + o.Number, Service.State.Tables.First(t => t.Id == o.TableId).Name, o.ClosedAt!.Value.ToLocalTime().ToString("HH:mm"), Money.Format(o.Total), string.Join(" + ", o.Payments.Select(p => p.Method).Distinct())));
        var ranked = TodayClosed.SelectMany(o => o.Lines).GroupBy(l => l.ProductId).Select(g => new { Name = g.First().Name, Category = Service.State.Products.FirstOrDefault(p => p.Id == g.Key)?.Category ?? "", Quantity = g.Sum(l => l.Quantity), Total = g.Sum(l => l.Total) }).OrderByDescending(p => p.Total).Take(5).ToList();
        Ranking.Clear(); foreach (var p in ranked) Ranking.Add(new(p.Name, p.Category, p.Quantity, Money.Format(p.Total), (double)(p.Total / Math.Max(1, ranked.Max(p => p.Total))) * 220));
        Activity.Clear(); foreach (var a in Service.State.Audit.TakeLast(12).Reverse()) Activity.Add(a);
        foreach (var name in new[] { nameof(BusinessName), nameof(SelectedCategory), nameof(TableName), nameof(OrderNumber), nameof(OrderMeta), nameof(Subtotal), nameof(Paid), nameof(Remaining), nameof(HasOrder), nameof(HasPayment), nameof(HasDraft), nameof(CartHint), nameof(OccupiedTables), nameof(EmptyTables), nameof(PendingCount), nameof(ClosedCount), nameof(Revenue), nameof(Collected), nameof(Cash), nameof(Card), nameof(Other), nameof(Average), nameof(OpenTotal), nameof(NoKitchen), nameof(NoSales), nameof(DateLabel), nameof(Clock) }) Raise(name);
        CommandManager.InvalidateRequerySuggested();
    }
}

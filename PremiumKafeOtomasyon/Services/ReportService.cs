using PremiumKafeOtomasyon.Domain;
namespace PremiumKafeOtomasyon.Services;
public sealed record ProductContribution(string Name,decimal Sales,decimal Cost)
{
    public decimal Contribution=>Sales-Cost;
    public decimal? Rate=>Sales==0?null:Contribution/Sales*100;
}
public sealed partial class CafeService
{
    public string BuildReport(DateTime start,DateTime end)
    {
        if(!CanManage) throw new InvalidOperationException("Yönetici girişi gerekli.");
        if(end.Date<start.Date) throw new InvalidOperationException("Tarih aralığı geçersiz.");
        var orders=State.Orders.Where(o=>o.ClosedAt?.LocalDateTime.Date>=start.Date&&o.ClosedAt?.LocalDateTime.Date<=end.Date).ToList();
        var refunds=State.Refunds.Where(r=>r.At.LocalDateTime.Date>=start.Date&&r.At.LocalDateTime.Date<=end.Date).Sum(r=>r.Amount);
        var sales=orders.Sum(o=>o.Total);var costs=orders.Sum(o=>o.Lines.Sum(l=>l.Cost));
        var products=orders.SelectMany(o=>o.Lines.Select(l=>new {l.ProductId,l.Name,Net=o.Lines.Sum(x=>x.Total)==0?0:l.Total/o.Lines.Sum(x=>x.Total)*o.Total,l.Cost})).GroupBy(l=>l.ProductId).Select(g=>new ProductContribution(g.First().Name,g.Sum(l=>l.Net),g.Sum(l=>l.Cost))).OrderByDescending(p=>p.Sales);
        return $"{start:d} — {end:d}\nTamamlanan hesap: {orders.Count}\nSatış: {Money.Format(sales)}\nDönemdeki iadeler: {Money.Format(refunds)}\nSatış − iade: {Money.Format(sales-refunds)}\nKayıtlı reçete maliyeti: {Money.Format(costs)}\nSatış − kayıtlı maliyet: {Money.Format(sales-costs)}\nİndirim: {Money.Format(orders.Sum(o=>o.Discount))}\nİkram: {Money.Format(orders.SelectMany(o=>o.Lines).Where(l=>l.Complimentary).Sum(l=>l.UnitPrice*l.Quantity))}\nİptal: {Money.Format(orders.SelectMany(o=>o.Lines).Where(l=>l.Status=="İptal").Sum(l=>l.UnitPrice*l.Quantity))}\n\nÜRÜN KATKI PAYI · İndirim sonrası, iadeler hariç\n"
            +string.Join("\n",products.Select(p=>$"{p.Name}: Satış {Money.Format(p.Sales)} · Maliyet {Money.Format(p.Cost)} · Katkı {Money.Format(p.Contribution)} · {(p.Rate is null?"—":p.Rate.Value.ToString("N1",Money.Turkish)+"%")}"))
            +"\n\nSAATLİK KAPANAN HESAPLAR\n"+string.Join("\n",orders.GroupBy(o=>o.ClosedAt!.Value.LocalDateTime.Hour).OrderBy(g=>g.Key).Select(g=>$"{g.Key:00}:00 · {g.Count()} hesap · {Money.Format(g.Sum(o=>o.Total))}"));
    }
}

using PremiumKafeOtomasyon.Domain;
namespace PremiumKafeOtomasyon.Services;
public sealed partial class CafeService
{
    public void SaveReceiptSettings(string printer,string paper)
    {
        if(paper is not "58 mm" and not "80 mm" and not "A4") throw new InvalidOperationException("Geçersiz kâğıt türü.");
        Commit("Yazdırma ayarları kaydedildi",s=>{s.ReceiptPrinter=printer;s.ReceiptPaper=paper;});
    }
}

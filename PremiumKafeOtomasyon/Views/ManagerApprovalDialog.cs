using System.Windows;
using System.Windows.Controls;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;
namespace PremiumKafeOtomasyon.Views;
public sealed class ManagerApprovalDialog : AtelierDialog
{
    public bool Approved {get;private set;}
    public ManagerApprovalDialog(CafeService service,Action action):base("Yönetici onayı","İşlem yönetici kimliğiyle kaydedilecek; mevcut personelin oturumu korunacak.")
    {
        var managers=new ComboBox {ItemsSource=service.State.Employees.Where(e=>e.Active&&e.Role=="Yönetici").ToList(),DisplayMemberPath="Name",SelectedIndex=0};Body.Children.Add(Label("Onaylayan yönetici"));Body.Children.Add(managers);
        var pin=new PasswordBox {Padding=new Thickness(12),MaxLength=12};Body.Children.Add(Label("Yönetici PIN"));Body.Children.Add(pin);
        var approve=Button("Onayla ve işlemi uygula",true);approve.Click+=(_,_)=>{try{var id=service.AuthenticateTerminal(((Employee)managers.SelectedItem).Id,pin.Password);service.AsEmployee(id,()=>{action();return true;});Approved=true;Close();}catch(Exception ex){Error.Text=ex.Message;pin.Clear();}};FinishActions(approve);
    }
}

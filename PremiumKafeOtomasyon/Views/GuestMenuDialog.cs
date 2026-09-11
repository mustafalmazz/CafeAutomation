using System.Windows;
using System.Windows.Controls;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.ViewModels;
using Microsoft.Win32;
namespace PremiumKafeOtomasyon.Views;
public sealed class GuestMenuDialog : AtelierDialog
{
    public GuestMenuDialog(MainViewModel vm) : base("Masadan gelen istekler", "QR menü · Aynı yerel ağdaki telefonlar için")
    {
        Width = 620;
        Body.Children.Add(Label("Sunucu adresi · Telefonlar için bilgisayarın yerel ağ IP adresini girin."));
        var address = new TextBox { Text = vm.GuestServer.Address ?? "https://127.0.0.1:5088" }; Body.Children.Add(address);
        var start = Button("Menü sunucusunu başlat", true); Body.Children.Add(start);
        var open = Button("Menüyü tarayıcıda aç ↗"); open.IsEnabled = vm.GuestServer.Address is not null; Body.Children.Add(open);
        open.Click += (_,_) => { try { if(vm.GuestServer.Address is string url) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); } catch(Exception ex) { Error.Text=ex.Message; } };
        start.Click += async (_,_) => { start.IsEnabled = false; try { await vm.GuestServer.StartAsync(address.Text.Trim()); Error.Text = "Menü açık: " + vm.GuestServer.Address + " · Ana sayfada menü görüntülenir; sipariş için masa QR kodunu kullanın."; } catch(Exception ex) { Error.Text = ex.Message; } finally { start.IsEnabled=vm.GuestServer.Address is null; open.IsEnabled=vm.GuestServer.Address is not null; address.IsEnabled=vm.GuestServer.Address is null; } };
        start.IsEnabled=vm.GuestServer.Address is null; address.IsEnabled=vm.GuestServer.Address is null;
        var stop = Button("Menü sunucusunu durdur"); stop.Margin = new Thickness(0,8,0,8); Body.Children.Add(stop); stop.Click += async (_,_) => { try { await vm.GuestServer.DisposeAsync(); Error.Text = "Menü sunucusu kapalı."; start.IsEnabled=true; open.IsEnabled=false; address.IsEnabled=true; } catch(Exception ex) { Error.Text=ex.Message; } };
        var export = Button("Masa QR kartlarını kaydet"); Body.Children.Add(export);
        export.Click += (_,_) => { try { var folder = new OpenFolderDialog(); if(folder.ShowDialog()==true) { vm.GuestServer.ExportTableCodes(folder.FolderName); Error.Text = "masa-qr.html kaydedildi. Tarayıcıdan açıp yazdırabilirsiniz."; } } catch(Exception ex) { Error.Text=ex.Message; } };
        Body.Children.Add(new TextBlock { Text = "127.0.0.1 yalnızca bu bilgisayarda çalışır. Telefon erişimi için aynı Wi-Fi, yerel IP ve Windows güvenlik duvarı izni gerekir. Uygulama kapanınca menü durur. İnternete açık servis değildir.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) });
        Body.Children.Add(new TextBlock { Text="Personel terminali: sunucu adresi + /terminal. HTTPS sertifikasını cihazlara güvenilir olarak tanıtmadan kullanmayın. Özel anahtar Windows kullanıcı sertifika deposunda tutulur.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,10,0,10) });
        var certificate=Button("TLS sertifikasını dışa aktar (.cer)"); Body.Children.Add(certificate); certificate.Click+=(_,_)=>{try{var file=new SaveFileDialog {Filter="Sertifika|*.cer",FileName="atelier-local.cer"};if(file.ShowDialog()==true)vm.GuestServer.ExportCertificate(file.FileName);}catch(Exception ex){Error.Text=ex.Message;}};
        var requests = new StackPanel(); Body.Children.Add(requests);
        void Refresh()
        {
            requests.Children.Clear();
            foreach(var r in vm.Service.State.GuestRequests.Where(r=>r.Status=="Bekliyor").OrderBy(r=>r.At))
            {
                var panel=new StackPanel { Margin=new Thickness(0,10,0,10) };
                panel.Children.Add(new TextBlock { Text=vm.Service.State.Tables.Single(t=>t.Id==r.TableId).Name+" · "+(r.ProductId==null?"Garson çağrısı":vm.Service.State.Products.Single(p=>p.Id==r.ProductId).Name+" × "+r.Quantity), FontWeight=FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text=r.Note, TextWrapping=TextWrapping.Wrap });
                foreach(var approve in new[]{true,false}) { var button=Button(approve?"Onayla / karşılandı":"Reddet"); button.Click+=(_,_)=>{try {vm.Service.HandleGuestRequest(r.Id,approve); Refresh();}catch(Exception ex){Error.Text=ex.Message;}};panel.Children.Add(button); }
                requests.Children.Add(panel);
            }
            if(requests.Children.Count==0) requests.Children.Add(Label("Bekleyen masa isteği yok."));
        }
        vm.Service.Changed += Refresh; Closed += (_,_)=>vm.Service.Changed-=Refresh; Refresh();
        var close=Button("Kapat"); close.Click+=(_,_)=>Close(); Footer.Children.Add(close);
    }
}

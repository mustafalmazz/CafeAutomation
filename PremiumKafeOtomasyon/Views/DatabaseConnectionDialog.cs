using System.Windows;
using System.Windows.Controls;
using PremiumKafeOtomasyon.Data;
namespace PremiumKafeOtomasyon.Views;
public sealed class DatabaseConnectionDialog : AtelierDialog
{
    public bool Saved {get;private set;}
    public DatabaseConnectionDialog(DatabaseConfiguration current,string? error=null) : base("SQL Server bağlantısı","Veriler SQL Server'da saklanır. Bağlantı kesilince dosya kaydına geçilmez.")
    {
        Width=560;
        var server=new TextBox {Text=current.Server,MaxLength=200};Body.Children.Add(Label("Sunucu / örnek (ör. .\\SQLEXPRESS)"));Body.Children.Add(server);
        var database=new TextBox {Text=current.Database,MaxLength=128};Body.Children.Add(Label("Veritabanı"));Body.Children.Add(database);
        var windows=new CheckBox {Content="Windows kimlik doğrulaması",IsChecked=current.WindowsAuthentication};Body.Children.Add(windows);
        var user=new TextBox {Text=current.UserName,MaxLength=128};Body.Children.Add(Label("SQL kullanıcı adı (Windows girişinde kullanılmaz)"));Body.Children.Add(user);
        var password=new PasswordBox {MaxLength=256,Padding=new Thickness(12)};Body.Children.Add(Label("SQL parolası · Değişmeyecekse boş bırakın"));Body.Children.Add(password);
        var trust=new CheckBox {Content="Sunucunun sertifikasına güven (yerel kurulum)",IsChecked=current.TrustServerCertificate};Body.Children.Add(trust);
        Body.Children.Add(new TextBlock {Text="Uzak sunucuda doğrulanabilir TLS sertifikası kullanın ve bu seçeneği kapatın. SQL parolası Windows kullanıcı hesabına bağlı olarak şifrelenir; bağlantı bilgileri Git deposuna yazılmaz.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12),FontSize=11});
        DatabaseConfiguration Draft()
        {
            var result=new DatabaseConfiguration {Server=server.Text,Database=database.Text,WindowsAuthentication=windows.IsChecked==true,UserName=user.Text,ProtectedPassword=current.ProtectedPassword,TrustServerCertificate=trust.IsChecked==true,LegacyImportAllowed=current.LegacyImportAllowed};
            if(password.Password.Length>0)result.SetPassword(password.Password);return result;
        }
        var test=Button("Bağlantıyı test et");Body.Children.Add(test);
        test.Click+=async(_,_)=>{test.IsEnabled=false;try{var config=Draft();await Task.Run(config.TestConnection);Error.Text="SQL Server bağlantısı başarılı.";}catch{Error.Text="Bağlantı kurulamadı. Sunucu, kimlik doğrulama ve sertifika ayarlarını kontrol edin.";}finally{test.IsEnabled=true;}};
        var save=Button("Test et ve ayarları kaydet",true);
        save.Click+=async(_,_)=>{save.IsEnabled=false;try{var config=Draft();await Task.Run(config.TestConnection);config.Save();Saved=true;Close();}catch{Error.Text="Ayarlar kaydedilemedi. Bağlantı bilgilerini kontrol edin.";}finally{save.IsEnabled=true;}};
        FinishActions(save);Error.Text=error??"";
    }
}

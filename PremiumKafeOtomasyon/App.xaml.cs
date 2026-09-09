using System.IO;
using System.Threading;
using System.Windows;
using PremiumKafeOtomasyon.Services;

namespace PremiumKafeOtomasyon;

public partial class App : Application
{
    private Mutex? _mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, "Local\\AtelierCafeLocalDemo", out var firstInstance);
        if (!firstInstance) { MessageBox.Show("Atelier zaten açık. Görev çubuğundan mevcut pencereye geçin.", "Atelier"); Shutdown(); return; }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtelierCafe", "demo-state.json");
            var service = new CafeService(new StateStore(path));
            var window = new MainWindow(service);
            window.Width = Math.Min(window.Width, SystemParameters.WorkArea.Width);
            window.Height = Math.Min(window.Height, SystemParameters.WorkArea.Height);
            MainWindow = window; window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Uygulama başlatılamadı. Kayıtlarınızın üzerine yazılmadı.\n\n" + ex.Message + "\n\nKayıt konumu: %LOCALAPPDATA%\\AtelierCafe\\demo-state.json\nÖnceki kayıt: demo-state.json.bak", "Atelier · Başlatma sorunu", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}

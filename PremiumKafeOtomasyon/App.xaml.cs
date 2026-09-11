using System.IO;
using System.Threading;
using System.Windows;
using PremiumKafeOtomasyon.Services;
using PremiumKafeOtomasyon.Data;

namespace PremiumKafeOtomasyon;

public partial class App : Application
{
    // EF tooling discovers this without starting WPF or opening the live database.
    public static Microsoft.Extensions.Hosting.IHostBuilder CreateHostBuilder(string[] args)
        => new Microsoft.Extensions.Hosting.HostBuilder();
    private Mutex? _mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, "Local\\AtelierCafeLocalDemo", out var firstInstance);
        if (!firstInstance) { MessageBox.Show("Atelier zaten açık. Görev çubuğundan mevcut pencereye geçin.", "Atelier"); Shutdown(); return; }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtelierCafe", "demo-state.json");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SqlStateStore store;
            while (true)
            {
                var configuration = DatabaseConfiguration.Load();
                try
                {
                    store = new SqlStateStore(configuration.ConnectionString, path);
                    if (configuration.LegacyImportAllowed)
                    {
                        store.Initialize(); configuration.LegacyImportAllowed = false; configuration.Save();
                    }
                    else { store.MigrateSchema(); store.Load(); }
                    break;
                }
                catch
                {
                    var setup = new PremiumKafeOtomasyon.Views.DatabaseConnectionDialog(configuration,
                        "SQL veritabanı açılamadı. Sunucu erişimi, veritabanı yetkisi ve şema kurulumunu kontrol edin. Dosya kaydına geçilmedi.");
                    setup.ShowDialog();
                    if (!setup.Saved) { Shutdown(); return; }
                }
            }
            var service = new CafeService(store);
            var window = new MainWindow(service);
            window.Width = Math.Min(window.Width, SystemParameters.WorkArea.Width);
            window.Height = Math.Min(window.Height, SystemParameters.WorkArea.Height);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var login = new PremiumKafeOtomasyon.Views.LoginDialog(service); login.ShowDialog();
            if (!login.Authenticated) { Shutdown(); return; }
            MainWindow = window; ShutdownMode = ShutdownMode.OnMainWindowClose; window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Uygulama başlatılamadı.\n\n" + ex.Message + "\n\nBağlantı ayarları: %LOCALAPPDATA%\\AtelierCafe\\database.json\nEski dosya ve aktarım yedekleri korunur. Otomatik JSON kaydına dönüş yapılmaz.", "Atelier · Başlatma sorunu", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}

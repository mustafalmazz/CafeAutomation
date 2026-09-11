using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;
using PremiumKafeOtomasyon.ViewModels;

namespace PremiumKafeOtomasyon.Views;

public sealed class PosDialog : AtelierDialog
{
    public PosDialog(MainViewModel vm, decimal amount = 100) : base("POS çalışma alanı", "TEST ORTAMI · Gerçek cihaz veya banka bağlantısı yoktur.")
    {
        Width = 560;
        var settings = vm.Service.State.Pos;
        var note = new TextBlock { Text = "Test onayları adisyonu kapatmaz ve satış raporlarına girmez. Gerçek ödemeleri mevcut manuel tahsilat ekranından kaydedebilirsiniz.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14), Foreground = Brushes.SaddleBrown };
        Body.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(237,229,216)), CornerRadius = new CornerRadius(10), Child = note });
        var enabled = new CheckBox { Content = "Test simülatörünü etkinleştir", IsChecked = settings.TestEnabled, Margin = new Thickness(0,16,0,8) };
        Body.Children.Add(enabled);
        var name = new TextBox { Text = settings.DeviceName, MaxLength = 80 };
        Body.Children.Add(Label("Cihaz adı")); Body.Children.Add(name);
        var connection = Choice(["Simülatör", "TCP/IP (hazırlık)", "Seri port (hazırlık)"], settings.ConnectionType);
        Body.Children.Add(Label("Planlanan bağlantı türü")); Body.Children.Add(connection);
        var endpoint = new TextBox { Text = settings.Endpoint, MaxLength = 120 };
        Body.Children.Add(Label("IP:port veya COM bilgisi (isteğe bağlı hazırlık notu)")); Body.Children.Add(endpoint);
        Body.Children.Add(Label("Sağlayıcı: Atelier simülatörü · Canlı sağlayıcı henüz kurulmadı."));
        var save = Button("POS ayarlarını kaydet"); Body.Children.Add(save);
        save.Click += (_, _) =>
        {
            try { vm.Service.SavePosSettings(new PosSettings { TestEnabled = enabled.IsChecked == true, DeviceName = name.Text, ConnectionType = (string)connection.SelectedItem, Endpoint = endpoint.Text }); Error.Text = "Test ayarları kaydedildi."; }
            catch (Exception ex) { Error.Text = ex.Message; }
        };
        var ping = Button("Simülatör bağlantısını test et"); ping.Margin = new Thickness(0,8,0,0); Body.Children.Add(ping);
        ping.Click += (_, _) => Error.Text = vm.Service.State.Pos.TestEnabled ? "Simülatör hazır. Fiziksel cihaz, IP veya COM bağlantısı test edilmedi." : "Önce test modunu açıp ayarları kaydedin.";
        Body.Children.Add(Label("TEST ÖDEMESİ · ₺"));
        var value = new TextBox { Text = amount.ToString("0.00", Money.Turkish), MaxLength = 12 }; Body.Children.Add(value);
        var scenarios = Choice(["Onay", "Ret", "Zaman aşımı", "Bağlantı kesintisi"], "Onay");
        Body.Children.Add(Label("Simüle edilecek sonuç")); Body.Children.Add(scenarios);
        var run = Button("Test ödemesi başlat", true); run.Margin = new Thickness(0,12,0,12); Body.Children.Add(run);
        var history = new StackPanel(); Body.Children.Add(Label("TEST İŞLEM GEÇMİŞİ · Son 30 işlem")); Body.Children.Add(history);
        void RefreshHistory()
        {
            history.Children.Clear();
            foreach (var t in vm.Service.State.PosTransactions.OrderByDescending(t => t.At).Take(30))
            {
                var panel = new StackPanel { Margin = new Thickness(12) };
                panel.Children.Add(new TextBlock { Text = t.Summary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(new TextBlock { Text = t.Reference, FontSize = 9, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,5,0,5) });
                panel.Children.Add(new TextBlock { Text = t.Detail, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                if (t.Status == PosStatus.Unknown)
                {
                    var query = Button("Test durumunu sorgula"); query.Margin = new Thickness(0,8,0,0);
                    query.Click += (_, _) => { try { vm.Service.QueryPosTest(t.Id); RefreshHistory(); Error.Text = "Test sonucu sorgulandı. Gerçek tahsilat yok."; } catch (Exception ex) { Error.Text = ex.Message; } };
                    panel.Children.Add(query);
                }
                history.Children.Add(new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1), Child = panel });
            }
            if (history.Children.Count == 0) history.Children.Add(Label("Henüz test işlemi yok."));
        }
        bool busy = false;
        Closing += (_, e) => { if (busy) { e.Cancel = true; Error.Text = "Test sonucu bekleniyor…"; } };
        run.Click += async (_, _) =>
        {
            if (busy) return;
            if (!decimal.TryParse(value.Text, System.Globalization.NumberStyles.AllowDecimalPoint, Money.Turkish, out var total)) { Error.Text = "Geçerli tutar girin (ör. 100,00)."; return; }
            busy = true; run.IsEnabled = false; save.IsEnabled = false;
            try
            {
                var scenario = (PosScenario)scenarios.SelectedIndex;
                var id = vm.Service.BeginPosTest(total, scenario); RefreshHistory(); Error.Text = "Test terminalinden yanıt bekleniyor…";
                var result = await new PosSimulator().ExecuteAsync(scenario);
                vm.Service.CompletePosTest(id, result); Error.Text = result.Detail;
            }
            catch (Exception ex) { Error.Text = ex.Message; }
            finally { busy = false; run.IsEnabled = true; save.IsEnabled = true; RefreshHistory(); }
        };
        RefreshHistory();
        var close = Button("Kapat"); close.Click += (_, _) => Close(); Footer.Children.Add(close);
    }
}

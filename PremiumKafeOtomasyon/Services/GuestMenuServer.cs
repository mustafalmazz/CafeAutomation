using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PremiumKafeOtomasyon.Domain;
using QRCoder;

namespace PremiumKafeOtomasyon.Services;

public sealed partial class GuestMenuServer(CafeService service) : IAsyncDisposable
{
    private WebApplication? _app;
    public string? Address { get; private set; }
    public async Task StartAsync(string address)
    {
        if (_app is not null) throw new InvalidOperationException("Menü sunucusu zaten açık.");
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https" || !IPAddress.TryParse(uri.Host, out var ip) || uri.Port < 1024 || uri.AbsolutePath != "/") throw new InvalidOperationException("Yerel IP ve port girin: http://127.0.0.1:5088");
        var bytes = ip.GetAddressBytes();
        if (!(IPAddress.IsLoopback(ip) || bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31))) throw new InvalidOperationException("Yalnızca yerel ağ IP adresi kullanılabilir.");
        if (!service.CanManage) throw new InvalidOperationException("Yönetici girişi gerekli.");
        var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls(address); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 4096; o.Limits.MaxConcurrentConnections = 64; });
        if (uri.Scheme == "https") { Certificate=GetCertificate(uri.Host); builder.WebHost.UseKestrelHttpsConfiguration(); builder.WebHost.ConfigureKestrel(o=>o.ConfigureHttpsDefaults(h=>h.ServerCertificate=Certificate)); }
        var app = builder.Build();
        ConfigureTerminals(app);
        app.MapGet("/", async () => Results.Content(
            await Application.Current.Dispatcher.InvokeAsync(() => RenderMenu(null)), "text/html; charset=utf-8"));
        app.MapGet("/menu/{token}", async (string token) =>
        {
            var html = await Application.Current.Dispatcher.InvokeAsync(() => RenderMenu(token));
            return html is null ? Results.NotFound() : Results.Content(html, "text/html; charset=utf-8");
        });
        app.MapGet("/photo/{key}", async (string key) =>
        {
            if (!ProductPhotos.Choices.Any(p => p.Key == key)) return Results.NotFound();
            var bytes = await Application.Current.Dispatcher.InvokeAsync(() => { using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/PremiumKafeOtomasyon;component/Assets/Products/{key}.jpg"))!.Stream; using var memory = new System.IO.MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); });
            return Results.Bytes(bytes, "image/jpeg");
        });
        app.MapPost("/request/{token}", async (string token, HttpContext context) =>
        {
            try
            {
                var request = await JsonSerializer.DeserializeAsync<GuestRequest>(context.Request.Body);
                if (request is null) return Results.BadRequest();
                var result = await Application.Current.Dispatcher.InvokeAsync(() => service.SubmitGuestRequest(token, request.Id, request.ProductId, request.Quantity, request.Note, request.Size, request.Milk, request.ExtraShot));
                return Results.Json(new { message = result });
            }
            catch (Exception ex) when (ex is InvalidOperationException or JsonException or ArgumentException) { return Results.BadRequest(new { message = ex is JsonException ? "Geçersiz istek." : ex.Message }); }
        });
        try { await app.StartAsync(); _app = app; Address = address.TrimEnd('/'); }
        catch { await app.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync() { if (_app is null) return; await _app.StopAsync(); await _app.DisposeAsync(); _app = null; Address = null; _sessions.Clear(); }
    public void ExportTableCodes(string folder)
    {
        if (Address is null) throw new InvalidOperationException("Önce menü sunucusunu başlatın.");
        System.IO.Directory.CreateDirectory(folder);
        var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Atelier Masa QR</title><style>body{font:18px Georgia;background:#f7f3ec}article{display:inline-block;width:280px;text-align:center;padding:24px;margin:12px;background:white;break-inside:avoid}img{width:220px}small{font:12px sans-serif;overflow-wrap:anywhere}</style>");
        foreach (var table in service.State.Tables)
        {
            var url = Address + "/menu/" + table.MenuToken;
            var png = PngByteQRCodeHelper.GetQRCode(url, QRCodeGenerator.ECCLevel.Q, 8);
            html.Append($"<article><h2>{WebUtility.HtmlEncode(service.State.BusinessName)}</h2><p>{WebUtility.HtmlEncode(table.Name)}</p><img src='data:image/png;base64,{Convert.ToBase64String(png)}'><p>Menüyü keşfedin</p><small>{WebUtility.HtmlEncode(url)}</small></article>");
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"masa-qr.html"), html.ToString());
    }
    public string? RenderMenu(string? token)
    {
        var table = token is null ? null : service.State.Tables.SingleOrDefault(t => t.MenuToken == token);
        if (token is not null && table is null) return null;
        var html = new StringBuilder("<!doctype html><html lang='tr'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Atelier Menü</title><style>body{margin:0;background:#f7f3ec;color:#302920;font:15px system-ui}header,main{max-width:1000px;margin:auto;padding:24px}h1{font:38px Georgia;margin:12px 0}p{color:#806d5b}#grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px}article{background:white;border:1px solid #e9e0d4;border-radius:16px;overflow:hidden}img{width:100%;height:180px;object-fit:cover}section{padding:18px}h2{margin:0;font-size:19px}button{background:#91613e;color:white;border:0;border-radius:10px;padding:14px;cursor:pointer}button:disabled{opacity:.5}input,textarea{box-sizing:border-box;width:100%;padding:10px;margin:8px 0;border:1px solid #dfd5c7;border-radius:8px}#status{position:sticky;top:0;background:#302920;color:white;padding:16px;display:none}small{display:block;color:#806d5b;margin:10px 0}</style><div id='status' role='status'></div><header><small>ATELIER · MASADAN SİPARİŞ</small>");
        html.Append($"<h1>{WebUtility.HtmlEncode(service.State.BusinessName)}</h1>");
        html.Append(table is null
            ? "<p>Menümüze hoş geldiniz. Sipariş vermek veya garson çağırmak için masanızdaki QR kodu okutun.</p>"
            : $"<p>{WebUtility.HtmlEncode(table.Name)} · Siparişler personel onayından sonra adisyona eklenir.</p><button onclick=\"send(null,1,'Garson çağrısı',this)\">Garson çağır</button>");
        html.Append("</header><main><div id='grid'>");
        foreach (var p in service.State.Products.Where(p => p.Available))
        {
            var id = WebUtility.HtmlEncode(p.Id); var key = ProductPhotos.ResolveKey(p);
            var options = table is not null && p.Category=="Kahveler" ? "<select class='size' aria-label='Boyut'><option>Standart</option><option>Büyük</option></select><select class='milk' aria-label='Süt'><option>Normal süt</option><option>Yulaf sütü</option></select><label><input type='checkbox' class='shot'> Ekstra shot</label><small>Büyük +25 ₺ · Yulaf +30 ₺ · Shot +25 ₺</small>" : "";
            html.Append($"<article data-product='{id}'><img src='/photo/{key}' alt='{WebUtility.HtmlEncode(p.Name)}'><section><small>{WebUtility.HtmlEncode(p.Category)}</small><h2>{WebUtility.HtmlEncode(p.Name)}</h2><p>{WebUtility.HtmlEncode(p.Description)}</p><strong>{Money.Format(p.Price)}</strong><small>Alerjen: {WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(p.Allergens) ? "Bilgi için personele danışın" : p.Allergens)}</small>{options}{(table is null ? "" : "<input aria-label='Adet' type='number' value='1' min='1' max='20'><textarea aria-label='Sipariş notu' maxlength='200' placeholder='Sipariş notu (fiyatlı seçenekler için personele danışın)'></textarea><button onclick=\"order(this)\">Sipariş isteği gönder</button>")}</section></article>");
        }
        html.Append("</div></main><script>function order(b){let a=b.closest('article');send(a.dataset.product,+a.querySelector('input[type=number]').value,a.querySelector('textarea').value,b,a.querySelector('.size')?.value||'Standart',a.querySelector('.milk')?.value||'Normal süt',a.querySelector('.shot')?.checked||false)} async function send(product,quantity,note,b,size='Standart',milk='Normal süt',shot=false){b.disabled=true;let status=document.getElementById('status');status.style.display='block';status.textContent='Gönderiliyor…';let payload=b.pending||(b.pending={Id:Array.from(crypto.getRandomValues(new Uint8Array(16)),x=>x.toString(16).padStart(2,'0')).join(''),ProductId:product,Quantity:quantity,Note:note,Size:size,Milk:milk,ExtraShot:shot});try{let r=await fetch('/request/'+location.pathname.split('/').pop(),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});let j=await r.json();status.textContent=j.message;if(r.ok){b.pending=null;b.textContent='İstek alındı';}else{b.pending=null;b.disabled=false}}catch{status.textContent='Bağlantı kesildi. Aynı isteği tekrar deneyebilirsiniz.';b.disabled=false}}</script></html>");
        return html.ToString();
    }
}

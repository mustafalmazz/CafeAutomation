using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;
public sealed partial class GuestMenuServer
{
    private readonly ConcurrentDictionary<string,(string Employee,DateTimeOffset Until)> _sessions = new();
    public X509Certificate2? Certificate { get; private set; }
    private X509Certificate2 GetCertificate(string host)
    {
        using var store=new X509Store(StoreName.My,StoreLocation.CurrentUser); store.Open(OpenFlags.ReadWrite);
        var subject="CN=Atelier Local "+host;
        var existing=store.Certificates.Cast<X509Certificate2>().FirstOrDefault(c=>c.Subject==subject && c.HasPrivateKey && c.NotAfter>DateTime.Now.AddDays(7));
        if(existing!=null) return existing;
        using var rsa=RSA.Create(2048);
        var request=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Parse(host)); request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },false));
        using var generated=request.CreateSelfSigned(DateTimeOffset.Now.AddMinutes(-5),DateTimeOffset.Now.AddYears(1));
        var cert=new X509Certificate2(generated.Export(X509ContentType.Pfx),(string?)null,X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        store.Add(cert); return cert;
    }
    public void ExportCertificate(string path)
    {
        if(Certificate==null) throw new InvalidOperationException("Önce HTTPS sunucusu başlatın.");
        System.IO.File.WriteAllBytes(path,Certificate.Export(X509ContentType.Cert));
    }
    private string EmployeeFor(HttpContext context)
    {
        if(!context.Request.IsHttps) throw new InvalidOperationException("Personel terminali HTTPS gerektirir.");
        if(!context.Request.Cookies.TryGetValue("atelier-session",out var token) || !_sessions.TryGetValue(token,out var session) || session.Until<DateTimeOffset.Now) throw new InvalidOperationException("Personel girişi gerekli.");
        return session.Employee;
    }
    private sealed record LoginRequest(string EmployeeId,string Pin);
    private sealed record TerminalRequest(string Id,string Operation,string TableId,string? ProductId,decimal Amount,string? OrderId);
    private void ConfigureTerminals(WebApplication app)
    {
        app.MapGet("/terminal", (HttpContext c) => c.Request.IsHttps ? Results.Content(TerminalHtml,"text/html; charset=utf-8") : Results.BadRequest("Personel terminali için HTTPS adresini kullanın."));
        app.MapGet("/staff/users",async(HttpContext c)=>
        {
            if(!c.Request.IsHttps) return Results.BadRequest();
            var users=await Application.Current.Dispatcher.InvokeAsync(()=>service.State.Employees.Where(e=>e.Active).Select(e=>new {e.Id,e.Name}).ToArray());
            return Results.Json(users);
        });
        app.MapPost("/staff/login",async(HttpContext c)=>
        {
            if(!c.Request.IsHttps) return Results.BadRequest();
            try
            {
                var body=await JsonSerializer.DeserializeAsync<LoginRequest>(c.Request.Body) ?? throw new InvalidOperationException("Giriş bilgisi gerekli.");
                if(body.Pin is null || body.Pin.Length>12) return Results.BadRequest();
                var employee=await Application.Current.Dispatcher.InvokeAsync(()=>service.AuthenticateTerminal(body.EmployeeId,body.Pin));
                foreach(var item in _sessions.Where(s=>s.Value.Until<DateTimeOffset.Now)) _sessions.TryRemove(item.Key,out _);
                if(_sessions.Count>=64) throw new InvalidOperationException("Terminal oturum sınırı dolu.");
                var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); _sessions[token]=(employee,DateTimeOffset.Now.AddHours(8));
                c.Response.Cookies.Append("atelier-session",token,new CookieOptions {HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,MaxAge=TimeSpan.FromHours(8),Path="/"});
                return Results.Json(new {message="Giriş başarılı."});
            }
            catch(Exception ex) when(ex is InvalidOperationException or JsonException) {return Results.BadRequest(new {message=ex is JsonException?"Geçersiz giriş":ex.Message});}
        });
        app.MapPost("/staff/logout",(HttpContext c)=> {if(c.Request.Cookies.TryGetValue("atelier-session",out var token)) _sessions.TryRemove(token,out _);c.Response.Cookies.Delete("atelier-session");return Results.Ok();});
        app.MapGet("/staff/state",async(HttpContext c)=>
        {
            try
            {
                var employee=EmployeeFor(c);
                var result=await Application.Current.Dispatcher.InvokeAsync(()=>service.AsEmployee(employee,()=>new { employee=service.CurrentEmployee!.Name, role=service.CurrentEmployee.Role, tables=service.State.Tables.Select(t=>new {t.Id,t.Name,order=service.OpenOrder(t.Id) is { } o ? new {o.Id,o.Number,o.Remaining,lines=o.Lines.Select(l=>new {l.Name,l.Quantity,l.Status,l.Total}).ToArray()}:null}).ToArray(),products=service.State.Products.Where(p=>p.Available).Select(p=>new {p.Id,p.Name,p.Price}).ToArray(),shift=service.ActiveShift!=null }));
                return Results.Json(result);
            }
            catch(InvalidOperationException ex){return Results.Json(new {message=ex.Message},statusCode:401);}
        });
        app.MapPost("/staff/action",async(HttpContext c)=>
        {
            try
            {
                var employee=EmployeeFor(c);
                var body=await JsonSerializer.DeserializeAsync<TerminalRequest>(c.Request.Body) ?? throw new InvalidOperationException("İşlem gerekli.");
                await Application.Current.Dispatcher.InvokeAsync(()=>service.AsEmployee(employee,()=> {service.TerminalAction(body.Id,body.Operation,body.TableId,body.ProductId,body.Amount,body.OrderId);return true;}));
                return Results.Json(new {message="İşlem kaydedildi."});
            }
            catch(Exception ex) when(ex is InvalidOperationException or JsonException){return Results.BadRequest(new {message=ex is JsonException?"Geçersiz işlem":ex.Message});}
        });
    }
    private const string TerminalHtml="""
<!doctype html><html lang="tr"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Atelier Terminal</title>
<style>body{margin:0;background:#f6f2eb;color:#302920;font:15px system-ui}main{max-width:1000px;margin:30px auto;padding:20px}h1{font:36px Georgia}button,input,select{font:inherit;padding:13px;border-radius:9px;border:1px solid #ded4c8;margin:5px}button{cursor:pointer;background:#91613e;color:white}button:disabled{opacity:.5}section{background:white;border-radius:16px;padding:20px;margin:15px 0}#products{display:flex;flex-wrap:wrap}#status{padding:14px;background:#e9dfd0}small{color:#7d6b59}.line{display:flex;justify-content:space-between;padding:10px;border-bottom:1px solid #eee}</style>
<main><h1>Atelier · Terminal</h1><p id="status" role="status">Personel girişi yapın.</p><section id="login"><select id="employee" aria-label="Personel"></select><input id="pin" type="password" inputmode="numeric" maxlength="12" placeholder="PIN"><button onclick="login()">Giriş</button></section>
<div id="work" hidden><small id="who"></small><button onclick="logout()">Çıkış</button><section><select id="table" aria-label="Masa" onchange="render()"></select><button onclick="refresh()">Yenile</button><div id="lines"></div><h2 id="total"></h2><button onclick="action('send',null,this)">Hazırlığa gönder</button><div id="pay"><input id="amount" type="number" step="0.01" min="0.01" placeholder="Tahsilat tutarı"><button onclick="action('cash',null,this)">Nakit kaydet</button><button onclick="action('card',null,this)">Manuel kart kaydet</button><p>Kart kaydı cihazdan para çekmez.</p></div></section><section><h2>Ürünler</h2><div id="products"></div></section></div></main>
<script>let state=null,busy=false;const $=id=>document.getElementById(id);const say=m=>$('status').textContent=m;async function login(){let r=await fetch('/staff/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({EmployeeId:$('employee').value,Pin:$('pin').value})});$('pin').value='';let j=await r.json();say(j.message);if(r.ok)await refresh()}async function logout(){await fetch('/staff/logout',{method:'POST'});state=null;$('work').hidden=true;$('login').hidden=false}async function refresh(){try{let r=await fetch('/staff/state');let j=await r.json();if(!r.ok){say(j.message);$('work').hidden=true;$('login').hidden=false;return}state=j;let selected=$('table').value;$('table').replaceChildren(...state.tables.map(t=>new Option(t.name,t.id)));if(state.tables.some(t=>t.id===selected))$('table').value=selected;$('who').textContent=state.employee+' · '+state.role;$('work').hidden=false;$('login').hidden=true;$('pay').hidden=state.role==='Garson';render()}catch{say('Sunucuya ulaşılamıyor. Yeni işlem göndermeden bağlantıyı kontrol edin.')}}function render(){let t=state.tables.find(t=>t.id===$('table').value);$('lines').replaceChildren();for(let l of t.order?.lines||[]){let el=document.createElement('div');el.className='line';el.textContent=l.quantity+' × '+l.name+' · '+l.status+' · '+l.total.toFixed(2)+' ₺';$('lines').append(el)}$('total').textContent='Kalan: '+(t.order?.remaining||0).toFixed(2)+' ₺';$('products').replaceChildren(...state.products.map(p=>{let b=document.createElement('button');b.textContent=p.name+' · '+p.price.toFixed(2)+' ₺';b.onclick=()=>action('add',p.id,b);return b}))}async function action(op,product,b){if(busy)return;let t=state.tables.find(t=>t.id===$('table').value);let key='atelier-pending';let pending=sessionStorage.getItem(key);let payload=pending?JSON.parse(pending):{Id:crypto.randomUUID().replaceAll('-',''),Operation:op,TableId:t.id,ProductId:product,Amount:Number($('amount').value)||0,OrderId:t.order?.id};if(pending&&payload.Operation!==op){say('Önce sonucu belirsiz işlemi aynı düğmeyle tekrar sorgulayın.');return}sessionStorage.setItem(key,JSON.stringify(payload));busy=true;b.disabled=true;try{let r=await fetch('/staff/action',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});let j=await r.json();say(j.message);sessionStorage.removeItem(key);await refresh()}catch{say('Sonuç belirsiz. Aynı işlemi tekrar deneyin; işlem numarası korunur.')}finally{busy=false;b.disabled=false}}fetch('/staff/users').then(r=>r.json()).then(users=>$('employee').replaceChildren(...users.map(u=>new Option(u.name,u.id))));setInterval(()=>{if(state&&!busy)refresh()},5000);</script></html>
""";
}

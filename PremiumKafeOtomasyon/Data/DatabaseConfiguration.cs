using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PremiumKafeOtomasyon.Data;
public sealed class DatabaseConfiguration
{
    public string Server {get;set;} = @".\SQLEXPRESS";
    public string Database {get;set;} = "AtelierCafe";
    public bool WindowsAuthentication {get;set;} = true;
    public string UserName {get;set;} = "";
    public string ProtectedPassword {get;set;} = "";
    public bool TrustServerCertificate {get;set;} = true;
    public bool LegacyImportAllowed {get;set;} = true;
    public static string ConfigurationPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AtelierCafe","database.json");
    public static DatabaseConfiguration Load() => File.Exists(ConfigurationPath) ? JsonSerializer.Deserialize<DatabaseConfiguration>(File.ReadAllText(ConfigurationPath)) ?? throw new InvalidDataException("SQL bağlantı ayarları okunamadı.") : new();
    public string ConnectionString
    {
        get
        {
            if(string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database) || Database.Length>128 || new[]{"master","model","msdb","tempdb"}.Contains(Database.Trim(),StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Uygulamaya özel bir SQL Server ve veritabanı girin.");
            var builder=new SqlConnectionStringBuilder { DataSource=Server.Trim(),InitialCatalog=Database.Trim(),IntegratedSecurity=WindowsAuthentication,Encrypt=true,TrustServerCertificate=TrustServerCertificate,ConnectTimeout=10,ApplicationName="AtelierCafe",PersistSecurityInfo=false };
            if(!WindowsAuthentication) { builder.UserID=UserName; builder.Password=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword),null,DataProtectionScope.CurrentUser)); }
            return builder.ConnectionString;
        }
    }
    public void SetPassword(string password) => ProtectedPassword=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.CurrentUser));
    public void TestConnection()
    {
        var builder=new SqlConnectionStringBuilder(ConnectionString) {InitialCatalog="master"};
        using var connection=new SqlConnection(builder.ConnectionString);connection.Open();
        using var command=new SqlCommand("SELECT 1",connection);command.ExecuteScalar();
    }
    public void Save()
    {
        _=ConnectionString;
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigurationPath)!);
        var temporary=ConfigurationPath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(this,new JsonSerializerOptions {WriteIndented=true}));File.Move(temporary,ConfigurationPath,true);
    }
}

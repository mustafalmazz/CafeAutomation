using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PremiumKafeOtomasyon.Data;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed class SqlStateStore(string connectionString, string? legacyPath = null) : IStateStore
{
    private long? _revision;
    public string FilePath { get; } = Describe(connectionString);
    public string? MigrationBackup { get; private set; }
    private static string Describe(string connection)
    {
        var builder=new SqlConnectionStringBuilder(connection);
        if(string.IsNullOrWhiteSpace(builder.InitialCatalog) || new[]{"master","model","msdb","tempdb"}.Contains(builder.InitialCatalog,StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Sistem veritabanları Atelier için kullanılamaz.");
        return "SQL Server · " + builder.DataSource + " / " + builder.InitialCatalog;
    }
    private CafeDbContext Open() => new(connectionString);
    public void MigrateSchema() { using var db=Open(); db.Database.Migrate(); }
    public string CreateNativeBackup()
    {
        var builder=new SqlConnectionStringBuilder(connectionString);
        using var connection=new SqlConnection(connectionString);connection.Open();
        using var folderCommand=new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))",connection);
        var folder=Convert.ToString(folderCommand.ExecuteScalar());
        if(string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("SQL Server varsayılan yedek dizini bulunamadı.");
        var name=System.Text.RegularExpressions.Regex.Replace(builder.InitialCatalog,"[^a-zA-Z0-9_.-]","_");
        var path=System.IO.Path.Combine(folder,name+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]+".bak");
        var database="["+builder.InitialCatalog.Replace("]","]]")+"]";
        using var command=new SqlCommand("BACKUP DATABASE "+database+" TO DISK=@path WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;",connection) {CommandTimeout=120};
        command.Parameters.Add("@path",SqlDbType.NVarChar,4000).Value=path;command.ExecuteNonQuery();return path;
    }
    public CafeState Load()
    {
        using var db=Open();
        using var transaction=db.Database.BeginTransaction(IsolationLevel.Serializable);
        var settings=db.Set<DatabaseSettings>().SingleOrDefault();
        if(settings is null) throw new InvalidOperationException("SQL veritabanı henüz başlatılmadı. Veritabanı kurulumunu çalıştırın.");
        var state=ReadState(db,settings);
        StateStore.Validate(state);
        transaction.Commit(); _revision=settings.Revision;
        return state;
    }
    public CafeState Initialize()
    {
        MigrateSchema();
        using var db=Open();
        using var transaction=db.Database.BeginTransaction(IsolationLevel.Serializable);
        // Serialize first-run import without overwriting an already initialized database.
        db.Database.ExecuteSqlRaw("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=N'AtelierInitialImport', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000; IF @result < 0 THROW 51000, 'Database initialization lock failed', 1;");
        if(db.Set<DatabaseSettings>().Any()) { transaction.Commit(); return Load(); }
        CafeState state;
        if(legacyPath is not null && System.IO.File.Exists(legacyPath))
        {
            var directory=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(legacyPath))!,"migration-backups");
            System.IO.Directory.CreateDirectory(directory);
            MigrationBackup=System.IO.Path.Combine(directory,"before-sql-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");
            System.IO.File.Copy(legacyPath,MigrationBackup,false);
            state=new StateStore(MigrationBackup).Load();
        }
        else state=DemoData.Create();
        StateStore.Validate(state);
        var settings=new DatabaseSettings {Revision=1,ImportHash=Fingerprint(state)};
        ApplySettings(settings,state);db.Add(settings);SyncState(db,state);
        db.SaveChanges();
        // Verify every scalar/list after SQL round-trip before committing the import.
        db.ChangeTracker.Clear();
        var reread=ReadState(db,db.Set<DatabaseSettings>().Single());
        if(Fingerprint(reread)!=Fingerprint(state)) throw new InvalidOperationException("SQL aktarım doğrulaması başarısız; işlem geri alındı, eski dosya korundu.");
        transaction.Commit();_revision=1;
        return reread;
    }
    public void Save(CafeState state)
    {
        StateStore.Validate(state);
        if(_revision is null) throw new InvalidOperationException("SQL kaydı öncesinde veriler yüklenmeli.");
        try
        {
            using var db=Open(); using var transaction=db.Database.BeginTransaction(IsolationLevel.ReadCommitted);
            var expected=_revision.Value;
            var updated=db.Database.ExecuteSqlInterpolated($"UPDATE dbo.Settings SET Revision=Revision+1 WHERE Id=1 AND Revision={expected}");
            if(updated!=1) throw new InvalidOperationException("Veriler başka bir uygulama tarafından değiştirildi. Eski verilerin üzerine yazılmadı; uygulamayı yeniden açın.");
            var settings=db.Set<DatabaseSettings>().Single(); ApplySettings(settings,state);
            SyncState(db,state);db.SaveChanges(); transaction.Commit();_revision=expected+1;
        }
        catch(Exception ex) when(ex is SqlException or DbUpdateException)
        {
            throw new InvalidOperationException("SQL Server kaydı tamamlanamadı. İşlem onaylanmadı; bağlantıyı kontrol edip yeniden deneyin. Sonuç belirsizse yeniden başlatıp kayıtları kontrol edin.",ex);
        }
    }
    private static void ApplySettings(DatabaseSettings settings,CafeState state)
    {
        settings.StateVersion=state.Version;settings.BusinessName=state.BusinessName;settings.IsDemo=state.IsDemo;
        settings.PosTestEnabled=state.Pos.TestEnabled;settings.PosDeviceName=state.Pos.DeviceName;
        settings.PosConnectionType=state.Pos.ConnectionType;settings.PosEndpoint=state.Pos.Endpoint;
        settings.ReceiptPrinter=state.ReceiptPrinter;settings.ReceiptPaper=state.ReceiptPaper;
    }
    private static List<T> Read<T>(CafeDbContext db) where T:class => db.Set<T>().OrderBy(x=>EF.Property<int>(x,"SortOrder")).ToList();
    private static CafeState ReadState(CafeDbContext db,DatabaseSettings settings)
    {
        var state=new CafeState {Version=settings.StateVersion,BusinessName=settings.BusinessName,IsDemo=settings.IsDemo,ReceiptPrinter=settings.ReceiptPrinter,ReceiptPaper=settings.ReceiptPaper,
            Pos=new PosSettings {TestEnabled=settings.PosTestEnabled,DeviceName=settings.PosDeviceName,ConnectionType=settings.PosConnectionType,Endpoint=settings.PosEndpoint},
            Products=Read<Product>(db),Tables=Read<CafeTable>(db),Customers=Read<Customer>(db),Coupons=Read<Coupon>(db),
            Orders=Read<Order>(db),Employees=Read<Employee>(db),Shifts=Read<CashShift>(db),Ingredients=Read<Ingredient>(db),
            Recipes=Read<RecipePart>(db),StockEntries=Read<StockEntry>(db),Refunds=Read<RefundEntry>(db),
            GuestRequests=Read<GuestRequest>(db),PosTransactions=Read<PosTransaction>(db),Audit=Read<AuditEntry>(db),TerminalOperations=Read<TerminalOperationRecord>(db).Select(x=>x.Id).ToList()};
        // EF relationship fix-up attaches children to the already tracked aggregate roots.
        Read<OrderLine>(db);Read<Payment>(db);Read<CashMovement>(db);
        return state;
    }
    private static void SyncState(CafeDbContext db,CafeState state)
    {
        Sync(db,state.Products);Sync(db,state.Tables);Sync(db,state.Customers);Sync(db,state.Coupons);Sync(db,state.Orders);
        var lines=state.Orders.SelectMany(o=>o.Lines.Select(l=>(Parent:o.Id,Value:l))).ToList();
        var payments=state.Orders.SelectMany(o=>o.Payments.Select(p=>(Parent:o.Id,Value:p))).ToList();
        var movements=state.Shifts.SelectMany(s=>s.Movements.Select(m=>(Parent:s.Id,Value:m))).ToList();
        var lineParents=lines.ToDictionary(x=>x.Value.Id,x=>x.Parent);var paymentParents=payments.ToDictionary(x=>x.Value.Id,x=>x.Parent);var movementParents=movements.ToDictionary(x=>x.Value.Id,x=>x.Parent);
        Sync(db,lines.Select(x=>x.Value),(e,v)=>e.Property("OrderId").CurrentValue=lineParents[v.Id]);
        Sync(db,payments.Select(x=>x.Value),(e,v)=>e.Property("OrderId").CurrentValue=paymentParents[v.Id]);
        Sync(db,state.Employees);Sync(db,state.Shifts);
        Sync(db,movements.Select(x=>x.Value),(e,v)=>e.Property("ShiftId").CurrentValue=movementParents[v.Id]);
        Sync(db,state.Ingredients);Sync(db,state.Recipes);Sync(db,state.StockEntries);Sync(db,state.Refunds);Sync(db,state.GuestRequests);Sync(db,state.PosTransactions);Sync(db,state.Audit);
        Sync(db,state.TerminalOperations.Select(id=>new TerminalOperationRecord {Id=id}));
    }
    private static void Sync<T>(CafeDbContext db,IEnumerable<T> values,Action<EntityEntry<T>,T>? relationships=null) where T:class,new()
    {
        var keys=db.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties;
        string Key(T entity)=>JsonSerializer.Serialize(keys.Select(k=>k.PropertyInfo!.GetValue(entity)).ToArray());
        var existing=db.Set<T>().ToList().ToDictionary(Key);
        var retained=new HashSet<string>();var index=0;
        foreach(var value in values)
        {
            var key=Key(value);if(!retained.Add(key)) throw new InvalidOperationException("Tekrarlanan SQL kayıt anahtarı.");
            if(!existing.TryGetValue(key,out var target)) {target=new T();db.Entry(target).CurrentValues.SetValues(value);db.Set<T>().Add(target);}
            else db.Entry(target).CurrentValues.SetValues(value);
            var entry=db.Entry(target);
            foreach(var property in entry.Properties)
                if(property.CurrentValue is decimal number && (decimal.Round(number,8)!=number || number<=-100000000000000000000m || number>=100000000000000000000m))
                    throw new InvalidOperationException("Sayısal değer SQL kayıt sınırını aşıyor; en fazla 8 ondalık basamak kullanın. İşlem kaydedilmedi.");
            entry.Property("SortOrder").CurrentValue=index++;relationships?.Invoke(entry,value);
        }
        foreach(var pair in existing.Where(p=>!retained.Contains(p.Key))) db.Remove(pair.Value);
    }
    private sealed class CanonicalDecimal : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer,decimal value,JsonSerializerOptions options)=>writer.WriteRawValue(value.ToString("G29",CultureInfo.InvariantCulture));
    }
    public static string Fingerprint(CafeState state)
    {
        var options=new JsonSerializerOptions();options.Converters.Add(new CanonicalDecimal());
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state,options)));
    }
}

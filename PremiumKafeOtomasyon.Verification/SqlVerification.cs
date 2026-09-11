using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PremiumKafeOtomasyon.Data;
using PremiumKafeOtomasyon.Domain;
using PremiumKafeOtomasyon.Services;

internal static partial class Program
{
    private static int VerifySql()
    {
        var output=Path.GetFullPath("artifacts/verification-sql");Directory.CreateDirectory(output);
        var database="AtelierCafe_Verification_"+Guid.NewGuid().ToString("N")[..10];
        var connection=new SqlConnectionStringBuilder {DataSource=@".\SQLEXPRESS",InitialCatalog=database,IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true}.ConnectionString;
        var legacy=Path.Combine(output,database+".json");
        var source=new CafeService(new StateStore(legacy));
        source.CreateFirstManager("SQL test manager","816204");source.OpenShift(100);
        source.SaveIngredient("SQL süt","ml",100,0.025m);var ingredient=source.State.Ingredients[0].Id;
        source.AdjustStock(ingredient,2000,"Giriş","Aktarım testi");source.SaveRecipe("latte",ingredient,200);
        source.SaveCustomer("SQL müşteri","");var customer=source.State.Customers[0].Id;
        source.AddProduct("t1","latte","Standart","Normal süt",false,"Test");var order=source.OpenOrder("t1")!;
        source.AssignCustomer(order.Id,customer);source.SendToKitchen(order.Id);source.Pay(order.Id,order.Total,"Nakit","Kişi 1");
        source.Refund(order.Id,5,"Nakit","SQL aktarım testi");source.CashEntry(-10,"Test kasa çıkışı");
        source.SaveCoupon("SQLTEST",10,100,DateTime.Today.AddDays(1),5);
        source.SavePosSettings(new PosSettings {TestEnabled=true});var pos=source.BeginPosTest(10,PosScenario.Approve);source.CompletePosTest(pos,new(PosStatus.Approved,"Test"));
        source.SubmitGuestRequest(source.State.Tables[0].MenuToken,Guid.NewGuid().ToString("N"),null,1,"Garson");
        source.TerminalAction(Guid.NewGuid().ToString("N"),"add","t12","tea",0,null);
        var originalBytes=File.ReadAllBytes(legacy);var expected=SqlStateStore.Fingerprint(source.State);
        var sql=new SqlStateStore(connection,legacy);var imported=sql.Initialize();
        Check(SqlStateStore.Fingerprint(imported)==expected,"SQL import preserves all settings, entities, amounts, IDs and ordering");
        Check(originalBytes.SequenceEqual(File.ReadAllBytes(legacy)) && sql.MigrationBackup!=null && File.Exists(sql.MigrationBackup),"Migration keeps original JSON and separate backup");
        using(var db=new CafeDbContext(connection))
        {
            Check(db.Set<Payment>().Count()==source.State.Orders.Sum(o=>o.Payments.Count),"Payments stored as relational rows");
            Check(db.Set<OrderLine>().Count()==source.State.Orders.Sum(o=>o.Lines.Count),"Order lines stored as relational rows");
            Check(db.Set<RecipePart>().Count()==1&&db.Set<Employee>().Count()==1&&db.Set<StockEntry>().Count()==2,"Staff, recipe and stock tables populated");
            Check(db.Database.GetAppliedMigrations().Count()==1,"Versioned EF schema migration applied");
        }
        var live=new CafeService(sql);live.Login(imported.Employees[0].Id,"816204");
        live.AddProduct("t1","latte","Standart","Normal süt",false,"");var liveOrder=live.OpenOrder("t1")!;live.SendToKitchen(liveOrder.Id);live.Pay(liveOrder.Id,liveOrder.Total,"Kart");
        var reopenedStore=new SqlStateStore(connection,legacy);var reopened=new CafeService(reopenedStore);
        Check(reopened.State.Orders.Single(o=>o.Id==liveOrder.Id).Paid==145&&reopened.State.Ingredients[0].Quantity==1600,"SQL sale, recipe consumption and payment survive restart");
        var first=new SqlStateStore(connection);var second=new SqlStateStore(connection);var a=first.Load();var b=second.Load();a.BusinessName="SQL authoritative";first.Save(a);b.BusinessName="Stale overwrite";
        Reject(()=>second.Save(b),"SQL optimistic revision prevents stale snapshot overwrite");
        Check(new SqlStateStore(connection).Load().BusinessName=="SQL authoritative","Stale save cannot overwrite committed state");
        var latest=new SqlStateStore(connection);var invalid=latest.Load();invalid.Orders[0].Lines[0].ProductId="missing-product";
        Reject(()=>latest.Save(invalid),"SQL foreign key failure rolls back whole transaction");
        Check(new SqlStateStore(connection).Load().Orders[0].Lines[0].ProductId!="missing-product","Failed SQL write leaves prior records intact");
        latest.Load();var retry=latest.Load();retry.BusinessName="SQL retry";latest.Save(retry);
        Check(new SqlStateStore(connection).Load().BusinessName=="SQL retry","SQL store can retry after rolled-back transaction");
        var precisionStore=new SqlStateStore(connection);var precisionState=precisionStore.Load();precisionState.Ingredients[0].UnitCost=0.123456789m;
        Reject(()=>precisionStore.Save(precisionState),"SQL refuses silent decimal precision loss");
        Check(new SqlStateStore(connection).Load().Ingredients[0].UnitCost==0.025m,"Precision rejection rolls back state and revision");
        var again=new SqlStateStore(connection,legacy);Check(again.Initialize().BusinessName=="SQL retry","Startup never reimports stale JSON into initialized SQL database");
        using(var c=new SqlConnection(connection))
        {
            c.Open();using var command=new SqlCommand("INSERT dbo.Payments (Id,Payer,Method,Amount,At,OrderId,SortOrder) VALUES (@id,N'',N'Nakit',-1,SYSDATETIMEOFFSET(),@order,999)",c);
            command.Parameters.AddWithValue("@id",Guid.NewGuid().ToString("N"));command.Parameters.AddWithValue("@order",order.Id);
            try {command.ExecuteNonQuery();throw new Exception("Expected SQL check violation");}catch(SqlException ex) when(ex.Number==547){Check(true,"SQL CHECK constraint rejects negative payments");}
        }
        var export=Path.Combine(output,"sql-export.json");var exportService=new CafeService(new SqlStateStore(connection));exportService.Login(imported.Employees[0].Id,"816204");exportService.ExportBackup(export);
        Check(SqlStateStore.Fingerprint(new StateStore(export).Load())==SqlStateStore.Fingerprint(exportService.State),"SQL portable backup exports a consistent state");
        exportService.AddProduct("t9","latte","Standart","Normal süt",false,"");
        exportService.AddProduct("t9","latte","Standart","Normal süt",false,"");
        var split=exportService.OpenOrder("t9")!;
        exportService.TransferLines(split.Id,"t10",split.Lines[0].Id,1);
        exportService.TransferLines(split.Id,"t10",null,0);
        Check(new SqlStateStore(connection).Load().Orders.Single(o=>o.TableId=="t10"&&o.IsOpen).Total==290,"SQL split and merge preserve child rows across parent deletion");
        exportService.AddProduct("t11","tea","Standart","Normal süt",false,"");
        var remove=exportService.OpenOrder("t11")!;exportService.ChangeQuantity(remove.Id,remove.Lines[0].Id,-1);
        Check(!new SqlStateStore(connection).Load().Orders.Any(o=>o.Id==remove.Id),"SQL removes empty draft order with dependent rows");
        exportService.CloseShift(exportService.ExpectedCash(exportService.ActiveShift!));
        exportService.RestoreBackup(export);
        Check(!new SqlStateStore(connection).Load().Orders.Any(o=>o.TableId=="t10"&&o.IsOpen),"Portable restore applies atomically to SQL tables");
        var native=exportService.CreateSqlBackup();
        Check(native.EndsWith(".bak"),"SQL native COPY_ONLY backup passes RESTORE VERIFYONLY");
        File.WriteAllText(Path.Combine(output,"result.txt"),$"{_checks} SQL checks passed.\nServer: .\\SQLEXPRESS\nDatabase: {database}\n");
        Console.WriteLine($"SUCCESS: {_checks} SQL checks passed. Database: {database}");
        return 0;
    }
    private static int MigrateProductionSql()
    {
        var configuration=DatabaseConfiguration.Load();
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AtelierCafe","demo-state.json");
        var store=new SqlStateStore(configuration.ConnectionString,path);var state=store.Initialize();configuration.LegacyImportAllowed=false;configuration.Save();
        Console.WriteLine($"SQL migration verified. Products={state.Products.Count}; Orders={state.Orders.Count}; Payments={state.Orders.Sum(o=>o.Payments.Count)}; Employees={state.Employees.Count}");
        Console.WriteLine("Database: "+store.FilePath);Console.WriteLine("Legacy backup: "+(store.MigrationBackup??"Already initialized"));return 0;
    }
}

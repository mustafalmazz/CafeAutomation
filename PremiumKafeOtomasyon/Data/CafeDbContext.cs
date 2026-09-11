using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Data;

public sealed class DatabaseSettings
{
    public int Id { get; set; } = 1;
    public long Revision { get; set; }
    public int StateVersion { get; set; } = 1;
    public string BusinessName { get; set; } = "";
    public bool IsDemo { get; set; }
    public bool PosTestEnabled { get; set; }
    public string PosDeviceName { get; set; } = "";
    public string PosConnectionType { get; set; } = "";
    public string PosEndpoint { get; set; } = "";
    public string ReceiptPrinter { get; set; } = "";
    public string ReceiptPaper { get; set; } = "80 mm";
    public DateTimeOffset InitializedAt { get; set; } = DateTimeOffset.Now;
    public string ImportHash { get; set; } = "";
}
public sealed class TerminalOperationRecord
{
    public string Id { get; set; } = "";
}

public sealed class CafeDbContext(string connectionString) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlServer(connectionString, sql => sql.CommandTimeout(30));
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DatabaseSettings>().ToTable("Settings", t=>t.HasCheckConstraint("CK_Settings_Singleton","[Id] = 1"));
        model.Entity<DatabaseSettings>().Property(s=>s.Id).ValueGeneratedNever();
        model.Entity<DatabaseSettings>().Property(s=>s.Revision).IsConcurrencyToken();
        Map<Product>(model,"Products"); Map<CafeTable>(model,"CafeTables"); Map<Order>(model,"Orders");
        Map<OrderLine>(model,"OrderLines"); Map<Payment>(model,"Payments"); Map<Employee>(model,"Employees");
        Map<CashShift>(model,"CashShifts"); Map<CashMovement>(model,"CashMovements");
        Map<Ingredient>(model,"Ingredients"); Map<RecipePart>(model,"RecipeParts"); Map<StockEntry>(model,"StockEntries");
        Map<Customer>(model,"Customers"); Map<Coupon>(model,"Coupons"); Map<RefundEntry>(model,"Refunds");
        Map<GuestRequest>(model,"GuestRequests"); Map<PosTransaction>(model,"PosTransactions");
        Map<AuditEntry>(model,"AuditEntries"); Map<TerminalOperationRecord>(model,"TerminalOperations");
        model.Entity<RecipePart>().HasKey(r=>new { r.ProductId,r.IngredientId,r.Variant });
        model.Entity<RecipePart>().Property(r=>r.Variant).HasMaxLength(120);
        model.Entity<Order>().HasOne<CafeTable>().WithMany().HasForeignKey(o=>o.TableId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Order>().HasOne<Customer>().WithMany().HasForeignKey(o=>o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Order>().HasOne<Coupon>().WithMany().HasForeignKey(o=>o.CouponId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Order>().HasMany(o=>o.Lines).WithOne().HasForeignKey("OrderId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        model.Entity<Order>().HasMany(o=>o.Payments).WithOne().HasForeignKey("OrderId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        model.Entity<OrderLine>().HasOne<Product>().WithMany().HasForeignKey(l=>l.ProductId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CashShift>().HasMany(s=>s.Movements).WithOne().HasForeignKey("ShiftId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        model.Entity<RecipePart>().HasOne<Product>().WithMany().HasForeignKey(r=>r.ProductId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RecipePart>().HasOne<Ingredient>().WithMany().HasForeignKey(r=>r.IngredientId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockEntry>().HasOne<Ingredient>().WithMany().HasForeignKey(r=>r.IngredientId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RefundEntry>().HasOne<Order>().WithMany().HasForeignKey(r=>r.OrderId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<GuestRequest>().HasOne<CafeTable>().WithMany().HasForeignKey(r=>r.TableId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<GuestRequest>().HasOne<Product>().WithMany().HasForeignKey(r=>r.ProductId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Order>().HasIndex(o=>o.TableId).IsUnique().HasFilter("[ClosedAt] IS NULL");
        model.Entity<Order>().HasIndex(o=>o.Number).IsUnique();
        model.Entity<Order>().HasIndex(o=>o.ClosedAt);
        model.Entity<CafeTable>().HasIndex(t=>t.MenuToken).IsUnique();
        model.Entity<Employee>().HasIndex(e=>e.Name).IsUnique();
        model.Entity<Coupon>().HasIndex(c=>c.Code).IsUnique();
        model.Entity<Payment>().HasIndex(p=>p.At);
        model.Entity<StockEntry>().HasIndex(p=>p.At);
        model.Entity<GuestRequest>().HasIndex(p=>new {p.TableId,p.Status});
        model.Entity<OrderLine>().ToTable("OrderLines", t=>t.HasCheckConstraint("CK_OrderLines_Amounts","[Quantity] > 0 AND [UnitPrice] >= 0 AND [Cost] >= 0"));
        model.Entity<Payment>().ToTable("Payments",t=>t.HasCheckConstraint("CK_Payments_Positive","[Amount] > 0"));
        model.Entity<RefundEntry>().ToTable("Refunds",t=>t.HasCheckConstraint("CK_Refunds_Positive","[Amount] > 0"));
        model.Entity<Ingredient>().ToTable("Ingredients",t=>t.HasCheckConstraint("CK_Ingredients_NonNegative","[Quantity] >= 0 AND [Minimum] >= 0 AND [UnitCost] >= 0"));
        model.Entity<RecipePart>().ToTable("RecipeParts",t=>t.HasCheckConstraint("CK_RecipeParts_Positive","[Quantity] > 0"));
        model.Entity<Employee>().ToTable("Employees",t=>t.HasCheckConstraint("CK_Employees_Role","[Role] IN (N'Yönetici',N'Kasiyer',N'Garson')"));
        // Explicit lengths/precision also apply to shadow foreign keys. No state JSON column exists.
        foreach(var entity in model.Model.GetEntityTypes())
            foreach(var property in entity.GetProperties())
            {
                if(property.ClrType==typeof(decimal) || property.ClrType==typeof(decimal?)) { property.SetPrecision(28);property.SetScale(8); }
                if(property.ClrType==typeof(string) && property.GetMaxLength()==null)
                    property.SetMaxLength(property.Name.EndsWith("Id") || property.Name=="Id" ? 64 : property.Name is "Detail" or "Message" ? 2000 : 500);
            }
        model.Entity<Employee>().Property(e=>e.Name).HasMaxLength(200);
        model.Entity<Coupon>().Property(c=>c.Code).HasMaxLength(200);
        model.Entity<CafeTable>().Property(t=>t.MenuToken).HasMaxLength(64);
    }
    private static void Map<T>(ModelBuilder model,string name) where T:class
    {
        model.Entity<T>().ToTable(name);
        model.Entity<T>().Property<int>("SortOrder");
    }
}

public sealed class CafeDbContextFactory : IDesignTimeDbContextFactory<CafeDbContext>
{
    public CafeDbContext CreateDbContext(string[] args) => new("Server=.\\SQLEXPRESS;Database=AtelierCafe;Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
}

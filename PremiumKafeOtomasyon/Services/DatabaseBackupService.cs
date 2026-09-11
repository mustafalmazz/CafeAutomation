using System.IO;
namespace PremiumKafeOtomasyon.Services;
public sealed partial class CafeService
{
    public string CreateSqlBackup()
    {
        if(!CanManage) throw new InvalidOperationException("Yönetici girişi gerekli.");
        return _store is SqlStateStore sql ? sql.CreateNativeBackup() : throw new InvalidOperationException("SQL Server bağlantısı aktif değil.");
    }
    public void ExportBackup(string path)
    {
        if(!CanManage) throw new InvalidOperationException("Yönetici girişi gerekli.");
        if(_store is StateStore && Path.GetFullPath(path).Equals(Path.GetFullPath(DataPath),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Yedek için farklı bir dosya seçin.");
        new StateStore(path).Save(StateStore.Copy(State));
    }
}

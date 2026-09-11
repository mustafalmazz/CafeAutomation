using PremiumKafeOtomasyon.Domain;

namespace PremiumKafeOtomasyon.Services;

public sealed record PosResult(PosStatus Status, string Detail);

// Only the simulator is available. A live adapter requires a provider-specific contract.
public interface IPosTestTerminal
{
    Task<PosResult> ExecuteAsync(PosScenario scenario);
    PosResult Query(PosScenario scenario);
}

public sealed class PosSimulator : IPosTestTerminal
{
    public async Task<PosResult> ExecuteAsync(PosScenario scenario)
    {
        await Task.Delay(1200);
        return scenario switch
        {
            PosScenario.Approve => new(PosStatus.Approved, "Simülatör onayı. Gerçek tahsilat yapılmadı."),
            PosScenario.Decline => new(PosStatus.Declined, "Simülatör ödemeyi reddetti."),
            PosScenario.Timeout => new(PosStatus.Unknown, "Yanıt zaman aşımına uğradı. Yeniden denemeden önce durumu sorgulayın."),
            _ => new(PosStatus.Disconnected, "Simüle edilen bağlantı kesintisi; işlem terminale ulaşmadı.")
        };
    }

    public PosResult Query(PosScenario scenario) => scenario is PosScenario.Approve or PosScenario.Timeout
        ? new(PosStatus.Approved, "Test sorgusu: terminalde onaylı. Gerçek tahsilat yapılmadı.")
        : new(PosStatus.Declined, "Test sorgusu: terminalde tahsilat yok.");
}

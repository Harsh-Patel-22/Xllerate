namespace Xllerate.Services;

public class NewsRefreshJob(DashboardService dashboardService)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await dashboardService.RefreshNewsAsync(ct);
    }
}
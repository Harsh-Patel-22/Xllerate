using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xllerate.Services;

namespace Xllerate.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminController(
    CorrelationRefreshJob correlationRefreshJob,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpPost("correlations/refresh")]
    public IActionResult TriggerCorrelationRefresh()
    {
        var jobId = BackgroundJob.Enqueue<CorrelationRefreshJob>(j => j.RefreshAsync(CancellationToken.None));
        logger.LogInformation("Correlation refresh job enqueued. JobId: {JobId}", jobId);
        return Accepted(new { message = "Correlation refresh enqueued.", jobId });
    }
}
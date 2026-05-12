using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xllerate.Services;
using Xllerate.Services.Roadmap;

namespace Xllerate.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminController(
    CorrelationRefreshJob correlationRefreshJob,
    AdminService adminService,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpPost("correlations/refresh")]
    public IActionResult TriggerCorrelationRefresh()
    {
        var jobId = BackgroundJob.Enqueue<CorrelationRefreshJob>(j => j.RefreshAsync(CancellationToken.None));
        logger.LogInformation("Correlation refresh job enqueued. JobId: {JobId}", jobId);
        return Accepted(new { message = "Correlation refresh enqueued.", jobId });
    }
    
    [HttpPost("skills/categorize-backfill")]
    public IActionResult TriggerCategoryBackfill()
    {
        var jobId = BackgroundJob.Enqueue<SkillCategorizationService>(
            s => s.BackfillAsync(CancellationToken.None));
        return Accepted(new { message = "Category backfill enqueued.", jobId });
    }

    [HttpPost("news/refresh")]
    public IActionResult TriggerNewsRefresh()
    {
        var jobId = BackgroundJob.Enqueue<NewsRefreshJob>(j => j.RunAsync(CancellationToken.None));
        logger.LogInformation("News refresh job enqueued manually. JobId: {JobId}", jobId);
        return Accepted(new { message = "News refresh enqueued.", jobId });
    }
    
    [HttpGet("skill-graph")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSkillGraph()
    {
        var result = await adminService.GetSkillGraphAsync();
        return Ok(result);
    }

    [HttpPatch("skill-graph/importance")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSkillImportance([FromBody] UpdateSkillImportanceDto dto)
    {
        if (dto.ProbabilisticScore is < 0.0 or > 1.0)
            return BadRequest("ProbabilisticScore must be between 0.0 and 1.0.");

        await adminService.UpdateSkillImportanceAsync(dto);
        return NoContent();
    }
    
    [HttpPatch("skill-graph/skill")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRoleSkill([FromBody] UpdateSkillImportanceDto dto)
    {
        if (dto.ProbabilisticScore is < 0.0 or > 1.0)
            return BadRequest("ProbabilisticScore must be between 0.0 and 1.0.");
        await adminService.UpdateRoleSkillAsync(dto);
        return NoContent();
    }

    [HttpPost("skill-graph/skill")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddRoleSkill([FromBody] AddRoleSkillDto dto)
    {
        if (dto.ProbabilisticScore is < 0.0 or > 1.0)
            return BadRequest("ProbabilisticScore must be between 0.0 and 1.0.");
        await adminService.AddRoleSkillAsync(dto);
        return NoContent();
    }

    [HttpDelete("skill-graph/skill")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRoleSkill([FromBody] DeleteRoleSkillDto dto)
    {
        await adminService.DeleteRoleSkillAsync(dto);
        return NoContent();
    }
}
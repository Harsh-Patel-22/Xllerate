using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models;
using Xllerate.Services;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController(
    UserProfileService profileService,
    JobFeedService jobFeedService,
    DashboardService dashboardService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/user/me — full profile: user + resume + latest analysis
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var profile = await profileService.GetProfileAsync(UserId, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    // GET /api/user/feed?limit=10?query=remote — domain-matched jobs with per-user match score
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int limit = 10,
        [FromQuery] string filter = "all",
        CancellationToken ct = default)
    {
        var jobs = await jobFeedService.GetFeedAsync(UserId, limit, filter, ct);
        return Ok(jobs);
    }
    
    [HttpPost("jobs/{jobId:guid}/save")]
    public async Task<IActionResult> SaveJob(Guid jobId, [FromServices] ApplicationDbContext db, CancellationToken ct)
    {
        var exists = await db.JobRoles.AnyAsync(j => j.Id == jobId, ct);
        if (!exists) return NotFound(new { message = "Job not found." });

        var alreadySaved = await db.SavedJobs.AnyAsync(s => s.UserId == UserId && s.JobRoleId == jobId, ct);
        if (alreadySaved) return Conflict(new { message = "Job already saved." });

        db.SavedJobs.Add(new SavedJob { Id = Guid.NewGuid(), UserId = UserId, JobRoleId = jobId });
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Job saved." });
    }

    [HttpDelete("jobs/{jobId:guid}/save")]
    public async Task<IActionResult> UnsaveJob(Guid jobId, [FromServices] ApplicationDbContext db, CancellationToken ct)
    {
        var row = await db.SavedJobs.FirstOrDefaultAsync(s => s.UserId == UserId && s.JobRoleId == jobId, ct);
        if (row == null) return NotFound(new { message = "Job not saved." });

        db.SavedJobs.Remove(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Job unsaved." });
    }

    [HttpPost("role/{role}")]
    public async Task<IActionResult> PostRole([FromRoute] string role, [FromServices] ApplicationDbContext db, [FromServices] AnalysisService analysisService, CancellationToken ct) {
        var rows = await db.Users.Where(user => user.Id == UserId).ExecuteUpdateAsync(setter => setter.SetProperty(u => u.DesiredRole, role), cancellationToken: ct);
        if (rows == 0) return BadRequest(new {message = "Could not update the role."});
        await analysisService.AnalyzeAgainstDesiredRoleAsync(UserId, ct);
        return Ok(new {message = "Role Updated Successfully"});
    }
    
    [HttpGet("role")]
    public async Task<IActionResult> GetRole([FromServices] ApplicationDbContext db) {
        var role = await db.Users.Where(user => user.Id == UserId).Select(user => user.DesiredRole).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(role)) return BadRequest(new {message = "No role selected."});
        return Ok(role);
    }
    
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var dashboard = await dashboardService.GetDashboardAsync(UserId, ct);
        return Ok(dashboard);
    }
}
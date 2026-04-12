using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Services;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController(
    UserProfileService profileService,
    JobFeedService jobFeedService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/user/me — full profile: user + resume + latest analysis
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var profile = await profileService.GetProfileAsync(UserId, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    // GET /api/user/feed?limit=10 — domain-matched jobs with per-user match score
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var jobs = await jobFeedService.GetFeedAsync(UserId, limit, ct);
        return Ok(jobs);
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
}
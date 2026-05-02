using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models.DTOs.Analysis;
using Xllerate.Services;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/analysis")]
[Authorize]
public class AnalysisController(AnalysisService analysisService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST /api/analysis — trigger new analysis
    [HttpPost]
    public async Task<IActionResult> Analyze(
        [FromBody] TriggerAnalysisRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await analysisService.AnalyzeAsync(UserId, request.JobRoleId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/analysis/jobs — all analyses for current user against jobs
    [HttpGet("jobs")]
    public async Task<IActionResult> GetAllAgainstJobs(CancellationToken ct)
    {
        var results = await analysisService.GetAllForUserAgainstJobsAsync(UserId, ct);
        return Ok(results);
    }
    // GET /api/analysis/role — all analyses for current user against roles
    [HttpGet("role")]
    public async Task<IActionResult> GetAllAgainstRoles(CancellationToken ct)
    {
        var results = await analysisService.GetAllForUserAgainstRolesAsync(UserId, ct);
        return Ok(results);
    }

    // GET /api/analysis/{id} — single analysis
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await analysisService.GetAsync(id, UserId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
    
    [HttpPost("desired-role")]
    [Authorize]
    public async Task<IActionResult> AnalyzeDesiredRole(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await analysisService.AnalyzeAgainstDesiredRoleAsync(userId ,ct);
        return Ok(result);
    }
    
    [HttpPost("posting")]
    [Authorize]
    public async Task<IActionResult> AnalyzeAgainstPosting(
        [FromBody] AnalyzeAgainstPostingRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await analysisService.AnalyzeAgainstPostingAsync(userId, request, ct);
        return Ok(result);
    }
    
    [HttpPost("{analysisId:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid analysisId, CancellationToken ct, [FromServices] ApplicationDbContext db)
    {
        var userId = GetUserId();
        var analysis = await db.Analyses
            .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId, ct);

        if (analysis == null) return NotFound();
        if (analysis.IsDeleted) return StatusCode(410, "Gone"); // 410

        var result = analysis.JobRoleId.HasValue
            ? await analysisService.AnalyzeAsync(userId, analysis.JobRoleId.Value, ct)
            : await analysisService.AnalyzeAgainstDesiredRoleAsync(userId, ct);

        return Ok(result);
    }
    
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
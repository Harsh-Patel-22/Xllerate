using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // GET /api/analysis — all analyses for current user
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var results = await analysisService.GetAllForUserAsync(UserId, ct);
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
}
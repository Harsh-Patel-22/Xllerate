using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xllerate.Models.DTOs.Roadmap;
using Xllerate.Services.Roadmap;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/roadmap")]
[Authorize]
public class RoadmapController(RoadmapService roadmapService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateRoadmapRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Timeframe))
            return BadRequest("Timeframe is required.");

        try
        {
            var result = await roadmapService.GenerateAsync(UserId, req);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONFLICT")
        {
            return Conflict("Roadmap with same analysis and timeframe already exists.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "LIMIT")
        {
            return Conflict("Maximum of 3 roadmaps per analysis reached.");
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? "no inner exception";
            return StatusCode(500, new { ex.Message, Inner = inner, ex.StackTrace });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var result = await roadmapService.GetActiveAsync(UserId);
        return result == null ? BadRequest("No active roadmap exists.") : Ok(result);
    }
}
// Controllers/EnrichmentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Enrichment;
using Xllerate.Services.Enrichment;

namespace Xllerate.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EnrichmentController(
    ProfileEnrichmentService enrichment,
    ApplicationDbContext db,
    ILogger<EnrichmentController> logger)
    : ControllerBase {

    // ── POST /api/enrichment/github ───────────────────────────────────────────
    [HttpPost("github")]
    public async Task<IActionResult> ConnectGitHub(
        [FromBody] ConnectGitHubRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "GitHub username is required." });

        try
        {
            var summary = await enrichment
                .EnrichFromGitHubAsync(userId, request.Username);

            var profile = await db.ExternalProfiles
                .FirstOrDefaultAsync(ep =>
                    ep.UserId == userId &&
                    ep.Platform == Platform.GitHub);

            return Ok(new EnrichmentStatusResponse
            {
                ProfileId    = profile!.Id,
                Platform     = "GitHub",
                Username     = request.Username,
                SyncStatus   = "Complete",
                LastSyncedAt = profile.LastSyncedAt,
                Summary      = summary
            });
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new
            {
                message = $"GitHub user '{request.Username}' not found."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "GitHub enrichment failed for user {UserId}", userId);
            return StatusCode(500, new
            {
                message = "GitHub enrichment failed. Please try again."
            });
        }
    }

    // ── POST /api/enrichment/leetcode ─────────────────────────────────────────
    [HttpPost("leetcode")]
    public async Task<IActionResult> ConnectLeetCode(
        [FromBody] ConnectLeetCodeRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "LeetCode username is required." });

        try
        {
            var summary = await enrichment
                .EnrichFromLeetCodeAsync(userId, request.Username);

            var profile = await db.ExternalProfiles
                .FirstOrDefaultAsync(ep =>
                    ep.UserId == userId &&
                    ep.Platform == Platform.LeetCode);

            return Ok(new EnrichmentStatusResponse
            {
                ProfileId    = profile!.Id,
                Platform     = "LeetCode",
                Username     = request.Username,
                SyncStatus   = "Complete",
                LastSyncedAt = profile.LastSyncedAt,
                Summary      = summary
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "LeetCode enrichment failed for user {UserId}", userId);
            return StatusCode(500, new
            {
                message = "LeetCode enrichment failed. " +
                          "Check the username and try again."
            });
        }
    }

    // ── GET /api/enrichment/status ────────────────────────────────────────────
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profiles = await db.ExternalProfiles
            .Where(ep => ep.UserId == userId)
            .ToListAsync();

        var response = profiles.Select(p => new EnrichmentStatusResponse
        {
            ProfileId    = p.Id,
            Platform     = p.Platform.ToString(),
            Username     = p.Username ?? "",
            SyncStatus   = p.SyncStatus.ToString(),
            LastSyncedAt = p.LastSyncedAt,
            ErrorMessage = p.SyncErrorMessage
        });

        return Ok(response);
    }

    // ── GET /api/enrichment/skills ────────────────────────────────────────────
    // Returns all enriched skills with their sources
    [HttpGet("skills")]
    public async Task<IActionResult> GetEnrichedSkills()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var skills = await db.UserSkills
            .Where(us => us.UserId == userId)
            .Include(us => us.Skill)
            .Include(us => us.Sources)
            .OrderByDescending(us => us.ConfidenceScore)
            .Select(us => new
            {
                SkillName          = us.Skill.Name,
                NormalizedName     = us.Skill.NormalizedName,
                Category           = us.Skill.Category,
                ConfidenceScore    = us.ConfidenceScore,
                VerificationStatus = us.VerificationStatus.ToString(),
                Sources = us.Sources.Select(s => new
                {
                    SourceType = s.SourceType.ToString(),
                    s.Evidence,
                    s.Weight,
                    s.CapturedAt
                }).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            TotalSkills        = skills.Count,
            VerifiedSkills     = skills.Count(s =>
                s.VerificationStatus == "Evidenced" ||
                s.VerificationStatus == "Verified"),
            Skills             = skills
        });
    }

    // ── POST /api/enrichment/resync/{platform} ────────────────────────────────
    [HttpPost("resync/{platform}")]
    public async Task<IActionResult> Resync(string platform)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await db.ExternalProfiles
            .FirstOrDefaultAsync(ep =>
                ep.UserId == userId &&
                ep.Platform.ToString().ToLower() == platform.ToLower());

        if (profile is null)
            return NotFound(new
            {
                message = $"{platform} profile not connected yet."
            });

        if (string.IsNullOrWhiteSpace(profile.Username))
            return BadRequest(new { message = "No username stored for resync." });

        // Re-run enrichment with existing username
        if (platform.ToLower() == "github")
        {
            var summary = await enrichment
                .EnrichFromGitHubAsync(userId, profile.Username);
            return Ok(new { message = "GitHub resync complete.", summary });
        }
        else if (platform.ToLower() == "leetcode")
        {
            var summary = await enrichment
                .EnrichFromLeetCodeAsync(userId, profile.Username);
            return Ok(new { message = "LeetCode resync complete.", summary });
        }

        return BadRequest(new { message = "Unknown platform." });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
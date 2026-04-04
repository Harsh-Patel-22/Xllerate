// Controllers/ResumeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models.DTOs.Resume;
using Xllerate.Services;

namespace Xllerate.Controllers;

// [Authorize]
[ApiController]
[Route("api/[controller]")]
public class ResumeController(
    ResumeUploadService uploadService,
    SparkJobService sparkService,
    ResumeProcessingService processingService,
    ApplicationDbContext db,
    ILogger<ResumeController> logger)
    : ControllerBase {
    // ── POST /api/resume/upload ───────────────────────────────────────────────
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]  // 10MB
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        try
        {
            // 1 — write PDF to HDFS + save Resume row
            var resume = await uploadService.UploadAsync(userId, file);

            // 2 — trigger Spark pipeline via Flask (fire and forget)
            await sparkService.SubmitResumeParseJobAsync(
                resume.Id, resume.StoragePath);

            return Accepted(new ResumeUploadResponse
            {
                ResumeId = resume.Id,
                Status = AnalysisStatus.Processing,
                Message = "Resume uploaded. Processing has started.",
                UploadedAt = resume.UploadedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            // Validation errors from the upload service
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Resume upload failed for user {UserId}", userId);
            return StatusCode(500, new { message = "Upload failed. Please try again." });
        }
    }

    // ── GET /api/resume/{resumeId}/status ─────────────────────────────────────
    // Frontend polls this after upload to know when parsing is complete
    [HttpGet("{resumeId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid resumeId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var resume = await db.Resumes
            .FirstOrDefaultAsync(r =>
                r.Id == resumeId && r.UserId == userId);

        if (resume is null)
            return NotFound(new { message = "Resume not found." });

        // If not yet parsed, check if Spark has written to staging
        if (resume.ParsedAt is null)
        {
            var hasStaging = await db.SparkSkillStaging
                .AnyAsync(s =>
                    s.ResumeId == resumeId.ToString() &&
                    !s.IsConsumed);

            if (hasStaging)
            {
                // Spark is done — consume staging rows into proper tables
                await processingService.ConsumeSparkOutputAsync(resumeId);

                // Reload resume to get updated ParsedAt
                await db.Entry(resume).ReloadAsync();
            }
        }

        // Build response
        var status = resume.ParsedAt is null ? AnalysisStatus.Processing : AnalysisStatus.Complete;

        var skills = resume.ParsedAt is null
            ? []
            : await db.UserSkills
                .Where(us => us.UserId == userId)
                .Include(us => us.Skill)
                .Include(us => us.Sources)
                .Select(us => new ExtractedSkillDto
                {
                    Name = us.Skill.Name,
                    NormalizedName = us.Skill.NormalizedName,
                    ConfidenceScore = us.ConfidenceScore,
                    VerificationStatus = us.VerificationStatus.ToString(),
                    Source = us.Sources
                        .OrderByDescending(s => s.Weight)
                        .Select(s => s.SourceType.ToString())
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync();

        return Ok(new ResumeStatusResponse
        {
            ResumeId = resume.Id,
            Status = status,
            UploadedAt = resume.UploadedAt,
            ParsedAt = resume.ParsedAt,
            Skills = skills
        });
    }

    // ── GET /api/resume/active ────────────────────────────────────────────────
    // Returns the user's current active resume metadata
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var resume = await db.Resumes
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderByDescending(r => r.UploadedAt)
            .FirstOrDefaultAsync();

        if (resume is null)
            return NotFound(new { message = "No active resume found." });

        return Ok(new ResumeUploadResponse
        {
            ResumeId = resume.Id,
            Status = resume.ParsedAt is null ? AnalysisStatus.Processing : AnalysisStatus.Complete,
            Message = resume.OriginalFileName ?? "",
            UploadedAt = resume.UploadedAt
        });
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
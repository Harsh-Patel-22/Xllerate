// Controllers/JobsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models;
using Xllerate.Models.DTOs.Jobs;
using Xllerate.Services.JobScraping;
using Hangfire;
using Xllerate.Enums;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController(
    ApplicationDbContext db,
    IBackgroundJobClient jobClient,
    ILogger<JobsController> logger)
    : ControllerBase {

    // ── GET /api/jobs ─────────────────────────────────────────────────────────
    // Public — no auth needed to browse jobs
    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string?   search     = null,
        [FromQuery] string?   seniority  = null,
        [FromQuery] string?   company    = null,
        [FromQuery] bool?     remote     = null,
        [FromQuery] string?   skill      = null,
        [FromQuery] int       page       = 1,
        [FromQuery] int       pageSize   = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = db.JobRoles
            .Include(jr => jr.Company)
            .Include(jr => jr.RequiredSkills)
                .ThenInclude(jrs => jrs.Skill)
            .Where(jr => jr.IsActive)
            .AsQueryable();

        // Search by title or company name
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(jr =>
                jr.Title.Contains(search) ||
                jr.Company.Name.Contains(search));

        // Filter by seniority
        if (!string.IsNullOrWhiteSpace(seniority) &&
            Enum.TryParse<Seniority>(seniority, true, out var sen))
            query = query.Where(jr => jr.Seniority == sen);

        // Filter by company
        if (!string.IsNullOrWhiteSpace(company))
            query = query.Where(jr =>
                jr.Company.NormalizedName.Contains(
                    company.ToLower()));

        // Filter by remote
        if (remote.HasValue)
            query = query.Where(jr => jr.IsRemote == remote.Value);

        // Filter by required skill
        if (!string.IsNullOrWhiteSpace(skill))
            query = query.Where(jr =>
                jr.RequiredSkills.Any(jrs =>
                    jrs.Skill.NormalizedName.Contains(
                        skill.ToLower())));

        var total = await query.CountAsync();

        var jobs = await query
            .OrderByDescending(jr => jr.ScrapedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(jr => new JobRoleDto
            {
                Id          = jr.Id,
                Title       = jr.Title,
                CompanyName = jr.Company.Name,
                CompanyLogo = jr.Company.LogoUrl,
                Location    = jr.Location,
                IsRemote    = jr.IsRemote,
                Seniority   = jr.Seniority.ToString(),
                SourceUrl   = jr.SourceUrl,
                ScrapedAt   = jr.ScrapedAt,
                RequiredSkills = jr.RequiredSkills
                    .OrderByDescending(jrs => jrs.ImportanceWeight)
                    .Take(10)
                    .Select(jrs => jrs.Skill.Name)
                    .ToList()
            })
            .ToListAsync();

        return Ok(new JobListResponse
        {
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Jobs       = jobs
        });
    }

    // ── GET /api/jobs/{id} ────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await db.JobRoles
            .Include(jr => jr.Company)
            .Include(jr => jr.RequiredSkills)
                .ThenInclude(jrs => jrs.Skill)
            .FirstOrDefaultAsync(jr => jr.Id == id && jr.IsActive);

        if (job is null)
            return NotFound(new { message = "Job role not found." });

        return Ok(new
        {
            job.Id,
            job.Title,
            CompanyName    = job.Company.Name,
            CompanyLogo    = job.Company.LogoUrl,
            CompanyWebsite = job.Company.Website,
            job.Location,
            job.IsRemote,
            Seniority      = job.Seniority.ToString(),
            job.SourceUrl,
            job.ScrapedAt,
            Description    = job.RequirementsRaw?.DescriptionText,
            RequiredSkills = job.RequiredSkills
                .OrderByDescending(jrs => jrs.ImportanceWeight)
                .Select(jrs => new
                {
                    SkillName        = jrs.Skill.Name,
                    jrs.IsRequired,
                    jrs.ImportanceWeight
                })
                .ToList()
        });
    }

    // ── GET /api/jobs/companies ───────────────────────────────────────────────
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(
        [FromQuery] string? search   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        var query = db.Companies
            .Include(c => c.JobRoles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                c.Name.Contains(search));

        var total = await query.CountAsync();

        var companies = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Industry,
                c.LogoUrl,
                c.Website,
                ActiveJobCount = c.JobRoles
                    .Count(jr => jr.IsActive)
            })
            .ToListAsync();

        return Ok(new
        {
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Companies  = companies
        });
    }

    // ── POST /api/jobs/scrape ─────────────────────────────────────────────────
    // Admin only — manual trigger
    [Authorize]
    [HttpPost("scrape")]
    public IActionResult TriggerScrape()
    {
        // Enqueue as a Hangfire background job
        var jobId = jobClient.Enqueue<ScrapingOrchestrator>(
            o => o.RunAsync(CancellationToken.None));

        logger.LogInformation(
            "Manual scraping run triggered — Hangfire job {JobId}", jobId);

        return Accepted(new
        {
            message = "Scraping job queued.",
            jobId
        });
    }
    
    [HttpPost("enrich-companies")]
    [Authorize]
    public async Task<IActionResult> EnrichCompanies(
        [FromServices] CompanyEnrichmentService enricher,
        CancellationToken ct)
    {
        await enricher.EnrichAsync(ct);
        return Ok(new { message = "Company enrichment complete." });
    }
}
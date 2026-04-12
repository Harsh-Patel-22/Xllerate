using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services;

public class JobFeedService(ApplicationDbContext db)
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;

    private static readonly Dictionary<SkillVerificationStatus, decimal> VerificationWeight = new()
    {
        [SkillVerificationStatus.Claimed]   = 0.50m,
        [SkillVerificationStatus.Evidenced] = 0.75m,
        [SkillVerificationStatus.Certified] = 0.90m,
        [SkillVerificationStatus.Verified]  = 1.00m,
    };

    public async Task<List<JobFeedDto>> GetFeedAsync(
        Guid userId, int limit = DefaultLimit, CancellationToken ct = default)
    {
        limit = Math.Min(limit, MaxLimit);

        // Load user skills once
        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId)
            .ToListAsync(ct);

        if (userSkills.Count == 0) return [];

        var userSkillMap = userSkills.ToDictionary(
            us => us.Skill.NormalizedName,
            us => us,
            StringComparer.OrdinalIgnoreCase);

        // Infer user's domain from their strongest skill bucket
        var domainKeywords = await InferDomainKeywordsAsync(userSkills, ct);

        // Load jobs matching user's domain — include skills
        var jobs = await db.JobRoles
            .Include(j => j.Company)
            .Include(j => j.RequiredSkills).ThenInclude(jrs => jrs.Skill)
            .Where(j => j.IsActive &&
                domainKeywords.Any(kw => j.NormalizedTitle.Contains(kw)))
            .OrderByDescending(j => j.ScrapedAt)
            .Take(limit * 3)    // over-fetch then re-rank by match score
            .ToListAsync(ct);
        
        var personalizedJobs = jobs;

        if (personalizedJobs.Count < limit)
        {
            var fallbackJobs = await db.JobRoles
                .Include(j => j.Company)
                .Include(j => j.RequiredSkills).ThenInclude(jrs => jrs.Skill)
                .Where(j => j.IsActive)
                .OrderByDescending(j => j.ScrapedAt)
                .Take(limit * 2)
                .ToListAsync(ct);

            jobs = personalizedJobs
                .Concat(fallbackJobs)
                .DistinctBy(j => j.Id)
                .Take(limit * 3)
                .ToList();
        }

        if (jobs.Count == 0) return [];

        // Score + build DTOs
        var scored = jobs
            .Select(job => (Job: job, Score: ComputeMatchScore(job, userSkillMap)))
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList();

        return scored.Select(x => BuildDto(x.Job, x.Score, userSkillMap)).ToList();
    }

    // ── Domain inference ──────────────────────────────────────────────────────

    private async Task<List<string>> InferDomainKeywordsAsync(
        List<Models.UserSkill> userSkills,
        CancellationToken ct)
    {
        // Find which RoleProfile best matches user's skillset
        var roleProfiles = await db.RoleProfiles
            .Include(rp => rp.Skills).ThenInclude(rps => rps.Skill)
            .ToListAsync(ct);

        var userSkillNames = userSkills
            .Select(us => us.Skill.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Score each role profile by overlap with user skills
        var bestProfile = roleProfiles
            .Select(rp => new
            {
                Profile = rp,
                Overlap = rp.Skills.Count(rps =>
                    userSkillNames.Contains(rps.Skill.NormalizedName))
            })
            .OrderByDescending(x => x.Overlap)
            .FirstOrDefault();

        if (bestProfile == null || bestProfile.Overlap == 0)
            return [];

        // Pull keywords for this role from DB mappings
        var keywords = await db.RoleKeywordMappings
            .Where(m => m.RoleKey == bestProfile.Profile.RoleKey)
            .Select(m => m.Keyword)
            .ToListAsync(ct);

        return keywords;
    }

    // ── Per-job match score ───────────────────────────────────────────────────

    private static int ComputeMatchScore(
        Models.JobRole job,
        Dictionary<string, Models.UserSkill> userSkillMap)
    {
        var required = job.RequiredSkills.ToList();
        if (required.Count == 0) return 0;

        var totalWeight = required.Sum(r => r.ImportanceWeight);
        if (totalWeight == 0) return 0;

        var earned = 0m;
        foreach (var req in required)
        {
            if (!userSkillMap.TryGetValue(req.Skill.NormalizedName, out var us))
                continue;

            var modifier = VerificationWeight.GetValueOrDefault(
                us.VerificationStatus, 0.50m);
            var userScore = Math.Min(us.ConfidenceScore * modifier, req.ImportanceWeight);
            earned += userScore;
        }

        return (int)Math.Round((earned / totalWeight) * 100);
    }

    // ── DTO builder ───────────────────────────────────────────────────────────

    private static JobFeedDto BuildDto(
        Models.JobRole job,
        int matchScore,
        Dictionary<string, Models.UserSkill> userSkillMap)
    {
        var allJobSkills     = job.RequiredSkills.Select(jrs => jrs.Skill.Name).ToList();
        var missingSkills    = job.RequiredSkills
            .Where(jrs => !userSkillMap.ContainsKey(jrs.Skill.NormalizedName))
            .OrderByDescending(jrs => jrs.ImportanceWeight)
            .Take(4)                                    // cap missing skills shown
            .Select(jrs => jrs.Skill.Name)
            .ToList();

        var postedAgo = FormatPostedAt(job.ScrapedAt);

        return new JobFeedDto
        {
            Id             = job.Id.ToString(),
            Title          = job.Title,
            Company        = job.Company?.Name ?? "Unknown",
            CompanyColor   = null,
            Location       = job.Location,
            JobUrl         = job.SourceUrl,
            Remote         = job.IsRemote,
            Salary         = null,
            Experience     = null,
            Skills         = allJobSkills.Take(5).ToList(),   // cap to avoid overload
            MissingSkills  = missingSkills,
            MatchScore     = matchScore,
            ScrapedAt       = postedAgo,
        };
    }

    private static string FormatPostedAt(DateTime scrapedAt)
    {
        var diff = DateTime.UtcNow - scrapedAt;
        if (diff.TotalDays < 1)  return "Today";
        if (diff.TotalDays < 2)  return "1 day ago";
        if (diff.TotalDays < 7)  return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 14) return "1 week ago";
        return $"{(int)(diff.TotalDays / 7)} weeks ago";
    }
}
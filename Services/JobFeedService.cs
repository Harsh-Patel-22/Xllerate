using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services;
public static class JobFeedFilter
{
    public const string All = "Explore";
    public const string StrongMatch = "Strong Match";
    public const string Remote = "Remote";
    public const string Saved = "Saved";
}
public class JobFeedService(ApplicationDbContext db, MongoDbContext mongo)
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;

    private static readonly Dictionary<SkillVerificationStatus, decimal> VerificationWeight = new()
    {
        [SkillVerificationStatus.Claimed]   = 0.40m,
        [SkillVerificationStatus.Evidenced] = 0.75m,
        [SkillVerificationStatus.Certified] = 0.90m,
        [SkillVerificationStatus.Verified]  = 1.00m,
    };

    public async Task<List<JobFeedDto>> GetFeedAsync(
        Guid userId, int limit = DefaultLimit, string filter = "Explore", CancellationToken ct = default)
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
        
        var savedJobIds = await db.SavedJobs
            .Where(s => s.UserId == userId)
            .Select(s => s.JobRoleId)
            .ToHashSetAsync(ct);
        
        if (filter == JobFeedFilter.Saved)
        {
            var savedJobs = await db.JobRoles
                .Include(j => j.Company)
                .Include(j => j.RequiredSkills).ThenInclude(jrs => jrs.Skill)
                .Where(j => savedJobIds.Contains(j.Id))
                .ToListAsync(ct);

            return savedJobs
                .Select(j => BuildDto(j, ComputeMatchScore(j, userSkillMap), userSkillMap, savedJobIds))
                .ToList();
        }

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
        
        var filtered = filter switch
        {
            JobFeedFilter.StrongMatch => scored.Where(x => x.Score >= 65).ToList(),
            JobFeedFilter.Remote      => scored.Where(x => x.Job.IsRemote).ToList(),
            _                         => scored
        };

        return filtered.Select(x => BuildDto(x.Job, x.Score, userSkillMap, savedJobIds)).ToList();
    }

    // ── Domain inference ──────────────────────────────────────────────────────

    private async Task<List<string>> InferDomainKeywordsAsync(
        List<Models.UserSkill> userSkills,
        CancellationToken ct)
    {
        var roleProfiles = await mongo.RoleProfiles
            .Find(_ => true)
            .ToListAsync(ct);

        var userSkillNames = userSkills
            .Select(us => us.Skill.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bestProfile = roleProfiles
            .Select(rp => new
            {
                Profile = rp,
                Overlap = rp.Skills.Count(s =>
                    userSkillNames.Contains(s.SkillName.ToLowerInvariant().Trim()))
            })
            .OrderByDescending(x => x.Overlap)
            .FirstOrDefault();

        if (bestProfile == null || bestProfile.Overlap == 0)
            return [];

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
            var effective = us.ConfidenceScore * modifier;

            // Only count toward score if it clears minimum bar (65% of required)
            if (effective < req.ImportanceWeight * 0.65m)
                earned += effective * 0.5m; // partial credit for weak/unverified
            else
                earned += Math.Min(effective, req.ImportanceWeight);
        }

        return (int)Math.Round((earned / totalWeight) * 100);
    }

    // ── DTO builder ───────────────────────────────────────────────────────────

    private static JobFeedDto BuildDto(
        Models.JobRole job,
        int matchScore,
        Dictionary<string, Models.UserSkill> userSkillMap,
        HashSet<Guid> savedJobIds)
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
            IsSaved        = savedJobIds.Contains(job.Id),
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
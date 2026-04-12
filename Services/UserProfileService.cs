using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models.DTOs.User;

namespace Xllerate.Services;

public class UserProfileService(ApplicationDbContext db)
{
    private static readonly Dictionary<SkillVerificationStatus, decimal> VerificationWeight = new()
    {
        [SkillVerificationStatus.Claimed]   = 0.50m,
        [SkillVerificationStatus.Evidenced] = 0.75m,
        [SkillVerificationStatus.Certified] = 0.90m,
        [SkillVerificationStatus.Verified]  = 1.00m,
    };

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return null;

        // Latest resume
        var resume = await db.Resumes
            .OrderByDescending(r => r.ParsedAt)
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        // User skills + sources
        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Include(us => us.Sources)
            .Where(us => us.UserId == userId)
            .ToListAsync(ct);

        // External profiles (github, leetcode etc)
        var externalProfiles = await db.ExternalProfiles
            .Where(ep => ep.UserId == userId)
            .ToListAsync(ct);

        // Latest complete analysis
        var analysis = await db.Analyses
            .Include(a => a.JobRole).ThenInclude(j => j.Company)
            .Include(a => a.SkillGaps).ThenInclude(g => g.Skill)
            .Where(a => a.UserId == userId && a.Status == AnalysisStatus.Complete)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var resumeDto   = resume == null ? null : BuildResumeDto(resume, userSkills, externalProfiles);
        var analysisDto = analysis == null ? null : BuildAnalysisDto(analysis, userSkills, resume);

            
        return new UserProfileDto
        {
            Id                = user.Id.ToString(),
            Name              = user.FullName ?? user.Email ?? string.Empty,
            Email             = user.Email ?? string.Empty,
            Plan              = null,
            ProfileCompletion = ComputeProfileCompletion(resume, userSkills, externalProfiles),
            Resume            = resumeDto,
            LatestAnalysis    = analysisDto,
        };
    }

    // ── Resume ────────────────────────────────────────────────────────────────

    public ResumeDto BuildResumeDto(
        Models.Resume resume,
        List<Models.UserSkill> userSkills,
        List<Models.ExternalProfile> profiles)
    {
        var parsed = resume.ParsedData;

        return new ResumeDto
        {
            Id         = resume.Id.ToString(),
            Filename   = resume.OriginalFileName,
            UploadedAt = resume.ParsedAt.ToString(),
            Status     = resume.ParsedAt.HasValue ? SyncStatus.Complete : SyncStatus.Syncing,
            Skills     = userSkills.Select(MapSkill).OrderByDescending(s => s.Confidence).ToList(),
            Education  = parsed?.Education?.Select(e => new ResumeEducationDto
            {
                Degree      = e.Degree,
                Institution = e.Institution,
                Period      = $"{e.StartYear}–{e.EndYear}",
                GradeValue = e.GradeValue,
                GradeType = e.GradeType
            }).ToList() ?? [],
            Experience = parsed?.WorkExperience?.Select(e => new ResumeExperienceDto
            {
                Role    = e.Title,
                Company = e.Company,
                Period  = $"{e.StartDate}–{e.EndDate}",
            }).ToList() ?? [],
            Links = BuildLinks(profiles, parsed),
        };
    }

    private static ResumeSkillDto MapSkill(Models.UserSkill us) => new()
    {
        Name       = us.Skill.Name,
        Confidence = (int)Math.Round(us.ConfidenceScore * 100),
        Sources    = us.Sources
                      .Select(s => s.SourceType)
                      .Distinct()
                      .ToList(),
    };

    private static List<ResumeLinkDto> BuildLinks(
        List<Models.ExternalProfile> profiles,
        Models.ResumeParseResult? parsed)
    {
        var links = new List<ResumeLinkDto>();

        string? githubUrl   = profiles.FirstOrDefault(p => p.Platform == Platform.GitHub)?.ProfileUrl
                           ?? parsed?.Links?.GitHub;
        string? leetcodeUrl = profiles.FirstOrDefault(p => p.Platform == Platform.LeetCode)?.ProfileUrl
                           ?? parsed?.Links?.LeetCode;

        links.Add(new ResumeLinkDto
        {
            Type      = Platform.GitHub,
            Url       = githubUrl,
            Connected = githubUrl != null,
        });
        links.Add(new ResumeLinkDto
        {
            Type      = Platform.LeetCode,
            Url       = leetcodeUrl,
            Connected = leetcodeUrl != null,
        });
        links.Add(new ResumeLinkDto
        {
            Type      = Platform.LinkedIn,
            Url       = null,
            Connected = false,             // LinkedIn enrichment not built
        });

        return links;
    }

    // ── Analysis ──────────────────────────────────────────────────────────────

    private static LatestAnalysisDto BuildAnalysisDto(
        Models.Analysis analysis,
        List<Models.UserSkill> userSkills,
        Models.Resume? resume)
    {
        var userSkillMap = userSkills.ToDictionary(
            us => us.Skill.NormalizedName,
            us => us,
            StringComparer.OrdinalIgnoreCase);

        var gaps = analysis.SkillGaps.ToList();

        // Strengths = user skills NOT in gap list, sorted by confidence desc
        var gapSkillNames = gaps.Select(g => g.Skill.NormalizedName).ToHashSet();
        var strengths = userSkills
            .Where(us => !gapSkillNames.Contains(us.Skill.NormalizedName))
            .OrderByDescending(us => us.ConfidenceScore)
            .Take(5)
            .Select(MapSkill)
            .ToList();

        var weakSkills = gaps
            .Where(g => g.GapType == GapType.Weak || g.GapType == GapType.Unverified)
            .Select(g => new ResumeSkillDto
            {
                Name       = g.Skill.Name,
                Confidence = userSkillMap.TryGetValue(g.Skill.NormalizedName, out var us)
                             ? (int)Math.Round(us.ConfidenceScore * 100) : 0,
                Sources    = userSkillMap.TryGetValue(g.Skill.NormalizedName, out var us2)
                             ? us2.Sources.Select(s => s.SourceType).Distinct().ToList()
                             : [],
            }).ToList();

        var missingSkills = gaps
            .Where(g => g.GapType == GapType.Missing)
            .Select(g => new ResumeSkillDto
            {
                Name       = g.Skill.Name,
                Confidence = 0,
                Sources    = [],
            }).ToList();

        return new LatestAnalysisDto
        {
            Id          = analysis.Id.ToString(),
            ResumeId    = resume?.Id.ToString() ?? string.Empty,
            TargetRole  = analysis.JobRole.Title,
            MatchScore  = analysis.MatchPercentage,
            MatchLabel  = ComputeMatchLabel(analysis.MatchPercentage),
            Strengths   = strengths,
            WeakSkills  = weakSkills,
            MissingSkills = missingSkills,
            Gaps        = gaps.Select(g => new GapSummaryDto
            {
                Skill    = g.Skill.Name,
                GapType  = g.GapType,
                Severity = g.Severity,
            }).ToList(),
            Insights    = null,   // Gemini not built yet
            CreatedAt   = analysis.CreatedAt.ToString("yyyy-MM-dd"),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ComputeProfileCompletion(
        Models.Resume? resume,
        List<Models.UserSkill> skills,
        List<Models.ExternalProfile> profiles)
    {
        var score = 0;
        if (resume != null)                     score += 30;
        if (resume?.ParsedAt.HasValue == true)  score += 20;
        if (skills.Count > 0)                   score += 20;
        if (profiles.Any(p => p.Platform == Platform.GitHub))   score += 10;
        if (profiles.Any(p => p.Platform == Platform.LeetCode)) score += 10;
        if (profiles.Any(p => p.Platform == Platform.LinkedIn)) score += 10;
        return score;
    }

    private static string ComputeMatchLabel(decimal score) => score switch
    {
        >= 75 => "competitive",
        >= 50 => "moderate",
        _     => "weak",
    };

    private static string MapSeverity(GapSeverity s) => s switch
    {
        GapSeverity.Critical    => "critical",
        GapSeverity.Important   => "important",
        GapSeverity.NiceToHave  => "nice",
        _                       => "nice",
    };
}
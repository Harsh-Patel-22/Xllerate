using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Analysis;

namespace Xllerate.Services;

public class AnalysisService(
    ApplicationDbContext db,
    ILogger<AnalysisService> logger)
{
    // Minimum confidence to consider a skill "matched" (not weak)
    private const decimal MatchThreshold = 0.60m;

    // Maps VerificationStatus → numeric confidence modifier
    // Evidenced/Certified/Verified skills count more toward UserScore
    private static readonly Dictionary<SkillVerificationStatus, decimal> VerificationWeight = new()
    {
        [SkillVerificationStatus.Claimed]    = 0.50m,
        [SkillVerificationStatus.Evidenced]  = 0.75m,
        [SkillVerificationStatus.Certified]  = 0.90m,
        [SkillVerificationStatus.Verified]   = 1.00m,
    };

    public async Task<AnalysisResultDto> AnalyzeAsync(Guid userId, Guid jobRoleId, CancellationToken ct = default)
    {
        // ── Load job role + required skills ──────────────────────────────────
        var jobRole = await db.JobRoles
            .Include(j => j.Company)
            .Include(j => j.RequiredSkills)
                .ThenInclude(jrs => jrs.Skill)
            .FirstOrDefaultAsync(j => j.Id == jobRoleId, ct)
            ?? throw new KeyNotFoundException($"JobRole {jobRoleId} not found.");

        var requiredSkills = jobRole.RequiredSkills.ToList();

        if (requiredSkills.Count == 0)
            throw new InvalidOperationException(
                "This job role has no skills attached. Run scraping first.");

        // ── Load user skills ──────────────────────────────────────────────────
        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId)
            .ToListAsync(ct);

        // Index by normalized skill name for O(1) lookup
        var userSkillMap = userSkills.ToDictionary(
            us => us.Skill.NormalizedName,
            us => us,
            StringComparer.OrdinalIgnoreCase);

        // ── Create Analysis record (Processing) ───────────────────────────────
        var analysis = new Analysis
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            JobRoleId    = jobRoleId,
            Status       = AnalysisStatus.Processing,
            ModelVersion = "v1.0",
            CreatedAt    = DateTime.UtcNow
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        // ── Core gap computation ──────────────────────────────────────────────
        var gaps       = new List<SkillGap>();
        var matchScore = 0m;
        var totalWeight = requiredSkills.Sum(r => r.ImportanceWeight);

        foreach (var required in requiredSkills)
        {
            var skillNorm = required.Skill.NormalizedName;
            userSkillMap.TryGetValue(skillNorm, out var userSkill);

            var requiredScore = required.ImportanceWeight; // how important is this skill
            var userScore     = ComputeUserScore(userSkill, required.ImportanceWeight);
            var gapType       = ClassifyGap(userSkill, userScore, required.ImportanceWeight);

            if (gapType == null)
            {
                // Fully matched — contribute to score proportionally
                matchScore += required.ImportanceWeight;
                continue;
            }

            var severity = ClassifySeverity(required.ImportanceWeight, gapType.Value);

            gaps.Add(new SkillGap
            {
                Id            = Guid.NewGuid(),
                AnalysisId    = analysis.Id,
                SkillId       = required.SkillId,
                GapType       = gapType.Value,
                Severity      = severity,
                UserScore     = userScore,
                RequiredScore = requiredScore,
                Recommendation = BuildRecommendation(required.Skill.Name, gapType.Value, severity)
            });

            // Partial credit — weak/unverified skills still contribute some score
            matchScore += userScore;
        }

        // ── Compute final match % ─────────────────────────────────────────────
        var matchPct = totalWeight > 0
            ? Math.Round((matchScore / totalWeight) * 100, 2)
            : 0m;

        // ── Persist gaps + finalize analysis ─────────────────────────────────
        db.SkillGaps.AddRange(gaps);

        analysis.MatchPercentage = matchPct;
        analysis.Status          = AnalysisStatus.Complete;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Analysis {Id} complete. User {UserId} vs Job {JobId} → {Pct}% match, {Gaps} gaps.",
            analysis.Id, userId, jobRoleId, matchPct, gaps.Count);

        return BuildResultDto(analysis, jobRole, gaps);
    }

    public async Task<AnalysisResultDto> GetAsync(Guid analysisId, Guid userId, CancellationToken ct = default)
    {
        var analysis = await db.Analyses
            .Include(a => a.JobRole).ThenInclude(j => j.Company)
            .Include(a => a.SkillGaps).ThenInclude(g => g.Skill)
            .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Analysis not found.");

        return BuildResultDto(analysis, analysis.JobRole, analysis.SkillGaps.ToList());
    }

    public async Task<List<AnalysisResultDto>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var analyses = await db.Analyses
            .Include(a => a.JobRole).ThenInclude(j => j.Company)
            .Include(a => a.SkillGaps).ThenInclude(g => g.Skill)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return analyses.Select(a => BuildResultDto(a, a.JobRole, a.SkillGaps.ToList())).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static decimal ComputeUserScore(UserSkill? userSkill, decimal requiredImportance)
    {
        if (userSkill == null) return 0m;

        // Base: raw confidence from resume/enrichment
        var base_ = userSkill.ConfidenceScore;

        // Modifier: verification status scales the effective score
        var modifier = VerificationWeight.GetValueOrDefault(
            userSkill.VerificationStatus, 0.50m);

        // Effective score is capped at the required importance
        // (no benefit to having 0.99 confidence if job only needs 0.6)
        return Math.Min(base_ * modifier, requiredImportance);
    }

    private static GapType? ClassifyGap(UserSkill? userSkill, decimal userScore, decimal requiredScore)
    {
        if (userSkill == null)
            return GapType.Missing;

        if (userSkill.VerificationStatus == SkillVerificationStatus.Claimed && userScore < MatchThreshold)
            return GapType.Unverified;

        if (userScore < requiredScore * MatchThreshold)
            return GapType.Weak;

        return null; // matched
    }

    private static GapSeverity ClassifySeverity(decimal importance, GapType gapType)
    {
        // Missing + high importance = Critical
        if (gapType == GapType.Missing && importance >= 0.80m) return GapSeverity.Critical;
        if (gapType == GapType.Missing && importance >= 0.60m) return GapSeverity.Important;
        if (gapType == GapType.Weak    && importance >= 0.75m) return GapSeverity.Critical;
        if (gapType == GapType.Weak    && importance >= 0.55m) return GapSeverity.Important;
        return GapSeverity.NiceToHave;
    }

    private static string BuildRecommendation(string skillName, GapType gapType, GapSeverity severity) =>
        (gapType, severity) switch
        {
            (GapType.Missing,    GapSeverity.Critical)    => $"You must learn {skillName} — it is a core requirement for this role.",
            (GapType.Missing,    GapSeverity.Important)   => $"Learn {skillName} to significantly improve your chances.",
            (GapType.Missing,    GapSeverity.NiceToHave)  => $"Familiarity with {skillName} is a bonus but not blocking.",
            (GapType.Weak,       GapSeverity.Critical)    => $"Your {skillName} proficiency is too low. Deepen it urgently.",
            (GapType.Weak,       GapSeverity.Important)   => $"Strengthen your {skillName} skills with practical projects.",
            (GapType.Unverified, _)                       => $"Add evidence of {skillName} — contributions, projects, or a certification.",
            _                                             => $"Consider improving your {skillName} skills."
        };

    private static AnalysisResultDto BuildResultDto(
        Analysis analysis, JobRole jobRole, List<SkillGap> gaps)
    {
        var matched    = gaps.Count(g => g.GapType != GapType.Missing);
        var missing    = gaps.Count(g => g.GapType == GapType.Missing);
        var weak       = gaps.Count(g => g.GapType == GapType.Weak);
        var unverified = gaps.Count(g => g.GapType == GapType.Unverified);

        // Total required = gaps + fully matched skills
        // We don't have matched count here directly so derive from JobRoleSkills
        var totalRequired = jobRole.RequiredSkills?.Count ?? (gaps.Count + matched);

        return new AnalysisResultDto
        {
            AnalysisId     = analysis.Id,
            JobTitle       = jobRole.Title,
            CompanyName    = jobRole.Company?.Name ?? "Unknown",
            MatchPercentage = analysis.MatchPercentage,
            Status         = analysis.Status.ToString(),
            CreatedAt      = analysis.CreatedAt,
            SkillGaps      = gaps.OrderByDescending(g => g.Severity)
                                 .ThenByDescending(g => g.RequiredScore)
                                 .Select(g => new SkillGapDto
                                 {
                                     SkillName     = g.Skill?.Name ?? "Unknown",
                                     GapType       = g.GapType.ToString(),
                                     Severity      = g.Severity.ToString(),
                                     UserScore     = g.UserScore,
                                     RequiredScore = g.RequiredScore,
                                     Recommendation = g.Recommendation
                                 }).ToList(),
            Summary = new SkillSummaryDto
            {
                TotalRequired = totalRequired,
                Matched       = totalRequired - missing - weak - unverified,
                Missing       = missing,
                Weak          = weak,
                Unverified    = unverified
            }
        };
    }
}
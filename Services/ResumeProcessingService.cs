using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;

namespace Xllerate.Services;

public class ResumeProcessingService(ApplicationDbContext db, ILogger<ResumeProcessingService> logger) {
    public async Task<bool> IsCompleteAsync(Guid resumeId)
    {
        // Spark writes to staging then resume.ParsedAt gets set in ConsumeAsync
        // So if ParsedAt is set, we are done
        return await db.Resumes
            .AnyAsync(r => r.Id == resumeId && r.ParsedAt != null);
    }

    public async Task ConsumeSparkOutputAsync(Guid resumeId)
    {
        var staged = await db.SparkSkillStaging
            .Where(s => s.ResumeId == resumeId.ToString() && !s.IsConsumed)
            .ToListAsync();

        if (!staged.Any())
        {
            logger.LogWarning(
                "No staging rows found for resume {ResumeId}", resumeId);
            return;
        }

        var resume = await db.Resumes
            .FirstOrDefaultAsync(r => r.Id == resumeId);

        if (resume is null) return;

        foreach (var row in staged)
        {
            // Resolve canonical skill — find by normalized name or create new
            var skill = await db.Skills
                .FirstOrDefaultAsync(s => s.NormalizedName == row.NormalizedName);

            if (skill is null)
            {
                skill = new Skill
                {
                    Id = Guid.NewGuid(),
                    Name = row.SkillName,
                    NormalizedName = row.NormalizedName,
                    IsVerified = false   // flagged for admin review
                };
                db.Skills.Add(skill);
                await db.SaveChangesAsync();
            }

            // Upsert UserSkill — one row per user+skill pair
            var userSkill = await db.UserSkills
                .FirstOrDefaultAsync(us =>
                    us.UserId == resume.UserId &&
                    us.SkillId == skill.Id);

            if (userSkill is null)
            {
                userSkill = new UserSkill
                {
                    Id = Guid.NewGuid(),
                    UserId = resume.UserId,
                    SkillId = skill.Id,
                    ConfidenceScore = row.Confidence,
                    VerificationStatus = SkillVerificationStatus.Claimed,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.UserSkills.Add(userSkill);
            }
            else
            {
                // If skill already existed, take the higher confidence score
                if (row.Confidence > userSkill.ConfidenceScore)
                    userSkill.ConfidenceScore = row.Confidence;

                userSkill.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            // Record where this skill came from
            db.SkillSources.Add(new SkillSource
            {
                Id = Guid.NewGuid(),
                UserSkillId = userSkill.Id,
                SourceType = row.Source == "gemini_backup"
                    ? SkillSourceType.Resume
                    : SkillSourceType.Resume,
                Evidence = $"Extracted by {row.Source} — resume {resumeId}",
                Weight = row.Confidence,
                CapturedAt = DateTime.UtcNow
            });

            row.IsConsumed = true;
        }

        // Mark resume as fully parsed
        resume.ParsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Consumed {Count} skill rows from staging for resume {ResumeId}",
            staged.Count, resumeId);
    }
}
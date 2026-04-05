using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;

namespace Xllerate.Services;

public class ResumeProcessingService(ApplicationDbContext db, ILogger<ResumeProcessingService> logger, HdfsService hdfsService) {
    public async Task<bool> IsCompleteAsync(Guid resumeId)
    {
        // Spark writes to staging then resume.ParsedAt gets set in ConsumeAsync
        // So if ParsedAt is set, we are done
        return await db.Resumes
            .AnyAsync(r => r.Id == resumeId && r.ParsedAt != null);
    }

    public async Task ConsumeSparkOutputAsync(Guid resumeId)
    {
        var resume = await db.Resumes
            .FirstOrDefaultAsync(r => r.Id == resumeId);

        if (resume is null) return;
        
        // ── Step 1: Read full parsed JSON from HDFS ───────────────────────────
        var hdfsJson = await hdfsService.ReadOutputJsonAsync(resumeId.ToString());

        if (hdfsJson != null)
        {
            await PopulateResumeDataAsync(resume, hdfsJson);
        }
        else
        {
            logger.LogWarning(
                "HDFS output JSON not found for resume {ResumeId} — " +
                "ParsedData will not be populated", resumeId);
        }

        // ── Step 2: Consume staging rows into UserSkills ──────────────────────
        
        var staged = await db.SparkSkillStaging
            .Where(s => s.ResumeId == resumeId.ToString() && !s.IsConsumed)
            .ToListAsync();

        if (!staged.Any())
        {
            logger.LogWarning(
                "No staging rows found for resume {ResumeId}", resumeId);
            return;
        }


        foreach (var row in staged)
        {
            await UpsertUserSkillAsync(resume, row);
            row.IsConsumed = true;
        }

        // Mark resume as fully parsed
        resume.ParsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Consumed {Count} skill rows from staging for resume {ResumeId}",
            staged.Count, resumeId);
    }
    
    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task PopulateResumeDataAsync(Resume resume, string hdfsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(hdfsJson);
            var root = doc.RootElement;

            // Map the Spark JSON output to your ResumeParseResult model
            resume.ParsedData = new ResumeParseResult
            {
                Summary = GetString(root, "summary"),
                Objective = GetString(root, "objective"),
                RawSkills = GetStringList(root, "skills", "name"),
                RawCertifications = [],

                ContactInfo = MapContactInfo(root),
                Links = MapLinks(root),
                Education = MapEducation(root),
                WorkExperience = MapExperience(root),

                Metadata = new ParsedMetadata
                {
                    ParsedAt = DateTime.UtcNow,
                    PromptVersion = "spark-nlp-v1",
                    ParseWarnings = []
                }
            };

            logger.LogInformation(
                "ParsedData populated for resume {ResumeId} — " +
                "{SkillCount} skills, {EduCount} education entries",
                resume.Id,
                resume.ParsedData.RawSkills.Count,
                resume.ParsedData.Education.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to parse HDFS JSON for resume {ResumeId}", resume.Id);
        }
    }

    private async Task UpsertUserSkillAsync(Resume resume, SparkSkillStaging row)
    {
        // Resolve or create canonical skill
        var skill = await db.Skills
            .FirstOrDefaultAsync(s => s.NormalizedName == row.NormalizedName);

        if (skill is null)
        {
            skill = new Skill
            {
                Id = Guid.NewGuid(),
                Name = row.SkillName,
                NormalizedName = row.NormalizedName,
                IsVerified = false
            };
            db.Skills.Add(skill);
            await db.SaveChangesAsync();
        }

        // Upsert UserSkill
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
            if (row.Confidence > userSkill.ConfidenceScore)
                userSkill.ConfidenceScore = row.Confidence;
            userSkill.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Record source
        db.SkillSources.Add(new SkillSource
        {
            Id = Guid.NewGuid(),
            UserSkillId = userSkill.Id,
            SourceType = SkillSourceType.Resume,
            Evidence = $"Extracted by {row.Source} — resume {resume.Id}",
            Weight = row.Confidence,
            CapturedAt = DateTime.UtcNow
        });
    }

    // ── JSON mapping helpers ──────────────────────────────────────────────────

    private static ParsedContactInfo? MapContactInfo(JsonElement root)
    {
        if (!root.TryGetProperty("contact_info", out var c))
            return null;

        return new ParsedContactInfo
        {
            Email    = GetString(c, "email"),
            Phone    = GetString(c, "phone"),
            FullName = GetString(c, "name"),
            City     = GetString(c, "city"),
            Country  = GetString(c, "country")
        };
    }

    private static ExtractedLinks MapLinks(JsonElement root)
    {
        if (!root.TryGetProperty("contact_info", out var c))
            return new ExtractedLinks();

        return new ExtractedLinks
        {
            LinkedIn  = GetString(c, "linkedin"),
            GitHub    = GetString(c, "github"),
            LeetCode  = GetString(c, "leetcode"),
            Portfolio = GetString(c, "portfolio")
        };
    }

    private static List<ParsedEducation> MapEducation(JsonElement root)
    {
        var result = new List<ParsedEducation>();
        if (!root.TryGetProperty("education", out var arr))
            return result;

        foreach (var item in arr.EnumerateArray())
        {
            result.Add(new ParsedEducation
            {
                Institution       = GetString(item, "institution"),
                Degree            = GetString(item, "degree"),
                FieldOfStudy      = GetString(item, "field"),
                Grade             = GetString(item, "grade"),
                IsCurrentlyEnrolled = false,
                Achievements      = [],
                RelevantCoursework = []
            });
        }
        return result;
    }

    private static List<ParsedExperience> MapExperience(JsonElement root)
    {
        var result = new List<ParsedExperience>();
        if (!root.TryGetProperty("experience", out var arr))
            return result;

        foreach (var item in arr.EnumerateArray())
        {
            result.Add(new ParsedExperience
            {
                Company          = GetString(item, "company"),
                Title            = GetString(item, "title"),
                StartDate        = GetString(item, "start_date")
                                ?? GetString(item, "date_range"),
                EndDate          = GetString(item, "end_date"),
                IsCurrent        = false,
                Responsibilities = [],
                TechnologiesUsed = [],
                EmploymentType   = GetString(item, "employment_type")
            });
        }
        return result;
    }

    private static string? GetString(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out var v) &&
               v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    private static List<string> GetStringList(
        JsonElement el, string arrayKey, string fieldKey)
    {
        if (!el.TryGetProperty(arrayKey, out var arr))
            return [];

        return arr.EnumerateArray()
            .Where(i => i.TryGetProperty(fieldKey, out var f) &&
                        f.ValueKind == JsonValueKind.String)
            .Select(i => i.GetProperty(fieldKey).GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
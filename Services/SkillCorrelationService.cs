using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;

namespace Xllerate.Services;

/// <summary>
/// Traverses a skill correlation graph to infer implied skills from verified evidence.
/// E.g. GitHub repo uses Spring Boot → infer Java, REST APIs, Hibernate, etc.
/// </summary>
public class SkillCorrelationService(
    ApplicationDbContext db,
    ILogger<SkillCorrelationService> logger)
{
    // Correlation graph: skill → list of (impliedSkill, strength)
    // Strength = multiplier applied to the source skill's confidence
    // Only direct implications — no transitive chaining (keeps it explainable)
    private static readonly Dictionary<string, List<(string Skill, decimal Strength)>> CorrelationGraph
        = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Java Ecosystem ────────────────────────────────────────────────────
        ["spring boot"] =
        [
            ("java",           0.95m),
            ("spring",         0.95m),
            ("spring mvc",     0.85m),
            ("rest apis",      0.90m),
            ("hibernate",      0.70m),
            ("maven",          0.70m),
            ("sql",            0.65m),
            ("jwt",            0.60m),
            ("microservices",  0.55m),
        ],
        ["spring"] =
        [
            ("java",       0.95m),
            ("rest apis",  0.80m),
            ("maven",      0.65m),
            ("sql",        0.60m),
        ],
        ["hibernate"] =
        [
            ("java",          0.90m),
            ("sql",           0.80m),
            ("spring boot",   0.60m),
        ],
        ["java"] =
        [
            ("object-oriented programming", 0.85m),
            ("maven",                       0.65m),
            ("junit",                       0.60m),
        ],

        // ── JavaScript / TypeScript Ecosystem ─────────────────────────────────
        ["react"] =
        [
            ("javascript",  0.95m),
            ("html",        0.85m),
            ("css",         0.80m),
            ("rest apis",   0.70m),
            ("typescript",  0.55m),
        ],
        ["next.js"] =
        [
            ("react",       0.95m),
            ("javascript",  0.95m),
            ("typescript",  0.70m),
            ("html",        0.80m),
            ("css",         0.75m),
            ("rest apis",   0.65m),
            ("node.js",     0.60m),
        ],
        ["node.js"] =
        [
            ("javascript",  0.95m),
            ("rest apis",   0.80m),
            ("express",     0.70m),
            ("npm",         0.75m),
        ],
        ["express"] =
        [
            ("node.js",     0.95m),
            ("javascript",  0.90m),
            ("rest apis",   0.85m),
        ],
        ["nestjs"] =
        [
            ("node.js",     0.90m),
            ("typescript",  0.90m),
            ("rest apis",   0.85m),
            ("javascript",  0.80m),
        ],
        ["typescript"] =
        [
            ("javascript",  0.95m),
        ],
        ["vue"] =
        [
            ("javascript",  0.95m),
            ("html",        0.85m),
            ("css",         0.80m),
        ],
        ["angular"] =
        [
            ("typescript",  0.90m),
            ("javascript",  0.85m),
            ("html",        0.80m),
            ("css",         0.75m),
            ("rest apis",   0.65m),
        ],

        // ── Python Ecosystem ──────────────────────────────────────────────────
        ["django"] =
        [
            ("python",      0.95m),
            ("rest apis",   0.80m),
            ("sql",         0.70m),
            ("html",        0.60m),
        ],
        ["fastapi"] =
        [
            ("python",      0.95m),
            ("rest apis",   0.90m),
            ("asyncio",     0.65m),
            ("sql",         0.60m),
        ],
        ["flask"] =
        [
            ("python",      0.95m),
            ("rest apis",   0.80m),
            ("html",        0.60m),
        ],
        ["pytorch"] =
        [
            ("python",          0.95m),
            ("numpy",           0.85m),
            ("machine learning",0.85m),
            ("deep learning",   0.85m),
            ("pandas",          0.65m),
        ],
        ["tensorflow"] =
        [
            ("python",          0.95m),
            ("numpy",           0.80m),
            ("machine learning",0.85m),
            ("deep learning",   0.80m),
            ("pandas",          0.65m),
        ],
        ["scikit-learn"] =
        [
            ("python",          0.95m),
            ("pandas",          0.85m),
            ("numpy",           0.85m),
            ("machine learning",0.90m),
        ],
        ["pandas"] =
        [
            ("python",  0.95m),
            ("numpy",   0.80m),
            ("sql",     0.60m),
        ],

        // ── DevOps / Cloud ────────────────────────────────────────────────────
        ["kubernetes"] =
        [
            ("docker",      0.90m),
            ("linux",       0.80m),
            ("yaml",        0.75m),
            ("helm",        0.60m),
            ("ci/cd",       0.65m),
        ],
        ["docker"] =
        [
            ("linux",       0.75m),
            ("ci/cd",       0.60m),
            ("yaml",        0.65m),
        ],
        ["terraform"] =
        [
            ("aws",     0.70m),
            ("linux",   0.70m),
            ("ci/cd",   0.65m),
            ("yaml",    0.60m),
        ],
        ["ansible"] =
        [
            ("linux",   0.85m),
            ("yaml",    0.80m),
            ("ci/cd",   0.65m),
        ],
        ["aws"] =
        [
            ("linux",   0.65m),
            ("ci/cd",   0.55m),
        ],

        // ── Databases ─────────────────────────────────────────────────────────
        ["postgresql"] =
        [
            ("sql",     0.95m),
        ],
        ["mysql"] =
        [
            ("sql",     0.95m),
        ],
        ["mongodb"] =
        [
            ("nosql",   0.90m),
        ],
        ["redis"] =
        [
            ("nosql",   0.70m),
        ],
        ["elasticsearch"] =
        [
            ("nosql",   0.70m),
        ],

        // ── .NET Ecosystem ────────────────────────────────────────────────────
        ["asp.net"] =
        [
            ("c#",          0.95m),
            ("rest apis",   0.85m),
            ("sql",         0.65m),
            ("entity framework", 0.70m),
        ],
        ["c#"] =
        [
            ("object-oriented programming", 0.85m),
            (".net",                        0.90m),
        ],
        ["entity framework"] =
        [
            ("c#",  0.90m),
            ("sql", 0.80m),
        ],

        // ── Data / Streaming ─────────────────────────────────────────────────
        ["kafka"] =
        [
            ("distributed systems", 0.75m),
            ("microservices",       0.65m),
            ("java",                0.55m),
        ],
        ["spark"] =
        [
            ("python",  0.70m),
            ("java",    0.60m),
            ("scala",   0.60m),
            ("hadoop",  0.55m),
            ("sql",     0.65m),
        ],
    };

    public async Task InferAndUpsertAsync(Guid userId, CancellationToken ct = default)
    {
        // Load all current user skills with sources
        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Include(us => us.Sources)
            .Where(us => us.UserId == userId)
            .ToListAsync(ct);

        // Only infer from skills with real evidence (not already inferred)
        var evidencedSkills = userSkills
            .Where(us =>
                us.VerificationStatus != SkillVerificationStatus.Claimed &&
                us.Sources.Any(s => s.SourceType is SkillSourceType.GitHub
                                                  or SkillSourceType.LeetCode
                                                  or SkillSourceType.Resume))
            .ToList();

        if (evidencedSkills.Count == 0) return;

        var existingSkillMap = userSkills.ToDictionary(
            us => us.Skill.NormalizedName,
            us => us,
            StringComparer.OrdinalIgnoreCase);

        var inferredCount = 0;

        foreach (var sourceSkill in evidencedSkills)
        {
            var sourceName = sourceSkill.Skill.NormalizedName;

            if (!CorrelationGraph.TryGetValue(sourceName, out var correlations))
                continue;

            foreach (var (impliedName, strength) in correlations)
            {
                var inferredConfidence = Math.Round(sourceSkill.ConfidenceScore * strength, 4);

                // Not worth adding very weak inferences
                if (inferredConfidence < 0.40m) continue;

                var normalizedImplied = impliedName.ToLowerInvariant().Trim();

                if (existingSkillMap.TryGetValue(normalizedImplied, out var existing))
                {
                    // Never downgrade — only upgrade
                    if (inferredConfidence <= existing.ConfidenceScore) continue;

                    existing.ConfidenceScore = inferredConfidence;
                    existing.UpdatedAt = DateTime.UtcNow;

                    // Upgrade verification if inferred confidence is strong
                    if (inferredConfidence >= 0.70m &&
                        existing.VerificationStatus == SkillVerificationStatus.Claimed)
                        existing.VerificationStatus = SkillVerificationStatus.Evidenced;
                }
                else
                {
                    // Resolve or create skill record
                    var skill = await db.Skills
                        .FirstOrDefaultAsync(s => s.NormalizedName == normalizedImplied, ct);

                    if (skill == null)
                    {
                        // Proper-case the implied name for display
                        var displayName = ToDisplayName(impliedName);
                        skill = new Skill
                        {
                            Id             = Guid.NewGuid(),
                            Name           = displayName,
                            NormalizedName = normalizedImplied,
                            IsVerified     = false
                        };
                        db.Skills.Add(skill);
                        await db.SaveChangesAsync(ct);
                    }

                    var newUserSkill = new UserSkill
                    {
                        Id                 = Guid.NewGuid(),
                        UserId             = userId,
                        SkillId            = skill.Id,
                        ConfidenceScore    = inferredConfidence,
                        VerificationStatus = inferredConfidence >= 0.70m
                            ? SkillVerificationStatus.Evidenced
                            : SkillVerificationStatus.Claimed,
                        CreatedAt          = DateTime.UtcNow,
                        UpdatedAt          = DateTime.UtcNow
                    };
                    db.UserSkills.Add(newUserSkill);
                    await db.SaveChangesAsync(ct);

                    db.SkillSources.Add(new SkillSource
                    {
                        Id           = Guid.NewGuid(),
                        UserSkillId  = newUserSkill.Id,
                        SourceType   = SkillSourceType.Inferred,
                        Evidence     = $"Inferred from '{sourceSkill.Skill.Name}' (strength {strength:P0})",
                        Weight       = inferredConfidence,
                        CapturedAt   = DateTime.UtcNow
                    });

                    // Track in map so later iterations don't re-add
                    existingSkillMap[normalizedImplied] = newUserSkill;
                    inferredCount++;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Skill correlation inference complete for user {UserId} — {Count} skills inferred/upgraded.",
            userId, inferredCount);
    }

    private static string ToDisplayName(string name) => name switch
    {
        "rest apis"                     => "REST APIs",
        "ci/cd"                         => "CI/CD",
        "nosql"                         => "NoSQL",
        "sql"                           => "SQL",
        "jwt"                           => "JWT",
        "html"                          => "HTML",
        "css"                           => "CSS",
        "yaml"                          => "YAML",
        "npm"                           => "npm",
        ".net"                          => ".NET",
        "object-oriented programming"   => "Object-Oriented Programming",
        _  => System.Globalization.CultureInfo.InvariantCulture
                  .TextInfo.ToTitleCase(name)
    };
}
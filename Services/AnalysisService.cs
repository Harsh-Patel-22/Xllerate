using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Analysis;
using Xllerate.Models.MongoDB;

namespace Xllerate.Services;

public class AnalysisService( ApplicationDbContext db, InsightService insightService, MongoDbContext mongo,IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AnalysisService> logger)
{
    // Minimum confidence to consider a skill "matched" (not weak)
    private const decimal MatchThreshold = 0.60m;
    
    private static readonly HashSet<string> KnownTechSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        // Languages
        "java", "python", "go", "golang", "c#", "c++", "c", "rust", "ruby", "php",
        "swift", "kotlin", "scala", "typescript", "javascript", "r", "matlab",

        // Frontend
        "react", "react.js", "next.js", "angular", "vue", "vue.js", "svelte",
        "html", "css", "scss", "sass", "tailwind", "bootstrap", "webpack", "vite",

        // Backend
        "spring", "spring boot", "hibernate", "django", "fastapi", "flask",
        "express", "nestjs", "laravel", "asp.net", "node.js", "nodejs",

        // Databases
        "postgresql", "mysql", "mssql", "sql server", "mongodb", "redis",
        "cassandra", "elasticsearch", "dynamodb", "sqlite", "oracle",
        "timescaledb", "nosql", "sql",

        // Cloud & DevOps
        "aws", "gcp", "azure", "docker", "kubernetes", "k8s", "terraform",
        "ansible", "jenkins", "github actions", "gitlab ci", "ci/cd", "cicd",
        "linux", "bash", "shell",

        // Messaging & Streaming
        "kafka", "rabbitmq", "grpc", "rest", "restful", "graphql", "websocket",

        // ML/AI
        "pytorch", "tensorflow", "scikit-learn", "pandas", "numpy", "spark",
        "hadoop", "airflow", "mlflow",

        // Tools
        "git", "jira", "confluence", "figma", "postman", "swagger",

        // Architecture
        "microservices", "system design", "distributed systems", "event-driven",
        "j2ee", "android", "flutter", "xamarin"
    };

    private static List<ExtractedSkill> ExtractSkillsWithKeywords(string jobText)
    {
        var lower  = jobText.ToLowerInvariant();
        var result = new List<ExtractedSkill>();

        foreach (var skill in KnownTechSkills)
        {
            // Word boundary match — avoid "java" matching "javascript"
            var pattern = $@"\b{Regex.Escape(skill)}\b";
            var matches = Regex.Matches(lower, pattern);

            if (matches.Count == 0) continue;

            // Weight by frequency and position (earlier = more important)
            var firstIndex = matches[0].Index;
            var frequency  = matches.Count;
            var positionBoost = firstIndex < 500 ? 0.10m : 0.00m;

            var weight = frequency switch
            {
                >= 4 => 0.90m,
                3    => 0.80m,
                2    => 0.70m,
                _    => 0.60m
            } + positionBoost;

            result.Add(new ExtractedSkill(ToProperName(skill), Math.Min(weight, 1.0m)));
        }

        return result;
    }

    // Preserve casing for known skills
    private static string ToProperName(string skill) => skill switch
    {
        "javascript"  => "JavaScript",
        "typescript"  => "TypeScript",
        "python"      => "Python",
        "java"        => "Java",
        "go"          => "Go",
        "golang"      => "Go",
        "c#"          => "C#",
        "c++"         => "C++",
        "postgresql"  => "PostgreSQL",
        "mysql"       => "MySQL",
        "mongodb"     => "MongoDB",
        "redis"       => "Redis",
        "aws"         => "AWS",
        "gcp"         => "GCP",
        "azure"       => "Azure",
        "docker"      => "Docker",
        "kubernetes"  => "Kubernetes",
        "k8s"         => "Kubernetes",
        "terraform"   => "Terraform",
        "kafka"       => "Kafka",
        "rabbitmq"    => "RabbitMQ",
        "grpc"        => "gRPC",
        "graphql"     => "GraphQL",
        "react"       => "React",
        "next.js"     => "Next.js",
        "node.js"     => "Node.js",
        "spring boot" => "Spring Boot",
        "spring"      => "Spring",
        "django"      => "Django",
        "fastapi"     => "FastAPI",
        "nestjs"      => "NestJS",
        "ci/cd"       => "CI/CD",
        "nosql"       => "NoSQL",
        "sql"         => "SQL",
        "linux"       => "Linux",
        "git"         => "Git",
        _             => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(skill)
    };

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

        var dto = BuildResultDto(analysis, jobRole, gaps);
        await PersistToMongoAsync(dto, "Job", userId);
        return dto;
    }

    public async Task<AnalysisResultDto> GetAsync(Guid analysisId, Guid userId, CancellationToken ct = default)
    {
        var doc = await mongo.Analyses
                      .Find(a => a.AnalysisId == analysisId && a.UserId == userId)
                      .FirstOrDefaultAsync(ct)
                  ?? throw new KeyNotFoundException("Analysis not found.");

        return ToDto(doc);
    }

    public async Task<List<AnalysisResultDto>> GetAllForUserAgainstJobsAsync(Guid userId, CancellationToken ct = default)
    {
        var docs = await mongo.Analyses
            .Find(a => a.UserId == userId && a.AnalysisType == "Job")
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return docs.Select(ToDto).ToList();
    }
    public async Task<List<AnalysisResultDto>> GetAllForUserAgainstRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var docs = await mongo.Analyses
            .Find(a => a.UserId == userId && a.AnalysisType == "Role")
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        
        return docs.Select(ToDto).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static AnalysisResultDto ToDto(AnalysisDocument doc)
    {
        var insight = doc.InsightData == null ? null : new AnalysisInsight
        {
            Summary       = doc.InsightData.Summary,
            WhyNotHired   = doc.InsightData.WhyNotHired,
            Strengths     = doc.InsightData.Strengths,
            CriticalGaps  = doc.InsightData.CriticalGaps,
            MarketContext  = doc.InsightData.MarketContext
        };

        return new AnalysisResultDto
        {
            AnalysisId      = doc.AnalysisId,
            JobTitle        = doc.JobTitle,
            CompanyName     = doc.CompanyName,
            MatchPercentage = doc.MatchPercentage,
            Status          = doc.Status,
            CreatedAt       = doc.CreatedAt,
            InsightData     = insight,
            SoftSkillSuggestions = doc.SoftSkillSuggestions,
            Summary = new SkillSummaryDto
            {
                TotalRequired = doc.Summary.TotalRequired,
                Matched       = doc.Summary.Matched,
                Missing       = doc.Summary.Missing,
                Weak          = doc.Summary.Weak,
                Unverified    = doc.Summary.Unverified
            },
            SkillGaps = doc.SkillGaps.Select(g => new SkillGapDto
            {
                SkillName      = g.SkillName,
                GapType        = g.GapType,
                Severity       = g.Severity,
                UserScore      = g.UserScore,
                RequiredScore  = g.RequiredScore,
                Recommendation = g.Recommendation
            }).ToList()
        };
    }
    
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
    
    private static GapSeverity ClassifyPostingSeverity(decimal weight, GapType gapType)
    {
        if (gapType == GapType.Missing && weight >= 0.65m) return GapSeverity.Critical;
        if (gapType == GapType.Missing && weight >= 0.40m) return GapSeverity.Important;
        if (gapType == GapType.Weak    && weight >= 0.60m) return GapSeverity.Critical;
        if (gapType == GapType.Weak    && weight >= 0.35m) return GapSeverity.Important;
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
        var totalRequired = jobRole.RequiredSkills?.Count ?? gaps.Count;
        var fullyMatched  = totalRequired - gaps.Count;

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
                Matched       = Math.Max(0, fullyMatched),
                Missing       = missing,
                Weak          = weak,
                Unverified    = unverified
            }
        };
    }
    
    public async Task<AnalysisResultDto> AnalyzeAgainstDesiredRoleAsync(Guid userId, CancellationToken ct = default)
    {
        // ── Load user + desired role ──────────────────────────────────────────
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (string.IsNullOrWhiteSpace(user.DesiredRole))
            throw new InvalidOperationException("No desired role set. Update your profile first.");

        // ── Match desired role → RoleProfile ─────────────────────────────────
        // Try exact RoleKey match first, then DisplayName contains
        var roleProfile = await db.RoleProfiles
            .Include(rp => rp.Skills)
                .ThenInclude(rps => rps.Skill)
            .FirstOrDefaultAsync(rp =>
                rp.RoleKey == user.DesiredRole ||
                rp.DisplayName.Contains(user.DesiredRole), ct)
            ?? throw new KeyNotFoundException($"No aggregated role profile found for '{user.DesiredRole}'. Run aggregation first.");

        var requiredSkills = roleProfile.Skills.ToList();

        if (requiredSkills.Count == 0)
            throw new InvalidOperationException("Role profile has no skills. Run aggregation first.");

        // ── Load user skills ──────────────────────────────────────────────────
        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId)
            .ToListAsync(ct);

        var userSkillMap = userSkills.ToDictionary(
            us => us.Skill.NormalizedName,
            us => us,
            StringComparer.OrdinalIgnoreCase);

        // ── Create Analysis record ────────────────────────────────────────────
        var analysis = new Analysis
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            JobRoleId    = null,
            Status       = AnalysisStatus.Processing,
            ModelVersion = "v1.0",
            CreatedAt    = DateTime.UtcNow
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        // ── Core gap computation ──────────────────────────────────────────────
        var gaps        = new List<SkillGap>();
        var matchScore  = 0m;
        var totalWeight = requiredSkills.Sum(r => r.ProbabilisticScore);

        foreach (var required in requiredSkills)
        {
            var skillNorm = required.Skill.NormalizedName;
            userSkillMap.TryGetValue(skillNorm, out var userSkill);

            var userScore = ComputeUserScore(userSkill, required.ProbabilisticScore);
            var gapType   = ClassifyGap(userSkill, userScore, required.ProbabilisticScore);

            if (gapType == null)
            {
                matchScore += required.ProbabilisticScore;
                continue;
            }

            var severity = ClassifySeverity(required.ProbabilisticScore, gapType.Value);

            gaps.Add(new SkillGap
            {
                Id             = Guid.NewGuid(),
                AnalysisId     = analysis.Id,
                SkillId        = required.SkillId,
                GapType        = gapType.Value,
                Severity       = severity,
                UserScore      = userScore,
                RequiredScore  = required.AggregatedWeight,
                Recommendation = BuildRecommendation(required.Skill.Name, gapType.Value, severity)
            });

            matchScore += userScore;
        }

        // ── Finalize ──────────────────────────────────────────────────────────
        var matchPct = totalWeight > 0
            ? Math.Round((matchScore / totalWeight) * 100, 2)
            : 0m;

        matchPct = Math.Max(0, Math.Min(100, matchPct)); // clamp 0–100

        db.SkillGaps.AddRange(gaps);
        analysis.MatchPercentage = matchPct;
        analysis.Status          = AnalysisStatus.Complete;
        await db.SaveChangesAsync(ct);

        var result   = BuildRoleProfileResultDto(analysis, roleProfile, gaps);
        var insight  = await insightService.GenerateInsightsAsync(result, ct);

        analysis.InsightData = insight;
        await db.SaveChangesAsync(ct);

        result.InsightData = insight;
        
        await PersistToMongoAsync(result, "Role", userId);
        return result;
    }
    
    private static AnalysisResultDto BuildRoleProfileResultDto(
        Analysis analysis, RoleProfile roleProfile, List<SkillGap> gaps)
    {
        var totalRequired = roleProfile.Skills.Count;
        var missing       = gaps.Count(g => g.GapType == GapType.Missing);
        var weak          = gaps.Count(g => g.GapType == GapType.Weak);
        var unverified    = gaps.Count(g => g.GapType == GapType.Unverified);

        return new AnalysisResultDto
        {
            AnalysisId      = analysis.Id,
            JobTitle        = roleProfile.DisplayName,
            CompanyName     = "Market Aggregate",
            MatchPercentage = analysis.MatchPercentage,
            Status          = analysis.Status.ToString(),
            CreatedAt       = analysis.CreatedAt,
            InsightData = analysis.InsightData,
            SkillGaps       = gaps.OrderByDescending(g => g.Severity)
                .ThenByDescending(g => g.RequiredScore)
                .Select(g => new SkillGapDto
                {
                    SkillName      = g.Skill?.Name ?? "Unknown",
                    GapType        = g.GapType.ToString(),
                    Severity       = g.Severity.ToString(),
                    UserScore      = g.UserScore,
                    RequiredScore  = g.RequiredScore,
                    Recommendation = g.Recommendation
                }).ToList(),
            Summary = new SkillSummaryDto
            {
                TotalRequired = totalRequired,
                Matched       = Math.Max(0, totalRequired - gaps.Count),
                Missing       = missing,
                Weak          = weak,
                Unverified    = unverified
            }
        };
    }
    
    private static readonly Dictionary<string, string[]> SkillAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["restful apis"]    = ["rest", "rest api", "restapi", "rest apis", "restful"],
        ["rest apis"]       = ["rest", "rest api", "restapi", "restful apis", "restful"],
        ["spring boot"]     = ["spring", "springboot"],
        ["spring"]          = ["spring boot", "springboot"],
        ["postgresql"]      = ["postgres", "psql"],
        ["microservices"]   = ["microservice", "micro services"],
        ["javascript"]      = ["js"],
        ["typescript"]      = ["ts"],
        ["node.js"]         = ["node", "nodejs"],
        ["react.js"]        = ["react", "reactjs"],
        ["next.js"]         = ["next", "nextjs"],
        ["kubernetes"]      = ["k8s"],
        ["ci/cd"]           = ["cicd", "ci cd", "continuous integration"],
        ["nosql"]           = ["no-sql"],
        ["j2ee"]            = ["java ee", "javaee"],
        ["java fullstack"]  = ["java full stack", "java"],
    };

    private bool UserHasSkill(Dictionary<string, UserSkill> userSkillMap, string extractedName)
    {
        var norm = Normalize(extractedName);
        if (userSkillMap.ContainsKey(norm)) return true;

        // Check aliases
        if (SkillAliases.TryGetValue(norm, out var aliases))
            return aliases.Any(userSkillMap.ContainsKey);

        // Check reverse — extracted name is alias of something user has
        foreach (var (key, aliases2) in SkillAliases)
            if (aliases2.Contains(norm, StringComparer.OrdinalIgnoreCase) && userSkillMap.ContainsKey(key))
                return true;

        return false;
    }

    private UserSkill? ResolveUserSkill(Dictionary<string, UserSkill> userSkillMap, string extractedName)
    {
        var norm = Normalize(extractedName);
        if (userSkillMap.TryGetValue(norm, out var direct)) return direct;

        if (SkillAliases.TryGetValue(norm, out var aliases))
            foreach (var alias in aliases)
                if (userSkillMap.TryGetValue(alias, out var match)) return match;

        foreach (var (key, aliases2) in SkillAliases)
            if (aliases2.Contains(norm, StringComparer.OrdinalIgnoreCase) && userSkillMap.TryGetValue(key, out var match))
                return match;

        return null;
    }
    
    public async Task<AnalysisResultDto> AnalyzeAgainstPostingAsync(
    Guid userId, AnalyzeAgainstPostingRequest request, CancellationToken ct = default)
        {
            // ── Resolve job text ──────────────────────────────────────────────────
            string jobText;

            if (!string.IsNullOrWhiteSpace(request.JobUrl))
                jobText = await ScrapeJobTextAsync(request.JobUrl, ct);
            else if (!string.IsNullOrWhiteSpace(request.JobText))
                jobText = request.JobText;
            else
                throw new ArgumentException("Provide either JobText or JobUrl.");

            // ── Extract skills from job text ───────────────────────────
            var extractedSkills = await ExtractSkillsFromTextAsync(jobText, ct);

            if (extractedSkills.Technical.Count == 0)
                throw new InvalidOperationException("Could not extract any technical skills from the job posting.");

            // ── Load user skills ──────────────────────────────────────────────────
            var userSkills = await db.UserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == userId)
                .ToListAsync(ct);

            var userSkillMap = userSkills.ToDictionary(
                us => us.Skill.NormalizedName,
                us => us,
                StringComparer.OrdinalIgnoreCase);

            // ── Score against extracted skills — no DB persistence ───────────────
            var gapSkillNames = new Dictionary<Guid, string>();
            
            var gaps        = new List<SkillGap>();
            var matchScore  = 0m;
            var totalWeight = extractedSkills.Technical.Sum(s => s.Weight);

            foreach (var required in extractedSkills.Technical)
            {
                
                var userSkill = ResolveUserSkill(userSkillMap, required.Name);

                var userScore = ComputeUserScore(userSkill, required.Weight);
                var gapType   = ClassifyGap(userSkill, userScore, required.Weight);

                if (gapType == null)
                {
                    matchScore += required.Weight;
                    continue;
                }

                var severity = ClassifyPostingSeverity(required.Weight, gapType.Value);

                // Not persisted — SkillId is Guid.Empty for extracted-only skills
                var tempId = Guid.NewGuid();
                gapSkillNames[tempId] = required.Name;
                gaps.Add(new SkillGap
                {
                    Id            = tempId,
                    AnalysisId    = Guid.Empty,
                    SkillId       = Guid.Empty,
                    GapType       = gapType.Value,
                    Severity      = severity,
                    UserScore     = userScore,
                    RequiredScore = required.Weight,
                    Recommendation = BuildRecommendation(required.Name, gapType.Value, severity)
                });

                matchScore += userScore;
            }

            var matchPct = totalWeight > 0
                ? Math.Clamp(Math.Round((matchScore / totalWeight) * 100, 2), 0, 100)
                : 0m;

            // ── Build ephemeral result (no Analysis row saved) ────────────────────
            var ephemeralAnalysis = new Analysis
            {
                Id              = Guid.NewGuid(),
                UserId          = userId,
                MatchPercentage = matchPct,
                Status          = AnalysisStatus.Complete,
                ModelVersion    = "v1.0-posting",
                CreatedAt       = DateTime.UtcNow
            };

            var result = BuildPostingResultDto(ephemeralAnalysis, gaps, extractedSkills.Technical.Count, gapSkillNames);
            result.SoftSkillSuggestions = extractedSkills.Soft;

            // ── Gemini insights ───────────────────────────────────────────────────
            var insight     = await insightService.GenerateInsightsAsync(result, ct);
            result.InsightData  = insight;
            await PersistToMongoAsync(result, "Posting", userId);
            return result;
        }
    
        private async Task<string> ScrapeJobTextAsync(string url, CancellationToken ct)
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var html     = await client.GetStringAsync(url, ct);
            html = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</(script|style)>", " ", RegexOptions.IgnoreCase);

            // Strip tags, collapse whitespace
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text     = Regex.Replace(text, @"\s{2,}", " ").Trim();

            if (text.Length > 8000) text = text[..8000]; // Gemini token budget

            return text;
        }
        
        private record ExtractedSkill(string Name, decimal Weight);
        private record ExtractionResult(List<ExtractedSkill> Technical, List<string> Soft);

        private async Task<ExtractionResult> ExtractSkillsWithGeminiAsync(
            string jobText, HashSet<string> alreadyFound, CancellationToken ct)
        {
            var apiKey = config["Gemini:ApiKey"];
            var model  = config["Gemini:Model"] ?? "gemini-2.5-flash";
            
            var prompt = $$"""
                           Extract ONLY technical skills that are explicitly mentioned in this job posting. That are NOT in this list: {{string.Join(", ", alreadyFound)}}
                           Do NOT infer or add skills that are not written in the text.
                           Include: programming languages, frameworks, libraries, tools, platforms, databases, cloud services, DevOps tools.
                           Exclude: soft skills, communication, teamwork, leadership, years of experience, education requirements.
                           Be specific: "Spring" not "Backend Frameworks", "Git" not "Version Control".

                           Assign importance weight 0.1–1.0:
                           - 0.9–1.0: labeled "must have", "required", "proficiency in"
                           - 0.6–0.8: listed under professional/technical skills without "must"
                           - 0.3–0.5: labeled "good to have", "familiar with", "knowledge of"
                           - 0.1–0.2: mentioned only in passing

                           Return ONLY a JSON object. No markdown, no preamble:
                           {
                               "technical": [{"name":"Java","weight":0.95},{"name":"Spring","weight":0.75}],
                               "soft": ["Teamwork","Leadership"]
                           }

                           Job posting:
                           {{jobText}}
                           """;

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { responseMimeType = "application/json" }
            };

            var client   = httpClientFactory.CreateClient("gemini");
            var url      = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var raw  = await response.Content.ReadAsStringAsync(ct);
            var doc  = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "[]";
            logger.LogInformation("Gemini extraction raw response: {Text}", text);
            try
            {
                var root = JsonDocument.Parse(text).RootElement;
                    
                var technical = root.GetProperty("technical").EnumerateArray()
                    .Select(el => new ExtractedSkill(
                        el.GetProperty("name").GetString() ?? string.Empty,
                        el.GetProperty("weight").GetDecimal()))
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                    .ToList();
                
                var soft = root.GetProperty("soft").EnumerateArray()
                    .Select(el => el.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                return new ExtractionResult(technical, soft);
            }
            catch { return new ExtractionResult([], []); }
        }
        
        private async Task<ExtractionResult> ExtractSkillsFromTextAsync(
            string jobText, CancellationToken ct)
        {
            var keywordSkills = ExtractSkillsWithKeywords(jobText);
            var keywordNames  = keywordSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Phase 2 — Gemini for anything not caught by keywords
            var geminiResult  = await ExtractSkillsWithGeminiAsync(jobText, keywordNames, ct);

            // Merge — keyword results take priority, Gemini fills gaps
            var merged = keywordSkills.ToList();
            merged.AddRange(geminiResult.Technical.Where(g => !keywordNames.Contains(g.Name)));

            return geminiResult with { Technical = merged };
        }
        private static AnalysisResultDto BuildPostingResultDto(
            Analysis analysis, List<SkillGap> gaps, int totalRequired,
            Dictionary<Guid, string> gapSkillNames)
        {
            var missing    = gaps.Count(g => g.GapType == GapType.Missing);
            var weak       = gaps.Count(g => g.GapType == GapType.Weak);
            var unverified = gaps.Count(g => g.GapType == GapType.Unverified);

            return new AnalysisResultDto
            {
                AnalysisId      = analysis.Id,
                JobTitle        = "Custom Job Posting",
                CompanyName     = "Unknown",
                MatchPercentage = analysis.MatchPercentage,
                Status          = analysis.Status.ToString(),
                CreatedAt       = analysis.CreatedAt,
                SkillGaps       = gaps.OrderByDescending(g => g.Severity)
                    .ThenByDescending(g => g.RequiredScore)
                    .Select(g => new SkillGapDto
                    {
                        SkillName      = gapSkillNames.TryGetValue(g.Id, out var n) ? n : "Unknown",
                        GapType        = g.GapType.ToString(),
                        Severity       = g.Severity.ToString(),
                        UserScore      = g.UserScore,
                        RequiredScore  = g.RequiredScore,
                        Recommendation = g.Recommendation
                    }).ToList(),
                Summary = new SkillSummaryDto
                {
                    TotalRequired = totalRequired,
                    Matched       = Math.Max(0, totalRequired - gaps.Count),
                    Missing       = missing,
                    Weak          = weak,
                    Unverified    = unverified
                }
            };
        }
        private static string Normalize(string name) =>
            name.ToLowerInvariant().Trim();
        
        private async Task PersistToMongoAsync(AnalysisResultDto dto, string analysisType, Guid userId)
        {
            var insight = dto.InsightData == null ? null : new AnalysisInsightDocument
            {
                Summary      = dto.InsightData.Summary,
                WhyNotHired  = dto.InsightData.WhyNotHired,
                Strengths    = dto.InsightData.Strengths,
                CriticalGaps = dto.InsightData.CriticalGaps,
                MarketContext = dto.InsightData.MarketContext
            };

            var doc = new AnalysisDocument
            {
                AnalysisId   = dto.AnalysisId,
                UserId       = userId,
                JobTitle     = dto.JobTitle,
                CompanyName  = dto.CompanyName,
                MatchPercentage = dto.MatchPercentage,
                Status       = dto.Status,
                AnalysisType = analysisType,
                CreatedAt    = dto.CreatedAt,
                InsightData  = insight,
                SoftSkillSuggestions = dto.SoftSkillSuggestions,
                Summary = new SkillSummaryDocument
                {
                    TotalRequired = dto.Summary.TotalRequired,
                    Matched       = dto.Summary.Matched,
                    Missing       = dto.Summary.Missing,
                    Weak          = dto.Summary.Weak,
                    Unverified    = dto.Summary.Unverified
                },
                SkillGaps = dto.SkillGaps.Select(g => new SkillGapDocument
                {
                    SkillName      = g.SkillName,
                    GapType        = g.GapType,
                    Severity       = g.Severity,
                    UserScore      = g.UserScore,
                    RequiredScore  = g.RequiredScore,
                    Recommendation = g.Recommendation
                }).ToList()
            };

            await mongo.Analyses.ReplaceOneAsync(
                a => a.AnalysisId == dto.AnalysisId,
                doc,
                new ReplaceOptions { IsUpsert = true });
        }
        
        private static string NormalizeUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                // Keep scheme + host + path, drop query + fragment
                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.TrimEnd('/')}".ToLowerInvariant();
            }
            catch
            {
                return url.ToLowerInvariant().TrimEnd('/');
            }
        }
}
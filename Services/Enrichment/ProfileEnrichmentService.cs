using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Enrichment;

namespace Xllerate.Services.Enrichment;

public class ProfileEnrichmentService(
    ApplicationDbContext db,
    GitHubEnrichmentService gitHub,
    LeetCodeEnrichmentService leetCode,
    SkillCorrelationService correlation,
    ILogger<ProfileEnrichmentService> logger) {
    // Language → normalized skill name mapping
    private static readonly Dictionary<string, string> LanguageSkillMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["C#"]         = "c#",
        ["JavaScript"] = "javascript",
        ["TypeScript"] = "typescript",
        ["Python"]     = "python",
        ["Java"]       = "java",
        ["Go"]         = "go",
        ["Rust"]       = "rust",
        ["C++"]        = "c++",
        ["C"]          = "c",
        ["Ruby"]       = "ruby",
        ["PHP"]        = "php",
        ["Swift"]      = "swift",
        ["Kotlin"]     = "kotlin",
        ["Scala"]      = "scala",
        ["Shell"]      = "bash",
        ["Dockerfile"] = "docker",
        ["HCL"]        = "terraform",
        ["HTML"]       = "html",
        ["CSS"]        = "css",
        ["SCSS"]       = "css",
    };

    // ── GitHub ────────────────────────────────────────────────────────────────

    public async Task<EnrichmentSummary> EnrichFromGitHubAsync(
        Guid userId, string username)
    {
        var profile = await GetOrCreateProfileAsync(
            userId, Platform.GitHub, username);

        profile.SyncStatus = SyncStatus.Syncing;
        await db.SaveChangesAsync();

        try
        {
            // Fetch from GitHub API
            var data = await gitHub.FetchAsync(username);

            // Persist raw snapshot
            profile.RawData = JsonSerializer.Serialize(data);
            profile.Username = data.Profile.Login;
            profile.LastSyncedAt = DateTime.UtcNow;

            // Extract skills from languages
            var skillsExtracted = await ExtractGitHubSkillsAsync(
                userId, data, profile.Id);

            // Populate projects from repos
            var projectsAdded = await SyncGitHubProjectsAsync(userId, data, profile.Id);

            profile.SyncStatus = SyncStatus.Complete;
            profile.SyncErrorMessage = null;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "GitHub enrichment complete for user {UserId} — " +
                "{Skills} skills, {Projects} projects",
                userId, skillsExtracted, projectsAdded);
            
            await correlation.InferAndUpsertAsync(userId);

            return new EnrichmentSummary
            {
                SkillsExtracted = skillsExtracted,
                ProjectsAdded   = projectsAdded
            };
        }
        catch (Exception ex)
        {
            profile.SyncStatus = SyncStatus.Failed;
            profile.SyncErrorMessage = ex.Message;
            await db.SaveChangesAsync();

            logger.LogError(ex,
                "GitHub enrichment failed for user {UserId}", userId);
            throw;
        }
    }

    // ── LeetCode ──────────────────────────────────────────────────────────────

    public async Task<EnrichmentSummary> EnrichFromLeetCodeAsync(
        Guid userId, string username)
    {
        var profile = await GetOrCreateProfileAsync(
            userId, Platform.LeetCode, username);

        profile.SyncStatus = SyncStatus.Syncing;
        await db.SaveChangesAsync();

        try
        {
            var data = await leetCode.FetchAsync(username);

            profile.RawData = JsonSerializer.Serialize(data);
            profile.Username = username;
            profile.LastSyncedAt = DateTime.UtcNow;

            var skillsExtracted = await ExtractLeetCodeSkillsAsync(
                userId, data, profile.Id);

            // Update UserSkill metadata with LeetCode stats
            await UpdateLeetCodeMetadataAsync(userId, data);

            profile.SyncStatus = SyncStatus.Complete;
            profile.SyncErrorMessage = null;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "LeetCode enrichment complete for user {UserId} — " +
                "{Skills} skills extracted, {Solved} problems solved",
                userId, skillsExtracted, data.TotalSolved);

            await correlation.InferAndUpsertAsync(userId);
            return new EnrichmentSummary
            {
                SkillsExtracted = skillsExtracted,
                ProjectsAdded   = 0
            };
        }
        catch (Exception ex)
        {
            profile.SyncStatus = SyncStatus.Failed;
            profile.SyncErrorMessage = ex.Message;
            await db.SaveChangesAsync();

            logger.LogError(ex,
                "LeetCode enrichment failed for user {UserId}", userId);
            throw;
        }
    }

    // ── GitHub skill extraction ───────────────────────────────────────────────

    private async Task<int> ExtractGitHubSkillsAsync(
        Guid userId,
        GitHubEnrichmentResult data,
        Guid profileId)
    {
        var count = 0;

        foreach (var (language, bytes) in data.AggregatedLanguages)
        {
            if (!LanguageSkillMap.TryGetValue(language, out var normalizedName))
                normalizedName = language.ToLower().Trim();

            // Confidence based on how dominant the language is
            var totalBytes = data.AggregatedLanguages.Values.Sum();
            var ratio = totalBytes > 0 ? (double)bytes / totalBytes : 0;
            
            // How many repos use this language
            var repoCount = data.Repos.Count(r => r.Language == language);
            var repoRatio = data.Repos.Count > 0 ? (double)repoCount / data.Repos.Count : 0;
            
            var confidence = (ratio, repoRatio) switch
            {
                (> 0.40, > 0.40)  => 0.90m,   // primary language
                (> 0.25, > 0.25)  => 0.80m,   // secondary language
                (> 0.10, > 0.15)  => 0.70m,   // used language
                (> 0.05, _)               => 0.60m,   // used language
                _                              => 0.50m    // minor usage
            };

            var updated = await UpsertUserSkillAsync(
                userId, language, normalizedName,
                confidence, SkillSourceType.GitHub,
                $"Used in {data.Repos.Count(r => r.Language == language)} repos " +
                $"({bytes:N0} bytes)",
                profileId);

            if (updated) count++;
        }
        
        // Extract frameworks from repo descriptions + topics
        var frameworkCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var repo in data.Repos.Where(r => !r.Fork).Take(20))
        {
            var textToScan = string.Join(" ",
                repo.Description ?? "",
                string.Join(" ", repo.Topics ?? []));

            foreach (var framework in ExtractFrameworksFromText(textToScan))
                frameworkCounts[framework] = frameworkCounts.GetValueOrDefault(framework, 0) + 1;
        }

        foreach (var (framework, repoCount) in frameworkCounts)
        {
            var normalizedName = framework.ToLowerInvariant().Trim();

            // More repos mentioning it = higher confidence, capped at 0.65
            var confidence = repoCount switch
            {
                >= 5 => 0.75m,
                >= 3 => 0.65m,
                2    => 0.55m,
                _    => 0.45m
            };
            
            var updated = await UpsertUserSkillAsync(
                userId, framework, normalizedName,
                confidence, SkillSourceType.GitHub,
                $"Detected in {repoCount} repo description(s) or topics",
                profileId);

            if (updated) count++;
        }

        return count;
    }

    // ── LeetCode skill extraction ─────────────────────────────────────────────

    private async Task<int> ExtractLeetCodeSkillsAsync(
        Guid userId,
        LeetCodeEnrichmentResult data,
        Guid profileId)
    {
        var count = 0;

        // Extract languages used in submissions
        foreach (var lang in data.LanguagesUsed)
        {
            if (!LanguageSkillMap.TryGetValue(lang, out var normalizedName))
                normalizedName = lang.ToLower().Trim();

            // Weight by how many problems solved — more problems = more confident
            var langConfidence = data.TotalSolved switch
            {
                > 300 => 0.90m,
                > 150 => 0.80m,
                > 75  => 0.70m,
                > 25  => 0.60m,
                _     => 0.50m
            };
            
            // LeetCode usage confirms practical coding ability
            var updated = await UpsertUserSkillAsync(
                userId, lang, normalizedName,
                langConfidence,
                SkillSourceType.LeetCode,
                $"Used to solve {data.TotalSolved} LeetCode problems",
                profileId);

            if (updated) count++;
        }

        // Add DSA skill based on solve count
        if (data.TotalSolved > 0)
        {
            var dsaConfidence = data.TotalSolved switch
            {
                > 300 => 0.95m,
                > 150 => 0.85m,
                > 75  => 0.75m,
                > 25  => 0.65m,
                _     => 0.55m
            };

            await UpsertUserSkillAsync(
                userId,
                "Data Structures & Algorithms",
                "data structures & algorithms",
                dsaConfidence,
                SkillSourceType.LeetCode,
                $"Solved {data.TotalSolved} problems " +
                $"(Easy: {data.EasySolved}, " +
                $"Medium: {data.MediumSolved}, " +
                $"Hard: {data.HardSolved})",
                profileId);

            count++;
        }

        return count;
    }

    private async Task UpdateLeetCodeMetadataAsync(
        Guid userId, LeetCodeEnrichmentResult data)
    {
        // Update metadata on all LeetCode-sourced UserSkills
        var leetCodeSkills = await db.UserSkills
            .Where(us => us.UserId == userId &&
                         us.Sources.Any(s => s.SourceType == SkillSourceType.LeetCode))
            .Include(us => us.Metadata)
            .ToListAsync();

        foreach (var skill in leetCodeSkills)
        {
            skill.Metadata ??= new SkillMetadata();
            skill.Metadata.LeetcodeTagSolves = data.TotalSolved;
            skill.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    // ── GitHub projects sync ──────────────────────────────────────────────────

    private static decimal ComputeProjectComplexity(GitHubRepoDto repo)
    {
        var score = 0m;

        var text = string.Join(" ",
            repo.Description ?? "",
            string.Join(" ", repo.Topics ?? [])).ToLowerInvariant();

        // Topics signal intentional architecture choices
        score += (repo.Topics?.Count ?? 0) switch
        {
            >= 5 => 0.20m,
            >= 2 => 0.10m,
            _    => 0.00m
        };

        // Documented projects tend to be more serious
        score += (repo.Description?.Length ?? 0) switch
        {
            > 100 => 0.15m,
            > 40  => 0.08m,
            _     => 0.00m
        };

        // DevOps/architecture signals
        if (text.Contains("docker") || text.Contains("kubernetes") || text.Contains("k8s"))
            score += 0.15m;

        if (text.Contains("ci") || text.Contains("cd") || text.Contains("pipeline") || text.Contains("actions"))
            score += 0.10m;

        if (text.Contains("microservice") || text.Contains("distributed") || text.Contains("event-driven"))
            score += 0.10m;

        // Production/deployment signals
        if (text.Contains("production") || text.Contains("deployed") || text.Contains("live"))
            score += 0.10m;

        return Math.Clamp(0.55m + score, 0.55m, 0.92m);
    }
    
    private async Task<int> SyncGitHubProjectsAsync(
        Guid userId, GitHubEnrichmentResult data, Guid profileId)
    {
        var added = 0;

        foreach (var repo in data.Repos.Where(r => !r.Fork).Take(20))
        {
            var existing = await db.Projects
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId &&
                    p.RepoUrl == repo.HtmlUrl);

            if (existing is not null)
            {
                // Update metrics on existing project
                existing.GithubMetrics = new GithubMetrics
                {
                    Stars        = repo.StargazersCount,
                    Forks        = repo.ForksCount,
                    LastCommitAt = repo.PushedAt,
                    PrimaryLanguage = repo.Language
                };
                continue;
            }

            // Build tech stack from repo language
            var techStack = new List<string>();
            if (!string.IsNullOrWhiteSpace(repo.Language))
                techStack.Add(repo.Language);
            
            // var complexityBoost = ComputeProjectComplexity(repo);
            //
            // foreach (var tech in techStack)
            // {
            //     var normalized = tech.ToLowerInvariant().Trim();
            //     await UpsertUserSkillAsync(
            //         userId, tech, normalized,
            //         complexityBoost, SkillSourceType.GitHub,
            //         $"Built project '{repo.Name}' — {repo.StargazersCount} stars, {repo.ForksCount} forks",
            //         profileId);
            // }
            
            db.Projects.Add(new Project
            {
                Id           = Guid.NewGuid(),
                UserId       = userId,
                Title        = repo.Name,
                Description  = repo.Description,
                RepoUrl      = repo.HtmlUrl,
                IsFromGithub = true,
                TechStack    = techStack,
                CreatedAt    = DateTime.UtcNow,
                GithubMetrics = new GithubMetrics
                {
                    Stars           = repo.StargazersCount,
                    Forks           = repo.ForksCount,
                    LastCommitAt    = repo.PushedAt,
                    PrimaryLanguage = repo.Language
                }
            });
            added++;
        }

        await db.SaveChangesAsync();
        return added;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static readonly string[] KnownFrameworkKeywords =
    [
        "spring boot", "spring", "react", "next.js", "angular", "vue",
        "django", "fastapi", "flask", "express", "nestjs", "laravel",
        "postgresql", "mysql", "mongodb", "redis", "kafka", "rabbitmq",
        "docker", "kubernetes", "terraform", "aws", "gcp", "azure",
        "graphql", "rest", "grpc", "microservices", "hibernate", "j2ee",
        "android", "flutter", "pytorch", "tensorflow", "elasticsearch"
    ];

    private static IEnumerable<string> ExtractFrameworksFromText(string text)
    {
        var lower = text.ToLowerInvariant();
        return KnownFrameworkKeywords.Where(kw => lower.Contains(kw));
    }
    
    private async Task<bool> UpsertUserSkillAsync(
        Guid userId,
        string skillName,
        string normalizedName,
        decimal confidence,
        SkillSourceType sourceType,
        string evidence,
        Guid sourceEntityId)
    {
        // Resolve or create canonical skill
        var skill = await db.Skills
            .FirstOrDefaultAsync(s => s.NormalizedName == normalizedName);

        if (skill is null)
        {
            skill = new Skill
            {
                Id             = Guid.NewGuid(),
                Name           = skillName,
                NormalizedName = normalizedName,
                IsVerified     = false
            };
            db.Skills.Add(skill);
            await db.SaveChangesAsync();
        }

        var userSkill = await db.UserSkills
            .Include(us => us.Sources)
            .FirstOrDefaultAsync(us =>
                us.UserId == userId && us.SkillId == skill.Id);

        var isNew = userSkill is null;

        if (isNew)
        {
            userSkill = new UserSkill
            {
                Id                 = Guid.NewGuid(),
                UserId             = userId,
                SkillId            = skill.Id,
                ConfidenceScore    = confidence,
                VerificationStatus = confidence >= 0.65m
                    ? SkillVerificationStatus.Evidenced
                    : SkillVerificationStatus.Claimed,
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow
            };
            db.UserSkills.Add(userSkill);
        }
        else
        {
            // Enrich existing skill — only upgrade confidence, never downgrade
            if (confidence > userSkill!.ConfidenceScore)
            {
                userSkill.ConfidenceScore = confidence;
                userSkill.VerificationStatus = confidence >= 0.80m
                    ? SkillVerificationStatus.Evidenced
                    : userSkill.VerificationStatus;
            }
            userSkill.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Add source record — one per source type per skill
        var sourceExists = await db.SkillSources
            .AnyAsync(s =>
                s.UserSkillId == userSkill.Id &&
                s.SourceType  == sourceType &&
                s.SourceEntityId == sourceEntityId);

        if (!sourceExists)
        {
            db.SkillSources.Add(new SkillSource
            {
                Id             = Guid.NewGuid(),
                UserSkillId    = userSkill.Id,
                SourceType     = sourceType,
                SourceEntityId = sourceEntityId,
                Evidence       = evidence,
                Weight         = confidence,
                CapturedAt     = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return isNew;
    }

    private async Task<ExternalProfile> GetOrCreateProfileAsync(
        Guid userId, Platform platform, string username)
    {
        var profile = await db.ExternalProfiles
            .FirstOrDefaultAsync(ep =>
                ep.UserId == userId && ep.Platform == platform);

        if (profile is not null)
            return profile;

        profile = new ExternalProfile
        {
            Id         = Guid.NewGuid(),
            UserId     = userId,
            Platform   = platform,
            Username   = username,
            ProfileUrl = platform switch
            {
                Platform.GitHub   => $"https://github.com/{username}",
                Platform.LeetCode => $"https://leetcode.com/{username}",
                _                 => ""
            },
            SyncStatus = SyncStatus.Pending
        };

        db.ExternalProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }
}
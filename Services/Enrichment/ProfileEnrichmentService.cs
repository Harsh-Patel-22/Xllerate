using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Enrichment;

namespace Xllerate.Services.Enrichment;

public class ProfileEnrichmentService
{
    private readonly ApplicationDbContext _db;
    private readonly GitHubEnrichmentService _gitHub;
    private readonly LeetCodeEnrichmentService _leetCode;
    private readonly ILogger<ProfileEnrichmentService> _logger;

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

    public ProfileEnrichmentService(
        ApplicationDbContext db,
        GitHubEnrichmentService gitHub,
        LeetCodeEnrichmentService leetCode,
        ILogger<ProfileEnrichmentService> logger)
    {
        _db = db;
        _gitHub = gitHub;
        _leetCode = leetCode;
        _logger = logger;
    }

    // ── GitHub ────────────────────────────────────────────────────────────────

    public async Task<EnrichmentSummary> EnrichFromGitHubAsync(
        Guid userId, string username)
    {
        var profile = await GetOrCreateProfileAsync(
            userId, Platform.GitHub, username);

        profile.SyncStatus = SyncStatus.Syncing;
        await _db.SaveChangesAsync();

        try
        {
            // Fetch from GitHub API
            var data = await _gitHub.FetchAsync(username);

            // Persist raw snapshot
            profile.RawData = JsonSerializer.Serialize(data);
            profile.Username = data.Profile.Login;
            profile.LastSyncedAt = DateTime.UtcNow;

            // Extract skills from languages
            var skillsExtracted = await ExtractGitHubSkillsAsync(
                userId, data, profile.Id);

            // Populate projects from repos
            var projectsAdded = await SyncGitHubProjectsAsync(userId, data);

            profile.SyncStatus = SyncStatus.Complete;
            profile.SyncErrorMessage = null;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "GitHub enrichment complete for user {UserId} — " +
                "{Skills} skills, {Projects} projects",
                userId, skillsExtracted, projectsAdded);

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
            await _db.SaveChangesAsync();

            _logger.LogError(ex,
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
        await _db.SaveChangesAsync();

        try
        {
            var data = await _leetCode.FetchAsync(username);

            profile.RawData = JsonSerializer.Serialize(data);
            profile.Username = username;
            profile.LastSyncedAt = DateTime.UtcNow;

            var skillsExtracted = await ExtractLeetCodeSkillsAsync(
                userId, data, profile.Id);

            // Update UserSkill metadata with LeetCode stats
            await UpdateLeetCodeMetadataAsync(userId, data);

            profile.SyncStatus = SyncStatus.Complete;
            profile.SyncErrorMessage = null;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "LeetCode enrichment complete for user {UserId} — " +
                "{Skills} skills extracted, {Solved} problems solved",
                userId, skillsExtracted, data.TotalSolved);

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
            await _db.SaveChangesAsync();

            _logger.LogError(ex,
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
            var confidence = ratio switch
            {
                > 0.4  => 0.85m,   // primary language
                > 0.2  => 0.75m,   // secondary language
                > 0.05 => 0.65m,   // used language
                _      => 0.55m    // minor usage
            };

            var updated = await UpsertUserSkillAsync(
                userId, language, normalizedName,
                confidence, SkillSourceType.GitHub,
                $"Used in {data.Repos.Count(r => r.Language == language)} repos " +
                $"({bytes:N0} bytes)",
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

            // LeetCode usage confirms practical coding ability
            var updated = await UpsertUserSkillAsync(
                userId, lang, normalizedName,
                confidence: 0.80m,
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
        var leetCodeSkills = await _db.UserSkills
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

        await _db.SaveChangesAsync();
    }

    // ── GitHub projects sync ──────────────────────────────────────────────────

    private async Task<int> SyncGitHubProjectsAsync(
        Guid userId, GitHubEnrichmentResult data)
    {
        var added = 0;

        foreach (var repo in data.Repos.Where(r => !r.Fork).Take(20))
        {
            var existing = await _db.Projects
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

            _db.Projects.Add(new Project
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

        await _db.SaveChangesAsync();
        return added;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

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
        var skill = await _db.Skills
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
            _db.Skills.Add(skill);
            await _db.SaveChangesAsync();
        }

        var userSkill = await _db.UserSkills
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
                VerificationStatus = confidence >= 0.80m
                    ? SkillVerificationStatus.Evidenced
                    : SkillVerificationStatus.Claimed,
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow
            };
            _db.UserSkills.Add(userSkill);
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

        await _db.SaveChangesAsync();

        // Add source record — one per source type per skill
        var sourceExists = await _db.SkillSources
            .AnyAsync(s =>
                s.UserSkillId == userSkill.Id &&
                s.SourceType  == sourceType &&
                s.SourceEntityId == sourceEntityId);

        if (!sourceExists)
        {
            _db.SkillSources.Add(new SkillSource
            {
                Id             = Guid.NewGuid(),
                UserSkillId    = userSkill.Id,
                SourceType     = sourceType,
                SourceEntityId = sourceEntityId,
                Evidence       = evidence,
                Weight         = confidence,
                CapturedAt     = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return isNew;
    }

    private async Task<ExternalProfile> GetOrCreateProfileAsync(
        Guid userId, Platform platform, string username)
    {
        var profile = await _db.ExternalProfiles
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

        _db.ExternalProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }
}
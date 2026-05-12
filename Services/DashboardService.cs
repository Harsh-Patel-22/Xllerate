using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models.DTOs.Enrichment;
using Xllerate.Models.DTOs.User;
using Xllerate.Services;

namespace Xllerate.Services;

public class DashboardService(
    ApplicationDbContext db,
    MongoDbContext mongo,
    IMemoryCache cache,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    SearchService searchService,
    ILogger<DashboardService> logger)
{
    private const string NewsCacheKey = "tech_hiring_news";

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        // ── Load user ─────────────────────────────────────────────────────────
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        // ── Load external profiles ────────────────────────────────────────────
        var profiles = await db.ExternalProfiles
            .Where(ep => ep.UserId == userId)
            .ToListAsync(ct);

        // ── Load active resume skills for radar ───────────────────────────────
        var activeResumeId = await db.Resumes
            .Where(r => r.UserId == userId && r.IsActive && r.ParsedAt != null)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var userSkills = await db.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId &&
                         (us.ResumeId == activeResumeId || us.ResumeId == null))
            .ToListAsync(ct);

        // ── Load latest analysis from MongoDB ─────────────────────────────────
        var latestDoc = await mongo.Analyses
            .Find(a => a.UserId == userId)
            .SortByDescending(a => a.CreatedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        // ── Load role profile for radar required scores ───────────────────────
        Models.MongoDB.RoleProfileDocument? roleProfile = null;
        if (!string.IsNullOrWhiteSpace(user.DesiredRole))
        {
            roleProfile = await mongo.RoleProfiles
                .Find(r => r.RoleKey == user.DesiredRole)
                .FirstOrDefaultAsync(ct);
        }

        // ── News (from cache or Hangfire-populated) ───────────────────────────
        var news = cache.TryGetValue<List<TechNewsDto>>(NewsCacheKey, out var cached)
            ? cached ?? []
            : [];

        // ── Build DTOs ────────────────────────────────────────────────────────
        var github   = BuildGitHubMetrics(profiles);
        var leetcode = BuildLeetCodeMetrics(profiles);
        var radar    = BuildRadarChart(userSkills, roleProfile);
        var analysis  = BuildAnalysisSummary(latestDoc);

        var completion = ComputeProfileCompletion(
            activeResumeId, userSkills.Count, profiles);

        return new DashboardDto
        {
            Profile = new ProfileSummaryDto
            {
                Name              = user.FullName,
                Email             = user.Email ?? string.Empty,
                ProfileCompletion = completion,
                DesiredRole       = user.DesiredRole,
                AvatarUrl         = user.AvatarUrl
            },
            GitHub        = github,
            LeetCode      = leetcode,
            LatestAnalysis = analysis,
            News          = news,
            RadarChart    = radar,
        };
    }

    // ── News refresh — RAG pattern ───────────────────────────────────────────
    public async Task RefreshNewsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Refreshing tech hiring news via RAG (Search + Gemini).");

        // 1. Search for recent news
        var searchResults = await searchService.SearchAsync(
            "tech industry hiring trends software engineering news 2026", 5, ct);

        var apiKey = config["Gemini:ApiKey"];
        var model  = config["Gemini:Model"] ?? "gemini-2.5-flash"; 
        string prompt;
        object? tools = null;

        if (searchResults.Count > 0)
        {
            // 2. Build RAG prompt with search context
            var context = string.Join("\n\n", searchResults.Select(r => 
                $"Title: {r.Title}\nSource: {r.Source}\nLink: {r.Link}\nSnippet: {r.Snippet}"));

            prompt = $$"""
                You are a tech industry news curator. I have performed a web search for recent news.
                Based on the following search results, curate 4 relevant news items about software 
                engineering hiring trends, skills, or market changes.

                Search Results:
                {{context}}

                Return ONLY a JSON array. Ensure the links are exactly as provided in the search results:
                [
                  {
                    "title": "...",
                    "summary": "2-3 sentence summary based on the snippet",
                    "url": "https://...",
                    "source": "...",
                    "publishedAt": "{{DateTime.UtcNow:yyyy-MM-dd}}"
                  }
                ]
                """;
        }
        else
        {
            // Fallback to internal search tool if manual search fails/no key
            logger.LogWarning("No search results found or API key missing. Falling back to Gemini's internal search tool.");
            prompt = $$"""
                You are a tech industry news curator. Find 4 recent and relevant news items
                about software engineering hiring trends, in-demand skills, tech layoffs or
                hiring booms, or emerging technologies that affect developer job markets.

                Return ONLY a JSON array. No markdown, no explanation:
                [
                  {
                    "title": "...",
                    "summary": "2-3 sentence summary",
                    "url": "https://...",
                    "source": "TechCrunch",
                    "publishedAt": "{{DateTime.UtcNow:yyyy-MM-dd}}"
                  }
                ]
                """;
            tools = new[] { new { googleSearch = new { } } };
        }

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" },
            tools = tools
        };

        try
        {
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

            var news = JsonSerializer.Deserialize<List<TechNewsDto>>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            cache.Set(NewsCacheKey, news, TimeSpan.FromHours(12));
            logger.LogInformation("News cache refreshed — {Count} items.", news.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "News refresh failed.");
        }
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private static GitHubMetricsDto? BuildGitHubMetrics(
        List<Models.ExternalProfile> profiles)
    {
        var profile = profiles.FirstOrDefault(p => p.Platform == Platform.GitHub);
        if (profile == null || profile.RawData == null) return null;

        try
        {
            var data = JsonSerializer.Deserialize<GitHubEnrichmentResult>(
                profile.RawData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null) return null;

            return new GitHubMetricsDto
            {
                Username             = data.Profile.Login,
                ProfileUrl           = profile.ProfileUrl,
                PublicRepos          = data.Profile.PublicRepos,
                TotalStars           = data.TotalStars,
                TotalForks           = data.TotalForks,
                Followers            = data.Profile.Followers,
                TotalCommitsEstimate = data.TotalCommitsEstimate,
                TopLanguages         = data.TopLanguages,
                LastSyncedAt         = profile.LastSyncedAt
            };
        }
        catch { return null; }
    }

    private static LeetCodeMetricsDto? BuildLeetCodeMetrics(
        List<Models.ExternalProfile> profiles)
    {
        var profile = profiles.FirstOrDefault(p => p.Platform == Platform.LeetCode);
        if (profile == null || profile.RawData == null) return null;

        try
        {
            var data = JsonSerializer.Deserialize<LeetCodeEnrichmentResult>(
                profile.RawData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null) return null;

            return new LeetCodeMetricsDto
            {
                Username       = data.Username,
                ProfileUrl     = profile.ProfileUrl,
                TotalSolved    = data.TotalSolved,
                EasySolved     = data.EasySolved,
                MediumSolved   = data.MediumSolved,
                HardSolved     = data.HardSolved,
                TotalQuestions = data.TotalQuestions,
                AcceptanceRate = data.AcceptanceRate,
                Ranking        = data.Ranking,
                LanguagesUsed  = data.LanguagesUsed,
                Badges         = data.Badges.Select(b => b.Name).ToList(),
                LastSyncedAt   = profile.LastSyncedAt
            };
        }
        catch { return null; }
    }

    private RadarChartDto BuildRadarChart(
    List<Models.UserSkill> userSkills,
    Models.MongoDB.RoleProfileDocument? roleProfile)
    {
        // Index user skills by normalized name for O(1) lookup
        var userSkillMap = userSkills
            .Where(us => us.Skill.Category != null)
            .ToDictionary(
                us => us.Skill.NormalizedName,
                us => us,
                StringComparer.OrdinalIgnoreCase);

        // ── Role profile skills grouped by category (primary axes) ────────────
        var roleByCategory = new Dictionary<string, List<(string SkillName, double Score)>>(
            StringComparer.OrdinalIgnoreCase);

        if (roleProfile != null)
        {
            foreach (var skill in roleProfile.Skills)
            {
                // Resolve category from DB via skill name lookup
                // We use SkillName from the doc — category must be on the Skill row
                // Since we can't join MongoDB + SQL here, we use a lightweight local resolve
                var category = ResolveCategory(skill.SkillName);
                if (!roleByCategory.ContainsKey(category))
                    roleByCategory[category] = [];
                roleByCategory[category].Add((skill.SkillName, skill.ProbabilisticScore));
            }
        }

        // ── User-only categories (secondary axes — skills user has but role doesn't mention) ──
        var userOnlyCategories = userSkills
            .Where(us => !string.IsNullOrEmpty(us.Skill.Category))
            .GroupBy(us => us.Skill.Category!)
            .Where(g => !roleByCategory.ContainsKey(g.Key))
            .ToDictionary(g => g.Key, g => g.ToList());

        var axes = new List<RadarAxisDto>();

        // ── Primary axes — role profile categories (weight 70%) ───────────────
        foreach (var (category, roleSkills) in roleByCategory)
        {
            var requiredScore = (int)Math.Round(
                roleSkills.Average(s => s.Score) * 100);

            // User score = avg confidence ONLY on skills the role requires
            var matchedUserSkills = roleSkills
                .Select(rs =>
                {
                    var norm = rs.SkillName.ToLowerInvariant().Trim();
                    return userSkillMap.TryGetValue(norm, out var us) ? us : null;
                })
                .Where(us => us != null)
                .ToList();

            var userScore = matchedUserSkills.Count > 0
                ? (int)Math.Round(matchedUserSkills.Average(us => us!.ConfidenceScore) * 100)
                : 0;

            axes.Add(new RadarAxisDto
            {
                Category      = category,
                UserScore     = userScore,
                RequiredScore = requiredScore,
                IsPrimary     = true
            });
        }

        // ── Secondary axes — user-only categories (weight 30%) ────────────────
        foreach (var (category, skills) in userOnlyCategories)
        {
            var userScore = (int)Math.Round(
                skills.Average(us => us.ConfidenceScore) * 100);

            axes.Add(new RadarAxisDto
            {
                Category      = category,
                UserScore     = userScore,
                RequiredScore = 0,   // role doesn't require this — shows extra breadth
                IsPrimary     = false
            });
        }
        foreach (var (category, roleSkills) in roleByCategory)
        {
            var matched = roleSkills
                .Select(rs => (rs.SkillName, Found: userSkillMap.ContainsKey(rs.SkillName.ToLowerInvariant().Trim())))
                .ToList();

            logger.LogInformation("Radar category {Cat}: role skills = [{Skills}]",
                category,
                string.Join(", ", matched.Select(m => $"{m.SkillName}:{m.Found}")));
        }
        return new RadarChartDto { Axes = axes.OrderByDescending(a => a.IsPrimary).ToList() };
    }

    // Local category resolution for MongoDB skill names — mirrors LocalMap
    private static string ResolveCategory(string skillName) =>
        SkillCategorizationService.LocalMap.TryGetValue(
            skillName.ToLowerInvariant().Trim(), out var cat)
            ? cat
            : "Other";
    

    private static LatestAnalysisSummaryDto? BuildAnalysisSummary(
        Models.MongoDB.AnalysisDocument? doc)
    {
        if (doc == null) return null;

        var matchLabel = doc.MatchPercentage switch
        {
            >= 75 => "competitive",
            >= 50 => "moderate",
            _     => "weak"
        };

        return new LatestAnalysisSummaryDto
        {
            AnalysisId      = doc.AnalysisId.ToString(),
            TargetRole      = doc.JobTitle,
            MatchPercentage = doc.MatchPercentage,
            MatchLabel      = matchLabel,
            TotalSkills     = doc.Summary.TotalRequired,
            MatchedSkills   = doc.Summary.Matched,
            MissingSkills   = doc.Summary.Missing,
            WeakSkills      = doc.Summary.Weak,
            CreatedAt       = doc.CreatedAt
        };
    }

    private static int ComputeProfileCompletion(
        Guid? activeResumeId,
        int skillCount,
        List<Models.ExternalProfile> profiles)
    {
        var score = 0;
        if (activeResumeId.HasValue) score += 30;
        if (skillCount > 0)         score += 20;
        if (profiles.Any(p => p.Platform == Platform.GitHub))   score += 20;
        if (profiles.Any(p => p.Platform == Platform.LeetCode)) score += 15;
        if (profiles.Any(p => p.Platform == Platform.LinkedIn)) score += 15;
        return score;
    }
}
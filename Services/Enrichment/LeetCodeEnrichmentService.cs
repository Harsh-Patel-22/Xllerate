// Services/Enrichment/LeetCodeEnrichmentService.cs
using System.Text;
using System.Text.Json;
using Xllerate.Models.DTOs.Enrichment;

namespace Xllerate.Services.Enrichment;

public class LeetCodeEnrichmentService(
    IHttpClientFactory httpFactory,
    ILogger<LeetCodeEnrichmentService> logger) {
    private const string GraphQlUrl = "https://leetcode.com/graphql";

    public async Task<LeetCodeEnrichmentResult> FetchAsync(string username)
    {
        var client = httpFactory.CreateClient("leetcode");

        // Fetch all data in parallel
        var statsTask = FetchUserStatsAsync(client, username);
        var submissionsTask = FetchRecentSubmissionsAsync(client, username);
        var badgesTask = FetchBadgesAsync(client, username);

        await Task.WhenAll(statsTask, submissionsTask, badgesTask);

        var stats = await statsTask;
        var submissions = await submissionsTask;
        var badges = await badgesTask;

        // Extract languages used from recent submissions
        var languagesUsed = submissions
            .Select(s => s.Lang)
            .Distinct()
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return new LeetCodeEnrichmentResult
        {
            Username           = username,
            TotalSolved        = stats.TotalSolved,
            EasySolved         = stats.EasySolved,
            MediumSolved       = stats.MediumSolved,
            HardSolved         = stats.HardSolved,
            TotalQuestions     = stats.TotalQuestions,
            EasyTotal          = stats.EasyTotal,
            MediumTotal        = stats.MediumTotal,
            HardTotal          = stats.HardTotal,
            AcceptanceRate     = stats.AcceptanceRate,
            Ranking            = stats.Ranking,
            ContributionPoints = stats.ContributionPoints,
            RecentSubmissions  = submissions,
            Badges             = badges,
            LanguagesUsed      = languagesUsed
        };
    }

    // ── GraphQL queries ───────────────────────────────────────────────────────

    private async Task<LeetCodeStatsInternal> FetchUserStatsAsync(
        HttpClient client, string username)
    {
        var query = new
        {
            query = """
                query getUserProfile($username: String!) {
                    matchedUser(username: $username) {
                        contributions { points }
                        profile { reputation ranking }
                        submitStats {
                            acSubmissionNum {
                                difficulty count submissions
                            }
                            totalSubmissionNum {
                                difficulty count submissions
                            }
                        }
                    }
                    allQuestionsCount {
                        difficulty count
                    }
                }
                """,
            variables = new { username }
        };

        var result = await PostGraphQlAsync(client, query);
        if (result == null) return new ();
        try
        {
            var matched = result.Value
                .GetProperty("data")
                .GetProperty("matchedUser");

            var acStats = matched
                .GetProperty("submitStats")
                .GetProperty("acSubmissionNum");

            var allQuestions = result.Value
                .GetProperty("data")
                .GetProperty("allQuestionsCount");

            int GetSolved(string difficulty) =>
                acStats.EnumerateArray()
                    .FirstOrDefault(x =>
                        x.GetProperty("difficulty").GetString() == difficulty)
                    .TryGetProperty("count", out var v) ? v.GetInt32() : 0;

            int GetTotal(string difficulty) =>
                allQuestions.EnumerateArray()
                    .FirstOrDefault(x =>
                        x.GetProperty("difficulty").GetString() == difficulty)
                    .TryGetProperty("count", out var v) ? v.GetInt32() : 0;

            var totalSolved = GetSolved("All");

            return new LeetCodeStatsInternal
            {
                TotalSolved        = totalSolved,
                EasySolved         = GetSolved("Easy"),
                MediumSolved       = GetSolved("Medium"),
                HardSolved         = GetSolved("Hard"),
                TotalQuestions     = GetTotal("All"),
                EasyTotal          = GetTotal("Easy"),
                MediumTotal        = GetTotal("Medium"),
                HardTotal          = GetTotal("Hard"),
                AcceptanceRate     = totalSolved > 0
                    ? Math.Round((double)totalSolved / GetTotal("All") * 100, 2)
                    : 0,
                Ranking            = matched.GetProperty("profile")
                    .GetProperty("ranking").GetInt32(),
                ContributionPoints = matched.GetProperty("contributions")
                    .GetProperty("points").GetInt32()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to parse LeetCode stats for {Username}", username);
            return new();
        }
    }

    private async Task<List<LeetCodeSubmissionDto>> FetchRecentSubmissionsAsync(
        HttpClient client, string username)
    {
        var query = new
        {
            query = """
                query getRecentSubmissions($username: String!, $limit: Int) {
                    recentSubmissionList(username: $username, limit: $limit) {
                        title
                        titleSlug
                        timestamp
                        statusDisplay
                        lang
                    }
                }
                """,
            variables = new { username, limit = 20 }
        };

        var result = await PostGraphQlAsync(client, query);
        if (result is null) return [];

        try
        {
            var list = result.Value
                .GetProperty("data")
                .GetProperty("recentSubmissionList");

            return list.EnumerateArray().Select(s => new LeetCodeSubmissionDto
            {
                Title         = s.GetProperty("title").GetString() ?? "",
                TitleSlug     = s.GetProperty("titleSlug").GetString() ?? "",
                StatusDisplay = s.GetProperty("statusDisplay").GetString() ?? "",
                Lang          = s.GetProperty("lang").GetString() ?? "",
                Timestamp     = long.Parse(
                    s.GetProperty("timestamp").GetString() ?? "0")
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to parse LeetCode submissions for {Username}", username);
            return [];
        }
    }

    private async Task<List<LeetCodeBadgeDto>> FetchBadgesAsync(
        HttpClient client, string username)
    {
        var query = new
        {
            query = """
                query getUserBadges($username: String!) {
                    matchedUser(username: $username) {
                        badges {
                            name
                            icon
                        }
                    }
                }
                """,
            variables = new { username }
        };

        var result = await PostGraphQlAsync(client, query);
        if (result is null) return [];

        try
        {
            var badges = result.Value
                .GetProperty("data")
                .GetProperty("matchedUser")
                .GetProperty("badges");

            return badges.EnumerateArray().Select(b => new LeetCodeBadgeDto
            {
                Name = b.GetProperty("name").GetString() ?? "",
                Icon = b.GetProperty("icon").GetString() ?? ""
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<JsonElement?> PostGraphQlAsync(
        HttpClient client, object query)
    {
        try
        {
            var json = JsonSerializer.Serialize(query);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(GraphQlUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "LeetCode GraphQL returned {Status}", response.StatusCode);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(responseJson).RootElement.Clone();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LeetCode GraphQL request failed");
            return null;
        }
    }

    // Internal DTO — not exposed outside service
    private class LeetCodeStatsInternal
    {
        public int TotalSolved { get; set; }
        public int EasySolved { get; set; }
        public int MediumSolved { get; set; }
        public int HardSolved { get; set; }
        public int TotalQuestions { get; set; }
        public int EasyTotal { get; set; }
        public int MediumTotal { get; set; }
        public int HardTotal { get; set; }
        public double AcceptanceRate { get; set; }
        public int Ranking { get; set; }
        public int ContributionPoints { get; set; }
    }
}
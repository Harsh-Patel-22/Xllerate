// Services/Enrichment/GitHubEnrichmentService.cs
using System.Net.Http.Headers;
using System.Text.Json;
using Xllerate.Models.DTOs.Enrichment;

namespace Xllerate.Services.Enrichment;

public class GitHubEnrichmentService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<GitHubEnrichmentService> logger) {
    private readonly ILogger<GitHubEnrichmentService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<GitHubEnrichmentResult> FetchAsync(string username)
    {
        var client = BuildClient();
        var maxRepos = int.Parse(config["GitHub:MaxReposToFetch"] ?? "30");

        // Fetch all in parallel where possible
        var profileTask = FetchProfileAsync(client, username);
        var reposTask = FetchReposAsync(client, username, maxRepos);

        await Task.WhenAll(profileTask, reposTask);

        var profile = await profileTask;
        var repos = await reposTask;

        // Fetch languages for each non-fork repo (sequential to respect rate limits)
        var aggregatedLanguages = new Dictionary<string, long>();
        var ownRepos = repos.Where(r => !r.Fork).ToList();

        foreach (var repo in ownRepos.Take(20))   // cap at 20 to avoid rate limits
        {
            var languages = await FetchRepoLanguagesAsync(client, username, repo.Name);
            foreach (var (lang, bytes) in languages)
            {
                aggregatedLanguages[lang] =
                    aggregatedLanguages.GetValueOrDefault(lang, 0) + bytes;
            }
            await Task.Delay(50);   // gentle throttle
        }

        var topLanguages = aggregatedLanguages
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => kv.Key)
            .ToList();

        return new GitHubEnrichmentResult
        {
            Profile = profile,
            Repos = repos,
            AggregatedLanguages = aggregatedLanguages,
            TotalStars = repos.Sum(r => r.StargazersCount),
            TotalForks = repos.Sum(r => r.ForksCount),
            TotalCommitsEstimate = ownRepos.Count * 15,  // rough estimate
            TopLanguages = topLanguages
        };
    }

    // ── Private fetch methods ─────────────────────────────────────────────────

    private async Task<GitHubProfileDto> FetchProfileAsync(
        HttpClient client, string username)
    {
        var response = await client.GetAsync($"/users/{username}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubProfileDto>(json, JsonOptions)
               ?? throw new Exception($"Failed to deserialize GitHub profile for {username}");
    }

    private async Task<List<GitHubRepoDto>> FetchReposAsync(
        HttpClient client, string username, int maxRepos)
    {
        var repos = new List<GitHubRepoDto>();
        var page = 1;
        const int perPage = 100;

        while (repos.Count < maxRepos)
        {
            var response = await client.GetAsync(
                $"/users/{username}/repos" +
                $"?sort=pushed&direction=desc" +
                $"&per_page={perPage}&page={page}");

            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadAsStringAsync();
            var batch = JsonSerializer.Deserialize<List<GitHubRepoDto>>(
                json, JsonOptions) ?? [];

            if (!batch.Any()) break;

            repos.AddRange(batch);
            if (batch.Count < perPage) break;
            page++;
        }

        return repos.Take(maxRepos).ToList();
    }

    private async Task<Dictionary<string, long>> FetchRepoLanguagesAsync(
        HttpClient client, string username, string repoName)
    {
        try
        {
            var response = await client.GetAsync(
                $"/repos/{username}/{repoName}/languages");

            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private HttpClient BuildClient()
    {
        var client = httpFactory.CreateClient("github");
        var token = config["GitHub:Token"]!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent
            .ParseAdd("Xllerate/1.0");
        return client;
    }
}
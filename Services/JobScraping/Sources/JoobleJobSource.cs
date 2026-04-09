using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services.JobScraping.Sources;

public class JoobleJobSource(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<JoobleJobSource> logger)
    : IJobSource {
    
    public string SourceName => "Jooble";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<JobScrapedDto>> FetchJobsAsync(CancellationToken ct = default)
    {
        var apiKey = config["JobScraping:Jooble:ApiKey"];
        var baseUrl = config["JobScraping:Jooble:BaseUrl"];
        var maxJobs = config.GetValue<int>("JobScraping:MaxJobsPerRun", 500);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_JOOBLE_KEY_HERE")
        {
            logger.LogWarning("Jooble API key not configured. Skipping.");
            return [];
        }

        var keywords = GetAllKeywords();
        var locations = config.GetSection("JobScraping:IndiaLocations").Get<List<string>>() ?? ["India"];
        var client = httpClientFactory.CreateClient();
        var results = new List<JobScrapedDto>();

        foreach (var keyword in keywords)
        {
            if (results.Count >= maxJobs) break;

            // Jooble only needs one representative India location — "India" gives broadest results
            var payload = new { keywords = keyword, location = "India", page = 1 };
            var url = $"{baseUrl}/{apiKey}";

            try
            {
                var response = await client.PostAsJsonAsync(url, payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Jooble returned {Status} for keyword '{Keyword}'", response.StatusCode, keyword);
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<JoobleResponse>(JsonOpts, ct);
                if (body?.Jobs == null || body.Jobs.Count == 0) continue;

                foreach (var job in body.Jobs)
                {
                    results.Add(MapToDto(job));
                    if (results.Count >= maxJobs) break;
                }

                logger.LogInformation("Jooble: {Count} jobs for '{Keyword}'", body.Jobs.Count, keyword);

                // Polite delay between keyword queries
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Jooble fetch failed for keyword '{Keyword}'", keyword);
            }
        }

        return results;
    }

    private List<string> GetAllKeywords()
    {
        var section = config.GetSection("JobScraping:SearchQueries");
        var all = new List<string>();
        foreach (var child in section.GetChildren())
            all.AddRange(child.Get<List<string>>() ?? []);
        return all.Distinct().ToList();
    }

    private JobScrapedDto MapToDto(JoobleJob job)
    {
        return new JobScrapedDto
        {
            Title = job.Title?.Trim() ?? "Unknown",
            CompanyName = job.Company?.Trim() ?? "Unknown",
            Location = job.Location?.Trim() ?? "India",
            Description = StripHtml(job.Snippet ?? ""),
            SourceUrl = job.Link,
            Source = SourceName,
            ScrapedAt = DateTime.UtcNow,
            IsRemote = job.Location?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false,
            SalaryMin = job.Salary
        };
    }

    // Jooble snippets contain basic HTML tags — strip them for clean storage
    private static string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Trim();
    }

    // Response POCOs — internal, not entities
    private class JoobleResponse
    {
        public List<JoobleJob> Jobs { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private class JoobleJob
    {
        public string? Title { get; set; }
        public string? Location { get; set; }
        public string? Snippet { get; set; }
        public string? Salary { get; set; }
        public string? Source { get; set; }
        public string? Link { get; set; }
        public string? Company { get; set; }
        public string? Updated { get; set; }
        public string? Type { get; set; }
    }
}
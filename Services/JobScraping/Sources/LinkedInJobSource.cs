using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services.JobScraping.Sources;

public class LinkedInJobSource(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<LinkedInJobSource> logger)
    : IJobSource {
    // LinkedIn's public unauthenticated jobs guest API
    private const string BaseUrl = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search";

    // Maps our category keywords to LinkedIn's geographicId for India
    private const string IndiaGeoId = "102713980";

    public string SourceName => "LinkedIn";

    public async Task<List<JobScrapedDto>> FetchJobsAsync(CancellationToken ct = default)
    {
        var delayMs = config.GetValue<int>("JobScraping:LinkedIn:DelayMs", 3000);
        var maxPages = config.GetValue<int>("JobScraping:LinkedIn:MaxPagesPerQuery", 5);
        var maxJobs = config.GetValue<int>("JobScraping:MaxJobsPerRun", 500);

        var keywords = GetAllKeywords();
        var client = BuildClient();
        var results = new List<JobScrapedDto>();
        var seen = new HashSet<string>(); // dedup by LinkedIn job ID within this run

        foreach (var keyword in keywords)
        {
            if (results.Count >= maxJobs) break;

            for (var page = 0; page < maxPages; page++)
            {
                if (results.Count >= maxJobs) break;
                ct.ThrowIfCancellationRequested();

                // LinkedIn paginates in steps of 25
                var url = $"{BaseUrl}?keywords={Uri.EscapeDataString(keyword)}" +
                          $"&location=India&geoId={IndiaGeoId}" +
                          $"&f_TPR=r86400" + // posted in last 24h — keeps data fresh
                          $"&start={page * 25}";

                try
                {
                    var html = await client.GetStringAsync(url, ct);
                    var jobs = ParseJobCards(html);

                    if (jobs.Count == 0)
                    {
                        logger.LogDebug("LinkedIn: no more results for '{Keyword}' at page {Page}", keyword, page);
                        break;
                    }

                    foreach (var job in jobs)
                    {
                        if (job.ExternalId != null && !seen.Add(job.ExternalId)) continue;
                        results.Add(job);
                        if (results.Count >= maxJobs) break;
                    }

                    logger.LogInformation("LinkedIn: {Count} jobs for '{Keyword}' page {Page}", jobs.Count, keyword, page);
                    await Task.Delay(delayMs, ct);
                }
                catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
                {
                    logger.LogWarning("LinkedIn rate limited for '{Keyword}'. Backing off 60s.", keyword);
                    await Task.Delay(60_000, ct);
                    break; // skip remaining pages for this keyword
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "LinkedIn fetch failed for '{Keyword}' page {Page}", keyword, page);
                    break;
                }
            }
        }

        return results;
    }

    // LinkedIn's guest endpoint returns raw HTML job cards — parse with regex
    // (no headless browser needed for this endpoint)
    private static List<JobScrapedDto> ParseJobCards(string html)
    {
        var results = new List<JobScrapedDto>();

        // Each job card is wrapped in <li> — extract job ID, title, company, location
        var cardMatches = System.Text.RegularExpressions.Regex.Matches(
            html,
            @"<li[^>]*>(.*?)</li>",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        foreach (System.Text.RegularExpressions.Match card in cardMatches)
        {
            var cardHtml = card.Groups[1].Value;

            var jobId = ExtractAttr(cardHtml, @"data-entity-urn=""urn:li:jobPosting:(\d+)""");
            var title = ExtractInner(cardHtml, @"<h3[^>]*class=""[^""]*base-search-card__title[^""]*""[^>]*>(.*?)</h3>");
            var company = ExtractInner(cardHtml, @"<h4[^>]*class=""[^""]*base-search-card__subtitle[^""]*""[^>]*>(.*?)</h4>");
            var location = ExtractInner(cardHtml, @"<span[^>]*class=""[^""]*job-search-card__location[^""]*""[^>]*>(.*?)</span>");
            var jobUrl = ExtractAttr(cardHtml, @"href=""(https://[a-z]+\.linkedin\.com/jobs/view/[^""?]+)");

            if (string.IsNullOrWhiteSpace(title)) continue;

            results.Add(new JobScrapedDto
            {
                ExternalId = jobId,
                Title = CleanText(title),
                CompanyName = CleanText(company) ?? "Unknown",
                Location = CleanText(location) ?? "India",
                Description = null, // guest endpoint doesn't return description — that's fine for skill extraction from title
                SourceUrl = jobUrl,
                Source = "LinkedIn",
                ScrapedAt = DateTime.UtcNow,
                IsRemote = location?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false,
                SalaryMin = null
            });
        }

        return results;
    }

    private static string? ExtractAttr(string html, string pattern)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractInner(string html, string pattern)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        return m.Success ? StripHtml(m.Groups[1].Value) : null;
    }

    private static string? CleanText(string? s) =>
        s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();

    private List<string> GetAllKeywords()
    {
        var section = config.GetSection("JobScraping:SearchQueries");
        var all = new List<string>();
        foreach (var child in section.GetChildren())
            all.AddRange(child.Get<List<string>>() ?? []);
        return all.Distinct().ToList();
    }

    private HttpClient BuildClient()
    {
        var client = httpClientFactory.CreateClient();
        // Must look like a browser — LinkedIn blocks default HttpClient UA
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        );
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }
}
namespace Xllerate.Services.JobScraping.Sources;

// Services/JobScraping/Sources/AdzunaJobSource.cs
using System.Text.Json;
using Models.DTOs.Jobs;

public class AdzunaJobSource(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<AdzunaJobSource> logger)
    : IJobSource {
    public string SourceName => "Adzuna";

    private static readonly string[] TechCategories =
    [
        "it-jobs", "engineering-jobs", "scientific-qa-jobs"
    ];

    public async Task<List<JobScrapedDto>> FetchJobsAsync(
        CancellationToken ct = default)
    {
        var appId  = config["JobScraping:Adzuna:AppId"]!;
        var appKey = config["JobScraping:Adzuna:AppKey"]!;
        var baseUrl = config["JobScraping:Adzuna:BaseUrl"]!;
        var perPage = int.Parse(
            config["JobScraping:Adzuna:ResultsPerPage"] ?? "50");
        var countries = config
            .GetSection("JobScraping:Adzuna:Countries")
            .Get<string[]>() ?? ["in"];

        var client  = httpFactory.CreateClient();
        var results = new List<JobScrapedDto>();

        foreach (var country in countries)
        {
            foreach (var category in TechCategories)
            {
                try
                {
                    var url = $"{baseUrl}/{country}/search/1" +
                              $"?app_id={appId}&app_key={appKey}" +
                              $"&results_per_page={perPage}" +
                              $"&category={category}" +
                              $"&content-type=application/json";

                    var response = await client.GetAsync(url, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning(
                            "Adzuna {Country}/{Category} returned {Status}",
                            country, category, response.StatusCode);
                        continue;
                    }

                    // var json = await response.Content.ReadAsStringAsync(ct);
                    var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                    var content = System.Text.Encoding.UTF8.GetString(bytes);
                    
                    var parsed = JsonDocument.Parse(content);

                    if (!parsed.RootElement.TryGetProperty(
                        "results", out var jobsArray))
                        continue;

                    foreach (var job in jobsArray.EnumerateArray())
                    {
                        results.Add(MapAdzunaJob(job, country));
                    }

                    // Gentle throttle between requests
                    await Task.Delay(300, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Adzuna fetch failed for {Country}/{Category}",
                        country, category);
                }
            }
        }

        logger.LogInformation(
            "Adzuna fetched {Count} jobs", results.Count);
        return results;
    }

    private static JobScrapedDto MapAdzunaJob(
        JsonElement job, string country)
    {
        static string? GetStr(JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) &&
            v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        var title   = GetStr(job, "title") ?? "";
        var company = job.TryGetProperty("company", out var c)
            ? GetStr(c, "display_name") : null;

        var location = job.TryGetProperty("location", out var loc)
            ? GetStr(loc, "display_name") : null;

        var salaryMin = job.TryGetProperty("salary_min", out var sMin)
            && sMin.ValueKind == JsonValueKind.Number
                ? sMin.GetDecimal().ToString("F0") : null;

        var salaryMax = job.TryGetProperty("salary_max", out var sMax)
            && sMax.ValueKind == JsonValueKind.Number
                ? sMax.GetDecimal().ToString("F0") : null;

        return new JobScrapedDto
        {
            Title          = title,
            CompanyName    = company ?? "Unknown",
            Location       = location,
            IsRemote       = title.Contains("remote",
                StringComparison.OrdinalIgnoreCase) ||
                (location?.Contains("remote",
                StringComparison.OrdinalIgnoreCase) ?? false),
            Description    = GetStr(job, "description"),
            SourceUrl      = GetStr(job, "redirect_url"),
            SalaryMin      = salaryMin,
            SalaryMax      = salaryMax,
            EmploymentType = GetStr(job, "contract_type"),
            Source         = $"Adzuna-{country.ToUpper()}",
            ScrapedAt      = DateTime.UtcNow
        };
    }
}
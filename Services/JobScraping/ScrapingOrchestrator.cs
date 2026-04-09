// Services/JobScraping/ScrapingOrchestrator.cs
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services.JobScraping;

public class ScrapingOrchestrator(
    IEnumerable<IJobSource> sources,
    JobNormalizationService normalization,
    ILogger<ScrapingOrchestrator> logger,
    CompanyEnrichmentService companyEnricher) {
    public async Task<ScrapingRunResult> RunAsync(
        CancellationToken ct = default)
    {
        var result = new ScrapingRunResult
        {
            StartedAt = DateTime.UtcNow
        };

        logger.LogInformation(
            "Scraping run started — {SourceCount} sources",
            sources.Count());

        var allJobs = new List<JobScrapedDto>();

        // Fetch from all sources
        foreach (var source in sources)
        {
            try
            {
                logger.LogInformation(
                    "Fetching from {Source}...", source.SourceName);

                var jobs = await source.FetchJobsAsync(ct);
                allJobs.AddRange(jobs);

                logger.LogInformation(
                    "{Source} returned {Count} jobs",
                    source.SourceName, jobs.Count);
            }
            catch (Exception ex)
            {
                var error = $"{source.SourceName}: {ex.Message}";
                result.Errors.Add(error);
                logger.LogError(ex,
                    "Source {Source} failed", source.SourceName);
            }
        }

        result.JobsFound = allJobs.Count;
        logger.LogInformation(
            "Total jobs fetched: {Count} — normalizing...",
            allJobs.Count);

        // Normalize and persist
        var (added, updated, companiesAdded) =
            await normalization.NormalizeAndPersistAsync(allJobs);

        result.JobsAdded      = added;
        result.JobsUpdated    = updated;
        result.CompaniesAdded = companiesAdded;
        result.CompletedAt    = DateTime.UtcNow;
        
        await companyEnricher.EnrichAsync(ct);
        
        logger.LogInformation(
            "Scraping run complete — " +
            "Added: {Added}, Updated: {Updated}, " +
            "Companies: {Companies}, Duration: {Duration}s",
            added, updated, companiesAdded,
            result.Duration.TotalSeconds);
    
        return result;
    }
}
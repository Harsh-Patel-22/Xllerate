using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services.JobScraping;

public interface IJobSource
{
    string SourceName { get; }
    Task<List<JobScrapedDto>> FetchJobsAsync(
        CancellationToken ct = default);
}
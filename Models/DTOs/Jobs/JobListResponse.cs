namespace Xllerate.Models.DTOs.Jobs;

public class JobListResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<JobRoleDto> Jobs { get; set; } = [];
}

public class JobRoleDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? CompanyLogo { get; set; }
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public string Seniority { get; set; } = "";
    public string? SalaryRange { get; set; }
    public string? SourceUrl { get; set; }
    public List<string> RequiredSkills { get; set; } = [];
    public DateTime ScrapedAt { get; set; }
}

public class ScrapingRunResult
{
    public int JobsFound { get; set; }
    public int JobsAdded { get; set; }
    public int JobsUpdated { get; set; }
    public int CompaniesAdded { get; set; }
    public List<string> Errors { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
}
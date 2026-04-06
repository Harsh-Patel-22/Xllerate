namespace Xllerate.Models.DTOs.Enrichment;

public class ConnectGitHubRequest
{
    public required string Username { get; set; }
}

public class ConnectLeetCodeRequest
{
    public required string Username { get; set; }
}

public class EnrichmentStatusResponse
{
    public Guid ProfileId { get; set; }
    public string Platform { get; set; } = "";
    public string Username { get; set; } = "";
    public string SyncStatus { get; set; } = "";
    public DateTime? LastSyncedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public EnrichmentSummary? Summary { get; set; }
}

public class EnrichmentSummary
{
    public int SkillsExtracted { get; set; }
    public int ProjectsAdded { get; set; }
    public int SkillsUpdated { get; set; }
}
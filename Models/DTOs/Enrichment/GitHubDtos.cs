namespace Xllerate.Models.DTOs.Enrichment;

public class GitHubProfileDto
{
    public string Login { get; set; } = "";
    public string? Name { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Location { get; set; }
    public string? Company { get; set; }
    public string? Blog { get; set; }
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GitHubRepoDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string HtmlUrl { get; set; } = "";
    public string? Language { get; set; }
    public int StargazersCount { get; set; }
    public int ForksCount { get; set; }
    public bool Fork { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime? PushedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GitHubEnrichmentResult
{
    public GitHubProfileDto Profile { get; set; } = new();
    public List<GitHubRepoDto> Repos { get; set; } = [];
    public Dictionary<string, long> AggregatedLanguages { get; set; } = [];
    public int TotalCommitsEstimate { get; set; }
    public int TotalStars { get; set; }
    public int TotalForks { get; set; }
    public List<string> TopLanguages { get; set; } = [];
}
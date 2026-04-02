namespace Xllerate.Models;

public class Project
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? RepoUrl { get; set; }
    public string? LiveUrl { get; set; }
    public bool IsFromGithub { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // JSON — display only, not used for querying
    public List<string> TechStack { get; set; } = [];
    public GithubMetrics? GithubMetrics { get; set; }

    public User User { get; set; } = null!;
    public ICollection<SkillSource> SkillSources { get; set; } = [];
}

public class GithubMetrics
{
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Commits { get; set; }
    public int Contributors { get; set; }
    public string? PrimaryLanguage { get; set; }
    public DateTime? LastCommitAt { get; set; }
}
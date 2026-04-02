using Xllerate.Enums;

namespace Xllerate.Models;

public class Roadmap
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AnalysisId { get; set; }
    public required string Title { get; set; }
    public RoadmapStatus Status { get; set; } = RoadmapStatus.Active;
    public int EstimatedTotalHours { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Analysis Analysis { get; set; } = null!;
    public ICollection<RoadmapItem> Items { get; set; } = [];
}

// Entities/RoadmapItem.cs
public class RoadmapItem
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public Guid? SkillGapId { get; set; }
    public Guid SkillId { get; set; }
    public int OrderIndex { get; set; }
    public ResourceType ResourceType { get; set; }
    public string? ResourceTitle { get; set; }
    public string? ResourceUrl { get; set; }
    public int EstimatedHours { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    public Roadmap Roadmap { get; set; } = null!;
    public SkillGap? SkillGap { get; set; }
    public Skill Skill { get; set; } = null!;
}
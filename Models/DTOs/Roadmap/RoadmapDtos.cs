namespace Xllerate.Models.DTOs.Roadmap;

public class GenerateRoadmapRequest
{
    public Guid AnalysisId { get; set; }
    public string Timeframe { get; set; } = string.Empty;  // "3 months", "2 weeks", etc.
}

public class RoadmapDto
{
    public Guid RoadmapId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TimeframeDays { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;  // "Day" | "Week" | "Month"
    public int EstimatedTotalHours { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<RoadmapPeriodDto> Periods { get; set; } = [];
}

public class RoadmapPeriodDto
{
    public int PeriodIndex { get; set; }
    public string Label { get; set; } = string.Empty;  // "Week 1", "Day 3", "Month 2"
    public List<RoadmapItemDto> Items { get; set; } = [];
}

public class RoadmapItemDto
{
    public Guid ItemId { get; set; }
    public string ItemType { get; set; } = string.Empty;   // "SkillTask" | "ProjectSuggestion"
    public string? ActivityType { get; set; }               // null for ProjectSuggestion
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedHours { get; set; }
    public int OrderIndex { get; set; }
    public bool IsCompleted { get; set; }
    public string? SkillName { get; set; }
}
public class SparkSkillStaging
{
    public Guid Id { get; set; }
    public string ResumeId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Source { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public bool IsConsumed { get; set; } = false;
}
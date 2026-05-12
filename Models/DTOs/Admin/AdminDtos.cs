public class SkillNodeDto
{
    public string SkillName { get; set; }
    public Guid? SkillId { get; set; }
    public double ProbabilisticScore { get; set; }   // editable — drives match scoring
    public double AggregatedWeight { get; set; }
    public double FrequencyRatio { get; set; }
    public bool IsCore { get; set; }
    public double Confidence { get; set; }
    public string? Category { get; set; }            // from Skills table via SkillId
}

public class SkillEdgeDto
{
    public string Source { get; set; }
    public string Target { get; set; }
    public double Strength { get; set; }
    public string CorrelationSource { get; set; }               // "Static" | "Gemini"
}

public class RoleSkillGraphDto
{
    public string RoleKey { get; set; }
    public string DisplayName { get; set; }
    public int JobsSampled { get; set; }
    public double Confidence { get; set; }
    public List<SkillNodeDto> Nodes { get; set; }
    public List<SkillEdgeDto> Edges { get; set; }
}

public class SkillGraphResponseDto
{
    public List<RoleSkillGraphDto> Roles { get; set; }
}


public class UpdateSkillImportanceDto
{
    public string RoleKey { get; set; }
    public string SkillName { get; set; }
    public double ProbabilisticScore { get; set; }
    public double AggregatedWeight { get; set; }
    public double FrequencyRatio { get; set; }
    public bool IsCore { get; set; }
}

public class AddRoleSkillDto
{
    public string RoleKey { get; set; }
    public string SkillName { get; set; }  
    public double ProbabilisticScore { get; set; }
    public double AggregatedWeight { get; set; }
    public double FrequencyRatio { get; set; }
    public bool IsCore { get; set; }
}

public class DeleteRoleSkillDto
{
    public string RoleKey { get; set; }
    public string SkillName { get; set; }
}
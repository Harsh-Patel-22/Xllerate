using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Roadmap;

namespace Xllerate.Services.Roadmap;

public class RoadmapService(ApplicationDbContext db, IConfiguration config, ILogger<RoadmapService> logger)
{
    private static readonly Dictionary<string, List<string>> RoleSeedTasks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["java-developer"] =
        [
            "Core Java (OOP, Collections, Streams)",
            "Maven/Gradle build setup",
            "Spring Boot project structure",
            "MVC architecture with Spring MVC",
            "REST API design",
            "Spring Data JPA + ORM basics",
            "Database migrations with Flyway/Liquibase",
            "JWT auth implementation",
            "Password hashing (BCrypt)",
            "CORS configuration",
            "Exception handling & validation",
            "Unit testing with JUnit + Mockito",
            "Docker containerization"
        ],
        ["frontend-developer"] =
        [
            "HTML/CSS fundamentals",
            "JavaScript ES6+",
            "React component model",
            "State management (useState, useReducer)",
            "Routing with React Router",
            "API integration (fetch/axios)",
            "Form handling and validation",
            "Responsive design (Flexbox/Grid)",
            "Performance optimization",
            "Testing with Jest + React Testing Library"
        ],
        ["data-engineer"] =
        [
            "Python fundamentals",
            "SQL (window functions, CTEs)",
            "Pandas + NumPy",
            "ETL pipeline design",
            "Apache Spark basics",
            "Data modeling (star/snowflake schema)",
            "Airflow DAG authoring",
            "Cloud storage (S3/GCS)",
            "Data quality checks",
            "Streaming basics (Kafka)"
        ]
        // extend as needed
    };

    public async Task<RoadmapDto> GenerateAsync(Guid userId, GenerateRoadmapRequest req)
{
    var (days, periodLabel) = TimeframeParser.Parse(req.Timeframe);

    var analysis = await db.Analyses
        .Include(a => a.SkillGaps.Where(sg => sg.SkillId != null))
            .ThenInclude(sg => sg.Skill)
        .Include(a => a.JobRole)
        .FirstOrDefaultAsync(a => a.Id == req.AnalysisId && a.UserId == userId)
        ?? throw new KeyNotFoundException("Analysis not found.");

    // cap check
    if (analysis.RoadmapCount >= 3)
        throw new InvalidOperationException("LIMIT");

    // duplicate check — same analysisId + same timeframe
    var duplicate = await db.Roadmaps.AnyAsync(r =>
        r.AnalysisId == req.AnalysisId &&
        r.TimeframeDays == days);
    if (duplicate)
        throw new InvalidOperationException("CONFLICT");

    // deactivate current active roadmap if any
    var currentActive = await db.Roadmaps
        .FirstOrDefaultAsync(r => r.UserId == userId && r.IsActive);
    if (currentActive != null)
        currentActive.IsActive = false;

    var gaps = analysis.SkillGaps.OrderByDescending(g => g.Severity).ToList();
    var roleKey = analysis.JobRole?.NormalizedTitle;
    var roleDisplay = analysis.JobRole?.Title;

    if (string.IsNullOrEmpty(roleDisplay))
    {
        var user = await db.Users.FindAsync(userId);
        if (!string.IsNullOrEmpty(user?.DesiredRole))
        {
            var mapping = await db.RoleKeywordMappings
                .FirstOrDefaultAsync(m =>
                    m.RoleKey == user.DesiredRole ||
                    m.DisplayName == user.DesiredRole);

            roleKey = mapping?.RoleKey ?? user.DesiredRole;
            roleDisplay = mapping?.DisplayName ?? user.DesiredRole;
        }
    }

    roleDisplay ??= "your target role";
    roleKey ??= "general";
    
    var gapSkillNames = gaps.Select(g => g.Skill.Name).ToList();
    var seeds = RoleSeedTasks.TryGetValue(roleKey, out var s) ? s : [];
    var allSkills = gapSkillNames.Union(seeds, StringComparer.OrdinalIgnoreCase).ToList();

    var geminiResult = await CallGeminiForRoadmap(allSkills, roleDisplay, days, periodLabel);

    var roadmap = new Models.Roadmap
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        AnalysisId = req.AnalysisId,
        Title = $"{roleDisplay} Roadmap — {req.Timeframe}",
        TimeframeDays = days,
        PeriodLabel = periodLabel,
        Status = RoadmapStatus.Active,
        IsActive = true,
        EstimatedTotalHours = geminiResult.Items.Sum(i => i.EstimatedHours),
        GeneratedAt = DateTime.UtcNow
    };

    db.Roadmaps.Add(roadmap);

    // increment count on analysis
    analysis.RoadmapCount++;

    var skillLookup = await db.Skills
        .Where(sk => allSkills.Contains(sk.Name))
        .ToDictionaryAsync(sk => sk.Name, sk => sk.Id, StringComparer.OrdinalIgnoreCase);

    var gapLookup = gaps.ToDictionary(g => g.Skill.Name, g => g, StringComparer.OrdinalIgnoreCase);

    var items = geminiResult.Items.Select((item, idx) =>
    {
        skillLookup.TryGetValue(item.SkillName ?? "", out var skillId);
        gapLookup.TryGetValue(item.SkillName ?? "", out var gap);

        return new RoadmapItem
        {
            Id = Guid.NewGuid(),
            RoadmapId = roadmap.Id,
            SkillId = skillId == Guid.Empty ? null : skillId,
            SkillGapId = gap?.Id,
            ItemType = item.IsProject ? RoadmapItemType.ProjectSuggestion : RoadmapItemType.SkillTask,
            ActivityType = item.IsProject ? null : Enum.TryParse<ActivityType>(item.ActivityType, true, out var at) ? at : ActivityType.Practice,
            OrderIndex = idx,
            PeriodIndex = item.PeriodIndex,
            Title = item.Title,
            Description = item.Description,
            EstimatedHours = item.EstimatedHours
        };
    }).ToList();

    db.RoadmapItems.AddRange(items);
    await db.SaveChangesAsync();

    return ToDto(roadmap, items, skillLookup);
}

public async Task<RoadmapDto?> GetActiveAsync(Guid userId)
{
    var roadmap = await db.Roadmaps
        .Include(r => r.Items)
            .ThenInclude(i => i.Skill)
        .FirstOrDefaultAsync(r => r.UserId == userId && r.IsActive);

    if (roadmap == null) return null;

    var skillLookup = roadmap.Items
        .Where(i => i.Skill != null).GroupBy(i => i.Skill!.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Skill!.Id, StringComparer.OrdinalIgnoreCase);

    return ToDto(roadmap, roadmap.Items.ToList(), skillLookup);
}

    private static RoadmapDto ToDto(Models.Roadmap roadmap, List<RoadmapItem> items, Dictionary<string, Guid> skillLookup)
    {
        var periods = items
            .GroupBy(i => i.PeriodIndex)
            .OrderBy(g => g.Key)
            .Select(g => new RoadmapPeriodDto
            {
                PeriodIndex = g.Key,
                Label = $"{roadmap.PeriodLabel} {g.Key}",
                Items = g.OrderBy(i => i.OrderIndex).Select(i => new RoadmapItemDto
                {
                    ItemId = i.Id,
                    ItemType = i.ItemType.ToString(),
                    ActivityType = i.ActivityType?.ToString(),
                    Title = i.Title,
                    Description = i.Description,
                    EstimatedHours = i.EstimatedHours,
                    OrderIndex = i.OrderIndex,
                    IsCompleted = i.IsCompleted,
                    SkillName = i.Skill?.Name
                }).ToList()
            }).ToList();

        return new RoadmapDto
        {
            RoadmapId = roadmap.Id,
            Title = roadmap.Title,
            Status = roadmap.Status.ToString(),
            TimeframeDays = roadmap.TimeframeDays,
            PeriodLabel = roadmap.PeriodLabel,
            EstimatedTotalHours = roadmap.EstimatedTotalHours,
            GeneratedAt = roadmap.GeneratedAt,
            Periods = periods
        };
    }

    // ---------- Gemini ----------

    private record GeminiItem(
        string Title,
        string Description,
        string? SkillName,
        string? ActivityType,
        int PeriodIndex,
        int EstimatedHours,
        bool IsProject
    );

    private record GeminiRoadmap(List<GeminiItem> Items);

    private async Task<GeminiRoadmap> CallGeminiForRoadmap(
        List<string> skills, string role, int days, string periodLabel)
    {
        int totalPeriods = periodLabel switch
        {
            "Day"   => days,
            "Week"  => (int)Math.Ceiling(days / 7.0),
            "Month" => (int)Math.Ceiling(days / 30.0),
            _       => days
        };

        var prompt = $$"""
            You are a technical career coach. Generate a structured learning roadmap.

            Target role: {{role}}
            Timeframe: {{days}} days ({{totalPeriods}} {{periodLabel.ToLower()}}s)
            Skills to cover: {{string.Join(", ", skills)}}

            Rules:
            - Distribute skills across {totalPeriods} {periodLabel.ToLower()}s (PeriodIndex 1 to {totalPeriods})
            - Each item must have: Title, Description (2-3 sentences, actionable), SkillName (exact match from skill list or null for project), ActivityType (Read|Build|Practice|Research), PeriodIndex, EstimatedHours
            - Order by dependency (fundamentals first)
            - Last item MUST be a project suggestion (IsProject: true, SkillName: null, ActivityType: null)
            - Project description must mention: specific features to build, key technical concepts to apply (CORS, JWT, ORM, hashing, architecture, etc.)
            - Do not include any URLs or course names
            - EstimatedHours per item: 2-8 hours
            - Keep total hours realistic for the timeframe

            Respond ONLY with a JSON object, no markdown, no explanation:
            {
              "items": [
                {
                  "title": "...",
                  "description": "...",
                  "skillName": "...",
                  "activityType": "Read|Build|Practice|Research",
                  "periodIndex": 1,
                  "estimatedHours": 4,
                  "isProject": false
                }
              ]
            }
            """;

        var apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key not configured.");
        var model = config["Gemini:Model"] ?? "gemini-2.5-flash-lite";
        using var http = new HttpClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.4, maxOutputTokens = 4096 }
        });

        var response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        var json = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error))
            throw new Exception($"Gemini API error: {error.GetProperty("message").GetString()}");

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new Exception($"No candidates in Gemini response: {json}");

        var candidate = candidates[0];

// Handle blocked/filtered responses
        if (candidate.TryGetProperty("finishReason", out var reason) && 
            reason.GetString() is "SAFETY" or "RECITATION" or "OTHER")
            throw new Exception($"Gemini blocked response: {reason.GetString()}");

        if (!candidate.TryGetProperty("content", out var content))
            throw new Exception($"No content in candidate: {json}");

        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            throw new Exception($"No parts in content: {json}");

        var text = parts[0].GetProperty("text").GetString()
                   ?? throw new Exception("Empty text in Gemini response");
        var clean = text.Replace("```json", "").Replace("```", "").Trim();

        var result = JsonSerializer.Deserialize<GeminiRoadmapRaw>(clean,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Failed to parse Gemini roadmap response");

        var items = result.Items.Select(i => new GeminiItem(
            i.Title ?? "",
            i.Description ?? "",
            i.SkillName,
            i.ActivityType,
            Math.Clamp(i.PeriodIndex, 1, totalPeriods),
            Math.Clamp(i.EstimatedHours, 1, 8),
            i.IsProject
        )).ToList();

        return new GeminiRoadmap(items);
    }

    // raw deserialization target
    private class GeminiRoadmapRaw
    {
        public List<GeminiItemRaw> Items { get; set; } = [];
    }

    private class GeminiItemRaw
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? SkillName { get; set; }
        public string? ActivityType { get; set; }
        public int PeriodIndex { get; set; }
        public int EstimatedHours { get; set; }
        public bool IsProject { get; set; }
    }
}
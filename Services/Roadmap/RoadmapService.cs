using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Roadmap;
using Xllerate.Models.MongoDB;
using Xllerate.Static;

namespace Xllerate.Services.Roadmap;

public class RoadmapService(ApplicationDbContext db, MongoDbContext mongo, IConfiguration config, ILogger<RoadmapService> logger)
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
    var existingCount = await mongo.Roadmaps
        .CountDocumentsAsync(r => r.AnalysisId == req.AnalysisId);
    
    if (existingCount >= 3)
        throw new InvalidOperationException("LIMIT");

    // duplicate check — same analysisId + same timeframe
    var duplicate = await mongo.Roadmaps.Find(r =>
        r.AnalysisId == req.AnalysisId &&
        r.TimeframeDays == days).AnyAsync();
    
    if (duplicate)
        throw new InvalidOperationException("CONFLICT");

    // deactivate current active roadmap
    await mongo.Roadmaps.UpdateOneAsync(
        r => r.UserId == userId && r.IsActive,
        Builders<RoadmapDocument>.Update.Set(r => r.IsActive, false));

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
    
    var gaps = analysis.SkillGaps.OrderByDescending(g => g.Severity).ToList();
    var gapSkillNames = gaps.Select(g => g.Skill.Name).ToList();
    var seeds = RoleSeedTasks.TryGetValue(roleKey, out var s) ? s : [];
    var allSkills = gapSkillNames.Union(seeds, StringComparer.OrdinalIgnoreCase).ToList();

    var geminiResult = await CallGeminiForRoadmap(allSkills, roleDisplay, days, periodLabel);

    var skillLookup = await db.Skills
        .Where(sk => allSkills.Contains(sk.Name))
        .ToDictionaryAsync(sk => sk.Name, sk => sk.Id, StringComparer.OrdinalIgnoreCase);

    var gapLookup = gaps.ToDictionary(g => g.Skill.Name, g => g, StringComparer.OrdinalIgnoreCase);

    var roadmapId = Guid.NewGuid();
    
    var items = geminiResult.Items.Select((item, idx) =>
    {
        skillLookup.TryGetValue(item.SkillName ?? "", out var skillId);
        gapLookup.TryGetValue(item.SkillName ?? "", out var gap);

        return new RoadmapItemDocument
        {
            Id = Guid.NewGuid(),
            SkillId = skillId == Guid.Empty ? null : skillId,
            SkillGapId = gap?.Id,
            ItemType = item.IsProject
                ? nameof(RoadmapItemType.ProjectSuggestion)
                : nameof(RoadmapItemType.SkillTask),
            ActivityType = item.IsProject ? null : item.ActivityType,
            OrderIndex = idx,
            PeriodIndex = item.PeriodIndex,
            Title = item.Title,
            Description = item.Description,
            EstimatedHours = item.EstimatedHours
        };
    }).ToList();

    var document = new RoadmapDocument
    {
        Id = roadmapId,
        UserId = userId,
        AnalysisId = req.AnalysisId,
        Title = $"{roleDisplay} Roadmap — {req.Timeframe}",
        Status = RoadmapStatus.Active.ToString(),
        TimeframeDays = days,
        PeriodLabel = periodLabel,
        IsActive = true,
        EstimatedTotalHours = geminiResult.Items.Sum(i => i.EstimatedHours),
        GeneratedAt = DateTime.UtcNow,
        Items = items
    };

    await mongo.Roadmaps.InsertOneAsync(document);

    return ToDto(document);
}

public async Task<RoadmapDto?> GetActiveAsync(Guid userId)
{
    var doc = await mongo.Roadmaps
        .Find(r => r.UserId == userId && r.IsActive)
        .FirstOrDefaultAsync();

    return doc == null ? null : ToDto(doc);
}

    private static RoadmapDto ToDto(RoadmapDocument doc)
    {
        var periods = doc.Items
            .GroupBy(i => i.PeriodIndex)
            .OrderBy(g => g.Key)
            .Select(g => new RoadmapPeriodDto
            {
                PeriodIndex = g.Key,
                Label = $"{doc.PeriodLabel} {g.Key}",
                Items = g.OrderBy(i => i.OrderIndex).Select(i => new RoadmapItemDto
                {
                    ItemId = i.Id,
                    ItemType = i.ItemType,
                    ActivityType = i.ActivityType,
                    Title = i.Title,
                    Description = i.Description,
                    EstimatedHours = i.EstimatedHours,
                    OrderIndex = i.OrderIndex,
                    IsCompleted = i.IsCompleted,
                    SkillName = i.SkillName
                }).ToList()
            }).ToList();
        Random random = new Random();
        var index = random.Next(0, Seed.Quotes.Length);
        return new RoadmapDto
        {
            RoadmapId = doc.Id,
            Title = doc.Title,
            Status = doc.Status.ToString(),
            TimeframeDays = doc.TimeframeDays,
            PeriodLabel = doc.PeriodLabel,
            EstimatedTotalHours = doc.EstimatedTotalHours,
            GeneratedAt = doc.GeneratedAt,
            Periods = periods,
            Quote = Seed.Quotes[index],
            Author = Seed.Authors[index]
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
            - Distribute skills across {{totalPeriods}} {{periodLabel.ToLower()}}s (PeriodIndex 1 to {{totalPeriods}})
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
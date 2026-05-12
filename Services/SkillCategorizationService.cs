using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models;

namespace Xllerate.Services;

public class SkillCategorizationService(
    ApplicationDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SkillCategorizationService> logger)
{
    // Controlled vocabulary — Gemini must pick from these
    public static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Frontend", "Backend", "DevOps", "Database", "ML/AI",
        "Mobile", "Security", "Testing", "Blockchain",
        "Systems", "Data Engineering", "Cloud", "Other"
    };

    // Fast local lookup for common skills — avoids Gemini call
    public static readonly Dictionary<string, string> LocalMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["react"] = "Frontend",       ["next.js"] = "Frontend",
        ["angular"] = "Frontend",     ["vue"] = "Frontend",
        ["html"] = "Frontend",        ["css"] = "Frontend",
        ["typescript"] = "Frontend",  ["javascript"] = "Frontend",
        ["tailwind"] = "Frontend",    ["sass"] = "Frontend",
        ["webpack"] = "Frontend",     ["vite"] = "Frontend",
        ["svelte"] = "Frontend",      ["bootstrap"] = "Frontend",

        ["spring boot"] = "Backend",  ["django"] = "Backend",
        ["fastapi"] = "Backend",      ["flask"] = "Backend",
        ["node.js"] = "Backend",      ["express"] = "Backend",
        ["asp.net"] = "Backend",      ["nestjs"] = "Backend",
        ["laravel"] = "Backend",      ["rest apis"] = "Backend",
        ["graphql"] = "Backend",      ["grpc"] = "Backend",
        ["java"] = "Backend",         ["python"] = "Backend",
        ["c#"] = "Backend",           ["go"] = "Backend",
        ["ruby"] = "Backend",         ["php"] = "Backend",
        ["scala"] = "Backend",        ["kotlin"] = "Backend",
        ["spring"] = "Backend",       ["hibernate"] = "Backend",
        ["microservices"] = "Backend",["maven"] = "Backend",
        ["jwt"] = "Backend",          [".net"] = "Backend",
        ["entity framework"] = "Backend",

        ["docker"] = "DevOps",        ["kubernetes"] = "DevOps",
        ["terraform"] = "DevOps",     ["ci/cd"] = "DevOps",
        ["ansible"] = "DevOps",       ["jenkins"] = "DevOps",
        ["github actions"] = "DevOps",["linux"] = "DevOps",
        ["bash"] = "DevOps",          ["helm"] = "DevOps",
        ["prometheus"] = "DevOps",    ["grafana"] = "DevOps",
        ["yaml"] = "DevOps",

        ["aws"] = "Cloud",            ["gcp"] = "Cloud",
        ["azure"] = "Cloud",          ["cloudflare"] = "Cloud",

        ["postgresql"] = "Database",  ["mysql"] = "Database",
        ["mongodb"] = "Database",     ["redis"] = "Database",
        ["sql"] = "Database",         ["elasticsearch"] = "Database",
        ["cassandra"] = "Database",   ["dynamodb"] = "Database",
        ["sqlite"] = "Database",      ["nosql"] = "Database",
        ["oracle"] = "Database",      ["timescaledb"] = "Database",

        ["pytorch"] = "ML/AI",        ["tensorflow"] = "ML/AI",
        ["scikit-learn"] = "ML/AI",   ["pandas"] = "ML/AI",
        ["numpy"] = "ML/AI",          ["spark"] = "Data Engineering",
        ["hadoop"] = "Data Engineering", ["kafka"] = "Data Engineering",
        ["airflow"] = "Data Engineering", ["mlflow"] = "ML/AI",
        ["machine learning"] = "ML/AI",  ["deep learning"] = "ML/AI",
        ["nlp"] = "ML/AI",            ["computer vision"] = "ML/AI",

        ["react native"] = "Mobile",  ["flutter"] = "Mobile",
        ["swift"] = "Mobile",         ["kotlin"] = "Mobile",
        ["android"] = "Mobile",       ["ios"] = "Mobile",
        ["xamarin"] = "Mobile",

        ["jwt"] = "Security",         ["oauth"] = "Security",
        ["penetration testing"] = "Security", ["cybersecurity"] = "Security",

        ["jest"] = "Testing",         ["junit"] = "Testing",
        ["selenium"] = "Testing",     ["cypress"] = "Testing",
        ["pytest"] = "Testing",       ["testing"] = "Testing",

        ["solidity"] = "Blockchain",  ["web3"] = "Blockchain",
        ["ethereum"] = "Blockchain",

        ["rust"] = "Systems",         ["c++"] = "Systems",
        ["c"] = "Systems",            ["assembly"] = "Systems",
        ["embedded"] = "Systems",

        ["git"] = "Other",            ["jira"] = "Other",
        ["figma"] = "Other",          ["agile"] = "Other",
        ["scrum"] = "Other",          ["npm"] = "Other",
        ["object-oriented programming"] = "Other",
        ["distributed systems"] = "Backend",
        ["data structures & algorithms"] = "Other",
    };

    /// <summary>
    /// Returns category for a skill — local map first, Gemini fallback.
    /// </summary>
    public async Task<string> GetCategoryAsync(string skillName, CancellationToken ct = default)
    {
        var normalized = skillName.ToLowerInvariant().Trim();

        if (LocalMap.TryGetValue(normalized, out var local))
            return local;

        return await AskGeminiAsync(skillName, ct);
    }

    /// <summary>
    /// Stamps Category on a skill if not already set.
    /// </summary>
    public async Task CategorizeAsync(Skill skill, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(skill.Category)) return;
        skill.Category = await GetCategoryAsync(skill.Name, ct);
    }

    /// <summary>
    /// Backfill job — categorizes all skills where Category is null.
    /// Batches to avoid Gemini token limits.
    /// </summary>
    public async Task BackfillAsync(CancellationToken ct = default)
    {
        var uncategorized = await db.Skills
            .Where(s => s.Category == null)
            .ToListAsync(ct);

        if (uncategorized.Count == 0)
        {
            logger.LogInformation("No skills to backfill.");
            return;
        }

        logger.LogInformation("Backfilling categories for {Count} skills.", uncategorized.Count);

        // First pass — local map (fast, no API calls)
        var remaining = new List<Skill>();
        foreach (var skill in uncategorized)
        {
            var normalized = skill.NormalizedName;
            if (LocalMap.TryGetValue(normalized, out var cat))
                skill.Category = cat;
            else
                remaining.Add(skill);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Local map resolved {Count} skills. {Remaining} need Gemini.",
            uncategorized.Count - remaining.Count, remaining.Count);

        // Second pass — batch Gemini for the rest (50 at a time)
        const int batchSize = 50;
        for (var i = 0; i < remaining.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = remaining.Skip(i).Take(batchSize).ToList();
            await BatchCategorizeAsync(batch, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Backfill progress: {Done}/{Total}",
                Math.Min(i + batchSize, remaining.Count), remaining.Count);
        }

        logger.LogInformation("Backfill complete.");
    }

    private async Task BatchCategorizeAsync(List<Skill> skills, CancellationToken ct)
    {
        var skillList = string.Join("\n", skills.Select((s, i) => $"{i + 1}. {s.Name}"));
        var validList = string.Join(", ", ValidCategories);

        var prompt = $$$"""
            Categorize each skill below into exactly one category from this list:
            {{{validList}}}

            Skills:
            {{{skillList}}}

            Return ONLY a JSON array with one entry per skill in the same order.
            No markdown, no explanation:
            [
              {{"index": 1, "category": "Backend"}},
              {{"index": 2, "category": "Frontend"}}
            ]
            """;

        var apiKey = config["Gemini:ApiKey"];
        var model  = config["Gemini:Model"] ?? "gemini-2.5-flash";

        var requestBody = new
        {
            contents         = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        try
        {
            var client   = httpClientFactory.CreateClient("gemini");
            var url      = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var raw  = await response.Content.ReadAsStringAsync(ct);
            var doc  = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "[]";

            var results = JsonSerializer.Deserialize<List<CategoryResult>>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            foreach (var result in results)
            {
                if (result.Index < 1 || result.Index > skills.Count) continue;
                var skill    = skills[result.Index - 1];
                var category = ValidCategories.Contains(result.Category)
                    ? result.Category
                    : "Other";
                skill.Category = category;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch categorization failed — defaulting to 'Other'.");
            foreach (var skill in skills.Where(s => s.Category == null))
                skill.Category = "Other";
        }
    }

    private async Task<string> AskGeminiAsync(string skillName, CancellationToken ct)
    {
        var validList = string.Join(", ", ValidCategories);
        var prompt    = $"Categorize this software skill into exactly one category from: {validList}\nSkill: {skillName}\nReturn ONLY the category name, nothing else.";

        var apiKey = config["Gemini:ApiKey"];
        var model  = config["Gemini:Model"] ?? "gemini-2.5-flash";

        var requestBody = new
        {
            contents         = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.1 }
        };

        try
        {
            var client   = httpClientFactory.CreateClient("gemini");
            var url      = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var raw      = await response.Content.ReadAsStringAsync(ct);
            var doc      = JsonDocument.Parse(raw);
            var category = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()?.Trim() ?? "Other";

            return ValidCategories.Contains(category) ? category : "Other";
        }
        catch
        {
            return "Other";
        }
    }

    private record CategoryResult(int Index, string Category);
}
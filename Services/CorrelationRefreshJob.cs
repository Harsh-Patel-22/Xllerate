using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models;

namespace Xllerate.Services;

public class CorrelationRefreshJob(
    ApplicationDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    SkillCategorizationService categorization,
    ILogger<CorrelationRefreshJob> logger)
{
    private static readonly string[] RoleBuckets =
    [
        "software_engineering", "data_ml", "devops",
        "frontend", "backend", "fullstack", "product_management"
    ];

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting Gemini correlation refresh for {Count} role buckets.", RoleBuckets.Length);

        foreach (var roleKey in RoleBuckets)
        {
            try
            {
                await RefreshForRoleAsync(roleKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Correlation refresh failed for role {RoleKey}.", roleKey);
            }
        }

        logger.LogInformation("Correlation refresh complete.");
    }

    private async Task RefreshForRoleAsync(string roleKey, CancellationToken ct)
    {
        var roleLabel = roleKey.Replace("_", " ");

        var prompt = $$$"""
            You are a technical hiring expert. For a {{{roleLabel}}} role, list the most important skill correlations.

            For each primary skill, list which other skills are almost always required alongside it in real industry job postings for this role.
            Assign a correlation strength from 0.0 to 1.0:
            - 0.85–1.0: virtually always required together (e.g. Spring Boot + Java)
            - 0.65–0.84: very commonly paired
            - 0.45–0.64: often seen together
            - 0.25–0.44: occasionally paired

            Return ONLY a JSON array. No markdown, no explanation:
            [
              { "skillA": "Spring Boot", "skillB": "Java", "strength": 0.95 },
              { "skillA": "Spring Boot", "skillB": "REST APIs", "strength": 0.90 }
            ]

            Focus on technical skills only. Include at least 30 pairs. Be specific (e.g. "PostgreSQL" not "Databases").
            """;

        var apiKey = config["Gemini:ApiKey"];
        var model  = config["Gemini:Model"] ?? "gemini-2.5-flash";

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

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

        List<GeminiCorrelationPair> pairs;
        try
        {
            pairs = JsonSerializer.Deserialize<List<GeminiCorrelationPair>>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Gemini response for role {RoleKey}.", roleKey);
            return;
        }

        var upserted = 0;

        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair.SkillA) ||
                string.IsNullOrWhiteSpace(pair.SkillB)) continue;

            var normA = pair.SkillA.ToLowerInvariant().Trim();
            var normB = pair.SkillB.ToLowerInvariant().Trim();

            var skillA = await ResolveOrCreateSkillAsync(pair.SkillA, normA, ct);
            var skillB = await ResolveOrCreateSkillAsync(pair.SkillB, normB, ct);

            var existing = await db.SkillCorrelations
                .FirstOrDefaultAsync(c =>
                    c.SkillAId == skillA.Id &&
                    c.SkillBId == skillB.Id &&
                    c.RoleKey  == roleKey, ct);

            if (existing != null)
            {
                existing.Strength    = pair.Strength;
                existing.Source      = CorrelationSource.Gemini;
                existing.ComputedAt  = DateTime.UtcNow;
            }
            else
            {
                db.SkillCorrelations.Add(new SkillCorrelation
                {
                    Id         = Guid.NewGuid(),
                    SkillAId   = skillA.Id,
                    SkillBId   = skillB.Id,
                    RoleKey    = roleKey,
                    Strength   = Math.Clamp(pair.Strength, 0.0m, 1.0m),
                    Source     = CorrelationSource.Gemini,
                    ComputedAt = DateTime.UtcNow
                });
            }

            upserted++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Correlation refresh for {RoleKey} complete — {Count} pairs upserted.", roleKey, upserted);
    }

    private async Task<Skill> ResolveOrCreateSkillAsync(
        string displayName, string normalized, CancellationToken ct)
    {
        var skill = await db.Skills
            .FirstOrDefaultAsync(s => s.NormalizedName == normalized, ct);

        if (skill != null) return skill;

        skill = new Skill
        {
            Id             = Guid.NewGuid(),
            Name           = displayName,
            NormalizedName = normalized,
            IsVerified     = false
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        
        await categorization.CategorizeAsync(skill, ct);
        await db.SaveChangesAsync(ct);
        return skill;
    }

    private record GeminiCorrelationPair(string SkillA, string SkillB, decimal Strength);
}
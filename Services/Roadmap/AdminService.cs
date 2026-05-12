using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Models.MongoDB;
using Microsoft.EntityFrameworkCore;

namespace Xllerate.Services.Roadmap;

public class AdminService (MongoDbContext mongo, ApplicationDbContext db, ILogger<ApplicationDbContext> logger) {
    public async Task<SkillGraphResponseDto> GetSkillGraphAsync()
    {
        var roleProfiles = await mongo.RoleProfiles.Find(_ => true).ToListAsync();

        // Prefetch categories for all skillIds in one SQL query
        var allSkillIds = roleProfiles
            .SelectMany(r => r.Skills)
            .Where(s => s.SkillId.HasValue)
            .Select(s => s.SkillId!.Value)
            .Distinct()
            .ToList();

        var categoryMap = await db.Skills
            .Where(s => allSkillIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Category);

        // Prefetch all relevant correlations in one SQL query
        var roleKeys = roleProfiles.Select(r => r.RoleKey).ToList();
        var correlations = await db.SkillCorrelations
            .Include(c => c.SkillA)
            .Include(c => c.SkillB)
            .Where(c => roleKeys.Contains(c.RoleKey) || c.RoleKey == null)
            .ToListAsync();

        var roles = roleProfiles.Select(profile =>
        {
            var nodes = profile.Skills.Select(s => new SkillNodeDto
            {
                SkillName          = s.SkillName,
                SkillId            = s.SkillId,
                ProbabilisticScore = s.ProbabilisticScore,
                AggregatedWeight   = s.AggregatedWeight,
                FrequencyRatio     = s.FrequencyRatio,
                IsCore             = s.IsCore,
                Confidence         = s.Confidence,
                Category           = s.SkillId.HasValue && categoryMap.TryGetValue(s.SkillId.Value, out var cat)
                                         ? cat : null
            }).ToList();

            var skillNamesInRole = nodes.Select(n => n.SkillName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var edges = correlations
                .Where(c => (c.RoleKey == profile.RoleKey || c.RoleKey == null)
                            && skillNamesInRole.Contains(c.SkillA.Name)
                            && skillNamesInRole.Contains(c.SkillB.Name))
                .Select(c => new SkillEdgeDto
                {
                    Source            = c.SkillA.Name,
                    Target            = c.SkillB.Name,
                    Strength          = (double)c.Strength,
                    CorrelationSource = c.Source.ToString()
                }).ToList();

            return new RoleSkillGraphDto
            {
                RoleKey     = profile.RoleKey,
                DisplayName = profile.DisplayName,
                JobsSampled = profile.JobsSampled,
                Confidence  = profile.Confidence,
                Nodes       = nodes,
                Edges       = edges
            };
        }).ToList();

        return new SkillGraphResponseDto { Roles = roles };
    }

    public async Task UpdateSkillImportanceAsync(UpdateSkillImportanceDto dto)
    {
        var filter = Builders<RoleProfileDocument>.Filter.Eq(r => r.RoleKey, dto.RoleKey);
        var profile = await mongo.RoleProfiles.Find(filter).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Role '{dto.RoleKey}' not found.");

        var skill = profile.Skills.FirstOrDefault(s =>
            s.SkillName.Equals(dto.SkillName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Skill '{dto.SkillName}' not found in role '{dto.RoleKey}'.");

        skill.ProbabilisticScore = dto.ProbabilisticScore;

        await mongo.RoleProfiles.ReplaceOneAsync(filter, profile);
    }
    
    public async Task UpdateRoleSkillAsync(UpdateSkillImportanceDto dto)
    {
        var filter = Builders<RoleProfileDocument>.Filter.Eq(r => r.RoleKey, dto.RoleKey);
        var profile = await mongo.RoleProfiles.Find(filter).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Role '{dto.RoleKey}' not found.");

        var skill = profile.Skills.FirstOrDefault(s =>
            s.SkillName.Equals(dto.SkillName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Skill '{dto.SkillName}' not found in role '{dto.RoleKey}'.");

        skill.ProbabilisticScore = dto.ProbabilisticScore;
        skill.AggregatedWeight   = dto.AggregatedWeight;
        skill.FrequencyRatio     = dto.FrequencyRatio;
        skill.IsCore             = dto.IsCore;

        await mongo.RoleProfiles.ReplaceOneAsync(filter, profile);
    }

    public async Task AddRoleSkillAsync(AddRoleSkillDto dto)
    {
        var filter = Builders<RoleProfileDocument>.Filter.Eq(r => r.RoleKey, dto.RoleKey);
        var profile = await mongo.RoleProfiles.Find(filter).FirstOrDefaultAsync()
                      ?? throw new KeyNotFoundException($"Role '{dto.RoleKey}' not found.");

        // Check duplicate in role profile first
        var alreadyInRole = profile.Skills.Any(s =>
            s.SkillName.Equals(dto.SkillName, StringComparison.OrdinalIgnoreCase));
        if (alreadyInRole)
            throw new InvalidOperationException($"'{dto.SkillName}' already exists in role '{dto.RoleKey}'.");

        // Try to match against SQL Skills table via normalized name (regex-style contains)
        var normalizedInput = dto.SkillName.ToLowerInvariant().Trim();
        var matchedSkill = await db.Skills
            .FirstOrDefaultAsync(s =>
                s.NormalizedName == normalizedInput ||
                s.NormalizedName.Contains(normalizedInput) ||
                normalizedInput.Contains(s.NormalizedName));

        profile.Skills.Add(new RoleProfileSkillDocument
        {
            SkillId            = matchedSkill?.Id,        // null if no SQL match — that's fine
            SkillName          = matchedSkill?.Name ?? dto.SkillName,  // prefer canonical name if matched
            ProbabilisticScore = dto.ProbabilisticScore,
            AggregatedWeight   = dto.AggregatedWeight,
            FrequencyRatio     = dto.FrequencyRatio,
            IsCore             = dto.IsCore,
            Confidence         = profile.Confidence
        });

        await mongo.RoleProfiles.ReplaceOneAsync(filter, profile);
    }

    public async Task DeleteRoleSkillAsync(DeleteRoleSkillDto dto)
    {
        var filter = Builders<RoleProfileDocument>.Filter.Eq(r => r.RoleKey, dto.RoleKey);
        var profile = await mongo.RoleProfiles.Find(filter).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Role '{dto.RoleKey}' not found.");

        var skill = profile.Skills.FirstOrDefault(s =>
            s.SkillName.Equals(dto.SkillName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Skill '{dto.SkillName}' not found in role '{dto.RoleKey}'.");

        profile.Skills.Remove(skill);
        await mongo.RoleProfiles.ReplaceOneAsync(filter, profile);

        // Clean up correlations in SQL where either side is this skill in this role
        var correlationsToDelete = await db.SkillCorrelations
            .Include(c => c.SkillA)
            .Include(c => c.SkillB)
            .Where(c => c.RoleKey == dto.RoleKey &&
                        (c.SkillA.Name == dto.SkillName || c.SkillB.Name == dto.SkillName))
            .ToListAsync();

        if (correlationsToDelete.Count > 0)
        {
            db.SkillCorrelations.RemoveRange(correlationsToDelete);
            await db.SaveChangesAsync();
        }
    }
}
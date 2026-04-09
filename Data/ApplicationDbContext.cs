using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Xllerate.Models;
using Xllerate.Models.Interfaces;

namespace Xllerate.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
 : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options) {
 public DbSet<User> Users => Set<User>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<SkillSource> SkillSources => Set<SkillSource>();
    public DbSet<ExternalProfile> ExternalProfiles => Set<ExternalProfile>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<JobRole> JobRoles => Set<JobRole>();
    public DbSet<JobRoleSkill> JobRoleSkills => Set<JobRoleSkill>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<SkillGap> SkillGaps => Set<SkillGap>();
    public DbSet<Roadmap> Roadmaps => Set<Roadmap>();
    public DbSet<RoadmapItem> RoadmapItems => Set<RoadmapItem>();
    public DbSet<MarketTrend> MarketTrends => Set<MarketTrend>();
    
    public DbSet<SparkSkillStaging> SparkSkillStaging => Set<SparkSkillStaging>();

    // ── Auto-set UpdatedAt on every SaveChanges ───────────────────────────────
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── User ─────────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);

            e.Property(u => u.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(u => u.Email)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(u => u.FullName)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(u => u.AvatarUrl)
             .HasMaxLength(2048);

            e.HasIndex(u => u.Email)
             .IsUnique();

            // UserPreferences stored as JSON in nvarchar(max)
            e.OwnsOne(u => u.Preferences, p => p.ToJson());
        });

        // ── Resume ────────────────────────────────────────────────────────────
        mb.Entity<Resume>(e =>
        {
         e.HasKey(r => r.Id);

         e.Property(r => r.Id)
          .HasDefaultValueSql("NEWID()");

         e.Property(r => r.StoragePath)
          .HasMaxLength(2048)
          .IsRequired();

         e.Property(r => r.OriginalFileName)
          .HasMaxLength(512);

         e.OwnsOne(r => r.ParsedData, pd =>
         {
          pd.ToJson();

          // Nested owned collections
          pd.OwnsMany(p => p.Education);
          pd.OwnsMany(p => p.WorkExperience);

          // Nested owned single objects
          pd.OwnsOne(p => p.Links);
          pd.OwnsOne(p => p.ContactInfo);
          pd.OwnsOne(p => p.Metadata, m =>
          {
           m.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 4);   // e.g. 0.9823
          });

          // List<string> primitives are handled automatically by EF Core — no config needed
         });

         e.HasOne(r => r.User)
          .WithMany(u => u.Resumes)
          .HasForeignKey(r => r.UserId)
          .OnDelete(DeleteBehavior.Cascade);

         e.HasIndex(r => r.UserId);
         e.HasIndex(r => new { r.UserId, r.IsActive });
        });
        
        mb.Entity<SparkSkillStaging>(e =>
        {
         e.HasKey(s => s.Id);

         e.Property(s => s.Id)
          .HasDefaultValueSql("NEWID()");

         e.Property(s => s.ResumeId)
          .HasMaxLength(120)
          .IsRequired();

         e.Property(s => s.SkillName)
          .HasMaxLength(128)
          .IsRequired();

         e.Property(s => s.NormalizedName)
          .HasMaxLength(128)
          .IsRequired();

         e.Property(s => s.Source)
          .HasMaxLength(32)
          .IsRequired();

         e.Property(s => s.Confidence)
          .HasColumnType("decimal(5,4)");

         // Index for the polling query in GetStatus
         e.HasIndex(s => new { s.ResumeId, s.IsConsumed });
        });

        // ── Skill ─────────────────────────────────────────────────────────────
        mb.Entity<Skill>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(s => s.Name)
             .HasMaxLength(128)
             .IsRequired();

            e.Property(s => s.NormalizedName)
             .HasMaxLength(128)
             .IsRequired();

            e.Property(s => s.Category)
             .HasMaxLength(64);

            e.Property(s => s.Domain)
             .HasMaxLength(64);

            // Unique on NormalizedName — all skill resolution hits this index
            e.HasIndex(s => s.NormalizedName)
             .IsUnique();

            e.HasIndex(s => new { s.Category, s.Domain });
        });

        // ── UserSkill ─────────────────────────────────────────────────────────
        mb.Entity<UserSkill>(e =>
        {
            e.HasKey(us => us.Id);

            e.Property(us => us.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(us => us.ConfidenceScore)
             .HasColumnType("decimal(5,4)");

            e.Property(us => us.YearsOfExperience)
             .HasColumnType("decimal(4,1)");

            e.Property(us => us.VerificationStatus)
             .HasConversion<string>()   // store enum as string, not int
             .HasMaxLength(32);

            // Lightweight metrics snapshot stored as JSON
            e.OwnsOne(us => us.Metadata, m => m.ToJson());

            // One user can have each skill only once
            e.HasIndex(us => new { us.UserId, us.SkillId })
             .IsUnique();

            e.HasIndex(us => new { us.UserId, us.ConfidenceScore });

            // FK → User
            e.HasOne(us => us.User)
             .WithMany(u => u.UserSkills)
             .HasForeignKey(us => us.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // FK → Skill (restrict: don't delete a skill if users have it)
            e.HasOne(us => us.Skill)
             .WithMany(s => s.UserSkills)
             .HasForeignKey(us => us.SkillId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SkillSource ───────────────────────────────────────────────────────
        mb.Entity<SkillSource>(e =>
        {
            e.HasKey(ss => ss.Id);

            e.Property(ss => ss.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(ss => ss.SourceType)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(ss => ss.Evidence)
             .HasMaxLength(1024);

            e.Property(ss => ss.Weight)
             .HasColumnType("decimal(5,4)");

            e.HasIndex(ss => new { ss.UserSkillId, ss.SourceType });

            // FK → UserSkill (cascade: removing a UserSkill removes its sources)
            e.HasOne(ss => ss.UserSkill)
             .WithMany(us => us.Sources)
             .HasForeignKey(ss => ss.UserSkillId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ExternalProfile ───────────────────────────────────────────────────
        mb.Entity<ExternalProfile>(e =>
        {
         e.HasKey(ep => ep.Id);

         e.Property(ep => ep.Id)
          .HasDefaultValueSql("NEWID()");

         e.Property(ep => ep.Platform)
          .HasConversion<string>()
          .HasMaxLength(32);

         e.Property(ep => ep.ProfileUrl)
          .HasMaxLength(2048)
          .IsRequired();

         e.Property(ep => ep.Username)
          .HasMaxLength(128);

         e.Property(ep => ep.SyncStatus)
          .HasConversion<string>()
          .HasMaxLength(32);

         e.Property(ep => ep.SyncErrorMessage)
          .HasMaxLength(1024);

         // Now just a plain string column — no converter needed
         e.Property(ep => ep.RawData)
          .HasColumnType("nvarchar(max)");

         e.HasIndex(ep => new { ep.UserId, ep.Platform })
          .IsUnique();

         e.HasOne(ep => ep.User)
          .WithMany(u => u.ExternalProfiles)
          .HasForeignKey(ep => ep.UserId)
          .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Project ───────────────────────────────────────────────────────────
        mb.Entity<Project>(e =>
        {
         e.HasKey(p => p.Id);

         e.Property(p => p.Id)
          .HasDefaultValueSql("NEWID()");

         e.Property(p => p.Title)
          .HasMaxLength(256)
          .IsRequired();

         e.Property(p => p.RepoUrl)
          .HasMaxLength(2048);

         e.Property(p => p.LiveUrl)
          .HasMaxLength(2048);

         // Value converter + comparer for List<string> → nvarchar(max)
         var listComparer = new ValueComparer<List<string>>(
          (c1, c2) => c1!.SequenceEqual(c2!),              // equality check
          c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),  // hash
          c => c.ToList()                                   // snapshot for change tracking
         );

         e.Property(p => p.TechStack)
          .HasConversion(
           v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
           v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
          .HasColumnType("nvarchar(max)")
          .Metadata.SetValueComparer(listComparer);

         // GithubMetrics as owned JSON object
         e.OwnsOne(p => p.GithubMetrics, m => m.ToJson());

         e.HasIndex(p => p.UserId);

         e.HasOne(p => p.User)
          .WithMany(u => u.Projects)
          .HasForeignKey(p => p.UserId)
          .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Certification ─────────────────────────────────────────────────────
        mb.Entity<Certification>(e =>
        {
            e.HasKey(c => c.Id);

            e.Property(c => c.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(c => c.Name)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(c => c.Issuer)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(c => c.CredentialId)
             .HasMaxLength(256);

            e.Property(c => c.VerificationUrl)
             .HasMaxLength(2048);

            e.HasIndex(c => c.UserId);

            e.HasOne(c => c.User)
             .WithMany(u => u.Certifications)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Company ───────────────────────────────────────────────────────────
        mb.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);

            e.Property(c => c.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(c => c.Name)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(c => c.NormalizedName)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(c => c.Industry)
             .HasMaxLength(128);

            e.Property(c => c.Website)
             .HasMaxLength(2048);

            e.Property(c => c.LogoUrl)
             .HasMaxLength(2048);

            e.HasIndex(c => c.NormalizedName)
             .IsUnique();
        });

        // ── JobRole ───────────────────────────────────────────────────────────
        mb.Entity<JobRole>(e =>
        {
            e.HasKey(jr => jr.Id);

            e.Property(jr => jr.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(jr => jr.Title)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(jr => jr.NormalizedTitle)
             .HasMaxLength(256)
             .IsRequired();

            e.Property(jr => jr.Seniority)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(jr => jr.Location)
             .HasMaxLength(256);

            e.Property(jr => jr.SourceUrl)
             .HasMaxLength(2048);

            // Raw JD blob as JSON
            e.OwnsOne(jr => jr.RequirementsRaw, r => r.ToJson());

            e.HasIndex(jr => new { jr.CompanyId, jr.NormalizedTitle });
            e.HasIndex(jr => new { jr.IsActive, jr.Seniority });

            e.HasOne(jr => jr.Company)
             .WithMany(c => c.JobRoles)
             .HasForeignKey(jr => jr.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── JobRoleSkill (many-to-many join with payload) ─────────────────────
        mb.Entity<JobRoleSkill>(e =>
        {
            // Composite PK — no separate Id column needed
            e.HasKey(jrs => new { jrs.JobRoleId, jrs.SkillId });

            e.Property(jrs => jrs.ImportanceWeight)
             .HasColumnType("decimal(5,4)");

            e.Property(jrs => jrs.FrequencyInMarket)
             .HasColumnType("decimal(5,4)");

            e.HasOne(jrs => jrs.JobRole)
             .WithMany(jr => jr.RequiredSkills)
             .HasForeignKey(jrs => jrs.JobRoleId)
             .OnDelete(DeleteBehavior.Cascade);

            // Restrict: don't allow a skill row to be deleted while job roles reference it
            e.HasOne(jrs => jrs.Skill)
             .WithMany(s => s.JobRoleSkills)
             .HasForeignKey(jrs => jrs.SkillId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Analysis ──────────────────────────────────────────────────────────
        mb.Entity<Analysis>(e =>
        {
            e.HasKey(a => a.Id);

            e.Property(a => a.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(a => a.MatchPercentage)
             .HasColumnType("decimal(5,2)");

            e.Property(a => a.Status)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(a => a.ModelVersion)
             .HasMaxLength(64);

            // Gemini narrative + structured insight as JSON
            e.OwnsOne(a => a.InsightData, i => i.ToJson());

            e.HasIndex(a => new { a.UserId, a.CreatedAt });
            e.HasIndex(a => new { a.UserId, a.JobRoleId });

            e.HasOne(a => a.User)
             .WithMany(u => u.Analyses)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Restrict: keep analysis records even if a job role is removed
            e.HasOne(a => a.JobRole)
             .WithMany(jr => jr.Analyses)
             .HasForeignKey(a => a.JobRoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SkillGap ──────────────────────────────────────────────────────────
        mb.Entity<SkillGap>(e =>
        {
            e.HasKey(sg => sg.Id);

            e.Property(sg => sg.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(sg => sg.GapType)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(sg => sg.Severity)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(sg => sg.UserScore)
             .HasColumnType("decimal(5,4)");

            e.Property(sg => sg.RequiredScore)
             .HasColumnType("decimal(5,4)");

            e.Property(sg => sg.Recommendation)
             .HasMaxLength(1024);

            e.HasIndex(sg => new { sg.AnalysisId, sg.Severity });

            // Cascade: deleting an analysis removes its skill gaps
            e.HasOne(sg => sg.Analysis)
             .WithMany(a => a.SkillGaps)
             .HasForeignKey(sg => sg.AnalysisId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sg => sg.Skill)
             .WithMany(s => s.SkillGaps)
             .HasForeignKey(sg => sg.SkillId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Roadmap ───────────────────────────────────────────────────────────
        mb.Entity<Roadmap>(e =>
        {
         e.HasKey(r => r.Id);

         e.Property(r => r.Id)
          .HasDefaultValueSql("NEWID()");

         e.Property(r => r.Title)
          .HasMaxLength(256)
          .IsRequired();

         e.Property(r => r.Status)
          .HasConversion<string>()
          .HasMaxLength(32);

         e.HasIndex(r => r.UserId);

         // Primary path — User owns the Roadmap
         e.HasOne(r => r.User)
          .WithMany(u => u.Roadmaps)
          .HasForeignKey(r => r.UserId)
          .OnDelete(DeleteBehavior.Cascade);

         // NoAction — breaks the cycle between User→Analysis→Roadmap and User→Roadmap
         e.HasOne(r => r.Analysis)
          .WithOne(a => a.Roadmap)
          .HasForeignKey<Roadmap>(r => r.AnalysisId)
          .OnDelete(DeleteBehavior.NoAction);
        });

        // ── RoadmapItem ───────────────────────────────────────────────────────
        mb.Entity<RoadmapItem>(e =>
        {
            e.HasKey(ri => ri.Id);

            e.Property(ri => ri.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(ri => ri.ResourceType)
             .HasConversion<string>()
             .HasMaxLength(32);

            e.Property(ri => ri.ResourceTitle)
             .HasMaxLength(512);

            e.Property(ri => ri.ResourceUrl)
             .HasMaxLength(2048);

            e.HasIndex(ri => new { ri.RoadmapId, ri.OrderIndex });

            e.HasOne(ri => ri.Roadmap)
             .WithMany(r => r.Items)
             .HasForeignKey(ri => ri.RoadmapId)
             .OnDelete(DeleteBehavior.Cascade);

            // SkillGap is optional — some roadmap items may be enrichment, not gap-filling
            e.HasOne(ri => ri.SkillGap)
             .WithMany(sg => sg.RoadmapItems)
             .HasForeignKey(ri => ri.SkillGapId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(ri => ri.Skill)
             .WithMany()
             .HasForeignKey(ri => ri.SkillId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        // ── MarketTrend ───────────────────────────────────────────────────────
        mb.Entity<MarketTrend>(e =>
        {
            e.HasKey(mt => mt.Id);

            e.Property(mt => mt.Id)
             .HasDefaultValueSql("NEWID()");

            e.Property(mt => mt.Region)
             .HasMaxLength(64)
             .IsRequired();

            e.Property(mt => mt.JobRoleTitle)
             .HasMaxLength(256);

            e.Property(mt => mt.MentionPercentage)
             .HasColumnType("decimal(5,4)");

            e.Property(mt => mt.GrowthRate)
             .HasColumnType("decimal(7,4)");

            // Unique per skill + period + region — prevents duplicate aggregation runs
            e.HasIndex(mt => new { mt.SkillId, mt.PeriodStart, mt.Region })
             .IsUnique();

            e.HasIndex(mt => new { mt.PeriodStart, mt.MentionPercentage });

            e.HasOne(mt => mt.Skill)
             .WithMany(s => s.MarketTrends)
             .HasForeignKey(mt => mt.SkillId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
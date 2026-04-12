using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xllerate.Data;
using Xllerate.Models;
using Xllerate.Services;
using Xllerate.Services.Enrichment;
using Hangfire;
using Hangfire.SqlServer;
using Xllerate.Infrastructure;
using Xllerate.Services.JobScraping;
using Xllerate.Services.JobScraping.Sources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        // Password rules
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Lockout — lock account for 5 min after 5 failed attempts
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        // Require unique emails
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();


// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew = TimeSpan.Zero   // tokens expire exactly on time, no 5-min grace
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient("hdfs", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Hdfs:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("spark", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Spark:FlaskTriggerUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Add after existing HTTP client registrations

// GitHub API client
builder.Services.AddHttpClient("github", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["GitHub:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("Accept",
        "application/vnd.github+json");
    client.DefaultRequestHeaders.Add(
        "X-GitHub-Api-Version", "2022-11-28");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// LeetCode client — no base address, uses full URLs
builder.Services.AddHttpClient("leetcode", client =>
{
    client.DefaultRequestHeaders.Add(
        "Referer", "https://leetcode.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout       = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout   = TimeSpan.FromMinutes(5),
            QueuePollInterval            = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks           = true
        }));

builder.Services.AddHangfireServer();


// Register enrichment services
builder.Services.AddScoped<GitHubEnrichmentService>();
builder.Services.AddScoped<LeetCodeEnrichmentService>();
builder.Services.AddScoped<ProfileEnrichmentService>();

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<HdfsService>();
builder.Services.AddScoped<ResumeProcessingService>();
builder.Services.AddScoped<ResumeUploadService>();
builder.Services.AddScoped<SparkJobService>();

builder.Services.AddScoped<AnalysisService>();  

// Register job sources — all registered so orchestrator gets them all
builder.Services.AddScoped<IJobSource, AdzunaJobSource>();
builder.Services.AddScoped<IJobSource, JoobleJobSource>();
builder.Services.AddScoped<IJobSource, LinkedInJobSource>();

// Register orchestrator and normalization
builder.Services.AddScoped<CompanyEnrichmentService>();
builder.Services.AddScoped<ScrapingOrchestrator>();
builder.Services.AddScoped<JobNormalizationService>();
builder.Services.AddScoped<RoleBucketDiscoveryService>();
builder.Services.AddScoped<RoleProfileAggregationService>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().WithOrigins(builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [])));

builder.Services.AddControllers();
var app = builder.Build();


if (app.Environment.IsDevelopment()) {
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Restrict dashboard to localhost in development
    Authorization = [new HangfireLocalOnlyFilter()]
});

// Schedule nightly scraping run at 2am
RecurringJob.AddOrUpdate<ScrapingOrchestrator>(
    "nightly-job-scraping",
    orchestrator => orchestrator.RunAsync(CancellationToken.None),
    Cron.Daily(2));

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
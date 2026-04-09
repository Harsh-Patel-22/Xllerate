using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xllerate.Data;

namespace Xllerate.Services.JobScraping;

public class CompanyEnrichmentService(
    ApplicationDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<CompanyEnrichmentService> logger)
{
    // Clearbit's logo API is free and requires no key
    private const string ClearbitLogoUrl = "https://logo.clearbit.com/{0}";

    // Known Indian tech company domains — extend as needed
    // Format: normalized company name fragment → domain
    private static readonly Dictionary<string, string> KnownDomains = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["google"]          = "google.com",
        ["microsoft"]       = "microsoft.com",
        ["amazon"]          = "amazon.com",
        ["flipkart"]        = "flipkart.com",
        ["infosys"]         = "infosys.com",
        ["wipro"]           = "wipro.com",
        ["tcs"]             = "tcs.com",
        ["tata consultancy"] = "tcs.com",
        ["hcl"]             = "hcltech.com",
        ["accenture"]       = "accenture.com",
        ["razorpay"]        = "razorpay.com",
        ["swiggy"]          = "swiggy.com",
        ["zomato"]          = "zomato.com",
        ["paytm"]           = "paytm.com",
        ["ola"]             = "olacabs.com",
        ["uber"]            = "uber.com",
        ["meesho"]          = "meesho.com",
        ["phonepe"]         = "phonepe.com",
        ["freshworks"]      = "freshworks.com",
        ["zoho"]            = "zoho.com",
        ["byju"]            = "byjus.com",
        ["unacademy"]       = "unacademy.com",
        ["atlassian"]       = "atlassian.com",
        ["bairesdev"]       = "bairesdev.com",
        ["flywire"]         = "flywire.com",
        ["nielsen"]         = "nielsen.com",
        ["deloitte"]        = "deloitte.com",
        ["ibm"]             = "ibm.com",
        ["oracle"]          = "oracle.com",
        ["sap"]             = "sap.com",
        ["salesforce"]      = "salesforce.com",
        ["adobe"]           = "adobe.com",
    };

    // Industry inference from normalized company name / known patterns
    private static readonly List<(string fragment, string industry)> IndustryHints = new()
    {
        ("bank", "Banking & Finance"),
        ("capital", "Banking & Finance"),
        ("finance", "Banking & Finance"),
        ("fintech", "Fintech"),
        ("pay", "Fintech"),
        ("insurance", "Insurance"),
        ("health", "Healthcare"),
        ("pharma", "Healthcare"),
        ("medic", "Healthcare"),
        ("consult", "Consulting & IT Services"),
        ("solution", "Consulting & IT Services"),
        ("infotech", "Consulting & IT Services"),
        ("technologies", "Technology"),
        ("software", "Technology"),
        ("systems", "Technology"),
        ("labs", "Technology"),
        ("ai", "Artificial Intelligence"),
        ("analytics", "Data & Analytics"),
        ("data", "Data & Analytics"),
        ("ecommerce", "E-Commerce"),
        ("logistics", "Logistics"),
        ("edtech", "EdTech"),
        ("academy", "EdTech"),
        ("learn", "EdTech"),
        ("game", "Gaming"),
        ("studio", "Gaming"),
        ("media", "Media & Entertainment"),
        ("entertainment", "Media & Entertainment"),
    };

    /// <summary>
    /// Enriches companies that are missing Website, LogoUrl, or Industry.
    /// Call this after a scrape run. Processes in batches to avoid hammering Clearbit.
    /// </summary>
    public async Task EnrichAsync(CancellationToken ct = default)
    {
        // Only process companies that are still missing data
        var companies = await db.Companies
            .Where(c => c.Website == null || c.LogoUrl == null || c.Industry == null)
            .ToListAsync(ct);

        if (companies.Count == 0)
        {
            logger.LogInformation("Company enrichment: nothing to enrich.");
            return;
        }

        logger.LogInformation("Enriching {Count} companies...", companies.Count);
        var client = httpClientFactory.CreateClient();
        var enriched = 0;

        foreach (var company in companies)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Step 1: infer domain
                var domain = InferDomain(company.NormalizedName, company.Name);
                if (domain != null) company.Website = $"https://{domain}";

                // Step 2: fetch logo from Clearbit if domain known
                if (domain != null && company.LogoUrl == null)
                {
                    var logoUrl = string.Format(ClearbitLogoUrl, domain);
                    var logoExists = await CheckUrlExistsAsync(client, logoUrl, ct);
                    if (logoExists) company.LogoUrl = logoUrl;
                }

                // Step 3: infer industry from name
                company.Industry ??= InferIndustry(company.NormalizedName);

                company.CreatedAt = DateTime.UtcNow;
                enriched++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enrich company '{Name}'", company.Name);
            }

            // Polite delay — Clearbit free tier has rate limits
            await Task.Delay(200, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Company enrichment complete. Enriched {Count}/{Total}.",
            enriched, companies.Count);
    }

    private static string? InferDomain(string normalizedName, string originalName)
    {
        // Check known domains first
        foreach (var (fragment, domain) in KnownDomains)
            if (normalizedName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return domain;

        // Fallback: build a guessed domain from cleaned company name
        // e.g. "Cypherox Technologies Pvt Ltd" → "cypherox.com"
        var cleaned = normalizedName
            .Replace("technologies", "")
            .Replace("solutions", "")
            .Replace("consulting", "")
            .Replace("software", "")
            .Replace("systems", "")
            .Replace("services", "")
            .Replace("pvt", "")
            .Replace("ltd", "")
            .Replace("llc", "")
            .Replace("llp", "")
            .Replace("inc", "")
            .Replace("private", "")
            .Replace("limited", "")
            .Trim();

        var firstWord = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault();

        // Only guess if first word is reasonably long — avoids junk like "a.com"
        return firstWord?.Length >= 4 ? $"{firstWord}.com" : null;
    }

    private static string? InferIndustry(string normalizedName)
    {
        foreach (var (fragment, industry) in IndustryHints)
            if (normalizedName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return industry;
        return null; // better null than wrong
    }

    private static async Task<bool> CheckUrlExistsAsync(
        HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var req      = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(req, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
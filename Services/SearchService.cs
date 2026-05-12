using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Xllerate.Services;

public class SearchService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SearchService> logger)
{
    private readonly string? _serperKey = config["Serper:ApiKey"];
    private readonly string? _googleKey = config["GoogleSearch:ApiKey"];
    private readonly string? _googleCx  = config["GoogleSearch:CX"];

    private const string SerperUrl = "https://google.serper.dev/search";
    private const string GoogleCseUrl = "https://www.googleapis.com/customsearch/v1";

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        // 1. Try Google Programmable Search (CSE) first — Official & Robust
        if (!string.IsNullOrWhiteSpace(_googleKey) && !string.IsNullOrWhiteSpace(_googleCx))
        {
            var results = await SearchViaGoogleCseAsync(query, limit, ct);
            if (results.Count > 0) return results;
        }

        // 2. Try Serper.dev as second option
        if (!string.IsNullOrWhiteSpace(_serperKey) && _serperKey != "YOUR_SERPER_API_KEY_HERE")
        {
            var results = await SearchViaSerperAsync(query, limit, ct);
            if (results.Count > 0) return results;
        }

        // 3. Robust 100% Free Fallback: Google News RSS
        return await SearchNewsViaRssAsync(query, limit, ct);
    }

    private async Task<List<SearchResult>> SearchViaGoogleCseAsync(string query, int limit, CancellationToken ct)
    {
        // Removed sort=date to test if it's causing the 403
        var url = $"{GoogleCseUrl}?key={_googleKey}&cx={_googleCx}&q={Uri.EscapeDataString(query)}&num={limit}";
        
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Google CSE API Error ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return [];
            }

            var data = await response.Content.ReadFromJsonAsync<GoogleCseResponse>(ct);
            return data?.Items?.Select(i => new SearchResult
            {
                Title = i.Title,
                Link = i.Link,
                Snippet = i.Snippet,
                Source = new Uri(i.Link).Host
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Google CSE search failed for query: {Query}", query);
            return [];
        }
    }

    private async Task<List<SearchResult>> SearchViaSerperAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _serperKey);

            var response = await client.PostAsJsonAsync(SerperUrl, new { q = query, num = limit }, ct);
            if (!response.IsSuccessStatusCode) return [];

            var data = await response.Content.ReadFromJsonAsync<SerperResponse>(ct);
            return data?.Organic?.Select(o => new SearchResult
            {
                Title = o.Title,
                Link = o.Link,
                Snippet = o.Snippet,
                Source = o.Source ?? new Uri(o.Link).Host
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Serper search failed for query: {Query}", query);
            return [];
        }
    }

    private async Task<List<SearchResult>> SearchNewsViaRssAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(query)}&hl=en-US&gl=US&ceid=US:en";
            
            var response = await client.GetStringAsync(url, ct);
            var doc = System.Xml.Linq.XDocument.Parse(response);
            
            return doc.Descendants("item")
                .Take(limit)
                .Select(item => new SearchResult
                {
                    Title = item.Element("title")?.Value ?? "No Title",
                    Link = item.Element("link")?.Value ?? "",
                    Snippet = "Latest news from " + (item.Element("source")?.Value ?? "Google News"),
                    Source = item.Element("source")?.Value ?? "Google News"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Google News RSS fallback failed for query: {Query}", query);
            return [];
        }
    }
}

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

internal class GoogleCseResponse
{
    public List<GoogleCseItem>? Items { get; set; }
}

internal class GoogleCseItem
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

internal class SerperResponse
{
    public List<SerperOrganic>? Organic { get; set; }
}

internal class SerperOrganic
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? Source { get; set; }
}

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xllerate.Models;
using Xllerate.Models.DTOs.Analysis;
using Xllerate.Models.DTOs.User;

namespace Xllerate.Services;

public class InsightService(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    private const string ClientName = "gemini";

    public async Task<AnalysisInsight> GenerateInsightsAsync(
        AnalysisResultDto analysis, CancellationToken ct = default)
    {
        var apiKey = config["Gemini:ApiKey"];
        var model  = config["Gemini:Model"] ?? "gemini-1.5-flash";
        var prompt = BuildPrompt(analysis);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var client   = httpClientFactory.CreateClient(ClientName);
        var url      = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var response = await client.PostAsJsonAsync(url, requestBody, ct);

        response.EnsureSuccessStatusCode();

        var raw     = await response.Content.ReadAsStringAsync(ct);
        var doc     = JsonDocument.Parse(raw);
        var text    = doc.RootElement
                         .GetProperty("candidates")[0]
                         .GetProperty("content")
                         .GetProperty("parts")[0]
                         .GetProperty("text")
                         .GetString() ?? "[]";

        return ParseInsight(text, raw);
    }

    private static string BuildPrompt(AnalysisResultDto analysis)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are a brutally honest hiring coach. Analyze this candidate's skill gap report for the role: {analysis.JobTitle}.");
        sb.AppendLine($"Match score: {analysis.MatchPercentage}%");
        sb.AppendLine($"Matched: {analysis.Summary.Matched}, Missing: {analysis.Summary.Missing}, Weak: {analysis.Summary.Weak}, Unverified: {analysis.Summary.Unverified}");
        sb.AppendLine();
        sb.AppendLine("Skill gaps:");

        foreach (var gap in analysis.SkillGaps)
            sb.AppendLine($"- {gap.SkillName} | {gap.GapType} | {gap.Severity} | user: {gap.UserScore:F2} vs required: {gap.RequiredScore:F2}");

        sb.AppendLine();
        sb.AppendLine("""
            Return a JSON object with exactly these fields:
            - "summary": 1-2 sentence overall verdict
            - "whyNotHired": the single most critical reason they won't get hired
            - "strengths": array of 2-3 short strings of what they have going for them
            - "criticalGaps": array of 3-5 short strings, each a specific brutal gap
            - "marketContext": 1-2 sentences on market demand for this role right now

            Return ONLY the JSON object. No markdown, no preamble.
            """);

        return sb.ToString();
    }

    private static AnalysisInsight ParseInsight(string json, string rawResponse)
    {
        try
        {
            var el = JsonDocument.Parse(json).RootElement;

            return new AnalysisInsight
            {
                Summary            = el.GetProperty("summary").GetString(),
                WhyNotHired        = el.GetProperty("whyNotHired").GetString(),
                Strengths          = el.GetProperty("strengths")
                                       .EnumerateArray()
                                       .Select(s => s.GetString() ?? string.Empty)
                                       .ToList(),
                CriticalGaps       = el.GetProperty("criticalGaps")
                                       .EnumerateArray()
                                       .Select(s => s.GetString() ?? string.Empty)
                                       .ToList(),
                MarketContext      = el.GetProperty("marketContext").GetString(),
                GeminiRawResponse  = rawResponse
            };
        }
        catch
        {
            return new AnalysisInsight { GeminiRawResponse = rawResponse };
        }
    }
}
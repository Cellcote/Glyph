using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glyph.Services;

[JsonSerializable(typeof(AiService.ChatRequest))]
[JsonSerializable(typeof(AiService.ChatResponse))]
internal partial class AiJsonContext : JsonSerializerContext;

public static class AiService
{
    private const string Endpoint = "https://models.inference.ai.azure.com/chat/completions";
    private const string Model = "gpt-4o-mini";
    private const int MaxDiffLength = 8000;

    public static async Task<string?> GenerateCommitMessageAsync(string diff)
    {
        var prompt = """
            You are a concise commit message generator. Given a git diff, write a single-line
            commit message following conventional commit style (e.g. feat:, fix:, refactor:, docs:, chore:).
            Be specific about what changed. Do not include a body or footer.
            Output ONLY the commit message, nothing else.
            """;

        return await CallAsync(prompt, TruncateDiff(diff));
    }

    public static async Task<(string Title, string Body)?> GeneratePrDescriptionAsync(
        string diff, string branchName, string parentBranch)
    {
        var prompt = $"""
            You are a pull request description generator. Given a git diff for a branch being merged
            from "{branchName}" into "{parentBranch}", generate a PR title and body.

            Respond in EXACTLY this format (no other text):
            TITLE: <short PR title under 70 chars>
            BODY:
            ## Summary
            <2-4 bullet points describing the changes>

            ## Changes
            <brief list of key file changes>
            """;

        var result = await CallAsync(prompt, TruncateDiff(diff));
        if (result == null)
            return null;

        return ParsePrResponse(result);
    }

    private static (string Title, string Body) ParsePrResponse(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.None);
        string title = "";
        var bodyLines = new List<string>();
        var inBody = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["TITLE:".Length..].Trim();
            }
            else if (line.StartsWith("BODY:", StringComparison.OrdinalIgnoreCase))
            {
                inBody = true;
                var rest = line["BODY:".Length..].Trim();
                if (!string.IsNullOrEmpty(rest))
                    bodyLines.Add(rest);
            }
            else if (inBody)
            {
                bodyLines.Add(line);
            }
        }

        if (string.IsNullOrEmpty(title))
            title = lines[0]; // Fallback: use first line as title

        var body = string.Join('\n', bodyLines).Trim();
        return (title, body);
    }

    public static async Task<string?> GetTokenAsync()
    {
        // 1. Check environment variable
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(token))
            return token;

        // 2. Fall back to gh CLI
        try
        {
            var (exitCode, output, _) = await ProcessRunner.RunAsync("gh", "auth token");
            if (exitCode == 0 && !string.IsNullOrEmpty(output))
                return output.Trim();
        }
        catch
        {
            // gh not available
        }

        return null;
    }

    private static async Task<string?> CallAsync(string systemPrompt, string userMessage)
    {
        var token = await GetTokenAsync();
        if (token == null)
            return null;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new ChatRequest
        {
            Model = Model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userMessage }
            ],
            MaxTokens = 500,
            Temperature = 0.3
        };

        var response = await http.PostAsJsonAsync(Endpoint, request, AiJsonContext.Default.ChatRequest);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(AiJsonContext.Default.ChatResponse);
        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
    }

    private static string TruncateDiff(string diff)
    {
        if (diff.Length <= MaxDiffLength)
            return diff;
        return diff[..MaxDiffLength] + "\n... (diff truncated)";
    }

    // JSON models for GitHub Models API (OpenAI-compatible)

    internal sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    internal sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    internal sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    internal sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}

using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Glyph.Services;

public static class UpdateChecker
{
    private const string NuGetIndexUrl = "https://api.nuget.org/v3-flatcontainer/glyph/index.json";
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".glyph");
    private static readonly string CacheFile = Path.Combine(CacheDir, "update-check");

    public static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Strip any +metadata suffix (e.g. "0.1.0+abc123")
        if (version != null)
        {
            var plusIndex = version.IndexOf('+');
            if (plusIndex >= 0)
                version = version[..plusIndex];
        }

        return version ?? "0.0.0";
    }

    public static async Task<string?> GetLatestVersionAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Glyph-UpdateChecker/1.0");

        var response = await http.GetFromJsonAsync<NuGetVersionIndex>(NuGetIndexUrl);
        if (response?.Versions is not { Count: > 0 })
            return null;

        // Versions are listed oldest to newest; take the last non-prerelease version
        for (var i = response.Versions.Count - 1; i >= 0; i--)
        {
            var v = response.Versions[i];
            if (!v.Contains('-'))
                return v;
        }

        return response.Versions[^1];
    }

    public static bool IsNewerVersion(string latest, string current)
    {
        return Version.TryParse(NormalizeVersion(latest), out var latestVer)
            && Version.TryParse(NormalizeVersion(current), out var currentVer)
            && latestVer > currentVer;
    }

    /// <summary>
    /// Checks for updates at most once per day. Returns a message if an update is
    /// available, or null if the version is current (or the check was skipped/failed).
    /// </summary>
    public static async Task<string?> CheckForUpdateAsync()
    {
        try
        {
            if (!ShouldCheck())
                return null;

            var latest = await GetLatestVersionAsync();
            WriteCacheTimestamp();

            if (latest == null)
                return null;

            var current = GetCurrentVersion();
            if (IsNewerVersion(latest, current))
                return $"[yellow]A new version of Glyph is available: {latest} (current: {current}). Run [bold]glyph update[/] to update.[/]";

            return null;
        }
        catch
        {
            // Never let update checks break the main flow
            return null;
        }
    }

    private static bool ShouldCheck()
    {
        if (!File.Exists(CacheFile))
            return true;

        var lastCheck = File.GetLastWriteTimeUtc(CacheFile);
        return DateTime.UtcNow - lastCheck > TimeSpan.FromHours(24);
    }

    private static void WriteCacheTimestamp()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(CacheFile, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Ignore cache write failures
        }
    }

    private static string NormalizeVersion(string version)
    {
        // Ensure we have at least major.minor for Version.TryParse
        var parts = version.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0",
            _ => version
        };
    }

    private sealed class NuGetVersionIndex
    {
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = [];
    }
}

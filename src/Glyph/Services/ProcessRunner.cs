using System.Diagnostics;

namespace Glyph.Services;

public static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string command, string arguments, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
                psi.Environment[key] = value;
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {command}");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output.Trim(), error.Trim());
    }
}

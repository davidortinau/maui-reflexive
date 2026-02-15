#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MauiReflexive.Services;

/// <summary>
/// Orchestrates build → stage → hot-swap of the running app via platform-specific scripts.
/// Copilot calls this service as a tool after making code changes.
/// </summary>
public class RelaunchService
{
    public event Action<string>? OnOutput;
    public event Action<bool, string>? OnComplete;

    /// <summary>
    /// Execute the platform-appropriate relaunch script.
    /// Returns (success, output).
    /// </summary>
    public async Task<(bool success, string output)> RelaunchAsync(string projectDirectory)
    {
        var scriptName = GetPlatformScript();
        var scriptPath = Path.Combine(projectDirectory, "Scripts", scriptName);

        if (!File.Exists(scriptPath))
            return (false, $"Relaunch script not found: {scriptPath}");

        // Make script executable on Unix
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await RunCommandAsync("chmod", $"+x \"{scriptPath}\"", projectDirectory);
        }

        var (exitCode, output) = await RunCommandAsync(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "bash",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"-ExecutionPolicy Bypass -File \"{scriptPath}\"" : $"\"{scriptPath}\"",
            projectDirectory
        );

        var success = exitCode == 0;
        OnComplete?.Invoke(success, output);
        return (success, output);
    }

    private static string GetPlatformScript()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "relaunch.ps1";

        // On macOS, check if we're running as Mac Catalyst or targeting iOS/Android
        if (OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS())
            return "relaunch.sh";

        if (OperatingSystem.IsIOS())
            return "relaunch-ios.sh";

        if (OperatingSystem.IsAndroid())
            return "relaunch-android.sh";

        return "relaunch.sh";
    }

    private async Task<(int exitCode, string output)> RunCommandAsync(
        string command, string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return (-1, "Failed to start process");

        var outputBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                OnOutput?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                OnOutput?.Invoke(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString());
    }
}
#endif

using System.Diagnostics;
using MauiReflexive.Models;

namespace MauiReflexive.Services;

/// <summary>
/// Runs first-launch initialization: checks for required tools and configures the environment.
/// </summary>
public class CopilotInitService
{
    private readonly List<InitStep> _steps = new();
    private bool _initialized;

    public event Action<IReadOnlyList<InitStep>>? OnStepsUpdated;
    public bool IsInitialized => _initialized;
    public IReadOnlyList<InitStep> Steps => _steps;

    /// <summary>
    /// Run the full init sequence. Safe to call multiple times — skips if already done.
    /// </summary>
    public async Task<bool> RunInitAsync()
    {
        if (_initialized) return true;

        _steps.Clear();
        var allPassed = true;

        // Step 1: Check dotnet CLI
        allPassed &= await RunStepAsync("dotnet-cli", ".NET CLI", "Checking for dotnet CLI...",
            async () =>
            {
                var (exitCode, output) = await RunCommandAsync("dotnet", "--version");
                return exitCode == 0
                    ? (InitStepStatus.Success, $"dotnet {output.Trim()}")
                    : (InitStepStatus.Failed, "dotnet CLI not found. Install .NET SDK from https://dot.net");
            });

        // Step 2: Restore dotnet tools (maui-devflow CLI)
        allPassed &= await RunStepAsync("maui-devflow", "maui-devflow CLI", "Restoring dotnet tools...",
            async () =>
            {
                var (exitCode, _) = await RunCommandAsync("dotnet", "tool restore");
                if (exitCode != 0)
                    return (InitStepStatus.Failed, "Failed to restore tools. Run 'dotnet tool restore' manually.");

                var (checkCode, output) = await RunCommandAsync("dotnet", "maui-devflow --version");
                return checkCode == 0
                    ? (InitStepStatus.Success, $"maui-devflow {output.Trim()}")
                    : (InitStepStatus.Warning, "Restored tools but maui-devflow not responding.");
            });

        // Step 3: Check for DevTunnel CLI (optional)
        await RunStepAsync("devtunnel", "DevTunnel CLI", "Checking for DevTunnel CLI...",
            async () =>
            {
                var (exitCode, output) = await RunCommandAsync("devtunnel", "--version");
                return exitCode == 0
                    ? (InitStepStatus.Success, $"devtunnel {output.Trim()}")
                    : (InitStepStatus.Warning, "DevTunnel CLI not found. Optional — needed for mobile remote access. Install: brew install --cask devtunnel (macOS) or winget install devtunnel (Windows)");
            });

        // Step 4: Check GitHub Copilot auth
        allPassed &= await RunStepAsync("copilot-auth", "Copilot Auth", "Checking Copilot authentication...",
            async () =>
            {
                // The SDK handles auth automatically; this is a connectivity check
                await Task.CompletedTask;
                return (InitStepStatus.Success, "Will be verified when session starts.");
            });

        _initialized = allPassed;
        return allPassed;
    }

    private async Task<bool> RunStepAsync(string id, string name, string description,
        Func<Task<(InitStepStatus status, string detail)>> action)
    {
        var step = new InitStep(name, description, InitStepStatus.Running);
        _steps.Add(step);
        OnStepsUpdated?.Invoke(_steps);

        try
        {
            var (status, detail) = await action();
            _steps[_steps.Count - 1] = step with { Status = status, Detail = detail };
            OnStepsUpdated?.Invoke(_steps);
            return status != InitStepStatus.Failed;
        }
        catch (Exception ex)
        {
            _steps[_steps.Count - 1] = step with { Status = InitStepStatus.Failed, Detail = ex.Message };
            OnStepsUpdated?.Invoke(_steps);
            return false;
        }
    }

    private static async Task<(int exitCode, string output)> RunCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (-1, "Failed to start process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, string.IsNullOrEmpty(output) ? error : output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}


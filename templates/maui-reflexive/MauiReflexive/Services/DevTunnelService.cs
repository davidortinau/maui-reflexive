#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MauiReflexive.Services;

/// <summary>
/// Manages Azure DevTunnel lifecycle for remote mobile access. DEBUG-only.
/// </summary>
public class DevTunnelService : IDisposable
{
    private Process? _tunnelProcess;

    public event Action<TunnelState>? OnStateChanged;
    public event Action<string>? OnLog;

    public TunnelState State { get; private set; } = TunnelState.NotStarted;
    public string? TunnelUrl { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<bool> StartAsync(int port = 4322)
    {
        var devtunnelPath = FindDevTunnelCli();
        if (devtunnelPath == null)
        {
            SetState(TunnelState.Error);
            OnLog?.Invoke("DevTunnel CLI not found. Install it first.");
            return false;
        }

        SetState(TunnelState.Starting);

        try
        {
            var psi = new ProcessStartInfo(devtunnelPath, $"host -p {port} --allow-anonymous")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _tunnelProcess = Process.Start(psi);
            if (_tunnelProcess == null)
            {
                SetState(TunnelState.Error);
                return false;
            }

            // Read output asynchronously to find tunnel URL
            var urlFound = new TaskCompletionSource<bool>();
            _tunnelProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                OnLog?.Invoke(e.Data);

                // Parse tunnel URL from output
                var urlMatch = Regex.Match(e.Data, @"https://[\w-]+\.[\w.]+devtunnels\.ms");
                if (urlMatch.Success && TunnelUrl == null)
                {
                    TunnelUrl = urlMatch.Value;
                    SetState(TunnelState.Running);
                    urlFound.TrySetResult(true);
                }
            };
            _tunnelProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    OnLog?.Invoke($"[stderr] {e.Data}");
            };

            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            // Wait up to 30 seconds for URL
            var timeoutTask = Task.Delay(30000);
            var completed = await Task.WhenAny(urlFound.Task, timeoutTask);

            if (completed == timeoutTask)
            {
                OnLog?.Invoke("Timeout waiting for tunnel URL.");
                SetState(TunnelState.Error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Failed to start tunnel: {ex.Message}");
            SetState(TunnelState.Error);
            return false;
        }
    }

    public void Stop()
    {
        if (_tunnelProcess != null && !_tunnelProcess.HasExited)
        {
            try { _tunnelProcess.Kill(entireProcessTree: true); }
            catch { /* ignore */ }
        }

        _tunnelProcess = null;
        TunnelUrl = null;
        AccessToken = null;
        SetState(TunnelState.NotStarted);
    }

    private void SetState(TunnelState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);
    }

    private static string? FindDevTunnelCli()
    {
        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "DevTunnels", "devtunnel.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".devtunnels", "bin", "devtunnel.exe"),
            }
            : new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "bin", "devtunnel"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "devtunnel"),
                "/usr/local/bin/devtunnel",
                "/opt/homebrew/bin/devtunnel",
            };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Try PATH
        try
        {
            var psi = new ProcessStartInfo("devtunnel", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(5000);
                if (p.ExitCode == 0) return "devtunnel";
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        Stop();
    }
}

public enum TunnelState
{
    NotStarted,
    Authenticating,
    Starting,
    Running,
    Stopping,
    Error
}
#endif

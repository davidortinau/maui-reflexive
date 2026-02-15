using System.Collections.Concurrent;
using System.Text;
using MauiReflexive.Models;
#if DEBUG
using GitHub.Copilot.SDK;
#endif

namespace MauiReflexive.Services;

/// <summary>
/// Single-session Copilot SDK wrapper. Active in DEBUG builds only.
/// In RELEASE builds, all methods are no-ops.
/// </summary>
public class CopilotService : IDisposable
{
#if DEBUG
    private CopilotClient? _client;
    private CopilotSession? _session;
    private IDisposable? _eventSubscription;
    private SynchronizationContext? _syncContext;
    private TaskCompletionSource<string>? _responseCompletion;
    private readonly StringBuilder _currentResponse = new();
    private string? _workingDirectory;
#endif
    private readonly List<ChatMessage> _history = new();

    // Events for UI binding
    public event Action<string>? OnContentDelta;
    public event Action<string>? OnContentComplete;
    public event Action<string>? OnToolStarted;
    public event Action<string, string?>? OnToolCompleted;
    public event Action<string>? OnIntentChanged;
    public event Action? OnTurnStart;
    public event Action? OnTurnEnd;
    public event Action<string>? OnError;
    public event Action? OnStateChanged;

#if DEBUG
    public bool IsConnected => _client != null;
    public bool IsSessionActive => _session != null;
    public string? LastSessionId { get; private set; }
#else
    public bool IsConnected => false;
    public bool IsSessionActive => false;
    public string? LastSessionId => null;
#endif
    public bool IsBusy { get; private set; }
    public string? CurrentIntent { get; private set; }
    public IReadOnlyList<ChatMessage> History => _history;

#if DEBUG
    /// <summary>
    /// Initialize the Copilot SDK client.
    /// </summary>
    public async Task InitializeAsync(string? workingDirectory = null)
    {
        _syncContext = SynchronizationContext.Current;

        var options = new CopilotClientOptions
        {
            AutoStart = true,
            AutoRestart = true,
            UseStdio = true
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            options.Cwd = workingDirectory;
            _workingDirectory = workingDirectory;
        }

        // Resolve CLI path — SDK may not find it automatically on Mac Catalyst
        var cliPath = ResolveCopilotCliPath();
        if (cliPath != null)
        {
            options.CliPath = cliPath;
        }

        _client = new CopilotClient(options);
        await _client.StartAsync();

        InvokeOnUI(() => OnStateChanged?.Invoke());
    }

    /// <summary>
    /// Create a new Copilot session with the given system prompt.
    /// </summary>
    public async Task CreateSessionAsync(string systemPrompt, string model = "claude-sonnet-4")
    {
        if (_client == null)
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync first.");

        if (_session != null)
        {
            _eventSubscription?.Dispose();
            await _session.DisposeAsync();
        }

        var config = new SessionConfig
        {
            Model = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemPrompt
            },
            OnPermissionRequest = HandlePermissionRequest
        };

        if (!string.IsNullOrEmpty(_workingDirectory))
        {
            config.WorkingDirectory = _workingDirectory;
        }

        _session = await _client.CreateSessionAsync(config);
        _eventSubscription = _session.On(HandleSessionEvent);
        LastSessionId = _session.SessionId;

        InvokeOnUI(() => OnStateChanged?.Invoke());
    }

    /// <summary>
    /// Resume a previous Copilot session by ID.
    /// </summary>
    public async Task ResumeSessionAsync(string sessionId, string? model = null)
    {
        if (_client == null)
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync first.");

        if (_session != null)
        {
            _eventSubscription?.Dispose();
            await _session.DisposeAsync();
        }

        var resumeConfig = new ResumeSessionConfig
        {
            Model = model ?? "claude-sonnet-4"
        };

        if (!string.IsNullOrEmpty(_workingDirectory))
        {
            resumeConfig.WorkingDirectory = _workingDirectory;
        }

        _session = await _client.ResumeSessionAsync(sessionId, resumeConfig);
        _eventSubscription = _session.On(HandleSessionEvent);
        LastSessionId = _session.SessionId;

        InvokeOnUI(() => OnStateChanged?.Invoke());
    }

    /// <summary>
    /// Send a user message and wait for the assistant to finish responding.
    /// </summary>
    public async Task<string> SendPromptAsync(string prompt)
    {
        if (_session == null)
            throw new InvalidOperationException("No active session. Call CreateSessionAsync first.");

        _history.Add(ChatMessage.User(prompt));
        _currentResponse.Clear();
        _responseCompletion = new TaskCompletionSource<string>();
        IsBusy = true;

        InvokeOnUI(() =>
        {
            OnTurnStart?.Invoke();
            OnStateChanged?.Invoke();
        });

        try
        {
            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            // Wait for SessionIdleEvent to signal the turn is complete
            await _responseCompletion.Task;
        }
        finally
        {
            IsBusy = false;
            InvokeOnUI(() =>
            {
                OnTurnEnd?.Invoke();
                OnStateChanged?.Invoke();
            });
        }

        return "";
    }

    private Task<PermissionRequestResult> HandlePermissionRequest(
        PermissionRequest request, PermissionInvocation invocation)
    {
        return Task.FromResult(new PermissionRequestResult { Kind = "approved" });
    }

    private void HandleSessionEvent(SessionEvent sessionEvent)
    {
        switch (sessionEvent)
        {
            case AssistantMessageDeltaEvent delta:
                var deltaContent = delta.Data.DeltaContent;
                _currentResponse.Append(deltaContent);
                InvokeOnUI(() => OnContentDelta?.Invoke(deltaContent));
                break;

            case AssistantMessageEvent message:
                // Each AssistantMessageEvent carries the complete text for one message segment.
                // Use the event's own content (not the accumulated buffer) to avoid duplicates.
                var msgContent = message.Data.Content;
                if (!string.IsNullOrEmpty(msgContent))
                {
                    InvokeOnUI(() => OnContentComplete?.Invoke(msgContent));
                }
                // Clear buffer for the next message segment in this turn
                _currentResponse.Clear();
                break;

            case ToolExecutionStartEvent toolStart:
                var toolName = toolStart.Data.ToolName ?? "unknown";
                InvokeOnUI(() => OnToolStarted?.Invoke(toolName));
                break;

            case ToolExecutionCompleteEvent toolComplete:
                InvokeOnUI(() => OnToolCompleted?.Invoke("tool", null));
                break;

            case AssistantIntentEvent intent:
                CurrentIntent = intent.Data.Intent;
                InvokeOnUI(() => OnIntentChanged?.Invoke(intent.Data.Intent ?? ""));
                break;

            case SessionIdleEvent:
                // Turn complete — resolve with last segment or empty
                var response = _currentResponse.ToString();
                _currentResponse.Clear();
                _responseCompletion?.TrySetResult(response);
                break;

            case SessionErrorEvent error:
                var errorMsg = error.Data.Message ?? "Unknown error";
                InvokeOnUI(() => OnError?.Invoke(errorMsg));
                _responseCompletion?.TrySetException(new Exception(errorMsg));
                break;
        }
    }

    private void InvokeOnUI(Action action)
    {
        if (_syncContext != null)
            _syncContext.Post(_ => action(), null);
        else
            action();
    }

    /// <summary>
    /// Resolves the copilot CLI path. Checks bundled binary first, then system paths.
    /// </summary>
    private static string? ResolveCopilotCliPath()
    {
        // 1. SDK bundled path: runtimes/{rid}/native/copilot
        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(CopilotClient).Assembly.Location);
            if (assemblyDir != null)
            {
                var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                var bundledPath = Path.Combine(assemblyDir, "runtimes", rid, "native", "copilot");
                if (File.Exists(bundledPath))
                    return bundledPath;

                // 2. MAUI Mac Catalyst flattens runtimes/ into MonoBundle — check same dir
                var monoBundlePath = Path.Combine(assemblyDir, "copilot");
                if (File.Exists(monoBundlePath))
                    return monoBundlePath;
            }
        }
        catch { }

        // 3. System-installed CLI (homebrew, npm, PATH)
        var systemPaths = new[]
        {
            "/opt/homebrew/lib/node_modules/@github/copilot/node_modules/@github/copilot-darwin-arm64/copilot",
            "/usr/local/lib/node_modules/@github/copilot/node_modules/@github/copilot-darwin-arm64/copilot",
            "/usr/local/bin/copilot",
        };
        foreach (var path in systemPaths)
        {
            if (File.Exists(path)) return path;
        }

        // 4. Search PATH
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            foreach (var dir in pathEnv.Split(separator))
            {
                var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "copilot.exe" : "copilot");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        _eventSubscription?.Dispose();
        _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _client?.Dispose();
    }
#else
    public Task InitializeAsync(string? workingDirectory = null) => Task.CompletedTask;
    public Task CreateSessionAsync(string systemPrompt, string model = "claude-sonnet-4") => Task.CompletedTask;
    public Task ResumeSessionAsync(string sessionId, string? model = null) => Task.CompletedTask;
    public Task<string> SendPromptAsync(string prompt) => Task.FromResult(string.Empty);
    public void Dispose() { }
#endif
}

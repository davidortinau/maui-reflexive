#if DEBUG
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MauiReflexive.Models;

namespace MauiReflexive.Services;

/// <summary>
/// WebSocket server for remote state sync (desktop side). DEBUG-only.
/// Mobile clients connect via DevTunnel to receive session state and send prompts.
/// </summary>
public class WsBridgeServer : IDisposable
{
    private HttpListener? _listener;
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private CancellationTokenSource? _cts;

    public const int DefaultPort = 4322;
    public bool IsRunning => _listener?.IsListening == true;
    public int ConnectedClients => _clients.Count;

    public event Action<string>? OnLog;
    public event Action<SendPromptMessage>? OnRemotePrompt;

    public async Task StartAsync(int port = DefaultPort)
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        try
        {
            _listener.Prefixes.Add($"http://+:{port}/");
            _listener.Start();
        }
        catch
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
        }

        OnLog?.Invoke($"WebSocket bridge listening on port {port}");

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        _ = HandleClientAsync(wsContext.WebSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Accept error: {ex.Message}");
                }
            }
        });
    }

    public async Task BroadcastStateAsync(CopilotService copilotService)
    {
        var msg = new SessionStateMessage
        {
            IsConnected = copilotService.IsConnected,
            IsSessionActive = copilotService.IsSessionActive,
            IsBusy = copilotService.IsBusy,
            CurrentIntent = copilotService.CurrentIntent
        };

        await BroadcastAsync(msg);
    }

    public async Task BroadcastChatDeltaAsync(string delta)
    {
        var msg = new ChatMessageBridge { Role = "assistant", Content = delta, IsDelta = true };
        await BroadcastAsync(msg);
    }

    private async Task BroadcastAsync(BridgeMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var deadClients = new List<string>();

        foreach (var (id, ws) in _clients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                else
                    deadClients.Add(id);
            }
            catch
            {
                deadClients.Add(id);
            }
        }

        foreach (var id in deadClients)
            _clients.TryRemove(id, out _);
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var clientId = Guid.NewGuid().ToString();
        _clients[clientId] = ws;
        OnLog?.Invoke($"Client connected: {clientId}");

        var buffer = new byte[4096];

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var msg = JsonSerializer.Deserialize<BridgeMessage>(json);
                    if (msg is SendPromptMessage prompt)
                        OnRemotePrompt?.Invoke(prompt);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Parse error: {ex.Message}");
                }
            }
        }
        catch (WebSocketException) { }
        finally
        {
            _clients.TryRemove(clientId, out _);
            OnLog?.Invoke($"Client disconnected: {clientId}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;

        foreach (var (_, ws) in _clients)
        {
            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None).Wait(1000); }
            catch { }
        }
        _clients.Clear();
    }

    public void Dispose()
    {
        Stop();
    }
}
#endif

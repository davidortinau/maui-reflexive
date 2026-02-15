#if DEBUG
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MauiReflexive.Models;

namespace MauiReflexive.Services;

/// <summary>
/// WebSocket client for remote access (mobile side). DEBUG-only.
/// Connects to a desktop WsBridgeServer via DevTunnel URL.
/// </summary>
public class WsBridgeClient : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    public event Action<SessionStateMessage>? OnStateReceived;
    public event Action<ChatMessageBridge>? OnChatReceived;
    public event Action<ToolEventMessage>? OnToolEvent;
    public event Action<string>? OnLog;
    public event Action<bool>? OnConnectionChanged;

    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public string? ServerUrl { get; private set; }

    public async Task ConnectAsync(string url, string? accessToken = null)
    {
        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();

        // Force HTTP/1.1 for DevTunnel compatibility
        _ws.Options.HttpVersion = new Version(1, 1);
        _ws.Options.HttpVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact;

        if (!string.IsNullOrEmpty(accessToken))
            _ws.Options.SetRequestHeader("X-Tunnel-Authorization", $"tunnel {accessToken}");

        var wsUrl = url.Replace("https://", "wss://").Replace("http://", "ws://");
        if (!wsUrl.EndsWith("/")) wsUrl += "/";

        try
        {
            await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
            ServerUrl = url;
            OnConnectionChanged?.Invoke(true);
            OnLog?.Invoke($"Connected to {url}");

            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Connection failed: {ex.Message}");
            OnConnectionChanged?.Invoke(false);
        }
    }

    public async Task SendPromptAsync(string prompt)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var msg = new SendPromptMessage { Prompt = prompt };
        var json = JsonSerializer.Serialize<BridgeMessage>(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task DisconnectAsync()
    {
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            catch { }
        }

        _cts?.Cancel();
        OnConnectionChanged?.Invoke(false);
        ServerUrl = null;
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];

        try
        {
            while (_ws?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var msg = JsonSerializer.Deserialize<BridgeMessage>(json);
                    switch (msg)
                    {
                        case SessionStateMessage state:
                            OnStateReceived?.Invoke(state);
                            break;
                        case ChatMessageBridge chat:
                            OnChatReceived?.Invoke(chat);
                            break;
                        case ToolEventMessage tool:
                            OnToolEvent?.Invoke(tool);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Parse error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            OnConnectionChanged?.Invoke(false);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }
}
#endif

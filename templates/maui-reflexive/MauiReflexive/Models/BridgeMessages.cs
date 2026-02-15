#if DEBUG
using System.Text.Json.Serialization;

namespace MauiReflexive.Models;

[JsonDerivedType(typeof(SessionStateMessage), "sessionState")]
[JsonDerivedType(typeof(ChatMessageBridge), "chatMessage")]
[JsonDerivedType(typeof(SendPromptMessage), "sendPrompt")]
[JsonDerivedType(typeof(ToolEventMessage), "toolEvent")]
public abstract class BridgeMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public class SessionStateMessage : BridgeMessage
{
    public override string Type => "sessionState";
    public bool IsConnected { get; set; }
    public bool IsSessionActive { get; set; }
    public bool IsBusy { get; set; }
    public string? CurrentIntent { get; set; }
}

public class ChatMessageBridge : BridgeMessage
{
    public override string Type => "chatMessage";
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsDelta { get; set; }
}

public class SendPromptMessage : BridgeMessage
{
    public override string Type => "sendPrompt";
    public string Prompt { get; set; } = "";
}

public class ToolEventMessage : BridgeMessage
{
    public override string Type => "toolEvent";
    public string ToolName { get; set; } = "";
    public bool IsStart { get; set; }
    public string? Result { get; set; }
}
#endif

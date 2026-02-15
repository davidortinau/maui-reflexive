namespace MauiReflexive.Models;

public record ChatMessage(
    string Role,
    string Content,
    DateTime Timestamp
)
{
    public static ChatMessage User(string content) =>
        new("user", content, DateTime.Now);

    public static ChatMessage Assistant(string content) =>
        new("assistant", content, DateTime.Now);

    public static ChatMessage System(string content) =>
        new("system", content, DateTime.Now);
}

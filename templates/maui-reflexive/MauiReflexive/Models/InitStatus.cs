namespace MauiReflexive.Models;

public record InitStep(
    string Name,
    string Description,
    InitStepStatus Status,
    string? Detail = null
);

public enum InitStepStatus
{
    Pending,
    Running,
    Success,
    Warning,
    Failed
}

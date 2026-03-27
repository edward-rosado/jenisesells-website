namespace RealEstateStar.Workers.Shared.Context;

public record ErrorEntry(
    int Attempt,
    DateTime Timestamp,
    string StepName,
    string Message,
    string? StackTrace);

namespace Taleweaver.Api.Common;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
}

/// <summary>Expected business failure. <see cref="Code"/> is stable (e.g. "Scenarios.NotFound").</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}

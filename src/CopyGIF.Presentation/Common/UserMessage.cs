namespace CopyGIF.Presentation.Common;

public enum UserMessageSeverity
{
    Information,
    Success,
    Warning,
    Error
}

public sealed record UserMessage
{
    public required string Text { get; init; }

    public UserMessageSeverity Severity { get; init; } =
        UserMessageSeverity.Information;

    public string? Code { get; init; }

    public bool IsError =>
        Severity == UserMessageSeverity.Error;

    public bool IsWarning =>
        Severity == UserMessageSeverity.Warning;

    public static UserMessage Information(
        string text,
        string? code = null)
    {
        return Create(
            text,
            UserMessageSeverity.Information,
            code);
    }

    public static UserMessage Success(
        string text,
        string? code = null)
    {
        return Create(
            text,
            UserMessageSeverity.Success,
            code);
    }

    public static UserMessage Warning(
        string text,
        string? code = null)
    {
        return Create(
            text,
            UserMessageSeverity.Warning,
            code);
    }

    public static UserMessage Error(
        string text,
        string? code = null)
    {
        return Create(
            text,
            UserMessageSeverity.Error,
            code);
    }

    private static UserMessage Create(
        string text,
        UserMessageSeverity severity,
        string? code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            text);

        return new UserMessage
        {
            Text =
                text.Trim(),

            Severity =
                severity,

            Code =
                NormalizeCode(
                    code)
        };
    }

    private static string? NormalizeCode(
        string? code)
    {
        return string.IsNullOrWhiteSpace(
                code)
            ? null
            : code.Trim();
    }
}

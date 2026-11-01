namespace KitaplikEditoru.Core;

public enum EpubIssueSeverity
{
    Error,
    Warning,
    Information
}

public sealed record EpubValidationIssue(
    EpubIssueSeverity Severity,
    string Path,
    string Message);

using KitaplikEditoru.Localization;

namespace KitaplikEditoru.Core;

public enum IssueSeverity
{
    Error,
    Warning,
    Information
}

public sealed record Issue(
    string RuleId,
    IssueSeverity Level,
    string File,
    int? Line,
    int? Column,
    string Message,
    IFix? Fix)
{
    public string LevelText => Strings.Get($"Validation.{Level}");
}

public sealed record FixApplicability(bool CanApply, string? Reason = null);

public interface IBookCheck
{
    string Id { get; }
    ValueTask<IReadOnlyList<Issue>> RunAsync(EpubBook book, CancellationToken cancellationToken);
}

public interface IFix : IBookEditOperation
{
    FixApplicability GetApplicability(EpubBook book);
}

public sealed record BookCheckProgress(int Completed, int Total, string RuleId);

public sealed class BookCheckRunner
{
    private readonly IReadOnlyList<IBookCheck> _checks;

    public BookCheckRunner(IEnumerable<IBookCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        _checks = checks.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in _checks)
        {
            ArgumentNullException.ThrowIfNull(check);
            ArgumentException.ThrowIfNullOrWhiteSpace(check.Id);
            if (!ids.Add(check.Id))
            {
                throw new ArgumentException($"Duplicate book check ID: {check.Id}", nameof(checks));
            }
        }
    }

    public async Task<IReadOnlyList<Issue>> RunAsync(
        EpubBook book,
        IProgress<BookCheckProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        var issues = new List<Issue>();

        for (var index = 0; index < _checks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = _checks[index];
            var checkIssues = await Task.Run(
                async () => await check.RunAsync(book, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            if (checkIssues.Any(issue => !string.Equals(issue.RuleId, check.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Book check '{check.Id}' returned an issue with a different rule ID.");
            }

            issues.AddRange(checkIssues);
            progress?.Report(new BookCheckProgress(index + 1, _checks.Count, check.Id));
        }

        return issues;
    }
}

using KitaplikEditoru.Core;
using Xunit;

namespace EpubStudio.Tests;

public sealed class BookCheckRunnerTests
{
    [Fact]
    public async Task RunAsyncCollectsIssuesAndReportsProgressInCheckOrder()
    {
        var workingDirectory = CreateWorkingDirectory();
        using var book = CreateBook(workingDirectory);
        var checks = new[]
        {
            new StubCheck("metadata", new Issue("metadata", IssueSeverity.Warning, "content.opf", null, null, "Missing title.", null)),
            new StubCheck("xhtml", new Issue("xhtml", IssueSeverity.Error, "Text/chapter.xhtml", 4, 2, "Invalid XML.", null))
        };
        var progress = new InlineProgress<BookCheckProgress>();
        var runner = new BookCheckRunner(checks);

        var issues = await runner.RunAsync(book, progress);

        Assert.Equal(new[] { "metadata", "xhtml" }, issues.Select(issue => issue.RuleId));
        Assert.Equal(
            new[] { new BookCheckProgress(1, 2, "metadata"), new BookCheckProgress(2, 2, "xhtml") },
            progress.Values);
    }

    [Fact]
    public async Task RunAsyncHonorsCancellationBeforeRunningChecks()
    {
        var workingDirectory = CreateWorkingDirectory();
        using var book = CreateBook(workingDirectory);
        var check = new StubCheck("metadata");
        var runner = new BookCheckRunner([check]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(book, cancellationToken: cancellation.Token));

        Assert.False(check.WasRun);
    }

    [Fact]
    public async Task RunAsyncStopsBetweenChecksWhenProgressCancels()
    {
        var workingDirectory = CreateWorkingDirectory();
        using var book = CreateBook(workingDirectory);
        var firstCheck = new StubCheck("first");
        var secondCheck = new StubCheck("second");
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<BookCheckProgress>(update =>
        {
            if (update.Completed == 1)
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new BookCheckRunner([firstCheck, secondCheck]).RunAsync(
                book, progress, cancellation.Token));

        Assert.True(firstCheck.WasRun);
        Assert.False(secondCheck.WasRun);
        Assert.Single(progress.Values);
    }

    [Fact]
    public async Task RunAsyncRejectsIssuesWithAnotherCheckRuleId()
    {
        var workingDirectory = CreateWorkingDirectory();
        using var book = CreateBook(workingDirectory);
        var check = new StubCheck(
            "metadata",
            new Issue("xhtml", IssueSeverity.Error, "chapter.xhtml", null, null, "Wrong rule.", null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BookCheckRunner([check]).RunAsync(book));
    }

    [Fact]
    public void ConstructorRejectsDuplicateCheckIds()
    {
        var checks = new[] { new StubCheck("metadata"), new StubCheck("metadata") };

        Assert.Throws<ArgumentException>(() => new BookCheckRunner(checks));
    }

    private static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"book-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static EpubBook CreateBook(string workingDirectory) => new()
    {
        PackageFile = Path.Combine(workingDirectory, "book.epub"),
        WorkingDirectory = workingDirectory,
        PackageRelativePath = "OEBPS/content.opf",
        Title = "Test book"
    };

    private sealed class StubCheck(string id, params Issue[] issues) : IBookCheck
    {
        public string Id { get; } = id;
        public bool WasRun { get; private set; }

        public ValueTask<IReadOnlyList<Issue>> RunAsync(EpubBook book, CancellationToken cancellationToken)
        {
            WasRun = true;
            return ValueTask.FromResult<IReadOnlyList<Issue>>(issues);
        }
    }

    private sealed class InlineProgress<T>(Action<T>? onReport = null) : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
            onReport?.Invoke(value);
        }
    }
}

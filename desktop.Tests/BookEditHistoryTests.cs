using KitaplikEditoru.Core;
using Xunit;

namespace EpubStudio.Tests;

public sealed class BookEditHistoryTests
{
    [Fact]
    public async Task ExecuteUndoRedoAndNewEditUpdateHistory()
    {
        using var book = CreateBook();
        var history = book.EditHistory;
        var operation = new TitleOperation("Updated title");

        await history.ExecuteAsync(book, operation);
        Assert.Equal("Updated title", book.Title);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.True(await history.UndoAsync(book));
        Assert.Equal("Original title", book.Title);
        Assert.True(history.CanRedo);

        Assert.True(await history.RedoAsync(book));
        Assert.Equal("Updated title", book.Title);

        await history.UndoAsync(book);
        await history.ExecuteAsync(book, new TitleOperation("Another title"));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public async Task BatchIsUndoneAndRedoneAsOneOperation()
    {
        using var book = CreateBook();
        var history = book.EditHistory;
        var operations = new IBookEditOperation[]
        {
            new TitleOperation("First title"),
            new DirtyFlagOperation(true)
        };

        await history.ExecuteBatchAsync(book, "Apply fixes", operations);
        Assert.Equal("First title", book.Title);
        Assert.True(book.IsPackageDirty);

        Assert.True(await history.UndoAsync(book));
        Assert.Equal("Original title", book.Title);
        Assert.False(book.IsPackageDirty);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        Assert.True(await history.RedoAsync(book));
        Assert.Equal("First title", book.Title);
        Assert.True(book.IsPackageDirty);
    }

    [Fact]
    public async Task FailedBatchRollsBackCompletedOperationsWithoutAddingHistory()
    {
        using var book = CreateBook();
        var history = book.EditHistory;
        var operations = new IBookEditOperation[]
        {
            new TitleOperation("Temporary title"),
            new FailingOperation()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => history.ExecuteBatchAsync(book, "Failing fixes", operations));

        Assert.Equal("Original title", book.Title);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public async Task CancellingBatchReportsProgressAndRollsBackCompletedOperations()
    {
        using var book = CreateBook();
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation);
        var operations = new IBookEditOperation[]
        {
            new TitleOperation("Temporary title"),
            new DirtyFlagOperation(true)
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            book.EditHistory.ExecuteBatchAsync(
                book, "Apply fixes", operations, cancellation.Token, progress));

        Assert.Equal("Original title", book.Title);
        Assert.False(book.IsPackageDirty);
        Assert.False(book.EditHistory.CanUndo);
        Assert.Equal(0, progress.Values[0].Completed);
        Assert.Contains(progress.Values, update => update.Completed == 1);
    }

    private static EpubBook CreateBook()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"book-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        return new EpubBook
        {
            PackageFile = Path.Combine(workingDirectory, "book.epub"),
            WorkingDirectory = workingDirectory,
            PackageRelativePath = "OEBPS/content.opf",
            Title = "Original title"
        };
    }

    private sealed class TitleOperation(string value) : IBookEditOperation
    {
        private string? _originalValue;

        public string Description => "Change title";

        public Task ApplyAsync(EpubBook book, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _originalValue ??= book.Title;
            book.Title = value;
            return Task.CompletedTask;
        }

        public Task UndoAsync(EpubBook book, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            book.Title = _originalValue ?? throw new InvalidOperationException("The operation has not been applied.");
            return Task.CompletedTask;
        }
    }

    private sealed class DirtyFlagOperation(bool value) : IBookEditOperation
    {
        private bool _originalValue;
        private bool _captured;

        public string Description => "Change dirty state";

        public Task ApplyAsync(EpubBook book, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_captured)
            {
                _originalValue = book.IsPackageDirty;
                _captured = true;
            }
            book.IsPackageDirty = value;
            return Task.CompletedTask;
        }

        public Task UndoAsync(EpubBook book, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            book.IsPackageDirty = _captured
                ? _originalValue
                : throw new InvalidOperationException("The operation has not been applied.");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOperation : IBookEditOperation
    {
        public string Description => "Fail";

        public Task ApplyAsync(EpubBook book, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Expected test failure.");

        public Task UndoAsync(EpubBook book, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<BookEditProgress>
    {
        public List<BookEditProgress> Values { get; } = [];

        public void Report(BookEditProgress value)
        {
            Values.Add(value);
            if (value.Completed == 1)
                cancellation.Cancel();
        }
    }
}

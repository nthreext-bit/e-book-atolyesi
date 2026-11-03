namespace KitaplikEditoru.Core;

/// <summary>
/// An operation must leave the book unchanged if ApplyAsync fails and restore
/// the state before its latest successful application when UndoAsync succeeds.
/// </summary>
public interface IBookEditOperation
{
    string Description { get; }
    Task ApplyAsync(EpubBook book, CancellationToken cancellationToken);
    Task UndoAsync(EpubBook book, CancellationToken cancellationToken);
}

public sealed record BookEditProgress(int Completed, int Total, string Description);

public sealed class BookEditHistory
{
    private readonly Stack<IBookEditOperation> _undoStack = new();
    private readonly Stack<IBookEditOperation> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public async Task ExecuteAsync(
        EpubBook book,
        IBookEditOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(operation);

        await operation.ApplyAsync(book, cancellationToken);
        _undoStack.Push(operation);
        _redoStack.Clear();
    }

    public async Task ExecuteBatchAsync(
        EpubBook book,
        string description,
        IReadOnlyList<IBookEditOperation> operations,
        CancellationToken cancellationToken = default,
        IProgress<BookEditProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Any(operation => operation is null))
        {
            throw new ArgumentException("An edit batch cannot contain null operations.", nameof(operations));
        }

        var completed = new List<IBookEditOperation>(operations.Count);
        progress?.Report(new BookEditProgress(0, operations.Count, description));
        try
        {
            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await operation.ApplyAsync(book, cancellationToken);
                completed.Add(operation);
                progress?.Report(new BookEditProgress(completed.Count, operations.Count, operation.Description));
                await Task.Yield();
            }
        }
        catch (Exception applyException)
        {
            try
            {
                await UndoOperationsAsync(book, completed, CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "The edit batch failed and its completed operations could not be fully rolled back.",
                    applyException,
                    rollbackException);
            }

            throw;
        }

        if (completed.Count == 0)
        {
            return;
        }

        _undoStack.Push(new BatchOperation(description, completed));
        _redoStack.Clear();
    }

    public async Task<bool> UndoAsync(EpubBook book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_undoStack.TryPeek(out var operation))
        {
            return false;
        }

        await operation.UndoAsync(book, cancellationToken);
        _undoStack.Pop();
        _redoStack.Push(operation);
        return true;
    }

    public async Task<bool> RedoAsync(EpubBook book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_redoStack.TryPeek(out var operation))
        {
            return false;
        }

        await operation.ApplyAsync(book, cancellationToken);
        _redoStack.Pop();
        _undoStack.Push(operation);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private static async Task UndoOperationsAsync(
        EpubBook book,
        IReadOnlyList<IBookEditOperation> operations,
        CancellationToken cancellationToken)
    {
        for (var index = operations.Count - 1; index >= 0; index--)
        {
            await operations[index].UndoAsync(book, cancellationToken);
            await Task.Yield();
        }
    }

    private sealed class BatchOperation(
        string description,
        IReadOnlyList<IBookEditOperation> operations) : IBookEditOperation
    {
        public string Description { get; } = description;

        public async Task ApplyAsync(EpubBook book, CancellationToken cancellationToken)
        {
            var completed = new List<IBookEditOperation>(operations.Count);
            try
            {
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await operation.ApplyAsync(book, cancellationToken);
                    completed.Add(operation);
                    await Task.Yield();
                }
            }
            catch (Exception applyException)
            {
                try
                {
                    await UndoOperationsAsync(book, completed, CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "The edit batch failed and its completed operations could not be fully rolled back.",
                        applyException,
                        rollbackException);
                }

                throw;
            }
        }

        public async Task UndoAsync(EpubBook book, CancellationToken cancellationToken)
        {
            var undone = new List<IBookEditOperation>(operations.Count);
            try
            {
                for (var index = operations.Count - 1; index >= 0; index--)
                {
                    await operations[index].UndoAsync(book, cancellationToken);
                    undone.Add(operations[index]);
                }
            }
            catch (Exception undoException)
            {
                try
                {
                    for (var index = undone.Count - 1; index >= 0; index--)
                    {
                        await undone[index].ApplyAsync(book, CancellationToken.None);
                    }
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "The edit batch could not be undone or restored to its prior state.",
                        undoException,
                        rollbackException);
                }

                throw;
            }
        }
    }
}

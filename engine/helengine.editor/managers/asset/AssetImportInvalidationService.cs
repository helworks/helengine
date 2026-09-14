namespace helengine.editor;

/// <summary>
/// Tracks source changes that require a later import execution operation.
/// </summary>
public sealed class AssetImportInvalidationService : IDisposable {
    readonly HashSet<string> InvalidatedSourcePaths = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource CancellationSource = new();
    bool IsDisposed;

    /// <summary>
    /// Raised when a source path is newly marked for import invalidation.
    /// </summary>
    public event Action<string> Invalidated;

    /// <summary>
    /// Gets a token that is cancelled when the invalidation owner is disposed.
    /// </summary>
    public CancellationToken CancellationToken => CancellationSource.Token;

    /// <summary>
    /// Marks one source path as requiring import execution.
    /// </summary>
    /// <param name="sourcePath">Source path that changed.</param>
    /// <returns>True when the path was newly added.</returns>
    public bool Invalidate(string sourcePath) {
        if (string.IsNullOrWhiteSpace(sourcePath)) {
            throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
        }

        string normalizedPath = Path.GetFullPath(sourcePath);
        if (!InvalidatedSourcePaths.Add(normalizedPath)) {
            return false;
        }

        Invalidated?.Invoke(normalizedPath);
        return true;
    }

    /// <summary>
    /// Takes the current invalidation batch in deterministic path order.
    /// </summary>
    /// <returns>Paths that were invalidated since the previous drain.</returns>
    public IReadOnlyList<string> Drain() {
        string[] paths = InvalidatedSourcePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        InvalidatedSourcePaths.Clear();
        return paths;
    }

    /// <summary>
    /// Cancels outstanding import work and releases the invalidation owner.
    /// </summary>
    public void Dispose() {
        if (IsDisposed) {
            return;
        }

        CancellationSource.Cancel();
        CancellationSource.Dispose();
        InvalidatedSourcePaths.Clear();
        Invalidated = null;
        IsDisposed = true;
    }
}

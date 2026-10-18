namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play cook step that throws the supplied exception, standing in for a cook that fails with an
/// unexpected error on the preparation thread.
/// </summary>
public sealed class ThrowingPlayCookRunner : IEditorPlayCookRunner {
    /// <summary>
    /// Initializes the fake with the exception every cook throws.
    /// </summary>
    /// <param name="failure">Exception thrown by the cook.</param>
    public ThrowingPlayCookRunner(Exception failure) {
        Failure = failure ?? throw new ArgumentNullException(nameof(failure));
    }

    /// <summary>
    /// Gets the exception every cook throws.
    /// </summary>
    public Exception Failure { get; }

    /// <summary>
    /// Throws <see cref="Failure"/>.
    /// </summary>
    /// <param name="platformId">Platform the session service asked to cook.</param>
    /// <returns>Never returns.</returns>
    public EditorPlayCookResult Cook(string platformId, EditorPlaySettingsSnapshot playSettings = null) {
        throw Failure;
    }
}

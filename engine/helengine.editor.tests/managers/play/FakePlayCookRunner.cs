namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play cook step that returns a scripted result and records every platform it was asked to cook.
/// </summary>
public sealed class FakePlayCookRunner : IEditorPlayCookRunner {
    /// <summary>
    /// Platform identifiers passed to <see cref="Cook"/>, in call order.
    /// </summary>
    public readonly List<string> CookedPlatformIds = new List<string>();

    /// <summary>
    /// Initializes the fake with the result every cook call returns.
    /// </summary>
    /// <param name="result">Cook outcome returned to the session service.</param>
    public FakePlayCookRunner(EditorPlayCookResult result) {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Gets the cook outcome returned by every call.
    /// </summary>
    public EditorPlayCookResult Result { get; }

    /// <summary>
    /// Records the requested platform and returns the scripted cook outcome.
    /// </summary>
    /// <param name="platformId">Platform the session service asked to cook.</param>
    /// <returns>The scripted cook outcome.</returns>
    public EditorPlayCookResult Cook(string platformId, EditorPlaySettingsSnapshot playSettings = null) {
        CookedPlatformIds.Add(platformId);
        return Result;
    }
}

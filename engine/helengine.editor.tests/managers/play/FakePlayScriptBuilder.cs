namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play script build step that returns a scripted result and records every platform it built for.
/// </summary>
public sealed class FakePlayScriptBuilder : IEditorPlayScriptBuilder {
    /// <summary>
    /// Platform identifiers passed to <see cref="Build"/>, in call order.
    /// </summary>
    public readonly List<string> BuiltPlatformIds = new List<string>();

    /// <summary>
    /// Initializes the fake with the result and runtime module root every build call returns.
    /// </summary>
    /// <param name="result">Build outcome returned to the session service.</param>
    /// <param name="runtimeModulesRootPath">Runtime module root reported through the out parameter.</param>
    public FakePlayScriptBuilder(EditorBuildExecutionResult result, string runtimeModulesRootPath) {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        RuntimeModulesRootPath = runtimeModulesRootPath ?? throw new ArgumentNullException(nameof(runtimeModulesRootPath));
    }

    /// <summary>
    /// Gets the build outcome returned by every call.
    /// </summary>
    public EditorBuildExecutionResult Result { get; }

    /// <summary>
    /// Gets the runtime module root reported through the out parameter.
    /// </summary>
    public string RuntimeModulesRootPath { get; }

    /// <summary>
    /// Records the requested platform and returns the scripted build outcome and module root.
    /// </summary>
    /// <param name="platformId">Platform the session service asked to build scripts for.</param>
    /// <param name="runtimeModulesRootPath">Receives the scripted runtime module root.</param>
    /// <returns>The scripted build outcome.</returns>
    public EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath) {
        BuiltPlatformIds.Add(platformId);
        runtimeModulesRootPath = RuntimeModulesRootPath;
        return Result;
    }
}

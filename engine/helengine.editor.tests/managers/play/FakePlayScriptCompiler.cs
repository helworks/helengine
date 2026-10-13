namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play script compiler: records every compile request and, on success, lays out one
/// <c>&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c> per configured module the way the real build publishes them, without
/// running <c>dotnet build</c>.
/// </summary>
public sealed class FakePlayScriptCompiler : IEditorPlayScriptCompiler {
    /// <summary>
    /// Environment identifiers passed to <see cref="Build"/>, in call order; the count is the number of compiles.
    /// </summary>
    public readonly List<string> CompiledEnvironmentIds = new List<string>();

    /// <summary>
    /// Root directory standing in for the project's Play compiler-output root.
    /// </summary>
    readonly string OutputRootPath;

    /// <summary>
    /// Module identifiers whose assemblies a successful compile writes.
    /// </summary>
    readonly IReadOnlyList<string> ModuleIds;

    /// <summary>
    /// Initializes the fake compiler.
    /// </summary>
    /// <param name="outputRootPath">Directory standing in for the Play compiler-output root.</param>
    /// <param name="moduleIds">Module identifiers whose assemblies a successful compile writes.</param>
    public FakePlayScriptCompiler(string outputRootPath, IReadOnlyList<string> moduleIds) {
        OutputRootPath = outputRootPath ?? throw new ArgumentNullException(nameof(outputRootPath));
        ModuleIds = moduleIds ?? throw new ArgumentNullException(nameof(moduleIds));
        NextResultSucceeds = true;
    }

    /// <summary>
    /// Gets or sets whether the next compile succeeds; a failing compile writes no assemblies.
    /// </summary>
    public bool NextResultSucceeds { get; set; }

    /// <summary>
    /// Returns the fixed Play output root.
    /// </summary>
    /// <param name="platformId">Platform identifier; ignored.</param>
    /// <returns>The configured output root.</returns>
    public string ResolvePlayOutputRootPath(string platformId) {
        return OutputRootPath;
    }

    /// <summary>
    /// Returns the runtime module root beneath the Play output root.
    /// </summary>
    /// <param name="platformId">Platform identifier; ignored.</param>
    /// <returns>The module root inside the output root.</returns>
    public string ResolveRuntimeModulesRootPath(string platformId) {
        return Path.Combine(OutputRootPath, "modules");
    }

    /// <summary>
    /// Records the compile and, when configured to succeed, writes one placeholder assembly per module.
    /// </summary>
    /// <param name="platformId">Platform identifier; ignored.</param>
    /// <param name="environmentId">Environment identifier recorded for the assertion.</param>
    /// <param name="runtimeModulesRootPath">Receives the module root.</param>
    /// <returns>Success or failure according to <see cref="NextResultSucceeds"/>.</returns>
    public EditorBuildExecutionResult Build(string platformId, string environmentId, out string runtimeModulesRootPath) {
        CompiledEnvironmentIds.Add(environmentId);
        runtimeModulesRootPath = ResolveRuntimeModulesRootPath(platformId);
        if (!NextResultSucceeds) {
            return EditorBuildExecutionResult.Failure("compile failed");
        }

        for (int index = 0; index < ModuleIds.Count; index++) {
            string moduleDirectoryPath = Path.Combine(runtimeModulesRootPath, ModuleIds[index]);
            Directory.CreateDirectory(moduleDirectoryPath);
            File.WriteAllText(Path.Combine(moduleDirectoryPath, ModuleIds[index] + ".dll"), "assembly");
        }

        return EditorBuildExecutionResult.Success("compiled");
    }
}

using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that the Play script builder only recompiles when a script input changed: sources, module manifests,
/// gameplay symbols or the referenced engine assemblies. Scene edits must never trigger a script rebuild.
/// </summary>
public sealed class EditorPlayScriptBuilderTests : IDisposable {
    /// <summary>
    /// Platform identifier used for every build.
    /// </summary>
    const string PlatformId = "ps2";

    /// <summary>
    /// Module identifier declared by the test project's code module manifest.
    /// </summary>
    const string ModuleId = "game";

    /// <summary>
    /// Scratch root holding the test project and its Play output, deleted on dispose.
    /// </summary>
    readonly string ScratchRootPath;

    /// <summary>
    /// Authored test project root.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Path of the single gameplay source file of the test project.
    /// </summary>
    readonly string SourceFilePath;

    /// <summary>
    /// Play compiler-output root used by the fake compiler.
    /// </summary>
    readonly string OutputRootPath;

    /// <summary>
    /// Environment identifier returned by the builder's environment resolver.
    /// </summary>
    string EnvironmentId;

    /// <summary>
    /// Creates a scratch project with one runtime code module holding one script.
    /// </summary>
    public EditorPlayScriptBuilderTests() {
        ScratchRootPath = Path.Combine(@"C:\dev\helworks\builds\helengine\play-tests", Guid.NewGuid().ToString("N"));
        ProjectRootPath = Path.Combine(ScratchRootPath, "project");
        string moduleDirectoryPath = Path.Combine(ProjectRootPath, "assets", ModuleId);
        Directory.CreateDirectory(moduleDirectoryPath);
        File.WriteAllText(Path.Combine(moduleDirectoryPath, "code.module.json"), "{ \"moduleId\": \"" + ModuleId + "\" }");
        SourceFilePath = Path.Combine(moduleDirectoryPath, "Mover.cs");
        File.WriteAllText(SourceFilePath, "public class Mover { }");
        OutputRootPath = Path.Combine(ProjectRootPath, "cache", "generated_code", "play", PlatformId);
        EnvironmentId = "release";
    }

    /// <summary>
    /// Deletes the scratch project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ScratchRootPath)) {
            Directory.Delete(ScratchRootPath, true);
        }
    }

    /// <summary>
    /// Ensures a second Play with no script changes reuses the built modules without compiling again and reports the compile as skipped, while the first build reports a compile.
    /// </summary>
    [Fact]
    public void Build_WhenInputsUnchanged_DoesNotCompileAgain() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);

        EditorBuildExecutionResult first = builder.Build(PlatformId, out string firstRoot, out bool firstSkipped);
        EditorBuildExecutionResult second = builder.Build(PlatformId, out string secondRoot, out bool secondSkipped);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.False(firstSkipped);
        Assert.True(secondSkipped);
        Assert.Single(compiler.CompiledEnvironmentIds);
        Assert.Equal(firstRoot, secondRoot);
        Assert.True(File.Exists(Path.Combine(secondRoot, ModuleId, ModuleId + ".dll")));
    }

    /// <summary>
    /// Ensures editing a script source invalidates the stored fingerprint and compiles again.
    /// </summary>
    [Fact]
    public void Build_WhenSourceChanges_CompilesAgain() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        builder.Build(PlatformId, out _, out _);

        File.WriteAllText(SourceFilePath, "public class Mover { public int Speed; }");
        builder.Build(PlatformId, out _, out bool compileSkipped);

        Assert.Equal(2, compiler.CompiledEnvironmentIds.Count);
        Assert.False(compileSkipped);
    }

    /// <summary>
    /// Ensures a change to the gameplay symbol set, here through the selected environment, compiles again.
    /// </summary>
    [Fact]
    public void Build_WhenGameplaySymbolsChange_CompilesAgain() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        builder.Build(PlatformId, out _, out _);

        EnvironmentId = "debug";
        builder.Build(PlatformId, out _, out _);

        Assert.Equal(new[] { "release", "debug" }, compiler.CompiledEnvironmentIds);
    }

    /// <summary>
    /// Ensures a failed compile stores no fingerprint, so the next Play compiles again.
    /// </summary>
    [Fact]
    public void Build_WhenCompileFails_DoesNotStoreFingerprint() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        compiler.NextResultSucceeds = false;

        EditorBuildExecutionResult failed = builder.Build(PlatformId, out _, out _);

        Assert.False(failed.Succeeded);
        Assert.False(File.Exists(Path.Combine(OutputRootPath, EditorPlayScriptBuilder.FingerprintFileName)));

        compiler.NextResultSucceeds = true;
        EditorBuildExecutionResult retried = builder.Build(PlatformId, out _, out _);

        Assert.True(retried.Succeeded);
        Assert.Equal(2, compiler.CompiledEnvironmentIds.Count);
    }

    /// <summary>
    /// Ensures a stale build deletes the module folders of modules the project no longer declares, so their assemblies
    /// cannot be loaded by the player, while keeping the rest of the output root (such as MSBuild intermediates) so the
    /// compile can stay incremental.
    /// </summary>
    [Fact]
    public void Build_WhenStale_DeletesRemovedModuleFoldersAndKeepsIncrementalState() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        builder.Build(PlatformId, out string modulesRoot, out _);
        string removedModuleDirectoryPath = Path.Combine(modulesRoot, "removed");
        Directory.CreateDirectory(removedModuleDirectoryPath);
        File.WriteAllText(Path.Combine(removedModuleDirectoryPath, "removed.dll"), "stale");
        string intermediateFilePath = Path.Combine(OutputRootPath, "generated_code", "obj", ModuleId, "project.assets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(intermediateFilePath));
        File.WriteAllText(intermediateFilePath, "{}");

        File.WriteAllText(SourceFilePath, "public class Mover { public int Health; }");
        builder.Build(PlatformId, out _, out _);

        Assert.Equal(2, compiler.CompiledEnvironmentIds.Count);
        Assert.False(Directory.Exists(removedModuleDirectoryPath));
        Assert.True(File.Exists(Path.Combine(modulesRoot, ModuleId, ModuleId + ".dll")));
        Assert.True(File.Exists(intermediateFilePath));
    }

    /// <summary>
    /// Ensures a stale build discards the stored fingerprint before compiling: an in-place compile that fails may leave
    /// some module assemblies rebuilt from the new sources, so reverting the sources afterwards must not match the old
    /// fingerprint and skip over those half-updated outputs.
    /// </summary>
    [Fact]
    public void Build_WhenStaleCompileFails_DiscardsPreviousFingerprint() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        builder.Build(PlatformId, out _, out _);

        File.WriteAllText(SourceFilePath, "public class Mover { public int Health; }");
        compiler.NextResultSucceeds = false;
        EditorBuildExecutionResult failed = builder.Build(PlatformId, out _, out _);

        Assert.False(failed.Succeeded);
        Assert.False(File.Exists(Path.Combine(OutputRootPath, EditorPlayScriptBuilder.FingerprintFileName)));
    }

    /// <summary>
    /// Ensures rebuilding the engine without changing it, which rewrites its assemblies with identical bytes and a new
    /// timestamp, does not rebuild the Play scripts.
    /// </summary>
    [Fact]
    public void Build_WhenEngineAssemblyTimestampChangesWithoutContentChange_DoesNotCompileAgain() {
        string engineAssemblyPath = WriteEngineAssembly(new byte[] { 1, 2, 3, 4 });
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler, engineAssemblyPath);
        builder.Build(PlatformId, out _, out _);

        File.SetLastWriteTimeUtc(engineAssemblyPath, File.GetLastWriteTimeUtc(engineAssemblyPath).AddMinutes(5));
        EditorBuildExecutionResult second = builder.Build(PlatformId, out _, out _);

        Assert.True(second.Succeeded);
        Assert.Single(compiler.CompiledEnvironmentIds);
    }

    /// <summary>
    /// Ensures an engine assembly whose bytes changed, even with the same length and timestamp, rebuilds the Play scripts.
    /// </summary>
    [Fact]
    public void Build_WhenEngineAssemblyContentChanges_CompilesAgain() {
        string engineAssemblyPath = WriteEngineAssembly(new byte[] { 1, 2, 3, 4 });
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler, engineAssemblyPath);
        builder.Build(PlatformId, out _, out _);

        DateTime originalWriteTimeUtc = File.GetLastWriteTimeUtc(engineAssemblyPath);
        File.WriteAllBytes(engineAssemblyPath, new byte[] { 4, 3, 2, 1 });
        File.SetLastWriteTimeUtc(engineAssemblyPath, originalWriteTimeUtc);
        builder.Build(PlatformId, out _, out _);

        Assert.Equal(2, compiler.CompiledEnvironmentIds.Count);
    }

    /// <summary>
    /// Ensures a missing module assembly forces a compile even when the fingerprint still matches.
    /// </summary>
    [Fact]
    public void Build_WhenModuleAssemblyIsMissing_CompilesAgain() {
        FakePlayScriptCompiler compiler = CreateCompiler();
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);
        builder.Build(PlatformId, out string modulesRoot, out _);

        File.Delete(Path.Combine(modulesRoot, ModuleId, ModuleId + ".dll"));
        builder.Build(PlatformId, out _, out _);

        Assert.Equal(2, compiler.CompiledEnvironmentIds.Count);
    }

    /// <summary>
    /// Ensures a successful build always leaves the module root on disk, even when no module assembly was produced.
    /// </summary>
    [Fact]
    public void Build_WhenCompileProducesNoModules_StillCreatesModuleRoot() {
        FakePlayScriptCompiler compiler = new FakePlayScriptCompiler(OutputRootPath, Array.Empty<string>());
        File.Delete(Path.Combine(ProjectRootPath, "assets", ModuleId, "code.module.json"));
        File.Delete(SourceFilePath);
        EditorPlayScriptBuilder builder = CreateBuilder(compiler);

        EditorBuildExecutionResult result = builder.Build(PlatformId, out string modulesRoot, out _);
        builder.Build(PlatformId, out _, out _);

        Assert.True(result.Succeeded);
        Assert.True(Directory.Exists(modulesRoot));
        Assert.Single(compiler.CompiledEnvironmentIds);
    }

    /// <summary>
    /// Creates the fake compiler that publishes the test project's single module.
    /// </summary>
    /// <returns>Fake compiler bound to the test output root.</returns>
    FakePlayScriptCompiler CreateCompiler() {
        return new FakePlayScriptCompiler(OutputRootPath, new[] { ModuleId });
    }

    /// <summary>
    /// Creates the builder under test with an environment resolver reading <see cref="EnvironmentId"/>.
    /// </summary>
    /// <param name="compiler">Compiler seam used instead of <c>dotnet build</c>.</param>
    /// <returns>Builder under test.</returns>
    EditorPlayScriptBuilder CreateBuilder(FakePlayScriptCompiler compiler) {
        return new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => EnvironmentId);
    }

    /// <summary>
    /// Creates the builder under test fingerprinting one scratch engine assembly instead of the real engine assemblies.
    /// </summary>
    /// <param name="compiler">Compiler seam used instead of <c>dotnet build</c>.</param>
    /// <param name="engineAssemblyPath">Scratch file standing in for an engine assembly the modules reference.</param>
    /// <returns>Builder under test.</returns>
    EditorPlayScriptBuilder CreateBuilder(FakePlayScriptCompiler compiler, string engineAssemblyPath) {
        return new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => EnvironmentId, new[] { engineAssemblyPath });
    }

    /// <summary>
    /// Writes a scratch stand-in for an engine assembly outside the project.
    /// </summary>
    /// <param name="contents">Bytes of the stand-in assembly.</param>
    /// <returns>Absolute path of the written file.</returns>
    string WriteEngineAssembly(byte[] contents) {
        string engineDirectoryPath = Path.Combine(ScratchRootPath, "engine");
        Directory.CreateDirectory(engineDirectoryPath);
        string engineAssemblyPath = Path.Combine(engineDirectoryPath, "helengine.core.dll");
        File.WriteAllBytes(engineAssemblyPath, contents);
        return engineAssemblyPath;
    }
}

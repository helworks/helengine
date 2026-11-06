using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies project code modules can compile against optional engine plugin assemblies declared in
/// <c>code.module.json</c> through <c>engineModules</c>, and only for platforms that transpile the plugin.
/// </summary>
public sealed class CodeModuleEngineModuleReferenceTests : IDisposable {
    /// <summary>Engine plugin project name the tests declare.</summary>
    const string AnimationRuntimeModuleId = "helengine.animation.runtime";

    /// <summary>Script that only compiles when the animation runtime plugin is referenced.</summary>
    const string PlayerScript = """
        using helengine.animation.runtime;

        namespace Caixinha {
            public sealed class ScrubDriver {
                public void Scrub(HierarchyAnimationPlayerComponent player, double seconds) {
                    player.Seek(seconds);
                }
            }
        }
        """;

    /// <summary>Temporary project root owned by one test.</summary>
    readonly string ProjectRootPath;

    /// <summary>Generated module output root beneath the temporary project.</summary>
    readonly string OutputRootPath;

    /// <summary>
    /// Creates a temporary project whose <c>Caixinha</c> module script uses the animation player.
    /// </summary>
    public CodeModuleEngineModuleReferenceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-engine-module-tests", Guid.NewGuid().ToString("N"));
        OutputRootPath = Path.Combine(ProjectRootPath, "Build", "code");
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "codebase", "Caixinha"));
        Directory.CreateDirectory(OutputRootPath);
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "codebase", "Caixinha", "ScrubDriver.cs"), PlayerScript);
    }

    /// <summary>
    /// Deletes the temporary project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>A module that declares the animation runtime compiles its player script for a platform that transpiles the plugin.</summary>
    [Fact]
    public void ModuleDeclaringEngineModule_compilesAgainstPluginTypes() {
        WriteModuleManifest("""
            {
              "moduleId": "Caixinha",
              "dependencyModuleIds": [],
              "loadScopes": [ "always-loaded" ],
              "moduleKind": "runtime",
              "engineModules": [ "helengine.animation.runtime" ]
            }
            """);
        EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(ProjectRootPath).Load();
        ManagedCompileCodegenToolRunner toolRunner = new();

        CompileForWebGpu(manifestDocument, toolRunner, [ResolvePluginProjectPath()]);

        Assert.Equal([AnimationRuntimeModuleId], Assert.Single(manifestDocument.Modules).EngineModules);
        Assert.Equal(0, Assert.Single(toolRunner.ExitCodes));
    }

    /// <summary>Without the declaration the module keeps compiling against core only, so the player type is missing.</summary>
    [Fact]
    public void UndeclaredEngineModule_failsToCompile() {
        WriteModuleManifest("""
            {
              "moduleId": "Caixinha",
              "dependencyModuleIds": [],
              "loadScopes": [ "always-loaded" ],
              "moduleKind": "runtime"
            }
            """);
        EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(ProjectRootPath).Load();
        ManagedCompileCodegenToolRunner toolRunner = new();

        CompileForWebGpu(manifestDocument, toolRunner, [ResolvePluginProjectPath()]);

        Assert.Empty(Assert.Single(manifestDocument.Modules).EngineModules);
        Assert.NotEqual(0, Assert.Single(toolRunner.ExitCodes));
        Assert.Contains("CS0246", toolRunner.Outputs[0], StringComparison.Ordinal);
    }

    /// <summary>A platform that does not transpile the plugin cannot ship the module, so the cook names module, plugin and platform.</summary>
    [Fact]
    public void EngineModuleNotTranspiledByPlatform_throwsNamingIt() {
        WriteModuleManifest("""
            {
              "moduleId": "Caixinha",
              "loadScopes": [ "always-loaded" ],
              "engineModules": [ "helengine.animation.runtime" ]
            }
            """);
        EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(ProjectRootPath).Load();
        ManagedCompileCodegenToolRunner toolRunner = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CompileForWebGpu(manifestDocument, toolRunner, []));

        Assert.Contains("Caixinha", exception.Message, StringComparison.Ordinal);
        Assert.Contains(AnimationRuntimeModuleId, exception.Message, StringComparison.Ordinal);
        Assert.Contains("webgpu", exception.Message, StringComparison.Ordinal);
        Assert.Empty(toolRunner.ExitCodes);
    }

    /// <summary>
    /// Writes the <c>Caixinha</c> module manifest.
    /// </summary>
    /// <param name="manifestJson">Manifest contents.</param>
    void WriteModuleManifest(string manifestJson) {
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "codebase", "Caixinha", "code.module.json"), manifestJson);
    }

    /// <summary>
    /// Returns a platform-plugin style project path for the animation runtime; only its file name identifies the plugin.
    /// </summary>
    /// <returns>Absolute plugin project path.</returns>
    string ResolvePluginProjectPath() {
        return Path.Combine(ProjectRootPath, "engine", AnimationRuntimeModuleId, AnimationRuntimeModuleId + ".csproj");
    }

    /// <summary>
    /// Cooks the <c>Caixinha</c> module for webgpu with the supplied generated-core project list.
    /// </summary>
    /// <param name="manifestDocument">Loaded project module manifest.</param>
    /// <param name="toolRunner">Runner that compiles the synthetic module project.</param>
    /// <param name="generatedCoreProjectPaths">Engine plugin projects the platform transpiles with core.</param>
    void CompileForWebGpu(
        EditorCodeModuleManifestDocument manifestDocument,
        ManagedCompileCodegenToolRunner toolRunner,
        IReadOnlyList<string> generatedCoreProjectPaths) {
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        service.CompileModules(
            manifestDocument,
            "webgpu",
            "webgpu-loose-files",
            "/fake/codegen.exe",
            new PlatformCodegenProfileDefinition(
                "webgpu-cpp",
                "WebGPU C++",
                "WebGPU C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            ["Caixinha"],
            new Dictionary<string, string>(),
            OutputRootPath,
            generatedCoreProjectPaths: generatedCoreProjectPaths);
    }
}

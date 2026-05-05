using helengine;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the generated native renderer-settings manifest writer emits C++ source for platform renderer defaults.
/// </summary>
public sealed class EditorRuntimeGraphicsRendererManifestWriterTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the renderer-manifest writer test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the test workspace.
    /// </summary>
    public EditorRuntimeGraphicsRendererManifestWriterTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-graphics-renderer-manifest-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Releases the temporary workspace used by the test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Ensures the writer emits native renderer-settings source files with the resolved platform defaults.
    /// </summary>
    [Fact]
    public void Write_emits_renderer_defaults_cpp_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-core");
        Directory.CreateDirectory(generatedCoreRootPath);
        RuntimeGraphicsRendererManifest manifest = new RuntimeGraphicsRendererManifest(
            DepthPrepassMode.Always,
            "ultra",
            true,
            PostProcessTier.High,
            Ps2DepthHandlerMode.Hardware);

        EditorRuntimeGraphicsRendererManifestWriter writer = new();
        writer.Write(generatedCoreRootPath, manifest);

        string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
        string headerPath = Path.Combine(runtimeRootPath, "runtime_graphics_renderer_manifest.hpp");
        string sourcePath = Path.Combine(runtimeRootPath, "runtime_graphics_renderer_manifest.cpp");

        Assert.True(File.Exists(headerPath));
        Assert.True(File.Exists(sourcePath));

        string source = File.ReadAllText(sourcePath);
        Assert.Contains("HERuntimeGraphicsRendererManifest", source);
        Assert.Contains("HERuntimeDepthPrepassMode::Always", source);
        Assert.Contains("\"ultra\"", source);
        Assert.Contains("true", source);
        Assert.Contains("HERuntimePostProcessTier::High", source);
        Assert.Contains("HERuntimePs2DepthHandlerMode::Hardware", source);
    }
}

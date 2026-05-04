using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime code-module residency manifest reader matches the editor-written JSON shape.
/// </summary>
public sealed class RuntimeCodeModuleManifestTests : IDisposable {
    /// <summary>
    /// Temporary root used by the current manifest test.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes the temporary test root.
    /// </summary>
    public RuntimeCodeModuleManifestTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-code-module-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    /// <summary>
    /// Deletes temporary manifest state after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the runtime manifest preserves module residency information and unload behavior.
    /// </summary>
    [Fact]
    public void ReadFromFile_parses_module_residency_shape() {
        string manifestPath = Path.Combine(TempRootPath, "runtime-code-modules.json");
        File.WriteAllText(
            manifestPath,
            """
            {
              "Entries": [
                {
                  "ModuleId": "gameplay",
                  "RuntimeSpecializationId": "windows-loose-files",
                  "LoadState": 0,
                  "DependencyModuleIds": []
                },
                {
                  "ModuleId": "ui",
                  "RuntimeSpecializationId": "windows-loose-files",
                  "LoadState": 1,
                  "DependencyModuleIds": ["gameplay"]
                },
                {
                  "ModuleId": "debug-tools",
                  "RuntimeSpecializationId": "windows-loose-files",
                  "LoadState": 2,
                  "DependencyModuleIds": ["gameplay"]
                }
              ]
            }
            """);

        RuntimeCodeModuleManifest manifest = RuntimeCodeModuleManifest.ReadFromFile(manifestPath);

        Assert.Equal("gameplay", manifest.Entries[0].ModuleId);
        Assert.False(manifest.CanUnloadModule("gameplay"));
        Assert.True(manifest.CanUnloadModule("ui"));
        Assert.True(manifest.CanUnloadModule("debug-tools"));
        Assert.Equal(new[] { "debug-tools" }, manifest.GetUnloadableModuleIds());
        Assert.Equal(new[] { "gameplay", "ui" }, manifest.GetResidentModuleIds());
    }
}

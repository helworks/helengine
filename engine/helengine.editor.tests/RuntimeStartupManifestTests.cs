using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime startup manifest reader matches the editor-written JSON shape.
/// </summary>
public sealed class RuntimeStartupManifestTests : IDisposable {
    readonly string TempRootPath;

    public RuntimeStartupManifestTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-startup-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    [Fact]
    public void ReadFromFile_parses_editor_startup_manifest_shape() {
        string manifestPath = Path.Combine(TempRootPath, "runtime-startup.json");
        File.WriteAllText(
            manifestPath,
            """
            {
              "StartupSceneId": "Scenes/MainMenu.helen",
              "StorageProfileId": {
                "Value": "windows-loose-files"
              }
            }
            """);

        RuntimeStartupManifest manifest = RuntimeStartupManifest.ReadFromFile(manifestPath);

        Assert.Equal("Scenes/MainMenu.helen", manifest.StartupSceneId);
        Assert.Equal("windows-loose-files", manifest.StorageProfileId.Value);
    }
}

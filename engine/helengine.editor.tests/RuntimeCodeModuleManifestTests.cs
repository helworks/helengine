using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime code-module manifest preserves residency behavior without relying on file parsing.
/// </summary>
public sealed class RuntimeCodeModuleManifestTests {
    /// <summary>
    /// Ensures the runtime manifest preserves module residency information and unload behavior.
    /// </summary>
    [Fact]
    public void Constructor_preserves_module_residency_shape() {
        RuntimeCodeModuleManifest manifest = new RuntimeCodeModuleManifest(
            [
                new RuntimeCodeModuleManifestEntry("gameplay", "windows-loose-files", RuntimeCodeModuleLoadState.ResidentAtStartup, []),
                new RuntimeCodeModuleManifestEntry("ui", "windows-loose-files", RuntimeCodeModuleLoadState.SceneResident, ["gameplay"]),
                new RuntimeCodeModuleManifestEntry("debug-tools", "windows-loose-files", RuntimeCodeModuleLoadState.Unloadable, ["gameplay"])
            ]);

        Assert.Equal("gameplay", manifest.Entries[0].ModuleId);
        Assert.False(manifest.CanUnloadModule("gameplay"));
        Assert.True(manifest.CanUnloadModule("ui"));
        Assert.True(manifest.CanUnloadModule("debug-tools"));
        Assert.Equal(new[] { "debug-tools" }, manifest.GetUnloadableModuleIds());
        Assert.Equal(new[] { "gameplay", "ui" }, manifest.GetResidentModuleIds());
    }
}

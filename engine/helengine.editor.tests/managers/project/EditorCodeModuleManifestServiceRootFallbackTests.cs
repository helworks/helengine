namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies scripts outside folder-scoped module manifests are assigned to the main gameplay module.
/// </summary>
public sealed class EditorCodeModuleManifestServiceRootFallbackTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated temporary project root for root-fallback manifest tests.
    /// </summary>
    public EditorCodeModuleManifestServiceRootFallbackTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-code-module-root-fallback-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui"));
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "PlayerController.cs"),
            "public sealed class PlayerController { }");
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui", "HudController.cs"),
            "public sealed class HudController { }");
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui", "code.module.json"),
            """
            {
              "moduleId": "ui",
              "dependencyModuleIds": [ "gameplay" ],
              "loadScopes": [ "scene-loaded" ]
            }
            """);
    }

    /// <summary>
    /// Deletes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the manifest service emits the root gameplay module when scripts exist outside any folder-scoped module boundary.
    /// </summary>
    [Fact]
    public void Load_when_scripts_exist_outside_manifests_emits_root_gameplay_module() {
        EditorCodeModuleManifestService service = new(ProjectRootPath);

        EditorCodeModuleManifestDocument document = service.Load();

        EditorCodeModuleManifestEntry gameplay = Assert.Single(document.Modules, module => module.ModuleId == "gameplay");
        Assert.Equal("assets", gameplay.FolderPath);
        Assert.Contains("assets/Scripts/Ui", gameplay.NestedModuleFolderPaths);
        Assert.Equal(new[] { "always-loaded" }, gameplay.LoadScopes);

        EditorCodeModuleManifestEntry ui = Assert.Single(document.Modules, module => module.ModuleId == "ui");
        Assert.Equal("assets/Scripts/Ui", ui.FolderPath);
        Assert.Equal(new[] { "gameplay" }, ui.DependencyModuleIds);
    }
}

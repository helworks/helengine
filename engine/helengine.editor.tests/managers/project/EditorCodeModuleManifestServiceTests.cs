namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies folder-scoped authored code-module manifests are discovered with nested boundaries.
/// </summary>
public sealed class EditorCodeModuleManifestServiceTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorCodeModuleManifestServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-code-module-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ProjectRootPath);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "ui"));
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "PlayerController.cs"),
            "public sealed class PlayerController { }");
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "ui", "HudController.cs"),
            "public sealed class HudController { }");
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "code.module.json"),
            """
            {
              "moduleId": "gameplay",
              "dependencyModuleIds": [],
              "loadScopes": [ "always-loaded" ]
            }
            """);
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "ui", "code.module.json"),
            """
            {
              "moduleId": "gameplay.ui",
              "dependencyModuleIds": [ "gameplay" ],
              "loadScopes": [ "scene-loaded" ]
            }
            """);
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void Load_discovers_nested_code_module_boundaries() {
        EditorCodeModuleManifestService service = new(ProjectRootPath);

        EditorCodeModuleManifestDocument document = service.Load();

        Assert.Contains(document.Modules, module => module.ModuleId == "gameplay");
        Assert.Contains(document.Modules, module => module.ModuleId == "gameplay.ui");

        EditorCodeModuleManifestEntry gameplay = Assert.Single(document.Modules, module => module.ModuleId == "gameplay");
        Assert.Equal("assets/Scripts/gameplay", gameplay.FolderPath);
        Assert.Contains("assets/Scripts/gameplay/ui", gameplay.NestedModuleFolderPaths);

        EditorCodeModuleManifestEntry gameplayUi = Assert.Single(document.Modules, module => module.ModuleId == "gameplay.ui");
        Assert.Equal("assets/Scripts/gameplay/ui", gameplayUi.FolderPath);
        Assert.Empty(gameplayUi.NestedModuleFolderPaths);
        Assert.Equal(new[] { "gameplay" }, gameplayUi.DependencyModuleIds);
        Assert.Equal(new[] { "scene-loaded" }, gameplayUi.LoadScopes);
    }
}

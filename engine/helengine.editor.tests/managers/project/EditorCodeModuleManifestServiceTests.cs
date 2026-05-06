namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies folder-scoped authored code-module manifests are discovered with nested boundaries.
/// </summary>
public sealed class EditorCodeModuleManifestServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated temporary project root for manifest service tests.
    /// </summary>
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

    /// <summary>
    /// Deletes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures nested folder-scoped manifests are discovered with their boundary metadata intact.
    /// </summary>
    [Fact]
    public void Load_discovers_nested_code_module_boundaries() {
        EditorCodeModuleManifestService service = new(ProjectRootPath);

        EditorCodeModuleManifestDocument document = service.Load();

        Assert.Contains(document.Modules, module => module.ModuleId == "gameplay");
        Assert.Contains(document.Modules, module => module.ModuleId == "gameplay.ui");

        EditorCodeModuleManifestEntry gameplay = Assert.Single(document.Modules, module => module.ModuleId == "gameplay");
        Assert.Equal("assets/Scripts/gameplay", gameplay.FolderPath);
        Assert.Contains("assets/Scripts/gameplay/ui", gameplay.NestedModuleFolderPaths);
        Assert.Equal(EditorCodeModuleKind.Runtime, gameplay.ModuleKind);

        EditorCodeModuleManifestEntry gameplayUi = Assert.Single(document.Modules, module => module.ModuleId == "gameplay.ui");
        Assert.Equal("assets/Scripts/gameplay/ui", gameplayUi.FolderPath);
        Assert.Empty(gameplayUi.NestedModuleFolderPaths);
        Assert.Equal(new[] { "gameplay" }, gameplayUi.DependencyModuleIds);
        Assert.Equal(new[] { "scene-loaded" }, gameplayUi.LoadScopes);
        Assert.Equal(EditorCodeModuleKind.Runtime, gameplayUi.ModuleKind);
    }

    /// <summary>
    /// Ensures manifests default the module kind to runtime when the authored JSON omits it.
    /// </summary>
    [Fact]
    public void Load_WhenModuleKindIsOmitted_DefaultsToRuntime() {
        WriteManifest(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "tools", "code.module.json"),
            """
            {
              "moduleId": "gameplay.tools",
              "dependencyModuleIds": [ "gameplay" ],
              "loadScopes": [ "always-loaded" ]
            }
            """);

        EditorCodeModuleManifestDocument document = new EditorCodeModuleManifestService(ProjectRootPath).Load();

        EditorCodeModuleManifestEntry module = Assert.Single(document.Modules, entry => entry.ModuleId == "gameplay.tools");
        Assert.Equal(EditorCodeModuleKind.Runtime, module.ModuleKind);
    }

    /// <summary>
    /// Ensures runtime modules cannot depend on editor-only modules.
    /// </summary>
    [Fact]
    public void Load_WhenRuntimeModuleDependsOnEditorModule_Throws() {
        WriteManifest(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "editor", "code.module.json"),
            """
            {
              "moduleId": "gameplay.editor",
              "dependencyModuleIds": [ "gameplay" ],
              "loadScopes": [ "always-loaded" ],
              "moduleKind": "editor"
            }
            """);
        WriteManifest(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "gameplay", "ui", "code.module.json"),
            """
            {
              "moduleId": "gameplay.ui",
              "dependencyModuleIds": [ "gameplay.editor" ],
              "loadScopes": [ "scene-loaded" ],
              "moduleKind": "runtime"
            }
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new EditorCodeModuleManifestService(ProjectRootPath).Load());

        Assert.Contains("gameplay.ui", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gameplay.editor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes one manifest file beneath the temporary project root.
    /// </summary>
    /// <param name="manifestPath">Absolute manifest file path to write.</param>
    /// <param name="contents">Manifest JSON contents.</param>
    void WriteManifest(string manifestPath, string contents) {
        string directoryPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(directoryPath)) {
            throw new InvalidOperationException("Manifest path must include a directory.");
        }

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(manifestPath, contents);
    }
}

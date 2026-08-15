using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies project-shared supported platforms persist inside `settings/platforms.json`.
/// </summary>
public sealed class EditorProjectPlatformsServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Creates one isolated temporary project root for the current test instance.
    /// </summary>
    public EditorProjectPlatformsServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-project-platforms-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
    }

    /// <summary>
    /// Deletes the temporary project root created for the current test instance.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempProjectRootPath)) {
            Directory.Delete(TempProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures project-supported platforms are loaded from `settings/platforms.json`.
    /// </summary>
    [Fact]
    public void Load_WhenPlatformsFileExists_ReturnsConfiguredSupportedPlatforms() {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(
            Path.Combine(settingsDirectoryPath, "platforms.json"),
            """
            {
              "supportedPlatforms": [ "windows", "ps2" ]
            }
            """);
        EditorProjectPlatformsService service = CreateService();

        EditorProjectPlatformsDocument document = service.Load();

        Assert.Equal(new[] { "windows", "ps2" }, document.SupportedPlatforms);
    }

    /// <summary>
    /// Ensures the service seeds the active editor platform when the project settings file is missing so a fresh project can open without crashing.
    /// </summary>
    [Fact]
    public void Load_WhenPlatformsFileIsMissing_CreatesDefaultDocumentWithActiveEditorPlatform() {
        EditorProjectPlatformsService service = CreateService();

        EditorProjectPlatformsDocument document = service.Load();

        Assert.Equal(new[] { EditorProjectPlatformsService.ActiveEditorPlatformId }, document.SupportedPlatforms);
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "settings", "platforms.json")));
    }

    /// <summary>
    /// Ensures the seeded default document persists to disk so later loads keep the active editor platform.
    /// </summary>
    [Fact]
    public void Load_WhenPlatformsFileIsMissing_PersistsSeededDefaultForLaterLoads() {
        EditorProjectPlatformsService service = CreateService();

        service.Load();
        EditorProjectPlatformsDocument reloaded = CreateService().Load();

        Assert.Equal(new[] { EditorProjectPlatformsService.ActiveEditorPlatformId }, reloaded.SupportedPlatforms);
    }

    /// <summary>
    /// Ensures an existing platforms file with an empty platform list self-heals to the active editor platform, since an
    /// empty list would prevent the editor from opening the project at all.
    /// </summary>
    [Fact]
    public void Load_WhenPlatformsFileHasEmptyPlatformList_SelfHealsToActiveEditorPlatform() {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(
            Path.Combine(settingsDirectoryPath, "platforms.json"),
            """
            {
              "supportedPlatforms": []
            }
            """);
        EditorProjectPlatformsService service = CreateService();

        EditorProjectPlatformsDocument document = service.Load();

        Assert.Equal(new[] { EditorProjectPlatformsService.ActiveEditorPlatformId }, document.SupportedPlatforms);
    }

    /// <summary>
    /// Ensures the self-healed platform list is persisted back to disk so later loads no longer see the broken empty list.
    /// </summary>
    [Fact]
    public void Load_AfterSavingEmptyDocument_SelfHealsAndPersistsActiveEditorPlatform() {
        EditorProjectPlatformsService service = CreateService();
        EditorProjectPlatformsDocument document = new EditorProjectPlatformsDocument {
            SupportedPlatforms = []
        };

        service.Save(document);
        service.Load();
        EditorProjectPlatformsDocument reloaded = CreateService().Load();

        Assert.Equal(new[] { EditorProjectPlatformsService.ActiveEditorPlatformId }, reloaded.SupportedPlatforms);
    }

    /// <summary>
    /// Creates one project-platforms service for the current temporary project root.
    /// </summary>
    /// <returns>Project-platforms service configured for the current test project.</returns>
    EditorProjectPlatformsService CreateService() {
        return new EditorProjectPlatformsService(TempProjectRootPath);
    }
}

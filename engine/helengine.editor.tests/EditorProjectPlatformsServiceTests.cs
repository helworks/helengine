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
    /// Ensures the service seeds one generic empty document when the project settings file is missing.
    /// </summary>
    [Fact]
    public void Load_WhenPlatformsFileIsMissing_CreatesDefaultEmptyDocument() {
        EditorProjectPlatformsService service = CreateService();

        EditorProjectPlatformsDocument document = service.Load();

        Assert.Empty(document.SupportedPlatforms);
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "settings", "platforms.json")));
    }

    /// <summary>
    /// Ensures the service persists empty supported-platform lists without injecting a preferred platform.
    /// </summary>
    [Fact]
    public void Save_WhenSupportedPlatformsIsEmpty_PersistsEmptyDocument() {
        EditorProjectPlatformsService service = CreateService();
        EditorProjectPlatformsDocument document = new EditorProjectPlatformsDocument {
            SupportedPlatforms = []
        };

        service.Save(document);

        EditorProjectPlatformsDocument reloaded = service.Load();
        Assert.Empty(reloaded.SupportedPlatforms);
    }

    /// <summary>
    /// Creates one project-platforms service for the current temporary project root.
    /// </summary>
    /// <returns>Project-platforms service configured for the current test project.</returns>
    EditorProjectPlatformsService CreateService() {
        return new EditorProjectPlatformsService(TempProjectRootPath);
    }
}

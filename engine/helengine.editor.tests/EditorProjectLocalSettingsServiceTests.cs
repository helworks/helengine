using System.Text.Json;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies editor-local project settings persist the active platform inside `settings/project.json`.
/// </summary>
public sealed class EditorProjectLocalSettingsServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Gets the supported platform list used by the current test instance.
    /// </summary>
    IReadOnlyList<string> SupportedPlatforms { get; }

    /// <summary>
    /// Creates one isolated project root for the current test instance.
    /// </summary>
    public EditorProjectLocalSettingsServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-project-local-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
        SupportedPlatforms = ["windows", "android"];
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
    /// Ensures the service returns one supported platform already persisted on disk.
    /// </summary>
    [Fact]
    public void LoadActivePlatform_WhenSettingsFileContainsSupportedPlatform_ReturnsStoredValue() {
        WriteSettingsFile(
            """
            {
              "activePlatform": "android"
            }
            """);
        EditorProjectLocalSettingsService service = CreateService();

        string activePlatform = service.LoadActivePlatform();

        Assert.Equal("android", activePlatform);
    }

    /// <summary>
    /// Ensures missing local settings default to the first supported platform and persist that choice.
    /// </summary>
    [Fact]
    public void LoadActivePlatform_WhenSettingsFileIsMissing_ReturnsFirstSupportedPlatformAndCreatesSettingsFile() {
        EditorProjectLocalSettingsService service = CreateService();

        string activePlatform = service.LoadActivePlatform();

        Assert.Equal("windows", activePlatform);
        Assert.Equal("windows", ReadActivePlatformFromDisk());
    }

    /// <summary>
    /// Ensures explicit platform changes rewrite the local settings file.
    /// </summary>
    [Fact]
    public void SaveActivePlatform_WhenSupportedPlatformIsChosen_WritesSelectionToDisk() {
        EditorProjectLocalSettingsService service = CreateService();

        service.SaveActivePlatform("android");

        Assert.Equal("android", ReadActivePlatformFromDisk());
    }

    /// <summary>
    /// Ensures unsupported stored platforms are silently replaced with the first supported platform.
    /// </summary>
    [Fact]
    public void LoadActivePlatform_WhenStoredPlatformIsUnsupported_ReplacesSettingsWithFirstSupportedPlatform() {
        WriteSettingsFile(
            """
            {
              "activePlatform": "ios"
            }
            """);
        EditorProjectLocalSettingsService service = CreateService();

        string activePlatform = service.LoadActivePlatform();

        Assert.Equal("windows", activePlatform);
        Assert.Equal("windows", ReadActivePlatformFromDisk());
    }

    /// <summary>
    /// Ensures malformed local settings are silently replaced with the current default platform.
    /// </summary>
    [Fact]
    public void LoadActivePlatform_WhenSettingsFileIsMalformed_ReplacesSettingsWithFirstSupportedPlatform() {
        WriteSettingsFile("{ invalid json");
        EditorProjectLocalSettingsService service = CreateService();

        string activePlatform = service.LoadActivePlatform();

        Assert.Equal("windows", activePlatform);
        Assert.Equal("windows", ReadActivePlatformFromDisk());
    }

    /// <summary>
    /// Creates the service under test for the current temporary project root.
    /// </summary>
    /// <returns>Project-local settings service configured for the current test project.</returns>
    EditorProjectLocalSettingsService CreateService() {
        return new EditorProjectLocalSettingsService(TempProjectRootPath, SupportedPlatforms);
    }

    /// <summary>
    /// Writes raw project-local settings JSON to the expected settings path.
    /// </summary>
    /// <param name="json">JSON payload to persist.</param>
    void WriteSettingsFile(string json) {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(Path.Combine(settingsDirectoryPath, "project.json"), json);
    }

    /// <summary>
    /// Reads the persisted active platform value from disk.
    /// </summary>
    /// <returns>Active platform value stored in the local settings file.</returns>
    string ReadActivePlatformFromDisk() {
        string settingsFilePath = Path.Combine(TempProjectRootPath, "settings", "project.json");
        string json = File.ReadAllText(settingsFilePath);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("activePlatform").GetString();
    }
}

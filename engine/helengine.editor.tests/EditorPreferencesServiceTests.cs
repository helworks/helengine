using System.Text.Json;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies editor-global UI scale preferences persist and recover correctly.
/// </summary>
public sealed class EditorPreferencesServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated preferences root used by the current test instance.
    /// </summary>
    string TempSettingsRootPath { get; }

    /// <summary>
    /// Creates one isolated preferences root for the current test instance.
    /// </summary>
    public EditorPreferencesServiceTests() {
        TempSettingsRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-preferences-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempSettingsRootPath);
    }

    /// <summary>
    /// Deletes the isolated preferences root created for the current test instance.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempSettingsRootPath)) {
            Directory.Delete(TempSettingsRootPath, true);
        }
    }

    /// <summary>
    /// Ensures missing preferences default to auto mode and seed one valid document on disk.
    /// </summary>
    [Fact]
    public void Load_WhenPreferencesFileIsMissing_ReturnsAutoAndCreatesDocument() {
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.UiScale.Mode);
        Assert.Equal(100, settings.UiScale.OverridePercent);
        Assert.Equal(EditorThemeCatalog.DefaultThemeId, settings.ThemeId);
        Assert.True(File.Exists(GetPreferencesFilePath()));
    }

    /// <summary>
    /// Ensures persisted override settings round-trip through the preferences service.
    /// </summary>
    [Fact]
    public void Load_WhenPreferencesFileContainsOverride_ReturnsStoredOverride() {
        File.WriteAllText(
            GetPreferencesFilePath(),
            """
            {
              "uiScaleMode": "Override",
              "uiScalePercent": 150,
              "themeId": "dark"
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Override, settings.UiScale.Mode);
        Assert.Equal(150, settings.UiScale.OverridePercent);
        Assert.Equal("dark", settings.ThemeId);
        Assert.Equal(1.5, settings.UiScale.ResolveEffectiveScale(192));
    }

    /// <summary>
    /// Ensures override mode always uses the explicit user-selected scale instead of monitor DPI.
    /// </summary>
    [Fact]
    public void ResolveEffectiveScale_WhenModeIsOverride_IgnoresMonitorDpi() {
        EditorUiScaleSettings settings = new EditorUiScaleSettings(EditorUiScaleMode.Override, 125);

        Assert.Equal(1.25, settings.ResolveEffectiveScale(96));
        Assert.Equal(1.25, settings.ResolveEffectiveScale(192));
    }

    /// <summary>
    /// Ensures malformed preference files are replaced with one valid default document.
    /// </summary>
    [Fact]
    public void Load_WhenPreferencesFileIsMalformed_RewritesDefaultDocument() {
        File.WriteAllText(GetPreferencesFilePath(), "{ invalid json");
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.UiScale.Mode);
        Assert.Equal(EditorUiScaleMode.Auto, ReadModeFromDisk());
        Assert.Equal(100, ReadPercentFromDisk());
        Assert.Equal(EditorThemeCatalog.DefaultThemeId, ReadThemeIdFromDisk());
    }

    /// <summary>
    /// Ensures unsupported stored percentages are rejected and replaced with the default document.
    /// </summary>
    [Fact]
    public void Load_WhenPreferencesFileContainsUnsupportedPercent_RewritesDefaultDocument() {
        File.WriteAllText(
            GetPreferencesFilePath(),
            """
            {
              "uiScaleMode": "Override",
              "uiScalePercent": 90,
              "themeId": "light"
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.UiScale.Mode);
        Assert.Equal(EditorUiScaleMode.Auto, ReadModeFromDisk());
        Assert.Equal(100, ReadPercentFromDisk());
        Assert.Equal(EditorThemeCatalog.DefaultThemeId, ReadThemeIdFromDisk());
    }

    /// <summary>
    /// Ensures persisted theme preferences round-trip through the editor preferences service.
    /// </summary>
    [Fact]
    public void Load_WhenPreferencesFileContainsThemeId_ReturnsStoredTheme() {
        File.WriteAllText(
            GetPreferencesFilePath(),
            """
            {
              "uiScaleMode": "Override",
              "uiScalePercent": 125,
              "themeId": "light"
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Override, settings.UiScale.Mode);
        Assert.Equal(125, settings.UiScale.OverridePercent);
        Assert.Equal("light", settings.ThemeId);
    }

    /// <summary>
    /// Ensures invalid persisted theme ids are replaced with the default persisted theme.
    /// </summary>
    [Fact]
    public void Load_WhenThemeIdIsInvalid_RewritesDefaultTheme() {
        File.WriteAllText(
            GetPreferencesFilePath(),
            """
            {
              "uiScaleMode": "Auto",
              "uiScalePercent": 100,
              "themeId": "missing-theme"
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorPreferencesSettings settings = service.Load();

        Assert.Equal(EditorThemeCatalog.DefaultThemeId, settings.ThemeId);
        Assert.Equal(EditorThemeCatalog.DefaultThemeId, ReadThemeIdFromDisk());
    }

    /// <summary>
    /// Ensures saving combined preferences persists both theme and scale fields.
    /// </summary>
    [Fact]
    public void Save_WhenCombinedPreferencesArePersisted_WritesThemeIdAndScaleSettings() {
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        service.Save(new EditorPreferencesSettings(
            new EditorUiScaleSettings(EditorUiScaleMode.Override, 125),
            "light"));

        Assert.Equal(EditorUiScaleMode.Override, ReadModeFromDisk());
        Assert.Equal(125, ReadPercentFromDisk());
        Assert.Equal("light", ReadThemeIdFromDisk());
    }

    /// <summary>
    /// Gets the absolute preferences file path used by the current test instance.
    /// </summary>
    /// <returns>Absolute path to the preferences JSON file.</returns>
    string GetPreferencesFilePath() {
        return Path.Combine(TempSettingsRootPath, "preferences.json");
    }

    /// <summary>
    /// Reads the persisted UI scale mode from the preferences file on disk.
    /// </summary>
    /// <returns>UI scale mode persisted in the preferences document.</returns>
    EditorUiScaleMode ReadModeFromDisk() {
        string json = File.ReadAllText(GetPreferencesFilePath());
        using JsonDocument document = JsonDocument.Parse(json);
        string mode = document.RootElement.GetProperty("uiScaleMode").GetString();
        return Enum.Parse<EditorUiScaleMode>(mode, true);
    }

    /// <summary>
    /// Reads the persisted UI scale percentage from the preferences file on disk.
    /// </summary>
    /// <returns>UI scale percentage persisted in the preferences document.</returns>
    int ReadPercentFromDisk() {
        string json = File.ReadAllText(GetPreferencesFilePath());
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("uiScalePercent").GetInt32();
    }

    /// <summary>
    /// Reads the persisted theme identifier from the preferences file on disk.
    /// </summary>
    /// <returns>Theme identifier persisted in the preferences document.</returns>
    string ReadThemeIdFromDisk() {
        string json = File.ReadAllText(GetPreferencesFilePath());
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("themeId").GetString();
    }
}

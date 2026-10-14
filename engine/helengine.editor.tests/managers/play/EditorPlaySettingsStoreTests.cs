using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the per-user Play settings store in <c>user_settings/play.json</c>.
/// </summary>
public sealed class EditorPlaySettingsStoreTests : IDisposable {
    /// <summary>Workspace-owned scratch project root.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-store", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the scratch project root.</summary>
    public EditorPlaySettingsStoreTests() {
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>A missing file loads as an empty document and is not created.</summary>
    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyDocumentWithoutWriting() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);

        EditorPlaySettingsDocument document = store.Load();

        Assert.Empty(document.CommonValues);
        Assert.Empty(document.PlatformValues);
        Assert.False(File.Exists(store.FilePath));
        Assert.Equal(Path.Combine(ProjectRoot, "user_settings", "play.json"), store.FilePath);
    }

    /// <summary>Common values, booleans and every platform section (including ones for other platforms) round-trip.</summary>
    [Fact]
    public void SaveThenLoad_RoundTripsAllSections() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "2x";
        document.CommonValues["wait-for-debugger"] = "true";
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "project" };
        document.PlatformValues["ds"] = new Dictionary<string, string> { ["screen-gap"] = "8" };

        store.Save(document);
        EditorPlaySettingsDocument loaded = store.Load();

        Assert.Equal("2x", loaded.CommonValues["window-scale"]);
        Assert.Equal("true", loaded.CommonValues["wait-for-debugger"]);
        Assert.Equal("project", loaded.PlatformValues["ps2"]["video-mode"]);
        Assert.Equal("8", loaded.PlatformValues["ds"]["screen-gap"]);
        Assert.Contains("\"wait-for-debugger\": true", File.ReadAllText(store.FilePath));
    }

    /// <summary>Another version fails, naming the file and the version.</summary>
    [Fact]
    public void Load_WhenVersionUnknown_ThrowsNamingFileAndVersion() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 7, \"common\": {} }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("7", exception.Message);
    }

    /// <summary>A common id the engine does not declare fails, naming the file and the id.</summary>
    [Fact]
    public void Load_WhenCommonIdUnknown_ThrowsNamingFileAndId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"common\": { \"target-fps\": \"60\" } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("target-fps", exception.Message);
    }

    /// <summary>A value that is neither a string nor a boolean fails, naming the id.</summary>
    [Fact]
    public void Load_WhenValueIsNumber_ThrowsNamingId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"platforms\": { \"ds\": { \"screen-gap\": 8 } } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("screen-gap", exception.Message);
        Assert.Contains("ds", exception.Message);
    }

    /// <summary>An unknown top-level property fails, naming it.</summary>
    [Fact]
    public void Load_WhenTopLevelPropertyUnknown_ThrowsNamingProperty() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"lastPlatform\": \"ps2\" }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("lastPlatform", exception.Message);
    }

    /// <summary>Writes raw text to play.json and returns a store over it.</summary>
    EditorPlaySettingsStore WriteRaw(string json) {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath));
        File.WriteAllText(store.FilePath, json);
        return store;
    }
}

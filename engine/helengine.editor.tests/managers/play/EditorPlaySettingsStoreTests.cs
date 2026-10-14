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
        Assert.Contains("version 7", exception.Message);
    }

    /// <summary>A file without a version fails, naming the file.</summary>
    [Fact]
    public void Load_WhenVersionMissing_ThrowsNamingFile() {
        EditorPlaySettingsStore store = WriteRaw("{ \"common\": {} }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("no version", exception.Message);
    }

    /// <summary>Text that is not JSON fails, naming the file.</summary>
    [Fact]
    public void Load_WhenJsonInvalid_ThrowsNamingFile() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, ");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("not valid JSON", exception.Message);
    }

    /// <summary>A root that is not an object fails, naming the file.</summary>
    [Fact]
    public void Load_WhenRootIsNotObject_ThrowsNamingFile() {
        EditorPlaySettingsStore store = WriteRaw("[ 1 ]");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("one JSON object", exception.Message);
    }

    /// <summary>A common id repeated in the file fails, naming the file and the id.</summary>
    [Fact]
    public void Load_WhenCommonIdRepeated_ThrowsNamingFileAndId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"common\": { \"shadows\": true, \"shadows\": false } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("'shadows'", exception.Message);
        Assert.Contains("common", exception.Message);
    }

    /// <summary>A platform setting id repeated in one section fails, naming the file, the platform and the id.</summary>
    [Fact]
    public void Load_WhenPlatformSettingIdRepeated_ThrowsNamingFilePlatformAndId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"platforms\": { \"ps2\": { \"video-mode\": \"project\", \"video-mode\": \"pal-640x512i\" } } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("platform 'ps2'", exception.Message);
        Assert.Contains("'video-mode'", exception.Message);
    }

    /// <summary>A platform section repeated in the file fails, naming the file and the platform.</summary>
    [Fact]
    public void Load_WhenPlatformRepeated_ThrowsNamingFileAndPlatform() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"platforms\": { \"ps2\": {}, \"ps2\": {} } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("platform 'ps2'", exception.Message);
    }

    /// <summary>A repeated top-level section fails instead of merging, naming the file and the section.</summary>
    [Fact]
    public void Load_WhenTopLevelSectionRepeated_ThrowsNamingFileAndSection() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"common\": { \"shadows\": true }, \"common\": { \"window-scale\": \"2x\" } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("'common'", exception.Message);
    }

    /// <summary>Saving a common id the engine does not declare fails before touching the existing file.</summary>
    [Fact]
    public void Save_WhenCommonIdUnknown_ThrowsNamingIdAndKeepsExistingFile() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument original = new EditorPlaySettingsDocument();
        original.CommonValues["window-scale"] = "2x";
        store.Save(original);
        string before = File.ReadAllText(store.FilePath);
        EditorPlaySettingsDocument invalid = new EditorPlaySettingsDocument();
        invalid.CommonValues["target-fps"] = "60";

        ArgumentException exception = Assert.Throws<ArgumentException>(() => store.Save(invalid));

        Assert.Contains("target-fps", exception.Message);
        Assert.Equal(before, File.ReadAllText(store.FilePath));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)));
    }

    /// <summary>Saving a null platform value fails naming the platform and id, and leaves the existing file intact.</summary>
    [Fact]
    public void Save_WhenPlatformValueNull_ThrowsNamingPlatformAndIdAndKeepsExistingFile() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument original = new EditorPlaySettingsDocument();
        original.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "project" };
        store.Save(original);
        string before = File.ReadAllText(store.FilePath);
        EditorPlaySettingsDocument invalid = new EditorPlaySettingsDocument();
        invalid.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = null };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => store.Save(invalid));

        Assert.Contains("platform 'ps2'", exception.Message);
        Assert.Contains("'video-mode'", exception.Message);
        Assert.Equal(before, File.ReadAllText(store.FilePath));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)));
    }

    /// <summary>Saving a null common value fails naming the id.</summary>
    [Fact]
    public void Save_WhenCommonValueNull_ThrowsNamingId() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument invalid = new EditorPlaySettingsDocument();
        invalid.CommonValues["shadows"] = null;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => store.Save(invalid));

        Assert.Contains("'shadows'", exception.Message);
        Assert.False(File.Exists(store.FilePath));
    }

    /// <summary>Saving over an existing file replaces it and leaves no staging file behind.</summary>
    [Fact]
    public void Save_OverExistingFile_ReplacesItWithoutStagingLeftovers() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument first = new EditorPlaySettingsDocument();
        first.CommonValues["window-scale"] = "2x";
        store.Save(first);
        EditorPlaySettingsDocument second = new EditorPlaySettingsDocument();
        second.CommonValues["window-scale"] = "4x";

        store.Save(second);

        Assert.Equal("4x", store.Load().CommonValues["window-scale"]);
        Assert.Equal(new[] { store.FilePath }, Directory.GetFiles(Path.GetDirectoryName(store.FilePath)));
    }

    /// <summary>A clone is deep: editing its common values or platform sections leaves the original unchanged.</summary>
    [Fact]
    public void Clone_IsDeepCopy() {
        EditorPlaySettingsDocument original = new EditorPlaySettingsDocument();
        original.CommonValues["window-scale"] = "2x";
        original.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "project" };

        EditorPlaySettingsDocument copy = original.Clone();
        copy.CommonValues["window-scale"] = "4x";
        copy.PlatformValues["ps2"]["video-mode"] = "pal-640x512i";
        copy.PlatformValues["ds"] = new Dictionary<string, string>();

        Assert.Equal("2x", original.CommonValues["window-scale"]);
        Assert.Equal("project", original.PlatformValues["ps2"]["video-mode"]);
        Assert.False(original.PlatformValues.ContainsKey("ds"));
        Assert.Equal("4x", copy.CommonValues["window-scale"]);
        Assert.Equal("pal-640x512i", copy.PlatformValues["ps2"]["video-mode"]);
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
        Assert.Contains("platform 'ds'", exception.Message);
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

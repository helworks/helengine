using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the Play Settings dialog's view-model without any GUI: initial values, highlighting of stored values the
/// declarations reject, edit validation and the document it saves.
/// </summary>
public sealed class EditorPlaySettingsDialogModelTests : IDisposable {
    /// <summary>File path reported in messages.</summary>
    const string FilePath = @"C:\project\user_settings\play.json";

    /// <summary>Workspace-owned scratch project root for the tests that save through a real store.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-dialog-model", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the scratch project root.</summary>
    public EditorPlaySettingsDialogModelTests() {
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>Edit values start from the effective values: stored where present, declared default otherwise.</summary>
    [Fact]
    public void CreateEditValues_StartFromEffectiveValues() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "3x";

        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);

        Assert.Equal("3x", model.CreateCommonEditValues()["window-scale"]);
        Assert.Equal("editor", model.CreateCommonEditValues()["window-monitor"]);
        Assert.Equal("stacked", model.CreatePlatformEditValues()["screen-layout"]);
        Assert.Empty(model.StoredIssues);
    }

    /// <summary>A stored value the declarations reject is flagged so the dialog can highlight its row.</summary>
    [Fact]
    public void StoredInvalidValue_IsFlagged() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";

        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);

        Assert.True(model.IsStoredValueInvalid(EditorPlaySettingScope.Common, "window-scale"));
        Assert.False(model.IsStoredValueInvalid(EditorPlaySettingScope.Common, "window-monitor"));
        Assert.Equal("5x", Assert.Single(model.StoredIssues).StoredValue);
    }

    /// <summary>Valid edits are saved for the common section and the selected platform only.</summary>
    [Fact]
    public void CreateSavedDocument_PreservesOtherPlatformSections() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "pal-320x256p" };
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["window-scale"] = "2x";

        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(common, model.CreatePlatformEditValues());
        EditorPlaySettingsDocument saved = model.CreateSavedDocument();

        Assert.Empty(issues);
        Assert.Equal("2x", saved.CommonValues["window-scale"]);
        Assert.Equal("stacked", saved.PlatformValues["ds"]["screen-layout"]);
        Assert.Equal("pal-320x256p", saved.PlatformValues["ps2"]["video-mode"]);
        Assert.Equal("5x", document.CommonValues["window-scale"]);
    }

    /// <summary>Invalid edits are reported and cannot be saved.</summary>
    [Fact]
    public void ApplyEdits_WhenInvalid_ReportsAndBlocksSave() {
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), new EditorPlaySettingsDocument(), FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["shadows"] = "True";

        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(common, model.CreatePlatformEditValues());

        Assert.Equal("shadows", Assert.Single(issues).SettingId);
        Assert.Throws<InvalidOperationException>(() => model.CreateSavedDocument());
    }

    /// <summary>Edits naming an undeclared id are a programming error.</summary>
    [Fact]
    public void ApplyEdits_WhenIdUndeclared_Throws() {
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), new EditorPlaySettingsDocument(), FilePath);
        Dictionary<string, string> platform = model.CreatePlatformEditValues();
        platform["video-mode"] = "x";

        Assert.Throws<ArgumentException>(() => model.ApplyEdits(model.CreateCommonEditValues(), platform));
    }

    /// <summary>
    /// A stored value the declarations reject is kept as-is in the edit values (never coerced), and resetting it to the
    /// declared default makes the document savable with that default.
    /// </summary>
    [Fact]
    public void StoredInvalidValue_IsKeptForEditingAndCanBeResetToDefault() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();

        Assert.Equal("5x", common["window-scale"]);
        Assert.Throws<InvalidOperationException>(() => model.CreateSavedDocument());

        common["window-scale"] = model.GetDefaultValue(EditorPlaySettingScope.Common, "window-scale");
        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(common, model.CreatePlatformEditValues());

        Assert.Empty(issues);
        Assert.Equal("fit", model.CreateSavedDocument().CommonValues["window-scale"]);
        Assert.Equal("stacked", model.GetDefaultValue(EditorPlaySettingScope.Platform, "screen-layout"));
        Assert.Throws<ArgumentException>(() => model.GetDefaultValue(EditorPlaySettingScope.Platform, "video-mode"));
    }

    /// <summary>
    /// A stored platform id the platform no longer declares blocks Play, but the dialog still opens on it: the entry is
    /// listed as a stored issue (so the status line names it), and saving removes it, after which Play evaluates again.
    /// </summary>
    [Fact]
    public void StoredUndeclaredPlatformSetting_OpensFlaggedAndSaveRemovesIt() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ds"] = new Dictionary<string, string> { ["screen-layout"] = "stacked", ["screen-gap"] = "8" };
        Assert.Throws<InvalidOperationException>(() => EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()));

        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);

        EditorPlaySettingIssue issue = Assert.Single(model.StoredIssues);
        Assert.Equal(EditorPlaySettingScope.Platform, issue.Scope);
        Assert.Equal("screen-gap", issue.SettingId);
        Assert.Equal("8", issue.StoredValue);
        Assert.Contains("Save removes it", issue.Reason);
        Assert.True(model.IsStoredValueInvalid(EditorPlaySettingScope.Platform, "screen-gap"));
        Assert.False(model.CreatePlatformEditValues().ContainsKey("screen-gap"));
        Assert.Throws<InvalidOperationException>(() => model.CreateSavedDocument());

        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(model.CreateCommonEditValues(), model.CreatePlatformEditValues());
        EditorPlaySettingsDocument saved = model.CreateSavedDocument();

        Assert.Empty(issues);
        Assert.False(saved.PlatformValues["ds"].ContainsKey("screen-gap"));
        Assert.Equal("stacked", saved.PlatformValues["ds"]["screen-layout"]);
        Assert.Equal("8", document.PlatformValues["ds"]["screen-gap"]);
        Assert.True(EditorPlaySettingsEvaluator.Evaluate(saved, FilePath, "ds", CreateCapability()).IsValid);
    }

    /// <summary>Saving through the store writes the document the model produced.</summary>
    [Fact]
    public void Save_WritesTheSavedDocumentThroughTheStore() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), store.Load(), store.FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["window-scale"] = "2x";
        model.ApplyEdits(common, model.CreatePlatformEditValues());

        model.Save(store);

        EditorPlaySettingsDocument loaded = store.Load();
        Assert.Equal("2x", loaded.CommonValues["window-scale"]);
        Assert.Equal("stacked", loaded.PlatformValues["ds"]["screen-layout"]);
    }

    /// <summary>A failed save names play.json, and the previous file content survives.</summary>
    [Fact]
    public void Save_WhenFileCannotBeReplaced_NamesTheFile() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "3x";
        store.Save(document);
        string before = File.ReadAllText(store.FilePath);
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), store.Load(), store.FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["window-scale"] = "2x";
        model.ApplyEdits(common, model.CreatePlatformEditValues());

        InvalidOperationException exception;
        using (FileStream held = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            exception = Assert.Throws<InvalidOperationException>(() => model.Save(store));
        }

        Assert.Contains("'" + store.FilePath + "'", exception.Message);
        Assert.NotNull(exception.InnerException);
        Assert.Equal(before, File.ReadAllText(store.FilePath));
    }

    /// <summary>Saving through a store for another file is a programming error.</summary>
    [Fact]
    public void Save_WhenStoreIsForAnotherFile_Throws() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), new EditorPlaySettingsDocument(), FilePath);
        model.ApplyEdits(model.CreateCommonEditValues(), model.CreatePlatformEditValues());

        Assert.Throws<ArgumentException>(() => model.Save(store));
        Assert.False(File.Exists(store.FilePath));
    }

    /// <summary>Creates a DS-like capability with one choice Play setting.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0",
            [new PlatformSettingDefinition("screen-layout", "Screen Layout", PlatformSettingKind.Choice, "stacked", true, ["stacked"])]);
    }
}

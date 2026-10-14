using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the Play Settings dialog controller: stored values the declarations reject are kept (never coerced) and
/// reported, user edits and resets re-check the latest validation, booleans reach the store as <c>true</c>/<c>false</c>,
/// undeclared stored ids are listed and removed on Save, and a save failure lands in the status line naming play.json.
/// </summary>
public sealed class EditorPlaySettingsDialogControllerTests : IDisposable {
    /// <summary>Workspace-owned scratch project root.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-dialog-controller", Guid.NewGuid().ToString("N"));

    /// <summary>Store over the scratch project root.</summary>
    readonly EditorPlaySettingsStore Store;

    /// <summary>Creates the scratch project root and its store.</summary>
    public EditorPlaySettingsDialogControllerTests() {
        Directory.CreateDirectory(ProjectRoot);
        Store = new EditorPlaySettingsStore(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>A valid stored file opens clean: no flagged row and an empty status line.</summary>
    [Fact]
    public void Open_WithValidValues_HasNoFlagsAndNoStatus() {
        SaveRaw("{ \"version\": 1, \"common\": { \"window-scale\": \"2x\" }, \"platforms\": {} }");

        EditorPlaySettingsDialogController controller = Open();

        Assert.Equal("2x", controller.GetValue(EditorPlaySettingScope.Common, "window-scale"));
        Assert.False(controller.IsRowFlagged(EditorPlaySettingScope.Common, "window-scale"));
        Assert.Equal(string.Empty, controller.StatusText);
    }

    /// <summary>
    /// A stored choice the declarations reject stays as stored (it is not swapped for an allowed value), its row is
    /// flagged and the status line names it; Save is refused and leaves the file untouched.
    /// </summary>
    [Fact]
    public void StoredInvalidChoice_IsKeptFlaggedAndBlocksSave() {
        string raw = "{ \"version\": 1, \"common\": { \"window-scale\": \"5x\" }, \"platforms\": {} }";
        SaveRaw(raw);

        EditorPlaySettingsDialogController controller = Open();

        Assert.Equal("5x", controller.GetValue(EditorPlaySettingScope.Common, "window-scale"));
        Assert.True(controller.IsRowFlagged(EditorPlaySettingScope.Common, "window-scale"));
        Assert.StartsWith("Stored values are no longer allowed: 'window-scale' = '5x'", controller.StatusText);

        Assert.False(controller.TrySave(Store));
        Assert.StartsWith("Invalid values: 'window-scale' = '5x'", controller.StatusText);
        Assert.Equal(raw, File.ReadAllText(Store.FilePath));
    }

    /// <summary>
    /// A stored boolean that is not exactly true/false stays as stored and flagged; the checkbox's own text form is
    /// never what reaches the model.
    /// </summary>
    [Fact]
    public void StoredInvalidBoolean_IsKeptAndFlagged() {
        SaveRaw("{ \"version\": 1, \"common\": { \"shadows\": \"yes\" }, \"platforms\": {} }");

        EditorPlaySettingsDialogController controller = Open();

        Assert.Equal("yes", controller.GetValue(EditorPlaySettingScope.Common, "shadows"));
        Assert.True(controller.IsRowFlagged(EditorPlaySettingScope.Common, "shadows"));
        Assert.False(controller.TrySave(Store));
    }

    /// <summary>
    /// Reset Invalid puts every flagged row back to its declared default and re-checks: the flag and the status clear at
    /// once (not only after reopening), and Save then writes a file without the rejected value.
    /// </summary>
    [Fact]
    public void ResetInvalidToDefaults_ClearsFlagsAndStatusAndSaveSucceeds() {
        SaveRaw("{ \"version\": 1, \"common\": { \"window-scale\": \"5x\", \"shadows\": \"yes\", \"frame-rate\": \"unlimited\" }, \"platforms\": {} }");
        EditorPlaySettingsDialogController controller = Open();

        controller.ResetInvalidToDefaults();

        Assert.Equal("fit", controller.GetValue(EditorPlaySettingScope.Common, "window-scale"));
        Assert.Equal("true", controller.GetValue(EditorPlaySettingScope.Common, "shadows"));
        Assert.Equal("unlimited", controller.GetValue(EditorPlaySettingScope.Common, "frame-rate"));
        Assert.False(controller.IsRowFlagged(EditorPlaySettingScope.Common, "window-scale"));
        Assert.False(controller.IsRowFlagged(EditorPlaySettingScope.Common, "shadows"));
        Assert.Equal(string.Empty, controller.StatusText);
        Assert.True(controller.TrySave(Store), controller.StatusText);
        EditorPlaySettingsDocument saved = Store.Load();
        Assert.False(saved.CommonValues.ContainsKey("window-scale"));
        Assert.False(saved.CommonValues.ContainsKey("shadows"));
        Assert.Equal("unlimited", saved.CommonValues["frame-rate"]);
    }

    /// <summary>
    /// Picking a valid value for one flagged row re-checks at once: that row stops being flagged while another
    /// still-rejected row stays flagged and named.
    /// </summary>
    [Fact]
    public void SetValue_RechecksLatestIssues() {
        SaveRaw("{ \"version\": 1, \"common\": { \"window-scale\": \"5x\", \"frame-rate\": \"120\" }, \"platforms\": {} }");
        EditorPlaySettingsDialogController controller = Open();

        controller.SetValue(EditorPlaySettingScope.Common, "window-scale", "4x");

        Assert.False(controller.IsRowFlagged(EditorPlaySettingScope.Common, "window-scale"));
        Assert.True(controller.IsRowFlagged(EditorPlaySettingScope.Common, "frame-rate"));
        Assert.StartsWith("Invalid values: 'frame-rate' = '120'", controller.StatusText);
        Assert.DoesNotContain("window-scale", controller.StatusText);
    }

    /// <summary>
    /// A checkbox edit is stored in the store's format (<c>false</c>, not <c>False</c>), so it passes validation and is
    /// written as a JSON boolean the store reads back as <c>false</c>.
    /// </summary>
    [Fact]
    public void SetBooleanValue_NormalisesToStoreFormatAndSaves() {
        EditorPlaySettingsDialogController controller = Open();

        controller.SetBooleanValue(EditorPlaySettingScope.Common, "shadows", false);

        Assert.Equal("false", controller.GetValue(EditorPlaySettingScope.Common, "shadows"));
        Assert.Equal(string.Empty, controller.StatusText);
        Assert.True(controller.TrySave(Store), controller.StatusText);
        Assert.Equal("false", Store.Load().CommonValues["shadows"]);
        Assert.Contains("\"shadows\": false", File.ReadAllText(Store.FilePath));
    }

    /// <summary>
    /// A stored platform id the platform no longer declares is listed in the status line (it has no row) and Save
    /// removes it while keeping the other values.
    /// </summary>
    [Fact]
    public void StoredUndeclaredPlatformId_IsListedAndSaveRemovesIt() {
        SaveRaw("{ \"version\": 1, \"common\": { \"window-scale\": \"2x\" }, \"platforms\": { \"ds\": { \"screen-gap\": \"8\" } } }");
        EditorPlaySettingsDialogController controller = Open();

        Assert.Contains("'screen-gap' = '8'", controller.StatusText);
        Assert.Contains("Save removes it", controller.StatusText);

        Assert.True(controller.TrySave(Store), controller.StatusText);
        EditorPlaySettingsDocument saved = Store.Load();
        Assert.False(saved.PlatformValues.ContainsKey("ds"));
        Assert.Equal("2x", saved.CommonValues["window-scale"]);
    }

    /// <summary>
    /// A file system failure while saving is caught from the model and shown in the status line naming play.json, Save
    /// reports failure (the dialog stays open) and the previous file content survives.
    /// </summary>
    [Fact]
    public void TrySave_WhenFileCannotBeReplaced_ShowsMessageNamingTheFile() {
        string raw = "{ \"version\": 1, \"common\": {}, \"platforms\": {} }";
        SaveRaw(raw);
        EditorPlaySettingsDialogController controller = Open();
        controller.SetValue(EditorPlaySettingScope.Common, "window-scale", "2x");

        bool saved;
        using (new FileStream(Store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            saved = controller.TrySave(Store);
        }

        Assert.False(saved);
        Assert.Contains("'" + Store.FilePath + "'", controller.StatusText);
        Assert.Equal(raw, File.ReadAllText(Store.FilePath));
    }

    /// <summary>
    /// Opens the controller for the DS-like test platform on the scratch store.
    /// </summary>
    /// <returns>Controller under test.</returns>
    EditorPlaySettingsDialogController Open() {
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), Store.Load(), Store.FilePath);
        return new EditorPlaySettingsDialogController(model);
    }

    /// <summary>
    /// Writes raw play.json content, the way a user hand-edits the file.
    /// </summary>
    /// <param name="content">File content.</param>
    void SaveRaw(string content) {
        Directory.CreateDirectory(Path.GetDirectoryName(Store.FilePath));
        File.WriteAllText(Store.FilePath, content);
    }

    /// <summary>Creates a DS-like capability with one platform Play setting.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0",
            [new PlatformSettingDefinition("screen-layout", "Screen Layout", PlatformSettingKind.Choice, "stacked", true, ["stacked", "side-by-side"])]);
    }
}

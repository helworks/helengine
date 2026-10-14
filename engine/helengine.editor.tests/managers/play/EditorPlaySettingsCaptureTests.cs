using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the press-time capture reads the store, refuses invalid settings with a message naming the file, platform
/// and id, and otherwise returns a snapshot.
/// </summary>
public sealed class EditorPlaySettingsCaptureTests : IDisposable {
    /// <summary>Workspace-owned scratch project root.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-capture", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the scratch project root.</summary>
    public EditorPlaySettingsCaptureTests() {
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>Without a file, the capture succeeds with the defaults.</summary>
    [Fact]
    public void TryCapture_WithoutFile_ReturnsDefaults() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);

        bool captured = EditorPlaySettingsCapture.TryCapture(store, "ps2", CreateCapability(), false, 99, out EditorPlaySettingsSnapshot snapshot, out string failure);

        Assert.True(captured);
        Assert.Equal(string.Empty, failure);
        Assert.Equal("fit", snapshot.CommonValues["window-scale"]);
    }

    /// <summary>
    /// A stored valid value reaches the snapshot, together with Shift and the editor process id, so the capture really
    /// reads the store instead of the declared defaults.
    /// </summary>
    [Fact]
    public void TryCapture_WithStoredValue_ReturnsIt() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "3x";
        store.Save(document);

        bool captured = EditorPlaySettingsCapture.TryCapture(store, "ps2", CreateCapability(), true, 99, out EditorPlaySettingsSnapshot snapshot, out string failure);

        Assert.True(captured, failure);
        Assert.Equal("3x", snapshot.CommonValues["window-scale"]);
        Assert.True(snapshot.EffectiveWaitForDebugger);
        Assert.Equal(99, snapshot.EditorProcessId);
    }

    /// <summary>An invalid stored value refuses Play with a message naming file, platform and id.</summary>
    [Fact]
    public void TryCapture_WithInvalidValue_RefusesNamingFilePlatformAndId() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["frame-rate"] = "120";
        store.Save(document);

        bool captured = EditorPlaySettingsCapture.TryCapture(store, "ps2", CreateCapability(), false, 99, out EditorPlaySettingsSnapshot snapshot, out string failure);

        Assert.False(captured);
        Assert.Null(snapshot);
        Assert.Contains(store.FilePath, failure);
        Assert.Contains("'ps2'", failure);
        Assert.Contains("frame-rate", failure);
    }

    /// <summary>Creates a PS2-like capability without platform Play settings.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "ps2-staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []);
    }
}

using helengine.baseplatform.Player;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the file the editor writes and the player reads for one Play session round-trips exactly and rejects
    /// anything outside the current format.
    /// </summary>
    public sealed class PlayerPlaySettingsFileTests : IDisposable {
        /// <summary>Workspace-owned scratch root (never %TEMP%).</summary>
        readonly string Root = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-file", Guid.NewGuid().ToString("N"));

        /// <summary>Creates the scratch root.</summary>
        public PlayerPlaySettingsFileTests() {
            Directory.CreateDirectory(Root);
        }

        /// <summary>Deletes the scratch root.</summary>
        public void Dispose() {
            Directory.Delete(Root, true);
        }

        /// <summary>Every field survives a write and read.</summary>
        [Fact]
        public void WriteThenRead_RoundTripsEveryField() {
            string path = Path.Combine(Root, "play-settings.json");
            PlayerPlaySettings written = new PlayerPlaySettings(
                PlayerWindowScale.Three, PlayerWindowMonitor.Primary, 4242, true, false, false, 59.94d,
                new Dictionary<string, string> { ["screen-gap"] = "8" });

            PlayerPlaySettingsFile.Write(path, written);
            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.Equal(PlayerWindowScale.Three, read.WindowScale);
            Assert.Equal(PlayerWindowMonitor.Primary, read.WindowMonitor);
            Assert.Equal(4242, read.EditorProcessId);
            Assert.True(read.WaitForDebugger);
            Assert.False(read.ShadowsEnabled);
            Assert.False(read.FrameRateUnlimited);
            Assert.Equal(59.94d, read.TargetFrameRate);
            Assert.Equal("8", read.PlatformValues["screen-gap"]);
        }

        /// <summary>An unlimited frame rate round-trips with no target.</summary>
        [Fact]
        public void WriteThenRead_UnlimitedFrameRate() {
            string path = Path.Combine(Root, "unlimited.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>()));

            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.True(read.FrameRateUnlimited);
            Assert.Equal(0d, read.TargetFrameRate);
        }

        /// <summary>A file of another version is rejected, naming the file and the version.</summary>
        [Fact]
        public void Read_WhenVersionIsNotCurrent_ThrowsNamingFileAndVersion() {
            string path = WriteRaw("v2.json", "{\"version\":2}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("2", exception.Message);
        }

        /// <summary>An unknown property is rejected, naming it.</summary>
        [Fact]
        public void Read_WhenPropertyUnknown_ThrowsNamingProperty() {
            string path = Path.Combine(Root, "known.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>()));
            string json = File.ReadAllText(path).Replace("\"version\": 1", "\"version\": 1, \"targetFps\": 60");
            File.WriteAllText(path, json);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains("targetFps", exception.Message);
        }

        /// <summary>A paced frame rate needs a positive target; an unlimited one must not carry one.</summary>
        [Fact]
        public void Ctor_RejectsInconsistentFrameRate() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 0d, new Dictionary<string, string>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 60d, new Dictionary<string, string>()));
        }

        /// <summary>The editor process id must be positive so the player can find the editor's monitor.</summary>
        [Fact]
        public void Ctor_RejectsNonPositiveEditorProcessId() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 0, false, true, false, 60d, new Dictionary<string, string>()));
        }

        /// <summary>Window scale text maps both ways and rejects unknown text.</summary>
        [Fact]
        public void WindowScaleText_MapsEveryValue() {
            foreach (string text in new[] { "1x", "2x", "3x", "4x", "fit" }) {
                Assert.Equal(text, PlayerWindowScaleText.Format(PlayerWindowScaleText.Parse(text)));
            }

            Assert.Equal(3, PlayerWindowScaleText.ToMultiplier(PlayerWindowScale.Three));
            Assert.Throws<ArgumentException>(() => PlayerWindowScaleText.Parse("5x"));
            Assert.Throws<ArgumentException>(() => PlayerWindowScaleText.ToMultiplier(PlayerWindowScale.Fit));
        }

        /// <summary>Writes raw JSON text into the scratch root.</summary>
        string WriteRaw(string name, string json) {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, json);
            return path;
        }
    }
}

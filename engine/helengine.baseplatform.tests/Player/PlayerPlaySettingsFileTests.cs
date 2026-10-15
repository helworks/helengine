using helengine.baseplatform.Definitions;
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
                new Dictionary<string, string> { ["screen-gap"] = "8" }, null);

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
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>(), null));

            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.True(read.FrameRateUnlimited);
            Assert.Equal(0d, read.TargetFrameRate);
        }

        /// <summary>A file of another version is rejected, naming the file and the version.</summary>
        [Fact]
        public void Read_WhenVersionIsNotCurrent_ThrowsNamingFileAndVersion() {
            string path = WriteRaw("future.json", "{\"version\":2}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("version 2", exception.Message);
        }

        /// <summary>
        /// A file of another version that also carries properties this version does not know is reported by its version,
        /// the real cause, rather than by its first unknown property.
        /// </summary>
        [Fact]
        public void Read_WhenVersionIsNotCurrentAndPropertyUnknown_ReportsVersion() {
            string path = WriteRaw("future-shape.json", "{\"newSetting\":true,\"version\":2}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("version 2", exception.Message);
            Assert.DoesNotContain("newSetting", exception.Message);
        }

        /// <summary>Malformed JSON fails naming the file, keeping the parser error as the inner exception.</summary>
        [Fact]
        public void Read_WhenJsonMalformed_ThrowsNamingFile() {
            string path = WriteRaw("truncated.json", "{\"version\": 1, \"windowScale\": ");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception.InnerException);
        }

        /// <summary>
        /// Every value the reader rejects fails as one error naming the file, the field and the offending value, instead of
        /// escaping as a parser or constructor error that names neither.
        /// </summary>
        /// <param name="find">Text of the valid file to replace.</param>
        /// <param name="replacement">Invalid replacement text.</param>
        /// <param name="field">Field the error must name.</param>
        /// <param name="value">Offending value the error must name.</param>
        [Theory]
        [InlineData("\"version\": 1,", "\"version\": 1.0,", "version", "1.0")]
        [InlineData("\"editorProcessId\": 4242,", "\"editorProcessId\": 4242.0,", "editorProcessId", "4242.0")]
        [InlineData("\"editorProcessId\": 4242,", "\"editorProcessId\": 99999999999,", "editorProcessId", "99999999999")]
        [InlineData("\"editorProcessId\": 4242,", "\"editorProcessId\": 0,", "editorProcessId", "0")]
        [InlineData("\"windowScale\": \"3x\"", "\"windowScale\": \"5x\"", "windowScale", "5x")]
        [InlineData("\"windowMonitor\": \"primary\"", "\"windowMonitor\": \"secondary\"", "windowMonitor", "secondary")]
        [InlineData("\"targetFrameRate\": 59.94", "\"targetFrameRate\": 0", "targetFrameRate", "0")]
        [InlineData("\"frameRateUnlimited\": false", "\"frameRateUnlimited\": true", "targetFrameRate", "59.94")]
        [InlineData("\"screen-gap\": \"8\"", "\"screen-gap\": \"8\", \"screen-gap\": \"9\"", "platform", "screen-gap")]
        [InlineData("\"screen-gap\": \"8\"", "\" \": \"8\"", "platform", "' '")]
        public void Read_WhenValueInvalid_ThrowsNamingFileFieldAndValue(string find, string replacement, string field, string value) {
            string path = Path.Combine(Root, "invalid-value.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(
                PlayerWindowScale.Three, PlayerWindowMonitor.Primary, 4242, false, true, false, 59.94d,
                new Dictionary<string, string> { ["screen-gap"] = "8" }, null));
            string json = File.ReadAllText(path);
            Assert.Contains(find, json);
            File.WriteAllText(path, json.Replace(find, replacement));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("'" + field + "'", exception.Message);
            Assert.Contains(value, exception.Message);
        }

        /// <summary>Writing to a bare file name is rejected naming that path, because the file must have a known location.</summary>
        [Fact]
        public void Write_WhenPathNotFullyQualified_ThrowsNamingPath() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerPlaySettingsFile.Write(
                "play-settings.json",
                new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), null)));

            Assert.Contains("play-settings.json", exception.Message);
        }

        /// <summary>An unknown property is rejected, naming it.</summary>
        [Fact]
        public void Read_WhenPropertyUnknown_ThrowsNamingProperty() {
            string path = Path.Combine(Root, "known.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), null));
            string json = File.ReadAllText(path).Replace("\"version\": 1", "\"version\": 1, \"targetFps\": 60");
            File.WriteAllText(path, json);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains("targetFps", exception.Message);
        }

        /// <summary>A paced frame rate needs a positive target; an unlimited one must not carry one.</summary>
        [Fact]
        public void Ctor_RejectsInconsistentFrameRate() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 0d, new Dictionary<string, string>(), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 60d, new Dictionary<string, string>(), null));
        }

        /// <summary>A resolved video mode round-trips in full.</summary>
        [Fact]
        public void WriteThenRead_RoundTripsVideoMode() {
            string path = Path.Combine(Root, "mode.json");
            PlatformVideoMode mode = new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d, 4, 3);
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>(), mode));

            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.True(read.HasVideoMode);
            Assert.Equal("pal-320x256p", read.VideoMode.Id);
            Assert.Equal(320, read.VideoMode.Width);
            Assert.Equal(256, read.VideoMode.Height);
            Assert.True(read.VideoMode.IsProgressive);
            Assert.Equal(50d, read.VideoMode.RefreshRateHz);
            Assert.Equal(50d, read.VideoMode.PlayerFrameRate);
            Assert.Equal(4, read.VideoMode.DisplayAspectWidth);
            Assert.Equal(3, read.VideoMode.DisplayAspectHeight);
        }

        /// <summary>A platform without video modes writes <c>videoMode</c> as JSON null and reads back no mode.</summary>
        [Fact]
        public void WriteThenRead_WithoutVideoMode_RoundTripsNull() {
            string path = Path.Combine(Root, "no-mode.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), null));

            Assert.Contains("\"videoMode\": null", File.ReadAllText(path));
            Assert.False(PlayerPlaySettingsFile.Read(path).HasVideoMode);
        }

        /// <summary>
        /// A file without <c>videoMode</c>, or with a <c>videoMode</c> that is neither an object nor null, fails naming the
        /// file and the property.
        /// </summary>
        /// <param name="replacement">Text that replaces <c>, "videoMode": null</c> in a valid file.</param>
        [Theory]
        [InlineData("")]
        [InlineData(", \"videoMode\": \"pal-320x256p\"")]
        public void Read_WhenVideoModeMissingOrWrongKind_ThrowsNamingFileAndProperty(string replacement) {
            string path = Path.Combine(Root, "bad-mode.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), null));
            string json = File.ReadAllText(path);
            string invalid = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"videoMode\": null", replacement);
            Assert.NotEqual(json, invalid);
            File.WriteAllText(path, invalid);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("'videoMode'", exception.Message);
        }

        /// <summary>A paced target frame rate that differs from the video mode's player rate fails naming the file and both rates.</summary>
        [Fact]
        public void Read_WhenTargetFrameRateDiffersFromVideoMode_ThrowsNamingFileAndRates() {
            string path = Path.Combine(Root, "mode-rate.json");
            PlatformVideoMode mode = new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d, 4, 3);
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>(), mode));
            string json = File.ReadAllText(path);
            Assert.Contains("\"targetFrameRate\": 50", json);
            File.WriteAllText(path, json.Replace("\"targetFrameRate\": 50", "\"targetFrameRate\": 60"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("'targetFrameRate'", exception.Message);
            Assert.Contains("pal-320x256p", exception.Message);
        }

        /// <summary>
        /// A paced Play with a video mode must pace at the mode's player frame rate; an unlimited one carries 0 and any mode.
        /// </summary>
        [Fact]
        public void Ctor_WithVideoMode_RequiresTheModePlayerFrameRateWhenPaced() {
            PlatformVideoMode mode = new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d, 4, 3);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), mode));
            PlayerPlaySettings unlimited = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>(), mode);

            Assert.Contains("pal-320x256p", exception.Message);
            Assert.Same(mode, unlimited.VideoMode);
        }

        /// <summary>The editor process id must be positive so the player can find the editor's monitor.</summary>
        [Fact]
        public void Ctor_RejectsNonPositiveEditorProcessId() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 0, false, true, false, 60d, new Dictionary<string, string>(), null));
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

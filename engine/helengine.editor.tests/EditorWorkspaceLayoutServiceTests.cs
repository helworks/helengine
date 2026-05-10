using System.Text.Json;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies workspace layout persistence in `user_settings/layout.json`.
    /// </summary>
    public sealed class EditorWorkspaceLayoutServiceTests : IDisposable {
        /// <summary>
        /// Isolated project root used by the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Creates one isolated temporary project root for the current test instance.
        /// </summary>
        public EditorWorkspaceLayoutServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-workspace-layout-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
        }

        /// <summary>
        /// Deletes the isolated project root after the test instance completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures saving slot one creates the layout file and writes the slot payload.
        /// </summary>
        [Fact]
        public void SaveSlot_WhenSlotOneIsWritten_CreatesLayoutJsonWithSlotOnePayload() {
            EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);
            EditorWorkspaceSlotDocument slot = new EditorWorkspaceSlotDocument {
                SchemaVersion = 1,
                Panels = [
                    new EditorWorkspacePanelDocument {
                        InstanceId = "preview-1",
                        PanelTypeId = "preview",
                        IsDocked = false
                    }
                ]
            };

            service.SaveSlot(1, slot);

            string filePath = Path.Combine(TempProjectRootPath, "user_settings", "layout.json");
            Assert.True(File.Exists(filePath));

            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(filePath));
            Assert.True(json.RootElement.TryGetProperty("slots", out JsonElement slots));
            Assert.True(slots.TryGetProperty("slot1", out JsonElement slot1));
            Assert.Equal(1, slot1.GetProperty("schemaVersion").GetInt32());
        }

        /// <summary>
        /// Ensures loading a slot from a missing layout file returns no slot document.
        /// </summary>
        [Fact]
        public void LoadSlot_WhenLayoutFileIsMissing_ReturnsNull() {
            EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);

            EditorWorkspaceSlotDocument slot = service.LoadSlot(3);

            Assert.Null(slot);
        }

        /// <summary>
        /// Ensures malformed layout JSON does not throw and instead returns no slot document.
        /// </summary>
        [Fact]
        public void LoadSlot_WhenJsonIsMalformed_ReturnsNullWithoutThrowing() {
            string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "user_settings");
            Directory.CreateDirectory(settingsDirectoryPath);
            File.WriteAllText(Path.Combine(settingsDirectoryPath, "layout.json"), "{ invalid json");
            EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);

            EditorWorkspaceSlotDocument slot = service.LoadSlot(2);

            Assert.Null(slot);
        }
    }
}

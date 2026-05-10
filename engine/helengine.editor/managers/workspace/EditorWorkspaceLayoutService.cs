using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and saves editor workspace slots in `user_settings/layout.json`.
    /// </summary>
    public sealed class EditorWorkspaceLayoutService {
        /// <summary>
        /// Shared JSON serialization options used for workspace layout documents.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Absolute project root that owns the local workspace settings file.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Absolute path to the workspace layout file inside `user_settings`.
        /// </summary>
        string LayoutFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "layout.json");
            }
        }

        /// <summary>
        /// Initializes one layout service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorWorkspaceLayoutService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads one saved workspace slot from disk.
        /// </summary>
        /// <param name="slotNumber">One-based slot number from one through five.</param>
        /// <returns>Loaded slot document, or null when no file or valid slot exists.</returns>
        public EditorWorkspaceSlotDocument LoadSlot(int slotNumber) {
            ValidateSlotNumber(slotNumber);
            EditorWorkspaceLayoutDocument document = TryLoadDocument();
            if (document == null) {
                return null;
            }

            return document.GetSlot(slotNumber);
        }

        /// <summary>
        /// Saves one workspace slot back to disk, creating the layout file when needed.
        /// </summary>
        /// <param name="slotNumber">One-based slot number from one through five.</param>
        /// <param name="slot">Workspace slot payload to persist.</param>
        public void SaveSlot(int slotNumber, EditorWorkspaceSlotDocument slot) {
            ValidateSlotNumber(slotNumber);
            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            }

            EditorWorkspaceLayoutDocument document = TryLoadDocument();
            if (document == null) {
                document = EditorWorkspaceLayoutDocument.CreateDefault();
            }

            document.SetSlot(slotNumber, slot);

            string settingsDirectoryPath = Path.GetDirectoryName(LayoutFilePath);
            if (string.IsNullOrWhiteSpace(settingsDirectoryPath)) {
                throw new InvalidOperationException("Workspace layout directory path could not be resolved.");
            }

            Directory.CreateDirectory(settingsDirectoryPath);
            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(LayoutFilePath, json);
        }

        /// <summary>
        /// Attempts to load the full workspace layout document from disk.
        /// </summary>
        /// <returns>Deserialized layout document, or null when the file is missing or malformed.</returns>
        EditorWorkspaceLayoutDocument TryLoadDocument() {
            if (!File.Exists(LayoutFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(LayoutFilePath);
                return JsonSerializer.Deserialize<EditorWorkspaceLayoutDocument>(json, JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Validates that one supported slot number was provided.
        /// </summary>
        /// <param name="slotNumber">Requested slot number.</param>
        static void ValidateSlotNumber(int slotNumber) {
            if (slotNumber < 1 || slotNumber > 5) {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Workspace slot number must be between 1 and 5.");
            }
        }
    }
}

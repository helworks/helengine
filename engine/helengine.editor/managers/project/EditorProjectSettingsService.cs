using helengine.projectfile;

namespace helengine.editor {
    /// <summary>
    /// Reads and writes the user-editable identity fields of the project file through the shared project file
    /// reader and writer, so every field this dialog does not own survives a save byte for byte in meaning.
    /// </summary>
    public sealed class EditorProjectSettingsService {
        /// <summary>
        /// Absolute path of the canonical project file.
        /// </summary>
        readonly string CanonicalProjectFilePath;

        /// <summary>
        /// Creates a service bound to one project file.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Absolute path of the project file to read and write.</param>
        public EditorProjectSettingsService(string canonicalProjectFilePath) {
            if (string.IsNullOrWhiteSpace(canonicalProjectFilePath)) {
                throw new ArgumentException("Canonical project file path must be provided.", nameof(canonicalProjectFilePath));
            }

            CanonicalProjectFilePath = Path.GetFullPath(canonicalProjectFilePath);
        }

        /// <summary>
        /// Reads the current name and description from the project file.
        /// </summary>
        public EditorProjectSettings Load() {
            ProjectFileDocument document = EditorProjectMetadataResolver.LoadProjectDocument(CanonicalProjectFilePath);
            return new EditorProjectSettings {
                Name = document.Name,
                Description = document.Description ?? string.Empty
            };
        }

        /// <summary>
        /// Writes the supplied name and description into the project file, leaving every other field as it was.
        /// </summary>
        /// <param name="settings">Settings to persist; the name must not be blank.</param>
        public void Save(EditorProjectSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (string.IsNullOrWhiteSpace(settings.Name)) {
                throw new ArgumentException("Project name must be provided.", nameof(settings));
            }

            ProjectFileDocument document = EditorProjectMetadataResolver.LoadProjectDocument(CanonicalProjectFilePath);
            document.Name = settings.Name.Trim();
            document.Description = string.IsNullOrWhiteSpace(settings.Description) ? null : settings.Description.Trim();
            ProjectFileWriter writer = new ProjectFileWriter();
            writer.WriteAsync(CanonicalProjectFilePath, document).GetAwaiter().GetResult();
        }
    }
}

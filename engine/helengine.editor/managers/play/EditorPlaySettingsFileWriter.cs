using helengine.baseplatform.Player;

namespace helengine.editor {
    /// <summary>
    /// Writes the resolved Play settings of one Play to <c>&lt;project&gt;/cache/play/&lt;platformId&gt;/play-settings.json</c>,
    /// the file the player reads through <c>--play-settings</c>. The cache folder stays deletable: the file is rewritten
    /// for every Play.
    /// </summary>
    public sealed class EditorPlaySettingsFileWriter {
        /// <summary>File name of the Play settings file inside one platform's Play cache folder.</summary>
        public const string FileName = "play-settings.json";

        /// <summary>Absolute project root.</summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the writer for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        public EditorPlaySettingsFileWriter(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Resolves the settings file path of one platform.
        /// </summary>
        /// <param name="platformId">Platform being played.</param>
        /// <returns>Absolute file path.</returns>
        public string ResolvePath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(ProjectRootPath, "cache", "play", platformId, FileName);
        }

        /// <summary>
        /// Writes the resolved settings of one Play.
        /// </summary>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="settings">Resolved settings.</param>
        /// <returns>Absolute path of the written file.</returns>
        public string Write(string platformId, PlayerPlaySettings settings) {
            string path = ResolvePath(platformId);
            PlayerPlaySettingsFile.Write(path, settings);
            return path;
        }
    }
}

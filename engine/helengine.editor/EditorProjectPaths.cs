namespace helengine.editor {
    /// <summary>
    /// Provides shared project path resolution for editor subsystems.
    /// </summary>
    public static class EditorProjectPaths {
        /// <summary>
        /// Tracks the resolved project root path.
        /// </summary>
        static string ProjectRootPath;

        /// <summary>
        /// Tracks the resolved assets root path.
        /// </summary>
        static string AssetsRootPath;

        /// <summary>
        /// Tracks the resolved cache root path.
        /// </summary>
        static string CacheRootPath;

        /// <summary>
        /// Tracks the resolved shader cache path.
        /// </summary>
        static string ShaderCachePath;

        /// <summary>
        /// Tracks the resolved generated code root path.
        /// </summary>
        static string GeneratedCodeRootPath;

        /// <summary>
        /// Tracks the resolved generated code projects root path.
        /// </summary>
        static string GeneratedCodeProjectsRootPath;

        /// <summary>
        /// Gets the resolved project root path.
        /// </summary>
        public static string ProjectRoot => ProjectRootPath;

        /// <summary>
        /// Gets the resolved assets root path.
        /// </summary>
        public static string AssetsRoot => AssetsRootPath;

        /// <summary>
        /// Gets the resolved cache root path.
        /// </summary>
        public static string CacheRoot => CacheRootPath;

        /// <summary>
        /// Gets the resolved shader cache path.
        /// </summary>
        public static string ShaderCache => ShaderCachePath;

        /// <summary>
        /// Gets the resolved generated code root path.
        /// </summary>
        public static string GeneratedCodeRoot => GeneratedCodeRootPath;

        /// <summary>
        /// Gets the resolved generated code projects root path.
        /// </summary>
        public static string GeneratedCodeProjectsRoot => GeneratedCodeProjectsRootPath;

        /// <summary>
        /// Initializes shared project paths from the provided root path.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public static void Initialize(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheRootPath = Path.Combine(ProjectRootPath, "cache");
            ShaderCachePath = Path.Combine(CacheRootPath, "shader-cache");
            GeneratedCodeRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code");
            GeneratedCodeProjectsRootPath = Path.Combine(GeneratedCodeRootPath, "projects");
        }
    }
}

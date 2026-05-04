namespace helengine.editor {
    /// <summary>
    /// Describes the execution workspace used by one platform build-graph invocation.
    /// </summary>
    internal sealed class EditorPlatformBuildGraphWorkspace {
        /// <summary>
        /// Initializes one build-graph workspace rooted at the supplied execution directory.
        /// </summary>
        public EditorPlatformBuildGraphWorkspace(string executionRootPath) {
            if (string.IsNullOrWhiteSpace(executionRootPath)) {
                throw new ArgumentException("Execution root path must be provided.", nameof(executionRootPath));
            }

            ExecutionRootPath = Path.GetFullPath(executionRootPath);
            GeneratedCoreRootPath = Path.Combine(ExecutionRootPath, "generated-core");
            CookRootPath = Path.Combine(ExecutionRootPath, "cooked");
            CodeRootPath = Path.Combine(ExecutionRootPath, "code");
            VariantRootPath = Path.Combine(ExecutionRootPath, "variants");
            LayoutRootPath = Path.Combine(ExecutionRootPath, "layout");
            PackageRootPath = Path.Combine(ExecutionRootPath, "package");
            BuilderWorkingRootPath = Path.Combine(ExecutionRootPath, "builder");
            LogsRootPath = Path.Combine(ExecutionRootPath, "logs");
        }

        /// <summary>
        /// Gets the top-level execution root path.
        /// </summary>
        public string ExecutionRootPath { get; }

        /// <summary>
        /// Gets the generated-core output root path.
        /// </summary>
        public string GeneratedCoreRootPath { get; }

        /// <summary>
        /// Gets the cooked-content root path.
        /// </summary>
        public string CookRootPath { get; }

        /// <summary>
        /// Gets the authored-code output root path.
        /// </summary>
        public string CodeRootPath { get; }

        /// <summary>
        /// Gets the resolved-variant output root path.
        /// </summary>
        public string VariantRootPath { get; }

        /// <summary>
        /// Gets the media-layout output root path.
        /// </summary>
        public string LayoutRootPath { get; }

        /// <summary>
        /// Gets the platform-package working root path.
        /// </summary>
        public string PackageRootPath { get; }

        /// <summary>
        /// Gets the builder scratch root used by platform-specific packagers.
        /// </summary>
        public string BuilderWorkingRootPath { get; }

        /// <summary>
        /// Gets the log root path.
        /// </summary>
        public string LogsRootPath { get; }

        /// <summary>
        /// Gets the canonical log path for the supplied build phase.
        /// </summary>
        public string GetLogPath(EditorPlatformBuildPhase phase) {
            return Path.Combine(LogsRootPath, phase switch {
                EditorPlatformBuildPhase.RegenerateCore => "regen.log",
                EditorPlatformBuildPhase.CookAssets => "cook.log",
                EditorPlatformBuildPhase.CompileCode => "code.log",
                EditorPlatformBuildPhase.ResolveVariants => "variants.log",
                EditorPlatformBuildPhase.LayoutMedia => "layout.log",
                EditorPlatformBuildPhase.WriteContainers => "container.log",
                EditorPlatformBuildPhase.PackagePlatform => "package.log",
                _ => "build.log"
            });
        }
    }
}

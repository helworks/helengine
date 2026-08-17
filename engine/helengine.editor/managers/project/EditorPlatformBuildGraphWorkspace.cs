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
        /// Initializes one build-graph workspace with independently managed graph, generated-core, and native roots.
        /// </summary>
        /// <param name="executionRootPath">Resettable build-graph execution root.</param>
        /// <param name="generatedCoreRootPath">Resettable generated-core output root.</param>
        /// <param name="builderWorkingRootPath">Persistent native builder working root.</param>
        public EditorPlatformBuildGraphWorkspace(
            string executionRootPath,
            string generatedCoreRootPath,
            string builderWorkingRootPath) {
            if (string.IsNullOrWhiteSpace(executionRootPath)) {
                throw new ArgumentException("Execution root path must be provided.", nameof(executionRootPath));
            }
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated-core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (string.IsNullOrWhiteSpace(builderWorkingRootPath)) {
                throw new ArgumentException("Builder working root path must be provided.", nameof(builderWorkingRootPath));
            }

            string canonicalExecutionRootPath = ResolveCanonicalRootPath(executionRootPath);
            string canonicalGeneratedCoreRootPath = ResolveCanonicalRootPath(generatedCoreRootPath);
            string canonicalBuilderWorkingRootPath = ResolveCanonicalRootPath(builderWorkingRootPath);
            EnsureRootsAreDisjoint(
                canonicalExecutionRootPath,
                "Execution root",
                canonicalGeneratedCoreRootPath,
                "generated-core root",
                nameof(generatedCoreRootPath));
            EnsureRootsAreDisjoint(
                canonicalExecutionRootPath,
                "Execution root",
                canonicalBuilderWorkingRootPath,
                "native root",
                nameof(builderWorkingRootPath));
            EnsureRootsAreDisjoint(
                canonicalGeneratedCoreRootPath,
                "generated-core root",
                canonicalBuilderWorkingRootPath,
                "native root",
                nameof(builderWorkingRootPath));

            ExecutionRootPath = canonicalExecutionRootPath;
            GeneratedCoreRootPath = canonicalGeneratedCoreRootPath;
            BuilderWorkingRootPath = canonicalBuilderWorkingRootPath;
            CookRootPath = Path.Combine(ExecutionRootPath, "cooked");
            CodeRootPath = Path.Combine(ExecutionRootPath, "code");
            VariantRootPath = Path.Combine(ExecutionRootPath, "variants");
            LayoutRootPath = Path.Combine(ExecutionRootPath, "layout");
            PackageRootPath = Path.Combine(ExecutionRootPath, "package");
            LogsRootPath = Path.Combine(ExecutionRootPath, "logs");
        }

        /// <summary>
        /// Canonicalizes one independently managed workspace root before overlap validation.
        /// </summary>
        /// <param name="path">Workspace root path to canonicalize.</param>
        /// <returns>Absolute path without a trailing separator unless the path is a filesystem root.</returns>
        static string ResolveCanonicalRootPath(string path) {
            string fullPath = Path.GetFullPath(path);
            string rootPath = Path.GetPathRoot(fullPath);
            if (fullPath.Length <= rootPath.Length) {
                return rootPath;
            }

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Rejects independently managed roots that are equal or nested in either direction.
        /// </summary>
        /// <param name="firstPath">First canonical workspace root.</param>
        /// <param name="firstName">First root name used in validation diagnostics.</param>
        /// <param name="secondPath">Second canonical workspace root.</param>
        /// <param name="secondName">Second root name used in validation diagnostics.</param>
        /// <param name="parameterName">Constructor parameter associated with the conflicting root.</param>
        static void EnsureRootsAreDisjoint(
            string firstPath,
            string firstName,
            string secondPath,
            string secondName,
            string parameterName) {
            if (firstPath.Equals(secondPath, StringComparison.OrdinalIgnoreCase)
                || IsStrictAncestorPath(firstPath, secondPath)
                || IsStrictAncestorPath(secondPath, firstPath)) {
                throw new ArgumentException(
                    $"{firstName} path '{firstPath}' conflicts with {secondName} path '{secondPath}'; independent workspace roots must not be equal or nested.",
                    parameterName);
            }
        }

        /// <summary>
        /// Determines whether one canonical path is a strict separator-delimited ancestor of another.
        /// </summary>
        /// <param name="ancestorPath">Potential canonical ancestor path.</param>
        /// <param name="descendantPath">Potential canonical descendant path.</param>
        /// <returns><c>true</c> when the descendant is strictly below the ancestor.</returns>
        static bool IsStrictAncestorPath(string ancestorPath, string descendantPath) {
            string ancestorPrefix = ancestorPath.EndsWith(Path.DirectorySeparatorChar)
                || ancestorPath.EndsWith(Path.AltDirectorySeparatorChar)
                ? ancestorPath
                : ancestorPath + Path.DirectorySeparatorChar;
            return descendantPath.StartsWith(ancestorPrefix, StringComparison.OrdinalIgnoreCase);
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

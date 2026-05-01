namespace helengine.editor {
    /// <summary>
    /// Stores the deployment-root paths used by one local Windows build execution.
    /// </summary>
    public sealed class EditorWindowsBuildPaths {
        /// <summary>
        /// Target identifier used by the current Windows DirectX build slice.
        /// </summary>
        public const string TargetIdValue = "windows-directx";

        /// <summary>
        /// Initializes one Windows build path set from the selected deployment root.
        /// </summary>
        /// <param name="deploymentRootPath">User-selected deployment root path.</param>
        public EditorWindowsBuildPaths(string deploymentRootPath) {
            if (string.IsNullOrWhiteSpace(deploymentRootPath)) {
                throw new ArgumentException("Deployment root path must be provided.", nameof(deploymentRootPath));
            }

            DeploymentRootPath = Path.GetFullPath(deploymentRootPath);
            GeneratedSourceRootPath = Path.Combine(DeploymentRootPath, "GeneratedSource", TargetIdValue);
            IntermediateRootPath = Path.Combine(DeploymentRootPath, "Intermediate", TargetIdValue);
            BuildRootPath = Path.Combine(DeploymentRootPath, "Build");
            CMakeBuildRootPath = Path.Combine(IntermediateRootPath, "cmake-build");
            AuditRunnerRootPath = Path.Combine(IntermediateRootPath, "cs2cpp-runner");
        }

        /// <summary>
        /// Gets the absolute deployment root selected by the user.
        /// </summary>
        public string DeploymentRootPath { get; }

        /// <summary>
        /// Gets the generated-source folder used by `cs2.cpp`.
        /// </summary>
        public string GeneratedSourceRootPath { get; }

        /// <summary>
        /// Gets the target-scoped intermediate folder used by the native Windows build.
        /// </summary>
        public string IntermediateRootPath { get; }

        /// <summary>
        /// Gets the shared final build output folder.
        /// </summary>
        public string BuildRootPath { get; }

        /// <summary>
        /// Gets the CMake build folder used by the native Windows host.
        /// </summary>
        public string CMakeBuildRootPath { get; }

        /// <summary>
        /// Gets the temporary source folder used to host the local `cs2.cpp` audit runner project.
        /// </summary>
        public string AuditRunnerRootPath { get; }
    }
}

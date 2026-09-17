namespace helengine.editor {
    /// <summary>
    /// Resolves the codegen executable produced by the engine build: the published copy beside the editor first, then an on-demand build keyed by the submodule commit.
    /// </summary>
    public sealed class EngineCodegenToolProvider : IEngineCodegenToolProvider {
        /// <summary>
        /// Directory beside the editor assembly that the build script publishes the tool into.
        /// </summary>
        public const string PublishedToolDirectoryName = "codegen";

        /// <summary>
        /// Executable file name of the codegen tool.
        /// </summary>
        public const string ToolFileName = "codegen.exe";

        /// <summary>
        /// Relative path of the codegen project inside the submodule.
        /// </summary>
        const string CodegenProjectRelativePath = "codegen/codegen.csproj";

        /// <summary>
        /// Environment setting that lets build hosts pick the on-demand cache root; matches the editor's other isolated build state.
        /// </summary>
        const string WorkspaceRootEnvironmentVariableName = "HELENGINE_BUILD_WORKSPACE_ROOT";

        /// <summary>
        /// Default on-demand cache folder under the temp directory, shared with other isolated build state.
        /// </summary>
        const string IsolationFolderName = "helengine-builds";

        readonly string EditorBaseDirectoryPath;
        readonly string SubmoduleRootPath;
        readonly string OnDemandCacheRootPath;
        readonly IEngineCodegenToolPublisher Publisher;

        /// <summary>
        /// Initializes the provider for the running editor using the real submodule and real external commands.
        /// </summary>
        public EngineCodegenToolProvider()
            : this(
                AppContext.BaseDirectory,
                new EditorSourceBuildWorkspaceLocator().ResolveCSharpCodegenRootPath(),
                ResolveDefaultOnDemandCacheRootPath(),
                new DotNetEngineCodegenToolPublisher()) {
        }

        /// <summary>
        /// Initializes the provider with explicit roots and publisher; used by tests.
        /// </summary>
        /// <param name="editorBaseDirectoryPath">Directory that contains the editor assembly.</param>
        /// <param name="submoduleRootPath">Codegen submodule root.</param>
        /// <param name="onDemandCacheRootPath">Root under which commit-keyed on-demand builds are stored.</param>
        /// <param name="publisher">External command abstraction.</param>
        internal EngineCodegenToolProvider(
            string editorBaseDirectoryPath,
            string submoduleRootPath,
            string onDemandCacheRootPath,
            IEngineCodegenToolPublisher publisher) {
            if (string.IsNullOrWhiteSpace(editorBaseDirectoryPath)) {
                throw new ArgumentException("Editor base directory path must be provided.", nameof(editorBaseDirectoryPath));
            }
            if (string.IsNullOrWhiteSpace(submoduleRootPath)) {
                throw new ArgumentException("Submodule root path must be provided.", nameof(submoduleRootPath));
            }
            if (string.IsNullOrWhiteSpace(onDemandCacheRootPath)) {
                throw new ArgumentException("On-demand cache root path must be provided.", nameof(onDemandCacheRootPath));
            }

            EditorBaseDirectoryPath = Path.GetFullPath(editorBaseDirectoryPath);
            SubmoduleRootPath = Path.GetFullPath(submoduleRootPath);
            OnDemandCacheRootPath = Path.GetFullPath(onDemandCacheRootPath);
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        /// <inheritdoc />
        public string Resolve() {
            string publishedToolPath = Path.Combine(EditorBaseDirectoryPath, PublishedToolDirectoryName, ToolFileName);
            if (File.Exists(publishedToolPath)) {
                return publishedToolPath;
            }

            string codegenProjectPath = Path.GetFullPath(Path.Combine(SubmoduleRootPath, CodegenProjectRelativePath));
            if (!File.Exists(codegenProjectPath)) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found. No published copy at '{publishedToolPath}', and the codegen submodule at '{SubmoduleRootPath}' is not initialised ('{codegenProjectPath}' is missing). Run 'git submodule update --init --recursive' in the engine checkout, or build through scripts/build-platform.ps1 which publishes the tool.");
            }

            string commit;
            try {
                commit = Publisher.ReadCommit(SubmoduleRootPath);
            } catch (Exception ex) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found at '{publishedToolPath}', and the on-demand build could not read the submodule commit because git could not be run. Build through scripts/build-platform.ps1, which publishes the tool.", ex);
            }
            if (string.IsNullOrWhiteSpace(commit)) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found at '{publishedToolPath}', and git returned no commit for the submodule at '{SubmoduleRootPath}'.");
            }

            string onDemandDirectoryPath = Path.Combine(OnDemandCacheRootPath, PublishedToolDirectoryName, commit.Trim());
            string onDemandToolPath = Path.Combine(onDemandDirectoryPath, ToolFileName);
            if (File.Exists(onDemandToolPath)) {
                return onDemandToolPath;
            }

            try {
                Publisher.Publish(codegenProjectPath, onDemandDirectoryPath);
            } catch (Exception ex) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found at '{publishedToolPath}', and the on-demand build of '{codegenProjectPath}' into '{onDemandDirectoryPath}' could not be run. Build through scripts/build-platform.ps1, which publishes the tool.", ex);
            }
            if (!File.Exists(onDemandToolPath)) {
                throw new InvalidOperationException(
                    $"Publishing the engine codegen tool from '{codegenProjectPath}' did not produce '{onDemandToolPath}'.");
            }

            return onDemandToolPath;
        }

        /// <summary>
        /// Resolves the default on-demand cache root, honouring the same workspace override the editor's other isolated build state uses.
        /// </summary>
        /// <returns>Absolute cache root.</returns>
        static string ResolveDefaultOnDemandCacheRootPath() {
            string configuredWorkspaceRootPath = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configuredWorkspaceRootPath)) {
                return Path.GetFullPath(configuredWorkspaceRootPath);
            }

            return Path.Combine(Path.GetTempPath(), IsolationFolderName);
        }
    }
}

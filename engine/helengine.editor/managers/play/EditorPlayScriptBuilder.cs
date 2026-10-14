namespace helengine.editor {
    /// <summary>
    /// Builds the runtime script modules for editor Play, but only when a script input changed. Moving an object or
    /// otherwise editing a scene must not rebuild scripts, so the builder fingerprints exactly the inputs that change the
    /// compiled modules: every <c>.cs</c> source and <c>code.module.json</c> manifest under the project's assets, the
    /// resolved gameplay symbol set for the platform and environment, and the content of the engine assemblies the
    /// modules reference. A matching fingerprint with every module assembly present skips the compile. Any mismatch
    /// discards the stored fingerprint, deletes the module folders of modules the project no longer declares (so their
    /// assemblies can never be loaded) and compiles incrementally over the previous output; the fingerprint is stored
    /// again only after a successful compile.
    /// </summary>
    public sealed class EditorPlayScriptBuilder : IEditorPlayScriptBuilder {
        /// <summary>
        /// File name of the stored fingerprint inside the Play output root.
        /// </summary>
        public const string FingerprintFileName = "play-script-build.fingerprint";

        /// <summary>
        /// Version token mixed into every fingerprint; bump it when the fingerprint inputs or the published module layout
        /// change meaning. Version 2: modules publish flat as <c>&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c> instead of
        /// below <c>Debug/net9.0</c>, so outputs fingerprinted under version 1 are always rebuilt. Version 3: engine
        /// assemblies are identified by content instead of path, length and timestamp, and the compile runs
        /// incrementally in place instead of into a fresh output root.
        /// </summary>
        const string FingerprintVersionToken = "play-script-fingerprint=3";

        /// <summary>
        /// Project-relative folder that holds every authored script and code module manifest.
        /// </summary>
        const string AssetsFolderName = "assets";

        /// <summary>
        /// File name of folder-scoped code module manifests.
        /// </summary>
        const string ModuleManifestFileName = "code.module.json";

        /// <summary>
        /// Absolute authored project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Performs the actual compile when the fingerprint is stale.
        /// </summary>
        readonly IEditorPlayScriptCompiler Compiler;

        /// <summary>
        /// Resolves the selected project environment for a platform; the same environment the Play cook uses.
        /// </summary>
        readonly Func<string, string> EnvironmentIdResolver;

        /// <summary>
        /// Discovers the project's code modules to know which module assemblies a valid build must contain.
        /// </summary>
        readonly EditorCodeModuleManifestService ModuleManifestService;

        /// <summary>
        /// Engine assemblies every runtime module references; a change to their bytes invalidates the Play modules.
        /// </summary>
        readonly IReadOnlyList<string> EngineAssemblyPaths;

        /// <summary>
        /// Initializes the Play script builder.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root.</param>
        /// <param name="compiler">Compiler that builds and publishes the runtime modules.</param>
        /// <param name="environmentIdResolver">Resolves the selected environment id for a platform; empty means none.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the compiler or environment resolver is null.</exception>
        public EditorPlayScriptBuilder(string projectRootPath, IEditorPlayScriptCompiler compiler, Func<string, string> environmentIdResolver)
            : this(projectRootPath, compiler, environmentIdResolver, EditorGameSolutionService.ResolveRuntimeEngineAssemblyPaths()) {
        }

        /// <summary>
        /// Initializes the Play script builder with an explicit set of engine assemblies to fingerprint, so tests can
        /// stand in scratch files for the real engine assemblies.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root.</param>
        /// <param name="compiler">Compiler that builds and publishes the runtime modules.</param>
        /// <param name="environmentIdResolver">Resolves the selected environment id for a platform; empty means none.</param>
        /// <param name="engineAssemblyPaths">Engine assemblies the runtime modules reference, fingerprinted by content.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the compiler, environment resolver or assembly list is null.</exception>
        internal EditorPlayScriptBuilder(
            string projectRootPath,
            IEditorPlayScriptCompiler compiler,
            Func<string, string> environmentIdResolver,
            IReadOnlyList<string> engineAssemblyPaths) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            Compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            EnvironmentIdResolver = environmentIdResolver ?? throw new ArgumentNullException(nameof(environmentIdResolver));
            EngineAssemblyPaths = engineAssemblyPaths ?? throw new ArgumentNullException(nameof(engineAssemblyPaths));
            ModuleManifestService = new EditorCodeModuleManifestService(ProjectRootPath);
        }

        /// <summary>
        /// Returns the up-to-date runtime modules for the platform, compiling only when a script input changed.
        /// </summary>
        /// <param name="platformId">Platform whose gameplay symbols the scripts compile against.</param>
        /// <param name="runtimeModulesRootPath">Receives the runtime module root; it exists on disk after success.</param>
        /// <param name="compileSkipped">Receives true when the fingerprint matched and the compile was skipped.</param>
        /// <returns>Success when the modules are current or were rebuilt, otherwise the compile failure.</returns>
        /// <exception cref="ArgumentException">Thrown when the platform id is blank.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the environment resolver or compiler returns nothing.</exception>
        public EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath, out bool compileSkipped) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            string environmentId = EnvironmentIdResolver(platformId);
            if (environmentId == null) {
                throw new InvalidOperationException($"No Play environment could be resolved for platform '{platformId}'.");
            }

            string playOutputRootPath = Compiler.ResolvePlayOutputRootPath(platformId);
            string fingerprintFilePath = Path.Combine(playOutputRootPath, FingerprintFileName);
            string fingerprint = EditorScriptBuildFingerprint.Compute(DescribeBuildInputs(platformId, environmentId));
            string expectedModulesRootPath = Compiler.ResolveRuntimeModulesRootPath(platformId);
            if (EditorScriptBuildFingerprint.MatchesStored(fingerprintFilePath, fingerprint)
                && AllRuntimeModuleAssembliesExist(expectedModulesRootPath)) {
                runtimeModulesRootPath = expectedModulesRootPath;
                compileSkipped = true;
                return EditorBuildExecutionResult.Success("Play scripts are up to date; script build skipped.");
            }

            // The compile is incremental and writes in place, so a failed compile can leave some modules rebuilt from the
            // new sources: drop the stored fingerprint first so those outputs can never be mistaken for a finished build.
            if (File.Exists(fingerprintFilePath)) {
                File.Delete(fingerprintFilePath);
            }

            compileSkipped = false;
            DeleteRemovedModuleFolders(expectedModulesRootPath);
            EditorBuildExecutionResult result = Compiler.Build(platformId, environmentId, out runtimeModulesRootPath);
            if (result == null) {
                throw new InvalidOperationException("The Play script compiler returned no result.");
            } else if (!result.Succeeded) {
                return result;
            }

            Directory.CreateDirectory(runtimeModulesRootPath);
            EditorScriptBuildFingerprint.WriteStored(fingerprintFilePath, fingerprint);
            return result;
        }

        /// <summary>
        /// Deletes every folder under the runtime module root that does not belong to a runtime module the project still
        /// declares. The player loads every module folder it finds, so a removed or renamed module's assembly must not
        /// survive into the next Play; current module folders stay so the compile can update them incrementally.
        /// </summary>
        /// <param name="runtimeModulesRootPath">Runtime module root laid out as <c>&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</param>
        void DeleteRemovedModuleFolders(string runtimeModulesRootPath) {
            if (!Directory.Exists(runtimeModulesRootPath)) {
                return;
            }

            HashSet<string> runtimeModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EditorCodeModuleManifestEntry[] modules = ModuleManifestService.Load().Modules;
            for (int index = 0; index < modules.Length; index++) {
                if (modules[index].ModuleKind == EditorCodeModuleKind.Runtime) {
                    runtimeModuleIds.Add(modules[index].ModuleId);
                }
            }

            string[] moduleDirectoryPaths = Directory.GetDirectories(runtimeModulesRootPath);
            for (int index = 0; index < moduleDirectoryPaths.Length; index++) {
                if (!runtimeModuleIds.Contains(Path.GetFileName(moduleDirectoryPaths[index]))) {
                    Directory.Delete(moduleDirectoryPaths[index], true);
                }
            }
        }

        /// <summary>
        /// Describes every input that changes the compiled Play modules for one platform and environment.
        /// </summary>
        /// <param name="platformId">Platform whose gameplay symbols are defined.</param>
        /// <param name="environmentId">Selected project environment, or empty when none is selected.</param>
        /// <returns>Fingerprint inputs: manifests by content, sources by stamp, engine assemblies by content, and symbols.</returns>
        EditorScriptBuildInputs DescribeBuildInputs(string platformId, string environmentId) {
            string assetsRootPath = Path.Combine(ProjectRootPath, AssetsFolderName);
            string[] manifestFilePaths = Directory.Exists(assetsRootPath)
                ? Directory.GetFiles(assetsRootPath, ModuleManifestFileName, SearchOption.AllDirectories)
                : Array.Empty<string>();
            IReadOnlyList<string> gameplaySymbols = EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId, environmentId);
            List<string> tokens = new List<string>(gameplaySymbols.Count + EngineAssemblyPaths.Count + 3) {
                FingerprintVersionToken,
                "platform=" + platformId,
                "environment=" + environmentId
            };
            for (int index = 0; index < gameplaySymbols.Count; index++) {
                tokens.Add("symbol=" + gameplaySymbols[index]);
            }

            // Engine assemblies are identified by content, not timestamp: an engine rebuild that changes nothing
            // rewrites byte-identical assemblies and must not trigger a script rebuild.
            for (int index = 0; index < EngineAssemblyPaths.Count; index++) {
                string engineAssemblyPath = Path.GetFullPath(EngineAssemblyPaths[index]);
                tokens.Add("engine=" + engineAssemblyPath + "|" + EditorScriptBuildFingerprint.DescribeFileContent(engineAssemblyPath));
            }

            return new EditorScriptBuildInputs(
                manifestFilePaths,
                new[] { assetsRootPath },
                Array.Empty<string>(),
                tokens);
        }

        /// <summary>
        /// Checks that the module root exists and holds the assembly of every runtime module the project declares.
        /// </summary>
        /// <param name="runtimeModulesRootPath">Runtime module root laid out as <c>&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</param>
        /// <returns>True when a previous build left every expected runtime module assembly in place.</returns>
        bool AllRuntimeModuleAssembliesExist(string runtimeModulesRootPath) {
            if (!Directory.Exists(runtimeModulesRootPath)) {
                return false;
            }

            EditorCodeModuleManifestEntry[] modules = ModuleManifestService.Load().Modules;
            for (int index = 0; index < modules.Length; index++) {
                if (modules[index].ModuleKind != EditorCodeModuleKind.Runtime) {
                    continue;
                }

                string moduleId = modules[index].ModuleId;
                if (!File.Exists(EditorPlayModuleLayout.ResolveModuleAssemblyPath(runtimeModulesRootPath, moduleId))) {
                    return false;
                }
            }

            return true;
        }
    }
}

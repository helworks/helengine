using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor {
    /// <summary>
    /// Cooks ordered build scenes and their dependent runtime assets into packaged build-graph outputs.
    /// </summary>
    internal sealed class EditorPlatformAssetCookService {
        readonly string ProjectRootPath;
        readonly string RequiredEngineVersion;
        readonly string ProjectId;
        readonly string ProjectVersion;
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;
        readonly FontAsset DefaultFontAsset;
        readonly AssetFileHasher FileHasher;
        readonly IScriptTypeResolver ScriptTypeResolver;
        readonly EditorProjectSceneCatalogService SceneCatalogService;
        readonly EditorProfileSettingsService ProfileSettingsService;
        readonly EditorStandardPlatformInputConfigurationFactory StandardPlatformInputConfigurationFactory;
        readonly EditorBuiltInShaderAssetLibrary BuiltInShaderAssetLibrary;

        /// <summary>
        /// Initializes one asset-cook service for the supplied project and optional script resolver.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        /// <param name="requiredEngineVersion">Exact engine version required by the current project build.</param>
        /// <param name="projectId">Stable project identifier reported to platform builders.</param>
        /// <param name="projectVersion">Human-visible project version reported to platform builders.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="defaultFontAsset">Default font asset packaged for player builds.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <param name="fileHasher">Optional file hasher override used by tests.</param>
        /// <param name="builtInShaderAssetLibrary">Session-owned built-in shader library used for shader source resolution.</param>
        public EditorPlatformAssetCookService(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver,
            AssetFileHasher fileHasher,
            EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            RequiredEngineVersion = requiredEngineVersion ?? throw new ArgumentNullException(nameof(requiredEngineVersion));
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            ProjectVersion = projectVersion ?? throw new ArgumentNullException(nameof(projectVersion));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            DefaultFontAsset = defaultFontAsset;
            ScriptTypeResolver = scriptTypeResolver;
            FileHasher = fileHasher ?? new AssetFileHasher(ProjectRootPath);
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
            ProfileSettingsService = new EditorProfileSettingsService(ProjectRootPath);
            StandardPlatformInputConfigurationFactory = new EditorStandardPlatformInputConfigurationFactory();
            BuiltInShaderAssetLibrary = builtInShaderAssetLibrary ?? throw new ArgumentNullException(nameof(builtInShaderAssetLibrary));
        }

        public PlatformBuildManifest Cook(
            PlatformDefinition platformDefinition,
            IReadOnlyList<string> orderedSceneIds,
            string outputRootPath,
            IReadOnlyList<string> targetIds,
            IPlatformAssetBuilder materialBuilder = null,
            string selectedBuildProfileId = "",
            string selectedGraphicsProfileId = "",
            IReadOnlyDictionary<string, string> scenePathOverrides = null,
            string selectedEnvironmentId = "",
            IReadOnlyDictionary<string, string> selectedGraphicsOptionValues = null,
            PlatformVideoMode playVideoMode = null,
            NumericPrecisionPolicy numericPolicy = null,
            PlatformCookContext cookContext = null,
            EditorBuiltInShaderAssetLibrary cookShaderLibrary = null) {
            numericPolicy ??= NumericPrecisionPolicy.Default;
            RealBinaryCodec.ValidatePolicy(numericPolicy);
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (orderedSceneIds.Count == 0) {
                throw new ArgumentException("At least one ordered scene id must be provided.", nameof(orderedSceneIds));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (targetIds == null) {
                throw new ArgumentNullException(nameof(targetIds));
            }

            if (materialBuilder is IPlatformCookContextProvider && (cookContext == null || cookShaderLibrary == null)) {
                throw new InvalidOperationException("SDK-dependent asset cooking requires the captured context and its build-owned shader library.");
            }
            PlatformCookContext effectiveCookContext = cookContext ?? new PlatformCookContext(platformDefinition.PlatformId);
            if (!string.Equals(effectiveCookContext.PlatformId, platformDefinition.PlatformId, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Asset cooking requires the selected platform's captured context.");
            }
            EditorBuiltInShaderAssetLibrary effectiveShaderLibrary = cookShaderLibrary ?? BuiltInShaderAssetLibrary;
            Console.WriteLine("[cook] context=" + effectiveCookContext.CacheIdentity);
            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            string effectiveExecutionRootPath = ResolveCookExecutionRootPath(fullOutputRootPath);
            string effectiveCookRootPath = ResolveCookRootPath(fullOutputRootPath);
            Directory.CreateDirectory(effectiveExecutionRootPath);
            IPlatformAssetBuilder effectiveMaterialBuilder = ResolveEffectiveMaterialBuilder(materialBuilder);
            string platformName = ResolvePlatformName(platformDefinition, materialBuilder);
            EditorPlatformProfileSettingsDocument platformProfile = ResolvePlatformProfile(platformName);
            string platformVersion = ResolvePlatformVersion(platformProfile);
            StandardPlatformInputConfiguration standardInputConfiguration = StandardPlatformInputConfigurationFactory.Create(platformProfile);

            PlatformLayoutTarget layoutTarget = null;
            if (materialBuilder is IPlatformLayoutTargetProvider layoutProvider) {
                if (selectedGraphicsOptionValues == null) {
                    throw new InvalidOperationException("Layout target resolution requires captured effective graphics option values.");
                }
                PlatformVideoMode selectedVideoMode = playVideoMode;
                if (selectedVideoMode == null && platformDefinition.VideoModeCapability != null) {
                    string settingId = platformDefinition.VideoModeCapability.SettingId;
                    if (!selectedGraphicsOptionValues.TryGetValue(settingId, out string videoModeId)) {
                        throw new InvalidOperationException($"Layout target resolution requires graphics option '{settingId}'.");
                    }
                    selectedVideoMode = platformDefinition.VideoModeCapability.Resolve(videoModeId, platformDefinition.PlatformId);
                }
                layoutTarget = layoutProvider.ResolveLayoutTarget(new PlatformLayoutTargetRequest(
                    selectedBuildProfileId, selectedGraphicsProfileId, selectedGraphicsOptionValues, selectedVideoMode));
                if (layoutTarget == null) { throw new InvalidOperationException("The platform layout provider returned no presentation target."); }
            }


            List<string> orderedCanonicalScenePaths = ResolveOrderedCanonicalScenePaths(orderedSceneIds);
            List<string> orderedSceneIdentityPaths = ResolvePackagedSceneIdentityPaths(orderedSceneIds, orderedCanonicalScenePaths);
            List<string> orderedScenePaths = ResolveOrderedScenePaths(orderedSceneIds, scenePathOverrides);
            EditorScenePackageCache scenePackageCache = new(ProjectRootPath);
            System.Diagnostics.Stopwatch scenePackageTimer = System.Diagnostics.Stopwatch.StartNew();
            EditorScenePackageKey scenePackageKey = scenePackageCache.ComputeKey(
                platformDefinition,
                effectiveMaterialBuilder,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                selectedEnvironmentId,
                orderedSceneIdentityPaths,
                orderedScenePaths,
                Importers,
                DefaultFontAsset,
                ScriptTypeResolver,
                effectiveShaderLibrary, layoutTarget, numericPolicy, effectiveCookContext);
            long fingerprintMilliseconds = scenePackageTimer.ElapsedMilliseconds;
            int hashedInputCount = scenePackageCache.LastHashedInputCount;
            int reusedInputCount = scenePackageCache.LastReusedInputCount;
            bool scenePackageCacheHit = scenePackageCache.TryRestore(
                scenePackageKey.Key,
                effectiveExecutionRootPath,
                out EditorPlatformBuildScenePackagerResult packagerResult);
            if (!scenePackageCacheHit) {
                using EditorPlatformBuildScenePackager packager = new(
                    ProjectRootPath,
                    Importers,
                    platformDefinition,
                    DefaultFontAsset,
                    effectiveMaterialBuilder,
                    selectedBuildProfileId,
                    selectedGraphicsProfileId,
                    effectiveShaderLibrary,
                    ScriptTypeResolver,
                    selectedEnvironmentId);
                packager.LayoutTarget = layoutTarget;
                packager.NumericPolicy = numericPolicy;
                packagerResult = packager.PackagePreservingIdentityPaths(
                    orderedSceneIdentityPaths,
                    orderedScenePaths,
                    effectiveExecutionRootPath);
                scenePackageCache.Store(scenePackageKey.Key, effectiveExecutionRootPath, packagerResult);

                // A fresh identity reconcile during packaging can give a source that had none its <source>.hmeta. The
                // key above was taken before that write, so the next Play would never hit it. Re-key after packaging
                // and also store the entry under the post-packaging key when the only differences are newly created
                // identity sidecars of unchanged sources. Import-settings sidecars hold authored data only and imports
                // never rewrite an unchanged one, so any other difference means the project changed during packaging,
                // and the output is then kept only under the inputs it was packaged from.
                // Behaviour change, intended: the post-packaging key computation runs the same verified reads and
                // reparse-point rejection as the first one, so if it fails the whole cook fails even though packaging
                // succeeded. A project that changed into an unsafe state during packaging must not be cooked or cached.
                EditorScenePackageKey packagedKey = scenePackageCache.ComputeKey(
                    platformDefinition,
                    effectiveMaterialBuilder,
                    selectedBuildProfileId,
                    selectedGraphicsProfileId,
                    selectedEnvironmentId,
                    orderedSceneIdentityPaths,
                    orderedScenePaths,
                    Importers,
                    DefaultFontAsset,
                    ScriptTypeResolver,
                    effectiveShaderLibrary, layoutTarget, numericPolicy, effectiveCookContext);
                if (scenePackageKey.DiffersOnlyByCreatedIdentitySidecars(packagedKey)) {
                    scenePackageCache.Store(packagedKey.Key, effectiveExecutionRootPath, packagerResult);
                    Console.WriteLine("[cook] scene-package-cache stored-post-packaging-key (packaging created identity sidecars)");
                } else if (!string.Equals(scenePackageKey.Key, packagedKey.Key, StringComparison.Ordinal)) {
                    Console.WriteLine("[cook] scene-package-cache inputs changed during packaging; post-packaging key not stored");
                }
            }
            Console.WriteLine($"[cook] scene-package-cache {(scenePackageCacheHit ? "hit" : "miss")} fingerprint-ms={fingerprintMilliseconds} inputs-hashed={hashedInputCount} inputs-reused={reusedInputCount} total-ms={scenePackageTimer.ElapsedMilliseconds}");
            PlatformCookWorkItem[] platformCookWorkItems = [.. packagerResult.PlatformCookWorkItems];
            System.Diagnostics.Stopwatch cookStageTimer = System.Diagnostics.Stopwatch.StartNew();
            PlatformCookedArtifactDeclaration[] cookedArtifactDeclarations = CookPlatformShaderArtifacts(
                materialBuilder,
                platformDefinition,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                effectiveCookRootPath,
                packagerResult,
                effectiveShaderLibrary,
                numericPolicy);
            Console.WriteLine($"[cook] shader-artifacts-ms={cookStageTimer.ElapsedMilliseconds}");

            Console.WriteLine("[helengine-editor] build scene entries begin");
            cookStageTimer.Restart();
            PlatformBuildScene[] scenes = BuildSceneEntries(orderedSceneIds, orderedSceneIdentityPaths, effectiveCookRootPath, packagerResult.SceneCookedDependencies, numericPolicy);
            Console.WriteLine("[helengine-editor] build scene entries completed");
            Console.WriteLine($"[cook] scene-entries-ms={cookStageTimer.ElapsedMilliseconds}");
            Console.WriteLine("[helengine-editor] build cooked artifacts begin");
            cookStageTimer.Restart();
            PlatformBuildArtifact[] cookedArtifacts = BuildCookedArtifacts(
                effectiveCookRootPath,
                targetIds,
                platformCookWorkItems,
                cookedArtifactDeclarations,
                effectiveExecutionRootPath,
                platformDefinition.RuntimeGenerationContract.SupportsSceneCodeModules
                    ? packagerResult.SceneCookedDependencies.Values.SelectMany(paths => paths).ToArray() : [], numericPolicy);
            Console.WriteLine("[helengine-editor] build cooked artifacts completed");
            Console.WriteLine($"[cook] artifact-manifest-ms={cookStageTimer.ElapsedMilliseconds}");

            PlatformBuildManifest manifest = new PlatformBuildManifest(
                2,
                ProjectId,
                ProjectVersion,
                RequiredEngineVersion,
                platformName,
                platformVersion,
                orderedSceneIds[0],
                scenes,
                Array.Empty<PlatformBuildAsset>(),
                cookedArtifacts,
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()),
                platformCookWorkItems,
                PlatformBuildRuntimeFeatureManifest.Empty, numericPolicy);
            manifest.StandardPlatformInputConfiguration = standardInputConfiguration;
            manifest.CookContext = effectiveCookContext;
            return manifest;
        }

        /// <summary>
        /// Resolves authored shader sources and invokes only the selected platform's explicit shader artifact capability.
        /// </summary>
        /// <param name="materialBuilder">Loaded platform builder selected for the cook.</param>
        /// <param name="platformDefinition">Selected platform definition used to resolve the stable platform id.</param>
        /// <param name="selectedBuildProfileId">Selected build profile identifier.</param>
        /// <param name="selectedGraphicsProfileId">Selected graphics profile identifier.</param>
        /// <param name="cookRootPath">Absolute cooked-content root receiving platform shader outputs.</param>
        /// <param name="packagerResult">Scene packaging output that reports material-selected shader dependencies.</param>
        /// <returns>All pre-existing and shader-capability-declared cooked artifacts.</returns>
        PlatformCookedArtifactDeclaration[] CookPlatformShaderArtifacts(
            IPlatformAssetBuilder materialBuilder,
            PlatformDefinition platformDefinition,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            string cookRootPath,
            EditorPlatformBuildScenePackagerResult packagerResult,
            EditorBuiltInShaderAssetLibrary shaderLibrary,
            NumericPrecisionPolicy numericPolicy) {
            if (packagerResult == null) {
                throw new ArgumentNullException(nameof(packagerResult));
            }

            List<PlatformCookedArtifactDeclaration> declarations = new(packagerResult.CookedArtifactDeclarations);
            IReadOnlyList<PlatformShaderDependency> shaderDependencies = packagerResult.ReferencedShaderDependencies;
            bool rendererDependencyProvider = materialBuilder is IPlatformRendererShaderDependencyProvider;
            if (rendererDependencyProvider) {
                IPlatformRendererShaderDependencyProvider provider = (IPlatformRendererShaderDependencyProvider)materialBuilder;
                shaderDependencies = EditorRendererShaderDependencyResolver.Merge(
                    packagerResult.ReferencedShaderDependencies,
                    provider.RendererShaderDependencies);
            }

            if (rendererDependencyProvider
                && shaderDependencies.Count > 0
                && materialBuilder is not IPlatformShaderArtifactBuilder) {
                throw new InvalidOperationException("Platform builders that declare renderer shader dependencies must provide shader artifact cooking.");
            }

            if (materialBuilder is not IPlatformShaderArtifactBuilder shaderArtifactBuilder
                || shaderDependencies.Count == 0) {
                return declarations.ToArray();
            }

            if (shaderLibrary == null) {
                throw new InvalidOperationException("Shader source cooking requires the owning built-in shader library.");
            }
            EditorProjectShaderSourceResolver sourceResolver = new(Path.Combine(ProjectRootPath, "assets"), shaderLibrary);
            IReadOnlyList<string> referencedShaderAssetIds = shaderDependencies
                .Select(dependency => dependency.ShaderAssetId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            IReadOnlyList<EditorProjectShaderSource> resolvedSources = sourceResolver.Resolve(referencedShaderAssetIds);
            PlatformShaderArtifactCookSource[] shaderSources = new PlatformShaderArtifactCookSource[resolvedSources.Count];
            for (int index = 0; index < resolvedSources.Count; index++) {
                EditorProjectShaderSource resolvedSource = resolvedSources[index];
                shaderSources[index] = new PlatformShaderArtifactCookSource(
                    resolvedSource.ShaderAssetId,
                    resolvedSource.SourceHash,
                    resolvedSource.SourceText,
                    resolvedSource.SourcePath);
            }

            PlatformShaderArtifactCookRequest shaderRequest = new PlatformShaderArtifactCookRequest(
                cookRootPath,
                ResolvePlatformName(platformDefinition, materialBuilder),
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                shaderDependencies,
                shaderSources,
                numericPolicy);
            PlatformShaderArtifactCookResult shaderResult = shaderArtifactBuilder.CookShaderArtifacts(shaderRequest)
                ?? throw new InvalidOperationException("Platform shader artifact builders must return an explicit cook result.");
            declarations.AddRange(shaderResult.CookedArtifactDeclarations);
            return declarations.ToArray();
        }

        /// <summary>
        /// Resolves the normalized profile for one platform without writing project settings.
        /// The cook only reads profiles; persisting them belongs to explicit profile saves.
        /// </summary>
        /// <param name="platformId">Stable platform identifier whose profile should be resolved.</param>
        /// <returns>Normalized profile document for the requested platform, with in-memory defaults when no file exists.</returns>
        EditorPlatformProfileSettingsDocument ResolvePlatformProfile(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorProfileSettingsDocument profileSettings = ProfileSettingsService.Resolve(new[] { platformId });
            for (int index = 0; index < profileSettings.Platforms.Count; index++) {
                EditorPlatformProfileSettingsDocument platformSettings = profileSettings.Platforms[index];
                if (platformSettings != null
                    && string.Equals(platformSettings.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platformSettings;
                }
            }

            throw new InvalidOperationException($"Platform profile '{platformId}' could not be resolved.");
        }

        /// <summary>
        /// Returns the builder instance that should own material cooking for the current build.
        /// </summary>
        /// <param name="materialBuilder">Builder loaded for the active platform.</param>
        /// <returns>The builder when it publishes material schemas; otherwise null to keep top-level material packaging active.</returns>
        static IPlatformAssetBuilder ResolveEffectiveMaterialBuilder(IPlatformAssetBuilder materialBuilder) {
            if (materialBuilder == null) {
                return null;
            }

            PlatformDefinition definition = materialBuilder.Definition;
            if (definition == null || definition.MaterialSchemas == null || definition.MaterialSchemas.Length == 0) {
                return null;
            }

            return materialBuilder;
        }

        /// <summary>
        /// Resolves the stable platform identifier that should be stamped into the cooked manifest.
        /// </summary>
        /// <param name="platformDefinition">Resolved platform definition selected for the current build.</param>
        /// <param name="builder">Loaded platform builder used by the build graph.</param>
        /// <returns>Stable platform identifier reported by the selected builder.</returns>
        static string ResolvePlatformName(PlatformDefinition platformDefinition, IPlatformAssetBuilder builder) {
            if (builder?.Descriptor != null && !string.IsNullOrWhiteSpace(builder.Descriptor.TargetPlatformId)) {
                return builder.Descriptor.TargetPlatformId;
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (string.IsNullOrWhiteSpace(platformDefinition.PlatformId)) {
                throw new InvalidOperationException("Platform definition must declare a platform id.");
            }

            return platformDefinition.PlatformId;
        }

        /// <summary>
        /// Resolves the profile-stamped platform version that should be reported by the running artifact.
        /// </summary>
        /// <param name="platform">Resolved profile of the platform being cooked.</param>
        /// <returns>Profile-stamped runtime platform version string.</returns>
        static string ResolvePlatformVersion(EditorPlatformProfileSettingsDocument platform) {
            if (platform == null) {
                throw new ArgumentNullException(nameof(platform));
            }

            return string.IsNullOrWhiteSpace(platform.Version)
                ? EditorPlatformProfileSettingsDocument.DefaultVersion
                : platform.Version.Trim();
        }

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, IReadOnlyList<string> orderedSceneIdentityPaths, string cookRootPath, IReadOnlyDictionary<string, string[]> sceneCookedDependencies, NumericPrecisionPolicy numericPolicy) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (orderedSceneIdentityPaths == null) {
                throw new ArgumentNullException(nameof(orderedSceneIdentityPaths));
            }
            if (orderedSceneIds.Count != orderedSceneIdentityPaths.Count) {
                throw new InvalidOperationException("Ordered scene ids and canonical authored scene paths must contain the same number of entries.");
            }

            PlatformBuildScene[] scenes = new PlatformBuildScene[orderedSceneIds.Count];
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                string canonicalScenePath = orderedSceneIdentityPaths[index];
                string cookedRelativePath = BuildCookedSceneRelativePath(canonicalScenePath, index);
                uint physics3DSceneFeatureFlags = ReadCookedScenePhysics3DFeatureFlags(cookRootPath, cookedRelativePath, numericPolicy);
                string automaticRuntimeComponentTypeIds = ReadCookedSceneAutomaticRuntimeComponentTypeIds(cookRootPath, cookedRelativePath, ScriptTypeResolver, numericPolicy);
                if (!sceneCookedDependencies.TryGetValue(canonicalScenePath, out string[] dependencies) || !dependencies.Contains(cookedRelativePath, StringComparer.Ordinal)) {
                    throw new InvalidOperationException($"Cooked dependency selection is incomplete for scene '{sceneId}'.");
                }
                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    SceneIdUtility.FromPath(canonicalScenePath),
                    cookedRelativePath,
                    [new PlatformBuildPayloadReference(cookedRelativePath, cookedRelativePath)],
                    [
                        new KeyValuePair<string, string>("build-order-index", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, cookedRelativePath),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, physics3DSceneFeatureFlags.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds, automaticRuntimeComponentTypeIds),
                        new KeyValuePair<string, string>("cooked-dependency-paths", System.Text.Json.JsonSerializer.Serialize(dependencies))
                    ]);
            }

            return scenes;
        }

        /// <summary>
        /// Resolves the canonical authored identity paths for the supplied scene ids, allowing synthetic scenes to identify themselves.
        /// </summary>
        /// <param name="orderedSceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Canonical authored identity paths in build order.</returns>
        List<string> ResolveOrderedCanonicalScenePaths(IReadOnlyList<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            List<string> orderedCanonicalScenePaths = new List<string>(orderedSceneIds.Count);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                orderedCanonicalScenePaths.Add(string.Equals(sceneId, EngineSceneIdentifiers.GeneratedBootSceneId, StringComparison.Ordinal)
                    ? sceneId
                    : SceneCatalogService.ResolveScenePath(sceneId));
            }

            return orderedCanonicalScenePaths;
        }

        /// <summary>
        /// Resolves the authored project-relative scene paths for the supplied stable scene ids.
        /// </summary>
        /// <param name="orderedSceneIds">Stable scene ids selected for the build.</param>
        /// <param name="scenePathOverrides">Optional per-scene authored path overrides keyed by stable scene id.</param>
        /// <returns>Project-relative authored scene paths in build order.</returns>
        List<string> ResolveOrderedScenePaths(IReadOnlyList<string> orderedSceneIds, IReadOnlyDictionary<string, string> scenePathOverrides) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            List<string> orderedScenePaths = new List<string>(orderedSceneIds.Count);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                if (scenePathOverrides != null && scenePathOverrides.TryGetValue(sceneId, out string overriddenScenePath)) {
                    if (string.IsNullOrWhiteSpace(overriddenScenePath)) {
                        throw new InvalidOperationException($"Scene path override for scene '{sceneId}' must be a non-empty project-relative asset path.");
                    }

                    orderedScenePaths.Add(overriddenScenePath);
                    continue;
                }

                orderedScenePaths.Add(SceneCatalogService.ResolveScenePath(sceneId));
            }

            return orderedScenePaths;
        }

        /// <summary>
        /// Resolves the packaged scene identity paths that should control runtime cooked scene names for the supplied build order.
        /// </summary>
        /// <param name="orderedSceneIds">Stable scene ids selected for the build.</param>
        /// <param name="orderedCanonicalScenePaths">Canonical authored scene paths resolved from the project catalog.</param>
        /// <returns>Packaged scene identity paths used to derive cooked scene output names.</returns>
        static List<string> ResolvePackagedSceneIdentityPaths(IReadOnlyList<string> orderedSceneIds, IReadOnlyList<string> orderedCanonicalScenePaths) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (orderedCanonicalScenePaths == null) {
                throw new ArgumentNullException(nameof(orderedCanonicalScenePaths));
            }
            if (orderedSceneIds.Count != orderedCanonicalScenePaths.Count) {
                throw new InvalidOperationException("Ordered scene ids and canonical authored scene paths must contain the same number of entries.");
            }

            List<string> packagedSceneIdentityPaths = new List<string>(orderedSceneIds.Count);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                packagedSceneIdentityPaths.Add(ResolvePackagedSceneIdentityPath(orderedSceneIds[index], orderedCanonicalScenePaths[index]));
            }

            return packagedSceneIdentityPaths;
        }

        /// <summary>
        /// Resolves the packaged scene identity path that should own one cooked scene output name.
        /// </summary>
        /// <param name="sceneId">Stable scene id selected for the build.</param>
        /// <param name="canonicalScenePath">Canonical authored scene path resolved from the project catalog.</param>
        /// <returns>Packaged scene identity path used to derive the cooked runtime asset path.</returns>
        static string ResolvePackagedSceneIdentityPath(string sceneId, string canonicalScenePath) {
            if (string.Equals(sceneId, EngineSceneIdentifiers.GeneratedBootSceneId, StringComparison.Ordinal)) {
                return sceneId;
            }

            return canonicalScenePath;
        }

        /// <summary>
        /// Reads the compact 3D physics scene feature mask stored in one cooked scene asset.
        /// </summary>
        /// <param name="outputRootPath">Cooked output root path.</param>
        /// <param name="cookedRelativePath">Runtime-relative cooked scene payload path.</param>
        /// <returns>Compact 3D physics scene feature mask embedded in the cooked scene asset.</returns>
        static uint ReadCookedScenePhysics3DFeatureFlags(string cookRootPath, string cookedRelativePath, NumericPrecisionPolicy numericPolicy) {
            if (string.IsNullOrWhiteSpace(cookRootPath)) {
                throw new ArgumentException("Cook root path must be provided.", nameof(cookRootPath));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            string normalizedCookedRelativePath = NormalizeCookedRelativePath(cookedRelativePath);
            string fullScenePath = Path.Combine(cookRootPath, normalizedCookedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullScenePath;
                using FileStream stream = File.OpenRead(fullScenePath);
                Asset asset = DeserializeSerializedArtifact(stream, numericPolicy);
                if (asset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' did not deserialize into a SceneAsset.");
                }

                return sceneAsset.Physics3DSceneFeatureFlags;
            } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(cookedRelativePath, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' at '{fullScenePath}' could not be read for physics feature discovery.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Reads the semicolon-delimited automatic runtime component type ids referenced by one cooked scene.
        /// </summary>
        /// <param name="cookRootPath">Absolute cook root that owns the cooked scene payloads.</param>
        /// <param name="cookedRelativePath">Runtime-relative cooked scene path beginning with <c>cooked/</c>.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for gameplay component discovery.</param>
        /// <returns>Semicolon-delimited automatic runtime component type ids referenced by the cooked scene.</returns>
        static string ReadCookedSceneAutomaticRuntimeComponentTypeIds(
            string cookRootPath,
            string cookedRelativePath,
            IScriptTypeResolver scriptTypeResolver, NumericPrecisionPolicy numericPolicy) {
            if (string.IsNullOrWhiteSpace(cookRootPath)) {
                throw new ArgumentException("Cook root path must be provided.", nameof(cookRootPath));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            string normalizedCookedRelativePath = NormalizeCookedRelativePath(cookedRelativePath);
            string fullScenePath = Path.Combine(cookRootPath, normalizedCookedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            IReadOnlyList<Type> componentTypes = EditorGeneratedCoreRegenerationService.DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(
                [fullScenePath],
                scriptTypeResolver, numericPolicy);
            if (componentTypes.Count == 0) {
                return string.Empty;
            }

            string[] componentTypeIds = new string[componentTypes.Count];
            for (int index = 0; index < componentTypes.Count; index++) {
                componentTypeIds[index] = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(componentTypes[index]);
            }

            return string.Join(";", componentTypeIds);
        }

        PlatformBuildArtifact[] BuildCookedArtifacts(
            string cookRootPath,
            IReadOnlyList<string> targetIds,
            IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems,
            IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations,
            string executionRootPath,
            IReadOnlyList<string> selectedDependencyPaths, NumericPrecisionPolicy numericPolicy) {
            if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            } else if (cookedArtifactDeclarations == null) {
                throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
            }

            string variantId = targetIds.Count == 1 && !string.IsNullOrWhiteSpace(targetIds[0])
                ? targetIds[0]
                : "shared";

            EditorPlatformCookedArtifactPool artifactPool = new(ProjectRootPath, FileHasher);
            string[] cookedFilePaths = Directory.GetFiles(cookRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(cookedFilePaths, StringComparer.OrdinalIgnoreCase);
            HashSet<string> builderOwnedOutputPaths = BuildBuilderOwnedOutputPathSet(platformCookWorkItems);
            HashSet<string> declaredOutputPaths = AddDeclaredCookedArtifacts(artifactPool, cookRootPath, cookedArtifactDeclarations);

            for (int index = 0; index < cookedFilePaths.Length; index++) {
                string fullPath = cookedFilePaths[index];
                string relativePath = "cooked/" + NormalizeRelativePath(Path.GetRelativePath(cookRootPath, fullPath));
                if (builderOwnedOutputPaths.Contains(relativePath) || declaredOutputPaths.Contains(relativePath)) {
                    continue;
                }

                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(fullPath, relativePath, numericPolicy), variantId);
            }

            string executionPrefix = Path.GetFullPath(executionRootPath) + Path.DirectorySeparatorChar;
            foreach (string selectedPath in selectedDependencyPaths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)) {
                string relativePath = CanonicalPackagedAssetPath.Normalize(selectedPath);
                if (relativePath.StartsWith("cooked/", StringComparison.Ordinal) || builderOwnedOutputPaths.Contains(relativePath)) continue;
                if (relativePath.StartsWith("generated/", StringComparison.Ordinal)) throw new InvalidOperationException("A runtime scene dependency points to a temporary generated cook input: " + relativePath);
                string fullPath = Path.GetFullPath(Path.Combine(executionRootPath, relativePath));
                if (!fullPath.StartsWith(executionPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) {
                    throw new FileNotFoundException("Selected runtime dependency was not materialized by the scene cook.", fullPath);
                }
                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(fullPath, relativePath, numericPolicy), variantId);
            }

            return artifactPool.ToArray();
        }

        /// <summary>
        /// Adds material and shader files whose producer declared their identity before directory scanning can inspect their paths or payloads.
        /// </summary>
        /// <param name="artifactPool">Artifact pool that receives declared files.</param>
        /// <param name="cookRootPath">Absolute cooked-content root containing declared output paths.</param>
        /// <param name="cookedArtifactDeclarations">Material and shader declarations emitted by scene packaging or platform staging.</param>
        /// <returns>Normalized runtime-relative paths that the directory scanner must skip.</returns>
        static HashSet<string> AddDeclaredCookedArtifacts(
            EditorPlatformCookedArtifactPool artifactPool,
            string cookRootPath,
            IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations) {
            if (artifactPool == null) {
                throw new ArgumentNullException(nameof(artifactPool));
            } else if (string.IsNullOrWhiteSpace(cookRootPath)) {
                throw new ArgumentException("Cook root path must be provided.", nameof(cookRootPath));
            } else if (cookedArtifactDeclarations == null) {
                throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
            }

            string fullCookRootPath = Path.GetFullPath(cookRootPath);
            string fullCookRootPrefix = fullCookRootPath.EndsWith(Path.DirectorySeparatorChar)
                ? fullCookRootPath
                : fullCookRootPath + Path.DirectorySeparatorChar;
            HashSet<string> declaredOutputPaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < cookedArtifactDeclarations.Count; index++) {
                PlatformCookedArtifactDeclaration declaration = cookedArtifactDeclarations[index];
                if (declaration == null) {
                    throw new InvalidOperationException("Cooked artifact declarations cannot contain null entries.");
                }

                string relativeCookPath = NormalizeCookedRelativePath(declaration.RelativePath);
                string fullArtifactPath = Path.GetFullPath(Path.Combine(fullCookRootPath, relativeCookPath));
                if (!fullArtifactPath.StartsWith(fullCookRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Declared cooked artifact '{declaration.RelativePath}' resolves outside cook root '{fullCookRootPath}'.");
                } else if (!File.Exists(fullArtifactPath)) {
                    throw new FileNotFoundException($"Declared cooked artifact '{declaration.RelativePath}' was not written before manifest collection.", fullArtifactPath);
                } else if (!declaredOutputPaths.Add(declaration.RelativePath)) {
                    throw new InvalidOperationException($"Cooked artifact '{declaration.RelativePath}' was declared more than once.");
                }

                artifactPool.AddDeclaredFile(fullArtifactPath, declaration);
            }

            return declaredOutputPaths;
        }

        /// <summary>
        /// Builds the set of cooked output paths that will be produced later by builder-owned platform cook work items.
        /// </summary>
        /// <param name="platformCookWorkItems">Builder-owned platform cook work items emitted by the editor build graph.</param>
        /// <returns>Normalized runtime-relative output paths owned by the builder.</returns>
        static HashSet<string> BuildBuilderOwnedOutputPathSet(IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems) {
            if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            }

            HashSet<string> builderOwnedOutputPaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < platformCookWorkItems.Count; index++) {
                PlatformCookWorkItem workItem = platformCookWorkItems[index];
                if (workItem == null || string.IsNullOrWhiteSpace(workItem.OutputRelativePath)) {
                    continue;
                }

                builderOwnedOutputPaths.Add(workItem.OutputRelativePath.Replace('\\', '/'));
            }

            return builderOwnedOutputPaths;
        }

        static string ResolveCookExecutionRootPath(string outputRootPath) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            if (Path.GetFileName(outputRootPath).Equals("cooked", StringComparison.OrdinalIgnoreCase)) {
                string? parentDirectoryPath = Directory.GetParent(outputRootPath)?.FullName;
                if (string.IsNullOrWhiteSpace(parentDirectoryPath)) {
                    throw new InvalidOperationException($"Cook root '{outputRootPath}' does not have a parent execution root.");
                }

                return parentDirectoryPath;
            }

            return outputRootPath;
        }

        static string ResolveCookRootPath(string outputRootPath) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            if (Path.GetFileName(outputRootPath).Equals("cooked", StringComparison.OrdinalIgnoreCase)) {
                return outputRootPath;
            }

            return Path.Combine(outputRootPath, "cooked");
        }

        static string BuildCookedSceneRelativePath(string sceneId, int sceneIndex) {
            return PackagedScenePathResolver.BuildRelativePath(sceneId, sceneIndex);
        }

        static string NormalizeCookedRelativePath(string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            if (cookedRelativePath.StartsWith("cooked/", StringComparison.OrdinalIgnoreCase)) {
                return cookedRelativePath.Substring("cooked/".Length);
            }

            return cookedRelativePath;
        }

        static string ResolveArtifactKind(string fullPath, string relativePath, NumericPrecisionPolicy numericPolicy) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return "asset";
            }

            if (relativePath.StartsWith("cooked/scenes/", StringComparison.OrdinalIgnoreCase)) {
                return "scene";
            }
            if (relativePath.StartsWith("cooked/fonts/", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("/fonts/", StringComparison.OrdinalIgnoreCase)) {
                return "font";
            }
            if (relativePath.StartsWith("cooked/shaders/", StringComparison.OrdinalIgnoreCase)) {
                return "shader";
            }
            string serializedArtifactKind = TryResolveSerializedArtifactKind(fullPath, relativePath, numericPolicy);
            if (!string.IsNullOrWhiteSpace(serializedArtifactKind)) {
                return serializedArtifactKind;
            }
            if (relativePath.Contains("/models/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("cooked/imported/Models/", StringComparison.OrdinalIgnoreCase)) {
                return "model";
            }
            if (relativePath.StartsWith("cooked/imported/", StringComparison.OrdinalIgnoreCase)) {
                return ResolveImportedArtifactKind(fullPath, relativePath, numericPolicy);
            }

            return "asset";
        }

        /// <summary>
        /// Resolves one cooked serialized artifact kind directly from the payload when the runtime path points at a generic cooked asset file.
        /// </summary>
        /// <param name="fullPath">Full cooked artifact path on disk.</param>
        /// <param name="relativePath">Runtime-relative cooked artifact path.</param>
        /// <returns>Resolved serialized artifact kind, or an empty string when payload-based classification should not apply.</returns>
        static string TryResolveSerializedArtifactKind(string fullPath, string relativePath, NumericPrecisionPolicy numericPolicy) {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }
            if (!relativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }
            if (!File.Exists(fullPath)) {
                return string.Empty;
            }

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullPath;
                using FileStream stream = File.OpenRead(fullPath);
                if (stream.ReadByte() != 'H'
                    || stream.ReadByte() != 'E'
                    || stream.ReadByte() != 'L'
                    || stream.ReadByte() != 'E') {
                    return string.Empty;
                }

                stream.Position = 0;
                if (!UsesGenericEditorAssetSerialization(stream)) {
                    return string.Empty;
                }

                Asset asset = DeserializeSerializedArtifact(stream, numericPolicy);
                if (asset is ModelAsset) {
                    return "model";
                }
                if (asset is MaterialAsset) {
                    return "material";
                }
                if (asset is AudioAsset) {
                    return "audio";
                }
                return string.Empty;
            } catch (Exception ex) {
                throw new InvalidOperationException($"Cooked artifact '{relativePath}' at '{fullPath}' could not be classified from serialized content.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        static string ResolveImportedArtifactKind(string fullPath, string relativePath, NumericPrecisionPolicy numericPolicy) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided for imported artifact classification.", nameof(fullPath));
            }
            if (!File.Exists(fullPath)) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' was not found at '{fullPath}' during classification.");
            }

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullPath;
                using FileStream stream = File.OpenRead(fullPath);
                if (!UsesGenericEditorAssetSerialization(stream)) {
                    return "asset";
                }

                Asset asset = DeserializeSerializedArtifact(stream, numericPolicy);
                if (asset is ModelAsset) {
                    return "model";
                }
                if (asset is MaterialAsset) {
                    return "material";
                }
                if (asset is AudioAsset) {
                    return "audio";
                }
                return "asset";
            } catch (Exception ex) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' at '{fullPath}' could not be classified from serialized content.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Deserializes one current cooked asset using the serializer selected by its HELE format id.
        /// Shader-owned materials deliberately use their own current format and are not registered with the generic core serializer.
        /// </summary>
        /// <param name="stream">Seekable stream positioned at the beginning of a serialized payload.</param>
        /// <returns>The decoded asset instance.</returns>
        static Asset DeserializeSerializedArtifact(Stream stream, NumericPrecisionPolicy numericPolicy) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (!stream.CanSeek) {
                throw new InvalidOperationException("Serialized artifact classification requires a seekable stream.");
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            stream.Position = 0;
            if (header.FormatId == ShaderMaterialAssetBinarySerializer.FormatId) {
                return ShaderMaterialAssetBinarySerializer.Deserialize(stream);
            }

            return PackagedAssetBinarySerializer.Deserialize(stream, numericPolicy);
        }

        /// <summary>
        /// Returns whether the supplied cooked asset stream uses the generic HELE editor-asset serializer owned by the main engine repository.
        /// </summary>
        /// <param name="stream">Readable cooked asset stream positioned at the start of the payload.</param>
        /// <returns>True when the payload uses the generic editor-asset serializer; otherwise false.</returns>
        static bool UsesGenericEditorAssetSerialization(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (!stream.CanSeek) {
                throw new InvalidOperationException("Serialized artifact classification requires a seekable stream.");
            }

            long previousPosition = stream.Position;
            try {
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                return header.FormatId == helengine.files.EditorAssetBinarySerializer.FormatId
                    || header.FormatId == ShaderMaterialAssetBinarySerializer.FormatId;
            } finally {
                stream.Position = previousPosition;
            }
        }
    }
}

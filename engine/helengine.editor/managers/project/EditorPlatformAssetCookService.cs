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
        public EditorPlatformAssetCookService(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver = null,
            AssetFileHasher fileHasher = null) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            RequiredEngineVersion = requiredEngineVersion ?? throw new ArgumentNullException(nameof(requiredEngineVersion));
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            ProjectVersion = projectVersion ?? throw new ArgumentNullException(nameof(projectVersion));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            DefaultFontAsset = defaultFontAsset;
            ScriptTypeResolver = scriptTypeResolver;
            FileHasher = fileHasher ?? new AssetFileHasher();
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
            ProfileSettingsService = new EditorProfileSettingsService(ProjectRootPath);
            StandardPlatformInputConfigurationFactory = new EditorStandardPlatformInputConfigurationFactory();
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
            string selectedEnvironmentId = "") {
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

            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            string effectiveExecutionRootPath = ResolveCookExecutionRootPath(fullOutputRootPath);
            string effectiveCookRootPath = ResolveCookRootPath(fullOutputRootPath);
            Directory.CreateDirectory(effectiveExecutionRootPath);
            IPlatformAssetBuilder effectiveMaterialBuilder = ResolveEffectiveMaterialBuilder(materialBuilder);

            EditorPlatformBuildScenePackager packager = new(
                ProjectRootPath,
                Importers,
                platformDefinition,
                DefaultFontAsset,
                effectiveMaterialBuilder,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                ScriptTypeResolver,
                selectedEnvironmentId);
            List<string> orderedCanonicalScenePaths = ResolveOrderedScenePaths(orderedSceneIds, null);
            List<string> orderedSceneIdentityPaths = ResolvePackagedSceneIdentityPaths(orderedSceneIds, orderedCanonicalScenePaths);
            List<string> orderedScenePaths = ResolveOrderedScenePaths(orderedSceneIds, scenePathOverrides);
            EditorPlatformBuildScenePackagerResult packagerResult = packager.PackagePreservingIdentityPaths(
                orderedSceneIdentityPaths,
                orderedScenePaths,
                effectiveExecutionRootPath);
            PlatformCookWorkItem[] platformCookWorkItems = [.. packagerResult.PlatformCookWorkItems];
            PlatformCookedArtifactDeclaration[] cookedArtifactDeclarations = CookPlatformShaderArtifacts(
                materialBuilder,
                platformDefinition,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                effectiveCookRootPath,
                packagerResult);

            Console.WriteLine("[helengine-editor] build scene entries begin");
            PlatformBuildScene[] scenes = BuildSceneEntries(orderedSceneIds, orderedSceneIdentityPaths, effectiveCookRootPath);
            Console.WriteLine("[helengine-editor] build scene entries completed");
            Console.WriteLine("[helengine-editor] build cooked artifacts begin");
            PlatformBuildArtifact[] cookedArtifacts = BuildCookedArtifacts(
                effectiveCookRootPath,
                targetIds,
                platformCookWorkItems,
                cookedArtifactDeclarations);
            Console.WriteLine("[helengine-editor] build cooked artifacts completed");

            string platformName = ResolvePlatformName(platformDefinition, materialBuilder);
            string platformVersion = ResolvePlatformVersion(platformName);

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
                platformCookWorkItems);
            manifest.StandardPlatformInputConfiguration = ResolveStandardPlatformInputConfiguration(manifest.PlatformName);
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
            EditorPlatformBuildScenePackagerResult packagerResult) {
            if (packagerResult == null) {
                throw new ArgumentNullException(nameof(packagerResult));
            }

            List<PlatformCookedArtifactDeclaration> declarations = new(packagerResult.CookedArtifactDeclarations);
            if (materialBuilder is not IPlatformShaderArtifactBuilder shaderArtifactBuilder
                || packagerResult.ReferencedShaderDependencies.Count == 0) {
                return declarations.ToArray();
            }

            EditorProjectShaderSourceResolver sourceResolver = new(Path.Combine(ProjectRootPath, "assets"));
            IReadOnlyList<EditorProjectShaderSource> resolvedSources = sourceResolver.Resolve(packagerResult.ReferencedShaderAssetIds);
            PlatformShaderArtifactCookSource[] shaderSources = new PlatformShaderArtifactCookSource[resolvedSources.Count];
            for (int index = 0; index < resolvedSources.Count; index++) {
                EditorProjectShaderSource resolvedSource = resolvedSources[index];
                shaderSources[index] = new PlatformShaderArtifactCookSource(
                    resolvedSource.ShaderAssetId,
                    resolvedSource.SourceHash,
                    resolvedSource.SourceText);
            }

            PlatformShaderArtifactCookRequest shaderRequest = new PlatformShaderArtifactCookRequest(
                cookRootPath,
                ResolvePlatformName(platformDefinition, materialBuilder),
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                packagerResult.ReferencedShaderDependencies,
                shaderSources);
            PlatformShaderArtifactCookResult shaderResult = shaderArtifactBuilder.CookShaderArtifacts(shaderRequest)
                ?? throw new InvalidOperationException("Platform shader artifact builders must return an explicit cook result.");
            declarations.AddRange(shaderResult.CookedArtifactDeclarations);
            return declarations.ToArray();
        }

        /// <summary>
        /// Resolves the runtime standard platform input configuration for the supplied platform id from project-shared profile settings.
        /// </summary>
        /// <param name="platformId">Stable platform identifier whose shared input settings should be loaded.</param>
        /// <returns>Runtime standard platform input configuration resolved from project settings.</returns>
        StandardPlatformInputConfiguration ResolveStandardPlatformInputConfiguration(string platformId) {
            return StandardPlatformInputConfigurationFactory.Create(ResolvePlatformProfile(platformId));
        }

        /// <summary>
        /// Resolves the normalized persisted profile for one platform.
        /// </summary>
        /// <param name="platformId">Stable platform identifier whose profile should be loaded.</param>
        /// <returns>Normalized profile document for the requested platform.</returns>
        EditorPlatformProfileSettingsDocument ResolvePlatformProfile(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorProfileSettingsDocument profileSettings = ProfileSettingsService.Load(new[] { platformId });
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
        /// <param name="platformId">Stable platform identifier whose profile owns the version.</param>
        /// <returns>Profile-stamped runtime platform version string.</returns>
        string ResolvePlatformVersion(string platformId) {
            EditorPlatformProfileSettingsDocument platform = ResolvePlatformProfile(platformId);
            return string.IsNullOrWhiteSpace(platform.Version)
                ? EditorPlatformProfileSettingsDocument.DefaultVersion
                : platform.Version.Trim();
        }

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, IReadOnlyList<string> orderedSceneIdentityPaths, string cookRootPath) {
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
                uint physics3DSceneFeatureFlags = ReadCookedScenePhysics3DFeatureFlags(cookRootPath, cookedRelativePath);
                string automaticRuntimeComponentTypeIds = ReadCookedSceneAutomaticRuntimeComponentTypeIds(cookRootPath, cookedRelativePath, ScriptTypeResolver);
                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    SceneIdUtility.FromPath(canonicalScenePath),
                    cookedRelativePath,
                    [
                        new PlatformBuildPayloadReference(cookedRelativePath, cookedRelativePath)
                    ],
                    [
                        new KeyValuePair<string, string>("build-order-index", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, cookedRelativePath),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, physics3DSceneFeatureFlags.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds, automaticRuntimeComponentTypeIds)
                    ]);
            }

            return scenes;
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
            if (string.Equals(sceneId, PlatformMenuSceneResolver.GeneratedBootSceneId, StringComparison.Ordinal)) {
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
        static uint ReadCookedScenePhysics3DFeatureFlags(string cookRootPath, string cookedRelativePath) {
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
                Asset asset = global::helengine.AssetSerializer.Deserialize(stream);
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
            IScriptTypeResolver scriptTypeResolver) {
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
                scriptTypeResolver);
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
            IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations) {
            if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            } else if (cookedArtifactDeclarations == null) {
                throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
            }

            string variantId = targetIds.Count == 1 && !string.IsNullOrWhiteSpace(targetIds[0])
                ? targetIds[0]
                : "shared";

            EditorPlatformCookedArtifactPool artifactPool = new(FileHasher);
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

                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(fullPath, relativePath), variantId);
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

        static string ResolveArtifactKind(string fullPath, string relativePath) {
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
            string serializedArtifactKind = TryResolveSerializedArtifactKind(fullPath, relativePath);
            if (!string.IsNullOrWhiteSpace(serializedArtifactKind)) {
                return serializedArtifactKind;
            }
            if (relativePath.Contains("/models/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("cooked/imported/Models/", StringComparison.OrdinalIgnoreCase)) {
                return "model";
            }
            if (relativePath.StartsWith("cooked/imported/", StringComparison.OrdinalIgnoreCase)) {
                return ResolveImportedArtifactKind(fullPath, relativePath);
            }

            return "asset";
        }

        /// <summary>
        /// Resolves one cooked serialized artifact kind directly from the payload when the runtime path points at a generic cooked asset file.
        /// </summary>
        /// <param name="fullPath">Full cooked artifact path on disk.</param>
        /// <param name="relativePath">Runtime-relative cooked artifact path.</param>
        /// <returns>Resolved serialized artifact kind, or an empty string when payload-based classification should not apply.</returns>
        static string TryResolveSerializedArtifactKind(string fullPath, string relativePath) {
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

                Asset asset = global::helengine.AssetSerializer.Deserialize(stream);
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

        static string ResolveImportedArtifactKind(string fullPath, string relativePath) {
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

                Asset asset = global::helengine.AssetSerializer.Deserialize(stream);
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

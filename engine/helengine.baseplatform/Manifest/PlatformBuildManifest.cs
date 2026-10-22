namespace helengine.baseplatform.Manifest {

    /// <summary>
    /// Defines the complete resolved manifest a platform builder consumes to cook game content.
    /// </summary>
    public class PlatformBuildManifest {
        /// <summary>
        /// Initializes a fully resolved build manifest with startup-scene metadata, explicit platform name/version values, and runtime feature requirements.
        /// </summary>
        /// <param name="manifestVersion">The manifest schema version.</param>
        /// <param name="projectId">The stable project identity for the build.</param>
        /// <param name="projectVersion">The project version being built.</param>
        /// <param name="requiredEngineVersion">The exact engine version the cooked output targets.</param>
        /// <param name="platformName">The stable target platform identifier stamped into the runtime output.</param>
        /// <param name="platformVersion">The builder-stamped platform version reported by the runtime output.</param>
        /// <param name="startupSceneId">The startup scene chosen by build order.</param>
        /// <param name="scenes">The fully resolved scenes the builder must cook.</param>
        /// <param name="looseAssets">The fully resolved loose assets the builder must cook.</param>
        /// <param name="cookedArtifacts">The cooked runtime artifacts prepared by the build graph.</param>
        /// <param name="codeModules">The authored code modules prepared by the build graph.</param>
        /// <param name="artifactPlacements">The planned physical placements for the cooked artifacts.</param>
        /// <param name="containerWritePlan">The planned container layout for the cooked artifacts.</param>
        /// <param name="platformCookWorkItems">The builder-owned platform cook work items emitted by the build graph.</param>
        /// <param name="runtimeFeatureManifest">The required runtime feature manifest resolved by the editor build graph.</param>
        public PlatformBuildManifest(
            int manifestVersion,
            string projectId,
            string projectVersion,
            string requiredEngineVersion,
            string platformName,
            string platformVersion,
            string startupSceneId,
            PlatformBuildScene[] scenes,
            PlatformBuildAsset[] looseAssets,
            PlatformBuildArtifact[] cookedArtifacts,
            PlatformBuildCodeModule[] codeModules,
            PlatformArtifactPlacement[] artifactPlacements,
            PlatformContainerWritePlan containerWritePlan,
            PlatformCookWorkItem[] platformCookWorkItems,
            PlatformBuildRuntimeFeatureManifest runtimeFeatureManifest) : this(manifestVersion, projectId, projectVersion, requiredEngineVersion, platformName, platformVersion, startupSceneId, scenes, looseAssets, cookedArtifacts, codeModules, artifactPlacements, containerWritePlan, platformCookWorkItems, runtimeFeatureManifest, NumericPrecisionPolicy.Default) { }

        /// <summary>Captures the exact immutable numeric contract for this build artifact.</summary>
        [System.Text.Json.Serialization.JsonConstructor]
        public PlatformBuildManifest(
            int manifestVersion,
            string projectId,
            string projectVersion,
            string requiredEngineVersion,
            string platformName,
            string platformVersion,
            string startupSceneId,
            PlatformBuildScene[] scenes,
            PlatformBuildAsset[] looseAssets,
            PlatformBuildArtifact[] cookedArtifacts,
            PlatformBuildCodeModule[] codeModules,
            PlatformArtifactPlacement[] artifactPlacements,
            PlatformContainerWritePlan containerWritePlan,
            PlatformCookWorkItem[] platformCookWorkItems,
            PlatformBuildRuntimeFeatureManifest runtimeFeatureManifest, NumericPrecisionPolicy numericPolicy) {
            NumericPolicy = numericPolicy ?? throw new ArgumentNullException(nameof(numericPolicy));
            if (manifestVersion < 1) {
                throw new ArgumentOutOfRangeException(nameof(manifestVersion), "Manifest version must be at least 1.");
            } else if (string.IsNullOrWhiteSpace(projectId)) {
                throw new ArgumentException("Project id is required.", nameof(projectId));
            } else if (string.IsNullOrWhiteSpace(projectVersion)) {
                throw new ArgumentException("Project version is required.", nameof(projectVersion));
            } else if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
                throw new ArgumentException("Required engine version is required.", nameof(requiredEngineVersion));
            } else if (string.IsNullOrWhiteSpace(platformName)) {
                throw new ArgumentException("Platform name is required.", nameof(platformName));
            } else if (string.IsNullOrWhiteSpace(platformVersion)) {
                throw new ArgumentException("Platform version is required.", nameof(platformVersion));
            } else if (startupSceneId == null) {
                throw new ArgumentNullException(nameof(startupSceneId), "Startup scene id is required.");
            } else if (scenes == null) {
                throw new ArgumentNullException(nameof(scenes), "Scene collection is required.");
            } else if (Array.Exists(scenes, scene => scene == null)) {
                throw new ArgumentException("Scene collection cannot contain null entries.", nameof(scenes));
            } else if (looseAssets == null) {
                throw new ArgumentNullException(nameof(looseAssets), "Loose asset collection is required.");
            } else if (Array.Exists(looseAssets, asset => asset == null)) {
                throw new ArgumentException("Loose asset collection cannot contain null entries.", nameof(looseAssets));
            } else if (cookedArtifacts == null) {
                throw new ArgumentNullException(nameof(cookedArtifacts), "Cooked artifact collection is required.");
            } else if (Array.Exists(cookedArtifacts, artifact => artifact == null)) {
                throw new ArgumentException("Cooked artifact collection cannot contain null entries.", nameof(cookedArtifacts));
            } else if (codeModules == null) {
                throw new ArgumentNullException(nameof(codeModules), "Code module collection is required.");
            } else if (Array.Exists(codeModules, codeModule => codeModule == null)) {
                throw new ArgumentException("Code module collection cannot contain null entries.", nameof(codeModules));
            } else if (artifactPlacements == null) {
                throw new ArgumentNullException(nameof(artifactPlacements), "Artifact placement collection is required.");
            } else if (Array.Exists(artifactPlacements, artifactPlacement => artifactPlacement == null)) {
                throw new ArgumentException("Artifact placement collection cannot contain null entries.", nameof(artifactPlacements));
            } else if (containerWritePlan == null) {
                throw new ArgumentNullException(nameof(containerWritePlan), "Container write plan is required.");
            } else if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems), "Platform cook work item collection is required.");
            } else if (Array.Exists(platformCookWorkItems, workItem => workItem == null)) {
                throw new ArgumentException("Platform cook work item collection cannot contain null entries.", nameof(platformCookWorkItems));
            } else if (runtimeFeatureManifest == null) {
                throw new ArgumentNullException(nameof(runtimeFeatureManifest), "Runtime feature manifest is required.");
            }

            foreach (PlatformBuildCodeModule module in codeModules) { module.RequireNumericPolicy(NumericPolicy); }
            ManifestVersion = manifestVersion;
            ProjectId = projectId;
            ProjectVersion = projectVersion;
            RequiredEngineVersion = requiredEngineVersion;
            PlatformName = platformName;
            PlatformVersion = platformVersion;
            StartupSceneId = startupSceneId;
            Scenes = [.. scenes];
            LooseAssets = [.. looseAssets];
            CookedArtifacts = [.. cookedArtifacts];
            CodeModules = [.. codeModules];
            ArtifactPlacements = [.. artifactPlacements];
            ContainerWritePlan = containerWritePlan;
            PlatformCookWorkItems = [.. platformCookWorkItems];
            RuntimeFeatureManifest = runtimeFeatureManifest;
        }

        /// <summary>Gets the exact numeric ABI and cooked-storage identity captured by the build.</summary>
        public NumericPrecisionPolicy NumericPolicy { get; }

        /// <summary>Gets or sets managed cook provenance so SDK-dependent builders can reject content cooked with another SDK snapshot; legacy manifests omit this value.</summary>
        public Builders.PlatformCookContext CookContext { get; set; }

        /// <summary>
        /// Gets or sets the engine-owned standard platform action bindings that packaged runtimes should register during startup.
        /// </summary>
        public StandardPlatformInputConfiguration StandardPlatformInputConfiguration { get; set; } = StandardPlatformInputConfiguration.Empty;

        /// <summary>Gets or sets the resolved cooperative loading budget in milliseconds per frame; an indivisible load step may exceed this value.</summary>
        public double SceneLoadingBudgetMilliseconds { get; set; } = 8d;

        /// <summary>Gets or sets the resolved platform text-command reuse metadata budget in bytes; zero disables reuse, independently of the existing command payload storage.</summary>
        public int Render2DTextCommandReuseMemoryBudgetBytes { get; set; }

        /// <summary>Gets or sets whether the platform runtime permits authored shadow casting and receiving across all scenes.</summary>
        public bool ShadowsEnabled { get; set; } = true;

        /// <summary>Gets or sets the complete user-authored runtime settings catalog embedded in the packaged game.</summary>
        public PlatformSettingsConfiguration PlatformSettingsConfiguration { get; set; }

        /// <summary>Gets or sets the optional loading-screen scene id from this manifest's packaged scene catalog.</summary>
        public string LoadingScreenSceneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets the manifest schema version.
        /// </summary>
        public int ManifestVersion { get; }

        /// <summary>
        /// Gets the stable project identity for the build.
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// Gets the project version being built.
        /// </summary>
        public string ProjectVersion { get; }

        /// <summary>
        /// Gets the exact engine version the cooked output targets.
        /// </summary>
        public string RequiredEngineVersion { get; }

        /// <summary>
        /// Gets the stable target platform identifier stamped into the runtime output.
        /// </summary>
        public string PlatformName { get; }

        /// <summary>
        /// Gets the builder-stamped platform version reported by the runtime output.
        /// </summary>
        public string PlatformVersion { get; }

        /// <summary>
        /// Gets the startup scene chosen by build order.
        /// </summary>
        public string StartupSceneId { get; }

        /// <summary>
        /// Gets the fully resolved scenes the builder must cook.
        /// </summary>
        public PlatformBuildScene[] Scenes { get; }

        /// <summary>
        /// Gets the fully resolved loose assets the builder must cook.
        /// </summary>
        public PlatformBuildAsset[] LooseAssets { get; }

        /// <summary>
        /// Gets the cooked runtime artifacts prepared by the build graph.
        /// </summary>
        public PlatformBuildArtifact[] CookedArtifacts { get; }

        /// <summary>
        /// Gets the authored code modules prepared by the build graph.
        /// </summary>
        public PlatformBuildCodeModule[] CodeModules { get; }

        /// <summary>
        /// Gets the planned physical placements for the cooked artifacts.
        /// </summary>
        public PlatformArtifactPlacement[] ArtifactPlacements { get; }

        /// <summary>
        /// Gets the planned container layout for the cooked artifacts.
        /// </summary>
        public PlatformContainerWritePlan ContainerWritePlan { get; }

        /// <summary>
        /// Gets the builder-owned platform cook work items emitted by the build graph.
        /// </summary>
        public PlatformCookWorkItem[] PlatformCookWorkItems { get; }

        /// <summary>
        /// Gets the required runtime feature manifest resolved by the editor build graph.
        /// </summary>
        public PlatformBuildRuntimeFeatureManifest RuntimeFeatureManifest { get; }
    }
}

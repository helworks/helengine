namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes the typed platform metadata a builder exposes to the editor.
/// </summary>
public class PlatformDefinition {
    /// <summary>
    /// Initializes one platform definition.
    /// </summary>
    /// <param name="platformId">Stable platform identifier.</param>
    /// <param name="displayName">Human-readable platform name.</param>
    /// <param name="buildProfiles">Build profiles exposed by the platform.</param>
    /// <param name="graphicsProfiles">Graphics profiles exposed by the platform.</param>
    /// <param name="assetRequirements">Asset requirements exposed by the platform.</param>
    /// <param name="materialSchemas">Material authoring schemas exposed by the platform.</param>
    /// <param name="componentSupportRules">Component support rules exposed by the platform.</param>
    /// <param name="codegenProfiles">Codegen profiles exposed by the platform.</param>
    /// <param name="storageProfiles">Storage/runtime profiles exposed by the platform.</param>
    /// <param name="mediaProfiles">Media profiles exposed by the platform.</param>
    /// <param name="runtimeGenerationContract">Cross-platform runtime-generation behavior exposed by the platform.</param>
    /// <param name="hostDebugCapability">Cross-platform host-debug capability metadata exposed by the platform.</param>
    /// <param name="assetCookCapabilities">Generic asset-kind cook capabilities exposed by the platform.</param>
    /// <param name="componentMemberDefinitions">Platform-specific synthetic component members exposed to editor and runtime packaging flows.</param>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformStorageProfileDefinition[] storageProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles,
        RuntimeGenerationContract runtimeGenerationContract = null,
        PlatformHostDebugCapability hostDebugCapability = null,
        PlatformAssetCookCapabilityDefinition[] assetCookCapabilities = null,
        PlatformComponentMemberDefinition[] componentMemberDefinitions = null) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Platform display name is required.", nameof(displayName));
        } else if (buildProfiles == null) {
            throw new ArgumentNullException(nameof(buildProfiles), "Build profiles are required.");
        } else if (graphicsProfiles == null) {
            throw new ArgumentNullException(nameof(graphicsProfiles), "Graphics profiles are required.");
        } else if (assetRequirements == null) {
            throw new ArgumentNullException(nameof(assetRequirements), "Asset requirements are required.");
        } else if (materialSchemas == null) {
            throw new ArgumentNullException(nameof(materialSchemas), "Material schemas are required.");
        } else if (componentSupportRules == null) {
            throw new ArgumentNullException(nameof(componentSupportRules), "Component support rules are required.");
        } else if (codegenProfiles == null) {
            throw new ArgumentNullException(nameof(codegenProfiles), "Codegen profiles are required.");
        } else if (storageProfiles == null) {
            throw new ArgumentNullException(nameof(storageProfiles), "Storage profiles are required.");
        } else if (mediaProfiles == null) {
            throw new ArgumentNullException(nameof(mediaProfiles), "Media profiles are required.");
        } else if (Array.Exists(buildProfiles, buildProfile => buildProfile == null)) {
            throw new ArgumentException("Build profiles cannot contain null entries.", nameof(buildProfiles));
        } else if (Array.Exists(graphicsProfiles, graphicsProfile => graphicsProfile == null)) {
            throw new ArgumentException("Graphics profiles cannot contain null entries.", nameof(graphicsProfiles));
        } else if (Array.Exists(assetRequirements, assetRequirement => assetRequirement == null)) {
            throw new ArgumentException("Asset requirements cannot contain null entries.", nameof(assetRequirements));
        } else if (Array.Exists(materialSchemas, materialSchema => materialSchema == null)) {
            throw new ArgumentException("Material schemas cannot contain null entries.", nameof(materialSchemas));
        } else if (Array.Exists(componentSupportRules, componentSupportRule => componentSupportRule == null)) {
            throw new ArgumentException("Component support rules cannot contain null entries.", nameof(componentSupportRules));
        } else if (Array.Exists(codegenProfiles, codegenProfile => codegenProfile == null)) {
            throw new ArgumentException("Codegen profiles cannot contain null entries.", nameof(codegenProfiles));
        } else if (Array.Exists(storageProfiles, storageProfile => storageProfile == null)) {
            throw new ArgumentException("Storage profiles cannot contain null entries.", nameof(storageProfiles));
        } else if (Array.Exists(mediaProfiles, mediaProfile => mediaProfile == null)) {
            throw new ArgumentException("Media profiles cannot contain null entries.", nameof(mediaProfiles));
        } else if (assetCookCapabilities != null && Array.Exists(assetCookCapabilities, assetCookCapability => assetCookCapability == null)) {
            throw new ArgumentException("Asset cook capabilities cannot contain null entries.", nameof(assetCookCapabilities));
        } else if (componentMemberDefinitions != null && Array.Exists(componentMemberDefinitions, definition => definition == null)) {
            throw new ArgumentException("Component member definitions cannot contain null entries.", nameof(componentMemberDefinitions));
        }

        PlatformId = platformId;
        DisplayName = displayName;
        BuildProfiles = [.. buildProfiles];
        GraphicsProfiles = [.. graphicsProfiles];
        AssetRequirements = [.. assetRequirements];
        MaterialSchemas = [.. materialSchemas];
        ComponentSupportRules = [.. componentSupportRules];
        CodegenProfiles = [.. codegenProfiles];
        StorageProfiles = [.. storageProfiles];
        MediaProfiles = [.. mediaProfiles];
        RuntimeGenerationContract = runtimeGenerationContract ?? RuntimeGenerationContract.CreateDefault();
        HostDebugCapability = hostDebugCapability ?? PlatformHostDebugCapability.CreateDefault();
        AssetCookCapabilities = assetCookCapabilities == null ? Array.Empty<PlatformAssetCookCapabilityDefinition>() : [.. assetCookCapabilities];
        ComponentMemberDefinitions = componentMemberDefinitions == null ? Array.Empty<PlatformComponentMemberDefinition>() : [.. componentMemberDefinitions];
    }

    /// <summary>
    /// Initializes one platform definition without any media profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformStorageProfileDefinition[] storageProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles,
        RuntimeGenerationContract runtimeGenerationContract)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            componentSupportRules,
            codegenProfiles,
            storageProfiles,
            mediaProfiles,
            runtimeGenerationContract,
            null) {
    }

    /// <summary>
    /// Initializes one platform definition while preserving the pre-asset-cook-capability constructor shape used by existing platform builders.
    /// </summary>
    /// <param name="platformId">Stable platform identifier.</param>
    /// <param name="displayName">Human-readable platform name.</param>
    /// <param name="buildProfiles">Build profiles exposed by the platform.</param>
    /// <param name="graphicsProfiles">Graphics profiles exposed by the platform.</param>
    /// <param name="assetRequirements">Asset requirements exposed by the platform.</param>
    /// <param name="materialSchemas">Material authoring schemas exposed by the platform.</param>
    /// <param name="componentSupportRules">Component support rules exposed by the platform.</param>
    /// <param name="codegenProfiles">Codegen profiles exposed by the platform.</param>
    /// <param name="storageProfiles">Storage/runtime profiles exposed by the platform.</param>
    /// <param name="mediaProfiles">Media profiles exposed by the platform.</param>
    /// <param name="runtimeGenerationContract">Cross-platform runtime-generation behavior exposed by the platform.</param>
    /// <param name="hostDebugCapability">Cross-platform host-debug capability metadata exposed by the platform.</param>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformStorageProfileDefinition[] storageProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles,
        RuntimeGenerationContract runtimeGenerationContract,
        PlatformHostDebugCapability hostDebugCapability)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            componentSupportRules,
            codegenProfiles,
            storageProfiles,
            mediaProfiles,
            runtimeGenerationContract,
            hostDebugCapability,
            null) {
    }

    /// <summary>
    /// Initializes one platform definition without any media profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            componentSupportRules,
            codegenProfiles,
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>()) {
    }

    /// <summary>
    /// Initializes one platform definition without any storage profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            componentSupportRules,
            codegenProfiles,
            Array.Empty<PlatformStorageProfileDefinition>(),
            mediaProfiles) {
    }

    /// <summary>
    /// Initializes one platform definition without any codegen profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentSupportRule[] componentSupportRules)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            componentSupportRules,
            Array.Empty<PlatformCodegenProfileDefinition>()) {
    }

    /// <summary>
    /// Initializes one platform definition without any material schemas.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformStorageProfileDefinition[] storageProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            componentSupportRules,
            codegenProfiles,
            storageProfiles,
            mediaProfiles) {
    }

    /// <summary>
    /// Initializes one platform definition without any material schemas or media profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            componentSupportRules,
            codegenProfiles,
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>()) {
    }

    /// <summary>
    /// Initializes one platform definition without any material schemas or storage profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformComponentSupportRule[] componentSupportRules,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            componentSupportRules,
            codegenProfiles,
            Array.Empty<PlatformStorageProfileDefinition>(),
            mediaProfiles) {
    }

    /// <summary>
    /// Initializes one platform definition without any material schemas or codegen profiles.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformComponentSupportRule[] componentSupportRules)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            componentSupportRules,
            Array.Empty<PlatformCodegenProfileDefinition>()) {
    }

    /// <summary>
    /// Initializes one platform definition with only material schemas and no support-rule/codegen/storage/media metadata.
    /// </summary>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas)
        : this(
            platformId,
            displayName,
            buildProfiles,
            graphicsProfiles,
            assetRequirements,
            materialSchemas,
            Array.Empty<PlatformComponentSupportRule>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>()) {
    }

    /// <summary>
    /// Gets the stable platform identifier.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the human-readable platform name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the build profiles exposed by the platform.
    /// </summary>
    public PlatformBuildProfileDefinition[] BuildProfiles { get; }

    /// <summary>
    /// Gets the graphics profiles exposed by the platform.
    /// </summary>
    public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }

    /// <summary>
    /// Gets the asset requirements exposed by the platform.
    /// </summary>
    public PlatformAssetRequirementDefinition[] AssetRequirements { get; }

    /// <summary>
    /// Gets the material authoring schemas exposed by the platform.
    /// </summary>
    public PlatformMaterialSchemaDefinition[] MaterialSchemas { get; }

    /// <summary>
    /// Gets the component support rules exposed by the platform.
    /// </summary>
    public PlatformComponentSupportRule[] ComponentSupportRules { get; }

    /// <summary>
    /// Gets the codegen profiles exposed by the platform.
    /// </summary>
    public PlatformCodegenProfileDefinition[] CodegenProfiles { get; }

    /// <summary>
    /// Gets the storage/runtime profiles exposed by the platform.
    /// </summary>
    public PlatformStorageProfileDefinition[] StorageProfiles { get; }

    /// <summary>
    /// Gets the media profiles exposed by the platform.
    /// </summary>
    public PlatformMediaProfileDefinition[] MediaProfiles { get; }

    /// <summary>
    /// Gets the cross-platform runtime-generation behavior exposed by the platform.
    /// </summary>
    public RuntimeGenerationContract RuntimeGenerationContract { get; }

    /// <summary>
    /// Gets the cross-platform host-debug capability metadata exposed by the platform.
    /// </summary>
    public PlatformHostDebugCapability HostDebugCapability { get; }

    /// <summary>
    /// Gets the generic asset-kind cook capabilities exposed by the platform.
    /// </summary>
    public PlatformAssetCookCapabilityDefinition[] AssetCookCapabilities { get; }

    /// <summary>
    /// Gets the platform-specific synthetic component members exposed to editor and runtime packaging flows.
    /// </summary>
    public PlatformComponentMemberDefinition[] ComponentMemberDefinitions { get; }
}



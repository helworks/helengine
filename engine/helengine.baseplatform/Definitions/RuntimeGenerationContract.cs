namespace helengine.baseplatform.Definitions;

/// <summary>
/// Captures the cross-platform runtime behaviors that generated player code depends on.
/// </summary>
public sealed class RuntimeGenerationContract {
    /// <summary>
    /// Creates the default contract used by existing shader-backed desktop-style runtimes.
    /// </summary>
    /// <returns>Default runtime-generation contract.</returns>
    public static RuntimeGenerationContract CreateDefault() {
        return new RuntimeGenerationContract(
            RuntimeMaterialResolutionMode.RawShaderBacked,
            true,
            PackagedPathPolicy.ContentRelativeOnly);
    }

    /// <summary>
    /// Initializes one runtime-generation contract.
    /// </summary>
    /// <param name="materialResolutionMode">How generated runtime code should resolve packaged material assets.</param>
    /// <param name="supportsRenderManager2DTextureReleaseFlush">Whether generated scene-management code may call texture-release flushing on the 2D render manager.</param>
    /// <param name="packagedPathPolicy">Which packaged file path forms generated player code may consume.</param>
    public RuntimeGenerationContract(
        RuntimeMaterialResolutionMode materialResolutionMode,
        bool supportsRenderManager2DTextureReleaseFlush,
        PackagedPathPolicy packagedPathPolicy) {
        MaterialResolutionMode = materialResolutionMode;
        SupportsRenderManager2DTextureReleaseFlush = supportsRenderManager2DTextureReleaseFlush;
        PackagedPathPolicy = packagedPathPolicy;
    }

    /// <summary>
    /// Gets how generated runtime code should resolve packaged material assets.
    /// </summary>
    public RuntimeMaterialResolutionMode MaterialResolutionMode { get; }

    /// <summary>
    /// Gets whether generated scene management may call texture-release flushing on the 2D render manager.
    /// </summary>
    public bool SupportsRenderManager2DTextureReleaseFlush { get; }

    /// <summary>
    /// Gets which packaged file path forms generated player code may consume.
    /// </summary>
    public PackagedPathPolicy PackagedPathPolicy { get; }
}

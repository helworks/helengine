using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor.tests.testing;

/// <summary>
/// Adds recorded shader bundle staging behavior to the shared test material builder without changing platforms that do not publish the capability.
/// </summary>
public sealed class ShaderArtifactTestPlatformMaterialAssetBuilder : IPlatformAssetBuilder, IPlatformShaderArtifactBuilder {
    /// <summary>
    /// Stores the existing material builder used for the schema and material payload behavior.
    /// </summary>
    readonly TestPlatformMaterialAssetBuilder MaterialBuilder;

    /// <summary>
    /// Initializes the shader-capable test builder.
    /// </summary>
    public ShaderArtifactTestPlatformMaterialAssetBuilder() {
        MaterialBuilder = new TestPlatformMaterialAssetBuilder();
    }

    /// <summary>
    /// Gets the material builder descriptor with its target replaced by the shader-capable test target.
    /// </summary>
    public PlatformBuilderDescriptor Descriptor => MaterialBuilder.Descriptor;

    /// <summary>
    /// Gets the shared standard material schema definition used by the scene packager.
    /// </summary>
    public PlatformDefinition Definition => MaterialBuilder.Definition;

    /// <summary>
    /// Gets the full source and material lookup request submitted by the editor cook service.
    /// </summary>
    public PlatformShaderArtifactCookRequest LastShaderArtifactCookRequest { get; private set; }

    /// <summary>
    /// Delegates material cooking to the established test material builder.
    /// </summary>
    /// <param name="request">Material translation request.</param>
    /// <returns>Cooked material payload and dependency metadata.</returns>
    public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
        return MaterialBuilder.CookMaterial(request);
    }

    /// <summary>
    /// Records the shader request and writes a declared test bundle into the normal cooked shader location.
    /// </summary>
    /// <param name="request">Resolved shader source and material lookup request.</param>
    /// <returns>Explicit declaration for the written test bundle.</returns>
    public PlatformShaderArtifactCookResult CookShaderArtifacts(PlatformShaderArtifactCookRequest request) {
        LastShaderArtifactCookRequest = request ?? throw new ArgumentNullException(nameof(request));
        string relativePath = "cooked/shaders/psvita/shaders.psvb";
        string fullPath = Path.Combine(request.CookRootPath, "shaders", "psvita", "shaders.psvb");
        string directoryPath = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Shader bundle directory path is required.");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(fullPath, [0x50, 0x56, 0x53, 0x42]);
        return new PlatformShaderArtifactCookResult([
            new PlatformCookedArtifactDeclaration(relativePath, "test:shader-bundle", "shader", "psvita")
        ]);
    }

    /// <summary>
    /// Delegates build reporting to the shared test material builder.
    /// </summary>
    /// <param name="request">Platform build request.</param>
    /// <param name="progressReporter">Build progress reporter.</param>
    /// <param name="diagnosticReporter">Build diagnostic reporter.</param>
    /// <param name="cancellationToken">Build cancellation token.</param>
    /// <returns>Completed test build report.</returns>
    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        return MaterialBuilder.BuildAsync(request, progressReporter, diagnosticReporter, cancellationToken);
    }
}

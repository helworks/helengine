using helengine.baseplatform.Results;
using helengine.baseplatform.Requests;
using Xunit;

namespace helengine.baseplatform.tests.Results;

/// <summary>
/// Verifies material-reported shader dependencies preserve runtime lookup keys without becoming material payload interpretation.
/// </summary>
public sealed class PlatformShaderDependencyTests {
    /// <summary>
    /// Ensures a shader-capable material dependency retains the selected vertex program, pixel program, and variant.
    /// </summary>
    [Fact]
    public void Constructor_whenProgramPairIsProvided_preservesMaterialLookupKeys() {
        PlatformShaderDependency dependency = new(
            "ForwardStandardShader",
            "ForwardStandardShader.vs",
            "ForwardStandardShader.ps",
            "Mesh");

        Assert.Equal("ForwardStandardShader", dependency.ShaderAssetId);
        Assert.Equal("ForwardStandardShader.vs", dependency.VertexProgramName);
        Assert.Equal("ForwardStandardShader.ps", dependency.PixelProgramName);
        Assert.Equal("Mesh", dependency.VariantName);
        Assert.True(dependency.HasProgramPair);
    }

    /// <summary>
    /// Ensures platform material cooking exposes complete dependencies while retaining its existing shader-id compatibility view.
    /// </summary>
    [Fact]
    public void MaterialCookResult_whenProgramDependencyIsProvided_preservesDependencyAndShaderId() {
        PlatformShaderDependency dependency = new(
            "ForwardStandardShader",
            "ForwardStandardShader.vs",
            "ForwardStandardShader.ps",
            "Mesh");

        PlatformMaterialCookResult result = PlatformMaterialCookResult.CreateWithDependencies([1, 2, 3], [dependency]);

        Assert.Same(dependency, Assert.Single(result.ReferencedShaderDependencies));
        Assert.Equal(["ForwardStandardShader"], result.ReferencedShaderAssetIds);
    }

    /// <summary>
    /// Ensures shader staging receives the complete material lookup key instead of only the shader asset identifier.
    /// </summary>
    [Fact]
    public void ShaderArtifactCookRequest_whenProgramDependencyIsProvided_preservesDependency() {
        PlatformShaderDependency dependency = new(
            "ForwardStandardShader",
            "ForwardStandardShader.vs",
            "ForwardStandardShader.ps",
            "Mesh");

        PlatformShaderArtifactCookRequest request = PlatformShaderArtifactCookRequest.CreateWithDependencies(
            Path.GetTempPath(),
            "psvita",
            "debug",
            "psvita-default",
            [dependency]);

        Assert.Same(dependency, Assert.Single(request.ShaderDependencies));
    }

    /// <summary>
    /// Ensures staging receives the resolved source content associated with the material-reported shader dependency.
    /// </summary>
    [Fact]
    public void ShaderArtifactCookRequest_whenSourceIsProvided_preservesSourceByShaderAssetId() {
        PlatformShaderDependency dependency = new(
            "Rendering.Custom.Water",
            "Rendering.Custom.Water.vs",
            "Rendering.Custom.Water.ps",
            "default");
        PlatformShaderArtifactCookSource source = new("Rendering.Custom.Water", "ABCDEF", "float4 VS() : POSITION { return 0; }");

        PlatformShaderArtifactCookRequest request = PlatformShaderArtifactCookRequest.CreateWithDependenciesAndSources(
            Path.GetTempPath(),
            "psvita",
            "debug",
            "psvita-default",
            [dependency],
            [source]);

        Assert.Same(source, Assert.Single(request.ShaderSources));
    }
}

using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.shaders;

/// <summary>
/// Verifies shader asset identifiers can be resolved during project builds without editor-session global paths.
/// </summary>
public sealed class ShaderAssetIdUtilsTests : IDisposable {
    /// <summary>
    /// Stores the temporary test workspace root.
    /// </summary>
    readonly string WorkspaceRootPath;

    /// <summary>
    /// Creates one isolated assets-root workspace.
    /// </summary>
    public ShaderAssetIdUtilsTests() {
        WorkspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-shader-id-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Deletes the temporary shader workspace after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(WorkspaceRootPath)) {
            Directory.Delete(WorkspaceRootPath, true);
        }
    }

    /// <summary>
    /// Ensures a nested project shader source derives its existing dotted shader asset id from an explicit assets root.
    /// </summary>
    [Fact]
    public void BuildShaderAssetId_whenAssetsRootIsProvided_derivesNestedProjectAssetId() {
        string assetsRootPath = Path.Combine(WorkspaceRootPath, "assets");
        string shaderPath = Path.Combine(assetsRootPath, "Rendering", "Custom", "Water.hlsl");
        Directory.CreateDirectory(Path.GetDirectoryName(shaderPath)!);
        File.WriteAllText(shaderPath, "float4 VS() : POSITION { return 0; }");

        string shaderAssetId = ShaderAssetIdUtils.BuildShaderAssetId(shaderPath, assetsRootPath);

        Assert.Equal("Rendering.Custom.Water", shaderAssetId);
    }

    /// <summary>
    /// Ensures an authored shader can be resolved by the generated shader asset id without imposing a shader-folder convention.
    /// </summary>
    [Fact]
    public void Resolve_whenNestedProjectShaderIsRequested_returnsSourceAndContentHash() {
        string assetsRootPath = Path.Combine(WorkspaceRootPath, "assets");
        string shaderPath = Path.Combine(assetsRootPath, "Rendering", "Custom", "Water.hlsl");
        const string sourceText = "float4 VS() : POSITION { return 0; }\nfloat4 PS() : COLOR { return 1; }";
        Directory.CreateDirectory(Path.GetDirectoryName(shaderPath)!);
        File.WriteAllText(shaderPath, sourceText);

        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        EditorProjectShaderSourceResolver resolver = new(assetsRootPath, library);

        EditorProjectShaderSource source = Assert.Single(resolver.Resolve(["Rendering.Custom.Water"]));

        Assert.Equal("Rendering.Custom.Water", source.ShaderAssetId);
        Assert.Equal(Path.GetFullPath(shaderPath), source.SourcePath);
        Assert.Equal(sourceText, source.SourceText);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceText))), source.SourceHash);
    }
}

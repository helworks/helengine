namespace helengine.editor.tests;

/// <summary>
/// Verifies the shader runtime material loader follows the shared cooked-texture runtime-generation symbol contract.
/// </summary>
public sealed class ShaderRuntimeMaterialLoaderSourceTests {
    /// <summary>
    /// Ensures imported diffuse textures on packaged shader-backed materials use the shared cooked-texture resolution symbol when the runtime platform owns texture payload creation.
    /// </summary>
    [Fact]
    public void ShaderRuntimeMaterialLoader_source_uses_generic_cooked_texture_resolution_symbol() {
        string sourcePath = Path.Combine(
            TestSourceRepositoryLocator.ResolveHelEngineRootPath(),
            "engine",
            "helengine.shader",
            "assets",
            "ShaderRuntimeMaterialLoader.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("BuildTextureFromCooked(texturePath, assetContentManager.ContentStreamSource)", source, StringComparison.Ordinal);
        Assert.Contains("BuildTextureFromRaw(textureAsset)", source, StringComparison.Ordinal);
    }

}

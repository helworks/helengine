namespace helengine.editor.tests;

/// <summary>
/// Verifies the Windows native 3D bridge keeps standard-material diffuse fallback binding aligned with the shared shader contract.
/// </summary>
public sealed class WindowsStandardMaterialFallbackSourceTests {
    /// <summary>
    /// Ensures non-textured standard materials bind a native opaque-white fallback shader resource view instead of leaving the diffuse slot unbound.
    /// </summary>
    [Fact]
    public void Win32_render_bridge_source_binds_native_white_fallback_for_standard_materials_without_diffuse_textures() {
        string headerPath = @"C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.hpp";
        string sourcePath = @"C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.cpp";
        string header = File.ReadAllText(headerPath);
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("void EnsureWhiteTextureFallbackResource();", header, StringComparison.Ordinal);
        Assert.Contains("Microsoft::WRL::ComPtr<ID3D11Texture2D> WhiteTextureFallback;", header, StringComparison.Ordinal);
        Assert.Contains("Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> WhiteTextureFallbackShaderResourceView;", header, StringComparison.Ordinal);
        Assert.Contains("if (resourceView == nullptr && texture == TextureUtils::get_PixelTexture())", source, StringComparison.Ordinal);
        Assert.Contains("EnsureWhiteTextureFallbackResource();", source, StringComparison.Ordinal);
        Assert.Contains("resourceView = WhiteTextureFallbackShaderResourceView.Get();", source, StringComparison.Ordinal);
    }
}

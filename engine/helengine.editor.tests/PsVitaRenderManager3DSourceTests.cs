namespace helengine.editor.tests;

/// <summary>
/// Verifies the PS Vita native 3D renderer source stays aligned with the current cooked material serialization formats.
/// </summary>
public sealed class PsVitaRenderManager3DSourceTests {
    /// <summary>
    /// Ensures the Vita cooked-material loader routes shader-owned format-id-2 payloads through the generated shader material serializer.
    /// </summary>
    [Fact]
    public void PsVita_render_manager_3d_source_dispatches_shader_material_asset_format() {
        string sourcePath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRenderManager3D.cpp";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#include \"ShaderMaterialAssetBinarySerializer.hpp\"", source, StringComparison.Ordinal);
        Assert.Contains("ShaderMaterialAssetBinarySerializer::FormatId", source, StringComparison.Ordinal);
        Assert.Contains("ShaderMaterialAssetBinarySerializer::Deserialize(stream, header)", source, StringComparison.Ordinal);
    }
}

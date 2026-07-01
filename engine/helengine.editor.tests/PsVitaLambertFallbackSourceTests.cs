namespace helengine.editor.tests;

/// <summary>
/// Verifies the PS Vita emulator-safe 3D fallback path upgrades from white triangles to Lambert-lit colored vertices.
/// </summary>
public sealed class PsVitaLambertFallbackSourceTests {
    /// <summary>
    /// Ensures the Vita runtime model copies authored normals alongside positions.
    /// </summary>
    [Fact]
    public void PsVita_runtime_model_copies_normals_for_lambert_fallback() {
        string headerPath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRuntimeModel.hpp";
        string sourcePath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRuntimeModel.cpp";
        string headerSource = File.ReadAllText(headerPath);
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("explicit PsVitaRuntimeModel(std::vector<::float3> positions, std::vector<::float3> normals);", headerSource, StringComparison.Ordinal);
        Assert.Contains("const std::vector<::float3>& GetNormals() const;", headerSource, StringComparison.Ordinal);
        Assert.Contains("std::vector<::float3> Normals;", headerSource, StringComparison.Ordinal);
        Assert.Contains("GetNormals", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Vita 3D fallback resolves scene lights and submits colored solid-color vertices instead of white-only mesh triangles.
    /// </summary>
    [Fact]
    public void PsVita_render_manager_uses_existing_lights_and_colored_vertices_for_lambert_fallback() {
        string headerPath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRenderManager3D.hpp";
        string sourcePath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRenderManager3D.cpp";
        string headerSource = File.ReadAllText(headerPath);
        string source = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("std::vector<rendering::PsVitaSolidColorVertex> QueuedMeshTriangles;", headerSource, StringComparison.Ordinal);
        Assert.Contains("DirectionalLightComponent", source, StringComparison.Ordinal);
        Assert.Contains("AmbientLightComponent", source, StringComparison.Ordinal);
        Assert.Contains("SubmitSolidColorTriangles(QueuedMeshTriangles);", source, StringComparison.Ordinal);
        Assert.Contains("GetNormals()", source, StringComparison.Ordinal);
        Assert.Contains("ResolveActiveDirectionalLight", source, StringComparison.Ordinal);
        Assert.Contains("ResolveAmbientLightColor", source, StringComparison.Ordinal);
        Assert.Contains("BuildLambertVertexColor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitSolidWhiteMeshTriangles(QueuedMeshTriangles);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Vita Lambert fallback respects material cull mode so hidden faces do not leak through the painter-sorted CPU path as small triangle artifacts.
    /// </summary>
    [Fact]
    public void PsVita_render_manager_honors_material_cull_mode_in_lambert_fallback() {
        string headerPath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRenderManager3D.hpp";
        string sourcePath = @"C:\dev\helworks\helengine-psvita\src\platform\psvita\rendering\PsVitaRenderManager3D.cpp";
        string headerSource = File.ReadAllText(headerPath);
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("ResolveSubmeshCullMode", headerSource, StringComparison.Ordinal);
        Assert.Contains("ShouldCullProjectedTriangle", headerSource, StringComparison.Ordinal);
        Assert.Contains("MaterialCullMode", source, StringComparison.Ordinal);
        Assert.Contains("ResolveSubmeshCullMode", source, StringComparison.Ordinal);
        Assert.Contains("ShouldCullProjectedTriangle", source, StringComparison.Ordinal);
        Assert.Contains("if (ShouldCullProjectedTriangle(", source, StringComparison.Ordinal);
        Assert.Contains(
            "if (cullMode == ::MaterialCullMode::Front) {\n            return signedAreaTwice < 0.0;\n        }\n\n        return signedAreaTwice > 0.0;",
            source,
            StringComparison.Ordinal);
    }
}

namespace helengine.editor.tests;

/// <summary>
/// Verifies shader hot-reload notifications stay on the editor frame thread before mutating DirectX runtime shader resources.
/// </summary>
public sealed class EditorSessionShaderThreadingSourceTests {
    /// <summary>
    /// Ensures background shader-build callbacks only enqueue pending invalidations and the editor frame loop drains those invalidations before drawing.
    /// </summary>
    [Fact]
    public void Editor_session_shader_reload_source_queues_renderer_invalidations_to_the_frame_thread() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("ProcessPendingShaderBuildNotifications();", source, StringComparison.Ordinal);
        Assert.Contains("PendingShaderBuildNotifications.Enqueue(new KeyValuePair<string, string>(shaderName ?? string.Empty, packagePath));", source, StringComparison.Ordinal);
        Assert.Contains("core.RenderManager3D.InvalidateShaderResources(shaderAssetId, shaderAsset);", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "void HandleShaderBuilt(string shaderName, string packagePath) {\r\n            if (string.IsNullOrWhiteSpace(packagePath)) {\r\n                return;\r\n            }\r\n\r\n            try {\r\n                ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAssetFromPackage(packagePath);\r\n                string shaderAssetId = string.IsNullOrWhiteSpace(shaderAsset.Id) ? shaderName : shaderAsset.Id;\r\n                core.RenderManager3D.InvalidateShaderResources(shaderAssetId, shaderAsset);",
            source,
            StringComparison.Ordinal);
    }
}

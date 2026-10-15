namespace helengine.editor.tests {
    /// <summary>Checks source snapshots for the desktop missing-material shader declared by other renderers.</summary>
    public sealed class EditorMissingMaterialSourceTests {
        /// <summary>Requires the existing FX source rather than a replacement shader.</summary>
        [Fact]
        public void Resolve_MissingMaterial_ReturnsOriginalDesktopSource() {
            using EditorBuiltInShaderAssetLibrary library = new EditorBuiltInShaderAssetLibrary(new ShaderBackendRegistry());
            EditorProjectShaderSourceResolver resolver = new EditorProjectShaderSourceResolver("C:/dev/helworks/builds/helengine-webgpu/renderer-parity/no-project-shaders", library);
            EditorProjectShaderSource source = Assert.Single(resolver.Resolve(new[] { "MissingMaterial" }));
            Assert.Equal("MissingMaterial.fx", Path.GetFileName(source.SourcePath));
            Assert.Equal(File.ReadAllText(source.SourcePath), source.SourceText);
            Assert.Contains("float4(1.0f, 0.0f, 1.0f, 1.0f)", source.SourceText);
        }
    }
}

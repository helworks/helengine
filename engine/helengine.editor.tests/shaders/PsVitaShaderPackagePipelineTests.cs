using helengine.editor;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the shared shader package pipeline recognizes the PS Vita target as a first-class compile target.
    /// </summary>
    public class PsVitaShaderPackagePipelineTests {
        /// <summary>
        /// Ensures the stable target-name mapping returns the expected lowercase PS Vita identifier.
        /// </summary>
        [Fact]
        public void GetTargetName_whenTargetIsPsVita_returnsPsvita() {
            Assert.Equal("psvita", ShaderTargetNames.GetTargetName(ShaderCompileTarget.PsVita));
        }

        /// <summary>
        /// Ensures the target-name parser resolves the PS Vita identifier back into the compile-target enum value.
        /// </summary>
        [Fact]
        public void TryParseTarget_whenNameIsPsvita_returnsPsVita() {
            Assert.True(ShaderTargetNames.TryParseTarget("psvita", out ShaderCompileTarget target));
            Assert.Equal(ShaderCompileTarget.PsVita, target);
        }

        /// <summary>
        /// Ensures shared shader compilation code does not directly instantiate renderer-specific backends.
        /// </summary>
        [Fact]
        public void SharedShaderCompilationSource_whenInspected_doesNotDirectlyInstantiateRendererSpecificBackends() {
            string helengineRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string moduleManagerPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "shaders", "ShaderModuleManager.cs");
            string builtInLibraryPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "shaders", "EditorBuiltInShaderAssetLibrary.cs");
            string editorSessionPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "EditorSession.cs");
            string renderValidationPath = Path.Combine(helengineRootPath, "engine", "helengine.render.validation", "RenderShaderFactory.cs");

            string moduleManagerSource = File.ReadAllText(moduleManagerPath);
            string builtInLibrarySource = File.ReadAllText(builtInLibraryPath);
            string editorSessionSource = File.ReadAllText(editorSessionPath);
            string renderValidationSource = File.ReadAllText(renderValidationPath);

            Assert.DoesNotContain("new helengine.directx11.DirectX11ShaderBackend()", moduleManagerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.vulkan.VulkanShaderBackend()", moduleManagerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.directx11.DirectX11ShaderBackend()", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.vulkan.VulkanShaderBackend()", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.directx11.DirectX11Renderer3D", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.vulkan.VulkanRenderer3D", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.directx11.DirectX11Renderer3D", editorSessionSource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.vulkan.VulkanRenderer3D", editorSessionSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new DirectX11ShaderBackend()", renderValidationSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new VulkanShaderBackend()", renderValidationSource, StringComparison.Ordinal);
            Assert.Contains("ShaderBackendRegistry", moduleManagerSource, StringComparison.Ordinal);
            Assert.Contains("ShaderBackendRegistry", builtInLibrarySource, StringComparison.Ordinal);
            Assert.Contains("IShaderCompileTargetProvider", builtInLibrarySource, StringComparison.Ordinal);
            Assert.Contains("IShaderCompileTargetProvider", editorSessionSource, StringComparison.Ordinal);
        }
    }
}

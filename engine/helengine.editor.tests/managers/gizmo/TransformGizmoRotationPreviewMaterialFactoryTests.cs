using System.Reflection;
using helengine;
using helengine.editor;
using helengine.directx11;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies shader compilation for the transform-gizmo rotation-preview material.
    /// </summary>
    public class TransformGizmoRotationPreviewMaterialFactoryTests {
        /// <summary>
        /// Ensures the rotation-preview shader source compiles into a DirectX11 shader asset.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForDirectX11() {
            using EditorBuiltInShaderAssetLibrary shaderLibrary = CreateShaderLibrary();
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.DirectX11, shaderLibrary);

            Assert.Equal("EditorTransformGizmoRotationPreview", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
        }

        /// <summary>
        /// Ensures the rotation-preview shader source compiles into a Vulkan shader asset.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForVulkan() {
            using EditorBuiltInShaderAssetLibrary shaderLibrary = CreateShaderLibrary();
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.Vulkan, shaderLibrary);

            Assert.Equal("EditorTransformGizmoRotationPreview", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
        }

        /// <summary>
        /// Invokes the private shader-build helper so compilation can be validated without constructing a renderer.
        /// </summary>
        /// <param name="target">Backend target that should receive the compiled shader binaries.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static EditorBuiltInShaderAssetLibrary CreateShaderLibrary() {
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            return new EditorBuiltInShaderAssetLibrary(shaderBackendRegistry);
        }

        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target, EditorBuiltInShaderAssetLibrary shaderLibrary) {
            if (shaderLibrary == null) {
                throw new ArgumentNullException(nameof(shaderLibrary));
            }

            Type factoryType = typeof(TransformGizmoRotationPreviewMaterialFactory);
            MethodInfo method = factoryType.GetMethod("BuildShaderAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) {
                throw new InvalidOperationException("Transform gizmo rotation-preview shader builder method was not found.");
            }

            object result = method.Invoke(null, new object[] { target, shaderLibrary });
            if (result is not ShaderAsset shaderAsset) {
                throw new InvalidOperationException("Transform gizmo rotation-preview shader builder did not return a shader asset.");
            }

            return shaderAsset;
        }
    }
}

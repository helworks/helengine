using System.Reflection;
using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies shader compilation for the transform-gizmo grid-preview material.
    /// </summary>
    public class TransformGizmoGridPreviewMaterialFactoryTests {
        /// <summary>
        /// Ensures the grid-preview shader source compiles into a DirectX11 shader asset.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForDirectX11() {
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.DirectX11);

            Assert.Equal("EditorTransformGizmoGridPreview", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
        }

        /// <summary>
        /// Ensures the grid-preview shader source compiles into a Vulkan shader asset.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForVulkan() {
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.Vulkan);

            Assert.Equal("EditorTransformGizmoGridPreview", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
        }

        /// <summary>
        /// Invokes the private shader-build helper so compilation can be validated without constructing a renderer.
        /// </summary>
        /// <param name="target">Backend target that should receive the compiled shader binaries.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target) {
            Type factoryType = typeof(TransformGizmoGridPreviewMaterialFactory);
            MethodInfo method = factoryType.GetMethod("BuildShaderAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) {
                throw new InvalidOperationException("Transform gizmo grid-preview shader builder method was not found.");
            }

            object result = method.Invoke(null, new object[] { target });
            if (result is not ShaderAsset shaderAsset) {
                throw new InvalidOperationException("Transform gizmo grid-preview shader builder did not return a shader asset.");
            }

            return shaderAsset;
        }
    }
}

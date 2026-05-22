using System.Reflection;
using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies shader compilation for the transform-gizmo axis-label billboard material.
    /// </summary>
    public class TransformGizmoAxisLabelMaterialFactoryTests {
        /// <summary>
        /// Ensures the axis-label shader source compiles into a DirectX11 shader asset.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForDirectX11() {
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.DirectX11);

            Assert.Equal("EditorTransformGizmoAxisLabel", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
            AssertAxisLabelLayout(shaderAsset, 0);
        }

        /// <summary>
        /// Ensures the axis-label shader source compiles into a Vulkan shader asset using the same texture declarations as DirectX11.
        /// </summary>
        [Fact]
        public void BuildShaderAsset_CompilesForVulkan() {
            ShaderAsset shaderAsset = BuildShaderAsset(ShaderCompileTarget.Vulkan);

            Assert.Equal("EditorTransformGizmoAxisLabel", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);
            AssertAxisLabelLayout(shaderAsset, 0);
        }

        /// <summary>
        /// Invokes the private shader-build helper so compilation can be validated without constructing a renderer.
        /// </summary>
        /// <param name="target">Backend target that should receive the compiled shader binaries.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target) {
            Type factoryType = typeof(TransformGizmoAxisLabelMaterialFactory);
            MethodInfo method = factoryType.GetMethod("BuildShaderAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) {
                throw new InvalidOperationException("Transform gizmo axis-label shader builder method was not found.");
            }

            object result = method.Invoke(null, new object[] { target });
            if (result is not ShaderAsset shaderAsset) {
                throw new InvalidOperationException("Transform gizmo axis-label shader builder did not return a shader asset.");
            }

            return shaderAsset;
        }

        /// <summary>
        /// Verifies that the compiled shader asset exposes the axis-label texture and sampler bindings to the material system.
        /// </summary>
        /// <param name="shaderAsset">Shader asset to validate.</param>
        /// <param name="expectedSet">Expected descriptor set used by the textured material bindings.</param>
        static void AssertAxisLabelLayout(ShaderAsset shaderAsset, int expectedSet) {
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            ShaderMaterialAsset materialAsset = CreateMaterialAsset(shaderAsset.Id);
            MaterialLayout layout = MaterialLayoutBuilder.Build(materialAsset, shaderAsset);

            Assert.Equal(0, layout.FindTextureBindingIndex("LabelTexture"));
            Assert.Equal(0, layout.FindSamplerBindingIndex("LabelSampler"));
            Assert.Single(layout.TextureBindings);
            Assert.Single(layout.SamplerBindings);
            Assert.Equal(expectedSet, layout.TextureBindings[0].Set);
            Assert.Equal(expectedSet, layout.SamplerBindings[0].Set);
        }

        /// <summary>
        /// Creates the material asset shape used by the runtime axis-label material factory.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier selected by the material.</param>
        /// <returns>Material asset configured for axis-label layout validation.</returns>
        static ShaderMaterialAsset CreateMaterialAsset(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            return new ShaderMaterialAsset {
                Id = "EditorTransformGizmoAxisLabel.material",
                ShaderAssetId = shaderAssetId,
                VertexProgram = "EditorTransformGizmoAxisLabel.vs",
                PixelProgram = "EditorTransformGizmoAxisLabel.ps",
                Variant = "default",
                RenderState = new MaterialRenderState()
            };
        }
    }
}

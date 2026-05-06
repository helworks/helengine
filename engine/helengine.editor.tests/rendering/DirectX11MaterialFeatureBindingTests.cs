using helengine.editor.tests.testing;
using helengine.directx11;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpDX.Direct3D11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies compact Windows-forward material feature binding for the DirectX11 material build path.
    /// </summary>
    public class DirectX11MaterialFeatureBindingTests {
        /// <summary>
        /// Ensures building one material from raw authored data sets the compact PBR feature flags.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRaw_WhenNormalAndEmissiveInputsExist_SetsCompactPbrFeatureFlags() {
            MaterialAsset materialAsset = new MaterialAsset {
                Id = "materials/test",
                ShaderAssetId = "shader/test",
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "default",
                NormalTextureAssetId = "textures/normal",
                EmissiveTextureAssetId = "textures/emissive"
            };
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "shader/test"
            };
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();

            RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, shaderAsset);

            Assert.Equal(RuntimeMaterialLightingModel.MetalRoughPbr, material.LightingModel);
            Assert.True(material.SupportsNormalMapping);
            Assert.True(material.SupportsEmissive);
        }

        /// <summary>
        /// Ensures textured 3D materials can sample one DirectX11 render target, which is required by the editor canvas-plane preview.
        /// </summary>
        [Fact]
        public void ResolveMaterialTextureResourceView_WhenMaterialUsesDirectX11RenderTarget_ReturnsRenderTargetShaderResourceView() {
            RuntimeMaterial material = CreateTexturedMaterial();
            DirectX11RenderTargetResource renderTarget = (DirectX11RenderTargetResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11RenderTargetResource));
            ShaderResourceView expectedResourceView = (ShaderResourceView)RuntimeHelpers.GetUninitializedObject(typeof(ShaderResourceView));
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ResolveMaterialTextureResourceView", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            SetAutoPropertyBackingField(renderTarget, "ShaderResourceView", expectedResourceView);
            material.Properties.SetTexture("CanvasTexture", renderTarget);

            ShaderResourceView resourceView = Assert.IsType<ShaderResourceView>(method.Invoke(renderer, new object[] { material }));

            Assert.Same(expectedResourceView, resourceView);
        }

        /// <summary>
        /// Creates one runtime material with a single texture binding that matches the viewport canvas-plane shader.
        /// </summary>
        /// <returns>Runtime material configured with a `CanvasTexture` binding.</returns>
        static RuntimeMaterial CreateTexturedMaterial() {
            RuntimeMaterial material = new RuntimeMaterial();
            material.SetLayout(new MaterialLayout(
                "shader/test",
                "VS",
                "PS",
                "default",
                new MaterialRenderState(),
                new[] {
                    new MaterialLayoutBinding("CanvasTexture", ShaderResourceType.Texture2D, 0, 0, 0)
                },
                Array.Empty<MaterialLayoutBinding>(),
                Array.Empty<MaterialLayoutBinding>()));
            return material;
        }

        /// <summary>
        /// Sets one compiler-generated auto-property backing field on an uninitialized test object.
        /// </summary>
        /// <param name="instance">Object whose backing field should be updated.</param>
        /// <param name="propertyName">Property whose backing field should be written.</param>
        /// <param name="value">Value to assign to the backing field.</param>
        static void SetAutoPropertyBackingField(object instance, string propertyName, object value) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            FieldInfo field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException($"Could not find auto-property backing field for '{propertyName}'.");
            }

            field.SetValue(instance, value);
        }
    }
}

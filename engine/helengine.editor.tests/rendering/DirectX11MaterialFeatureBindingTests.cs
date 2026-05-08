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
                CastsShadows = false,
                ReceivesShadows = false,
                NormalTextureAssetId = "textures/normal",
                EmissiveTextureAssetId = "textures/emissive"
            };
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "shader/test",
                Programs = new[] {
                    CreateProgram("VS", ShaderStage.Vertex),
                    CreateProgram("PS", ShaderStage.Pixel)
                },
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();

            RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, shaderAsset);

            Assert.Equal(RuntimeMaterialLightingModel.MetalRoughPbr, material.LightingModel);
            Assert.True(material.SupportsNormalMapping);
            Assert.True(material.SupportsEmissive);
            Assert.False(material.CastsShadows);
            Assert.False(material.ReceivesShadows);
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
        /// Ensures one textured standard-material layout keeps the authored diffuse binding available on the runtime material.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRaw_WhenMaterialExposesDiffuseTextureBinding_PreservesTheDiffuseTextureBinding() {
            MaterialAsset materialAsset = new MaterialAsset {
                Id = "materials/test",
                ShaderAssetId = "shader/test",
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "default"
            };
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "shader/test",
                Programs = new[] {
                    CreateProgram("VS", ShaderStage.Vertex),
                    CreateProgram("PS", ShaderStage.Pixel, CreateBinding(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, ShaderResourceType.Texture2D, 0, 0, 0))
                },
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();

            RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, shaderAsset);

            Assert.Equal(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, material.Layout.TextureBindings[0].Name);
        }

        /// <summary>
        /// Ensures DirectX11 shadow-caster eligibility stays on the DirectX11 material root instead of leaking into shared material state.
        /// </summary>
        [Fact]
        public void ShouldMaterialCastShadows_WhenDirectX11RootDisablesShadowCasting_ReturnsFalse() {
            DirectX11ShaderResource shaderResource = (DirectX11ShaderResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11ShaderResource));
            var rootMaterial = new DirectX11MaterialResource(shaderResource);
            RuntimeMaterial childMaterial = new RuntimeMaterial();
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ShouldMaterialCastShadows", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            rootMaterial.CastsShadows = false;
            childMaterial.SetParentMaterial(rootMaterial);

            bool castsShadows = Assert.IsType<bool>(method.Invoke(renderer, new object[] { childMaterial }));

            Assert.False(castsShadows);
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
        /// Creates one shader program asset with the supplied bindings.
        /// </summary>
        /// <param name="name">Program name.</param>
        /// <param name="stage">Shader stage for the program.</param>
        /// <param name="bindings">Bindings declared by the program.</param>
        /// <returns>Shader program asset for layout-builder tests.</returns>
        static ShaderProgramAsset CreateProgram(string name, ShaderStage stage, params ShaderBindingAsset[] bindings) {
            return new ShaderProgramAsset {
                Name = name,
                Stage = stage,
                EntryPoint = name,
                Bindings = bindings,
                Inputs = Array.Empty<ShaderVertexElementAsset>(),
                Outputs = Array.Empty<ShaderVertexElementAsset>(),
                Variants = Array.Empty<ShaderVariantAsset>()
            };
        }

        /// <summary>
        /// Creates one shader binding asset with the supplied layout metadata.
        /// </summary>
        /// <param name="name">Binding name.</param>
        /// <param name="resourceType">Shader resource kind exposed by the binding.</param>
        /// <param name="set">Logical descriptor set index.</param>
        /// <param name="slot">Logical binding slot inside the descriptor set.</param>
        /// <param name="size">Byte size for constant-buffer bindings, or zero for other resource kinds.</param>
        /// <returns>Shader binding asset configured for layout tests.</returns>
        static ShaderBindingAsset CreateBinding(string name, ShaderResourceType resourceType, int set, int slot, int size) {
            return new ShaderBindingAsset {
                Name = name,
                Type = resourceType,
                Set = set,
                Slot = slot,
                Size = size,
                Members = Array.Empty<ShaderConstantMemberAsset>()
            };
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

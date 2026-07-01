using helengine.editor.tests.testing;
using helengine.directx11;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
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
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
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

            ShaderRuntimeMaterial material = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(renderer.BuildMaterialFromRaw(materialAsset, shaderAsset));

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
            ShaderRuntimeMaterial material = CreateTexturedMaterial();
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
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
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

            ShaderRuntimeMaterial material = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(renderer.BuildMaterialFromRaw(materialAsset, shaderAsset));

            Assert.Equal(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, material.Layout.TextureBindings[0].Name);
        }

        /// <summary>
        /// Ensures DirectX11 shadow-caster eligibility stays on the DirectX11 material root instead of leaking into shared material state.
        /// </summary>
        [Fact]
        public void ShouldMaterialCastShadows_WhenDirectX11RootDisablesShadowCasting_ReturnsFalse() {
            DirectX11ShaderResource shaderResource = (DirectX11ShaderResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11ShaderResource));
            var rootMaterial = new DirectX11MaterialResource(shaderResource);
            TestRuntimeMaterial childMaterial = new TestRuntimeMaterial();
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ShouldMaterialCastShadows", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            rootMaterial.CastsShadows = false;
            childMaterial.SetParentMaterial(rootMaterial);

            bool castsShadows = Assert.IsType<bool>(method.Invoke(renderer, new object[] { childMaterial }));

            Assert.False(castsShadows);
        }

        /// <summary>
        /// Ensures the DirectX11 material-binding path resolves authored constant-buffer payloads to their shader slots.
        /// </summary>
        [Fact]
        public void ResolveMaterialConstantBufferBindings_WhenMaterialProvidesLocalPayload_ReturnsResolvedBindingData() {
            ShaderRuntimeMaterial material = CreateMaterialWithConstantBufferBinding("BaseColorBuffer", 3, 16);
            byte[] expectedData = CreateConstantBufferPayload(1f, 0.5f, 0.25f, 1f);
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ResolveMaterialConstantBufferBindings", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            material.Properties.SetConstantBufferData("BaseColorBuffer", expectedData);

            IList resolvedBindings = Assert.IsAssignableFrom<IList>(method.Invoke(renderer, new object[] { material }));

            Assert.Single(resolvedBindings);
            AssertResolvedConstantBufferBinding(resolvedBindings[0], "BaseColorBuffer", 3, expectedData);
        }

        /// <summary>
        /// Ensures the DirectX11 material-binding path can inherit constant-buffer payloads from one parent material chain.
        /// </summary>
        [Fact]
        public void ResolveMaterialConstantBufferBindings_WhenMaterialInheritsParentPayload_ReturnsParentBindingData() {
            DirectX11ShaderResource shaderResource = (DirectX11ShaderResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11ShaderResource));
            var rootMaterial = new DirectX11MaterialResource(shaderResource);
            TestRuntimeMaterial childMaterial = new TestRuntimeMaterial();
            byte[] expectedData = CreateConstantBufferPayload(0.1f, 0.2f, 0.3f, 1f);
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ResolveMaterialConstantBufferBindings", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            rootMaterial.SetLayout(CreateMaterialLayoutWithConstantBufferBinding("BaseColorBuffer", 3, 16));
            rootMaterial.Properties.SetConstantBufferData("BaseColorBuffer", expectedData);
            childMaterial.SetParentMaterial(rootMaterial);

            IList resolvedBindings = Assert.IsAssignableFrom<IList>(method.Invoke(renderer, new object[] { childMaterial }));

            Assert.Single(resolvedBindings);
            AssertResolvedConstantBufferBinding(resolvedBindings[0], "BaseColorBuffer", 3, expectedData);
        }

        /// <summary>
        /// Ensures material-owned constant-buffer rebinding does not clear engine-owned forward-light and shadow shader buffers.
        /// </summary>
        [Fact]
        public void ApplyMaterialConstantBufferBindings_WhenMaterialOnlyOwnsBaseColor_PreservesEngineForwardLightAndShadowBuffers() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            material.SetLayout(new MaterialLayout(
                "shader/test",
                "VS",
                "PS",
                "default",
                new MaterialRenderState(),
                Array.Empty<MaterialLayoutBinding>(),
                new[] {
                    new MaterialLayoutBinding("ForwardLightBuffer", ShaderResourceType.ConstantBuffer, 0, 1, 272),
                    new MaterialLayoutBinding("ShadowBuffer", ShaderResourceType.ConstantBuffer, 0, 2, 336),
                    new MaterialLayoutBinding("BaseColorBuffer", ShaderResourceType.ConstantBuffer, 0, 3, 16)
                },
                Array.Empty<MaterialLayoutBinding>()));
            material.Properties.SetConstantBufferData("BaseColorBuffer", CreateConstantBufferPayload(1f, 1f, 1f, 1f));
            MethodInfo applyMethod = typeof(DirectX11Renderer3D).GetMethod("ApplyMaterialConstantBufferBindings", BindingFlags.Instance | BindingFlags.NonPublic);
            SharpDX.Direct3D11.Buffer forwardLightBuffer = GetPrivateFieldValue<SharpDX.Direct3D11.Buffer>(renderer, "forwardLightConstantBuffer");
            SharpDX.Direct3D11.Buffer shadowBuffer = GetPrivateFieldValue<SharpDX.Direct3D11.Buffer>(renderer, "shadowConstantBuffer");
            DeviceContext context = renderer.Device.ImmediateContext;

            Assert.NotNull(applyMethod);
            context.PixelShader.SetConstantBuffer(1, forwardLightBuffer);
            context.PixelShader.SetConstantBuffer(2, shadowBuffer);

            applyMethod.Invoke(renderer, new object[] { material });

            SharpDX.Direct3D11.Buffer reboundForwardLightBuffer = context.PixelShader.GetConstantBuffers(1, 1)[0];
            SharpDX.Direct3D11.Buffer reboundShadowBuffer = context.PixelShader.GetConstantBuffers(2, 1)[0];
            Assert.NotNull(reboundForwardLightBuffer);
            Assert.NotNull(reboundShadowBuffer);
            Assert.Equal(forwardLightBuffer.NativePointer, reboundForwardLightBuffer.NativePointer);
            Assert.Equal(shadowBuffer.NativePointer, reboundShadowBuffer.NativePointer);
        }

        /// <summary>
        /// Ensures the DirectX11 standard-mesh shader data transposes the inverse-transpose matrix before upload so HLSL row-vector normal transforms match the uploaded world-matrix convention.
        /// </summary>
        [Fact]
        public void BuildStandardMeshShaderData_WhenWorldContainsRotation_UploadsTransposedInverseTransposeNormalMatrix() {
            float4x4.CreateFromYawPitchRoll((float)(Math.PI * 0.5d), 0f, 0f, out float4x4 world);
            float4x4.InverseTranspose(ref world, out float4x4 expectedUploadedNormalMatrix);
            float4x4.Transpose(ref expectedUploadedNormalMatrix, out expectedUploadedNormalMatrix);
            MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("BuildStandardMeshShaderData", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            StandardMeshShaderData shaderData = Assert.IsType<StandardMeshShaderData>(method.Invoke(null, new object[] { world, new float3(1f, 2f, 3f), true }));

            AssertMatrixEqual(expectedUploadedNormalMatrix, shaderData.NormalMatrix);
        }

        /// <summary>
        /// Creates one runtime material with a single texture binding that matches the viewport canvas-plane shader.
        /// </summary>
        /// <returns>Runtime material configured with a `CanvasTexture` binding.</returns>
        static ShaderRuntimeMaterial CreateTexturedMaterial() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
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
        /// Asserts that two matrices match element-by-element within a tight tolerance.
        /// </summary>
        /// <param name="expected">Expected matrix value.</param>
        /// <param name="actual">Actual matrix value.</param>
        static void AssertMatrixEqual(float4x4 expected, float4x4 actual) {
            Assert.Equal(expected.M11, actual.M11, 5);
            Assert.Equal(expected.M12, actual.M12, 5);
            Assert.Equal(expected.M13, actual.M13, 5);
            Assert.Equal(expected.M14, actual.M14, 5);
            Assert.Equal(expected.M21, actual.M21, 5);
            Assert.Equal(expected.M22, actual.M22, 5);
            Assert.Equal(expected.M23, actual.M23, 5);
            Assert.Equal(expected.M24, actual.M24, 5);
            Assert.Equal(expected.M31, actual.M31, 5);
            Assert.Equal(expected.M32, actual.M32, 5);
            Assert.Equal(expected.M33, actual.M33, 5);
            Assert.Equal(expected.M34, actual.M34, 5);
            Assert.Equal(expected.M41, actual.M41, 5);
            Assert.Equal(expected.M42, actual.M42, 5);
            Assert.Equal(expected.M43, actual.M43, 5);
            Assert.Equal(expected.M44, actual.M44, 5);
        }

        /// <summary>
        /// Creates one runtime material with a single constant-buffer binding for DirectX11 material-binding tests.
        /// </summary>
        /// <param name="bindingName">Constant-buffer binding name.</param>
        /// <param name="slot">DirectX11 shader slot.</param>
        /// <param name="size">Constant-buffer payload size in bytes.</param>
        /// <returns>Runtime material configured with the supplied constant-buffer binding.</returns>
        static ShaderRuntimeMaterial CreateMaterialWithConstantBufferBinding(string bindingName, int slot, int size) {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            material.SetLayout(CreateMaterialLayoutWithConstantBufferBinding(bindingName, slot, size));
            return material;
        }

        /// <summary>
        /// Creates one material layout with a single constant-buffer binding for DirectX11 material-binding tests.
        /// </summary>
        /// <param name="bindingName">Constant-buffer binding name.</param>
        /// <param name="slot">DirectX11 shader slot.</param>
        /// <param name="size">Constant-buffer payload size in bytes.</param>
        /// <returns>Material layout configured with the supplied constant-buffer binding.</returns>
        static MaterialLayout CreateMaterialLayoutWithConstantBufferBinding(string bindingName, int slot, int size) {
            return new MaterialLayout(
                "shader/test",
                "VS",
                "PS",
                "default",
                new MaterialRenderState(),
                Array.Empty<MaterialLayoutBinding>(),
                new[] {
                    new MaterialLayoutBinding(bindingName, ShaderResourceType.ConstantBuffer, 0, slot, size)
                },
                Array.Empty<MaterialLayoutBinding>());
        }

        /// <summary>
        /// Creates one packed constant-buffer payload from four single-precision values.
        /// </summary>
        /// <param name="x">First packed float.</param>
        /// <param name="y">Second packed float.</param>
        /// <param name="z">Third packed float.</param>
        /// <param name="w">Fourth packed float.</param>
        /// <returns>Packed 16-byte payload.</returns>
        static byte[] CreateConstantBufferPayload(float x, float y, float z, float w) {
            byte[] payload = new byte[sizeof(float) * 4];
            Array.Copy(BitConverter.GetBytes(x), 0, payload, 0, sizeof(float));
            Array.Copy(BitConverter.GetBytes(y), 0, payload, sizeof(float), sizeof(float));
            Array.Copy(BitConverter.GetBytes(z), 0, payload, sizeof(float) * 2, sizeof(float));
            Array.Copy(BitConverter.GetBytes(w), 0, payload, sizeof(float) * 3, sizeof(float));
            return payload;
        }

        /// <summary>
        /// Verifies one reflected resolved constant-buffer binding.
        /// </summary>
        /// <param name="resolvedBinding">Resolved binding object returned by the DirectX11 renderer.</param>
        /// <param name="expectedName">Expected binding name.</param>
        /// <param name="expectedSlot">Expected shader slot.</param>
        /// <param name="expectedData">Expected packed payload.</param>
        static void AssertResolvedConstantBufferBinding(object resolvedBinding, string expectedName, int expectedSlot, byte[] expectedData) {
            if (resolvedBinding == null) {
                throw new ArgumentNullException(nameof(resolvedBinding));
            }

            Type bindingType = resolvedBinding.GetType();
            PropertyInfo nameProperty = bindingType.GetProperty("Name");
            PropertyInfo slotProperty = bindingType.GetProperty("Slot");
            PropertyInfo dataProperty = bindingType.GetProperty("Data");

            Assert.NotNull(nameProperty);
            Assert.NotNull(slotProperty);
            Assert.NotNull(dataProperty);
            Assert.Equal(expectedName, Assert.IsType<string>(nameProperty.GetValue(resolvedBinding)));
            Assert.Equal(expectedSlot, Assert.IsType<int>(slotProperty.GetValue(resolvedBinding)));
            Assert.Equal(expectedData, Assert.IsType<byte[]>(dataProperty.GetValue(resolvedBinding)));
        }

        /// <summary>
        /// Reads one private instance field from the supplied object.
        /// </summary>
        /// <typeparam name="TValue">Expected field value type.</typeparam>
        /// <param name="instance">Object that owns the requested field.</param>
        /// <param name="fieldName">Private field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        static TValue GetPrivateFieldValue<TValue>(object instance, string fieldName) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException($"Could not find private field '{fieldName}'.");
            }

            return (TValue)field.GetValue(instance);
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

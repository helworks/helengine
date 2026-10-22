using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Verifies headless authoring retains real CPU asset data and rejects operations that require a graphics device.</summary>
    [Collection(EditorBuildCacheEnvironmentCollection.Name)]
    public sealed class EditorBuildRenderManagerTests {
        /// <summary>Initializes the actual editor core and materializes isolated RGBA pixels without loading a graphics device.</summary>
        [Fact]
        public void CoreInitializationAndTextureRegionUpdatePreserveCpuPixels() {
            using EditorBuildRenderManager3D renderer = new(ShaderCompileTarget.Vulkan);
            using EditorCore core = new(null);
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("editor", "test"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            using TextureAsset asset = new() {
                Width = 2, Height = 2, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[16]
            };
            EditorBuildRuntimeTexture texture = Assert.IsType<EditorBuildRuntimeTexture>(renderer.Render2D.BuildTextureFromRaw(asset));
            asset.Colors[0] = 200;
            byte[] source = [1, 2, 3, 4, 90, 90, 90, 90, 5, 6, 7, 8];
            renderer.Render2D.UpdateTextureRegion(texture, 1, 0, 1, 2, source, 8);
            source[0] = 99;
            Assert.Equal(new byte[] { 0, 0, 0, 0, 1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8 }, texture.Pixels.ToArray());
            Assert.Same(core, renderer.OwnerCore);
            Assert.Same(core, renderer.Render2D.OwnerCore);
            renderer.Render2D.ReleaseTexture(texture);
            Assert.True(texture.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => renderer.Render2D.UpdateTextureRegion(texture, 0, 0, 1, 1, new byte[4], 4));
            Assert.NotNull(asset.Colors);
        }

        /// <summary>Rejects foreign texture ownership and malformed source payloads before changing any CPU pixels.</summary>
        [Fact]
        public void TextureOwnershipAndPayloadValidationRemainStrict() {
            using EditorBuildRenderManager2D first = new();
            using EditorBuildRenderManager2D second = new();
            using TextureAsset asset = new() {
                Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, Colors = [10, 20, 30, 255]
            };
            RuntimeTexture texture = first.BuildTextureFromRaw(asset);
            Assert.Throws<ArgumentException>(() => second.UpdateTextureRegion(texture, 0, 0, 1, 1, new byte[4], 4));
            Assert.Throws<ArgumentException>(() => second.ReleaseTexture(texture));
            Assert.Throws<ArgumentException>(() => first.BuildTextureFromRaw(new TextureAsset { Width = 2, Height = 1, Colors = new byte[4] }));
            first.Dispose();
            Assert.True(texture.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => first.BuildTextureFromRaw(asset));
        }

        /// <summary>Retains an independent geometry copy, authored bounds and validated material slots while leaving the source asset owned by its caller.</summary>
        [Fact]
        public void ModelMaterializationOwnsGeometryCopy() {
            using EditorBuildRenderManager3D renderer = new(ShaderCompileTarget.Metal);
            using ModelAsset source = new() {
                Positions = [new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 1, 0)],
                Indices16 = [0, 1, 2], BoundsMin = new float3(-1, -2, -3), BoundsMax = new float3(4, 5, 6),
                Submeshes = [new ModelSubmeshAsset { IndexStart = 0, IndexCount = 3, MaterialSlotName = "body" }]
            };
            EditorBuildRuntimeModel model = Assert.IsType<EditorBuildRuntimeModel>(renderer.BuildModelFromRaw(source));
            source.Positions[0] = new float3(99, 99, 99);
            source.Submeshes[0].MaterialSlotName = "changed";
            Assert.Equal(0, model.RawModelAsset.Positions[0].X);
            Assert.Equal("body", Assert.Single(model.Submeshes).MaterialSlotName);
            Assert.Equal(source.BoundsMin, model.BoundsMin);
            Assert.Equal(source.BoundsMax, model.BoundsMax);
            renderer.ReleaseModel(model);
            Assert.True(model.IsDisposed);
            Assert.Null(model.RawModelAsset);
            Assert.Equal(3, source.Positions.Length);
            Assert.Equal(3, source.Indices16.Length);
        }

        /// <summary>Preserves shader layouts, authored constants and render state, then refreshes CPU metadata after a shader invalidation.</summary>
        [Fact]
        public void MaterialLayoutAndConstantsSurviveShaderRefresh() {
            using EditorBuildRenderManager3D renderer = new(ShaderCompileTarget.Vulkan);
            using ShaderMaterialAsset source = new() {
                ShaderAssetId = "shader/test", VertexProgram = "VS", PixelProgram = "PS",
                RenderState = new MaterialRenderState { BlendMode = MaterialBlendMode.AlphaBlend },
                ConstantBuffers = [new MaterialConstantBufferAsset { Name = "Params", Data = new byte[16] }]
            };
            using ShaderAsset shader = CreateShader(0);
            ShaderRuntimeMaterial material = Assert.IsType<ShaderRuntimeMaterial>(renderer.BuildMaterialFromRaw(source, shader));
            Assert.Equal(MaterialBlendMode.AlphaBlend, material.RenderState.BlendMode);
            Assert.Equal("Params", Assert.Single(material.Layout.ConstantBufferBindings).Name);
            Assert.True(material.TryResolveConstantBufferData("Params", out byte[] constants));
            source.ConstantBuffers[0].Data[0] = 255;
            Assert.Equal(0, constants[0]);
            using ShaderAsset updated = CreateShader(2);
            renderer.InvalidateShaderResources("shader/test", updated);
            Assert.Equal(2, Assert.Single(material.Layout.ConstantBufferBindings).Slot);
            Assert.True(material.TryResolveConstantBufferData("Params", out byte[] refreshed));
            Assert.Equal(0, refreshed[0]);
            renderer.ReleaseMaterial(material);
            Assert.Null(material.Layout);
        }

        /// <summary>Reports no GPU rendering capability and fails explicitly instead of pretending that draw requests succeeded.</summary>
        [Fact]
        public void GpuOperationsFailWithContext() {
            using EditorBuildRenderManager3D renderer = new(ShaderCompileTarget.Metal);
            Assert.False(renderer.GetCapabilityProfile().SupportsForwardRendering);
            Assert.False(renderer.GetCapabilityProfile().SupportsDeferredRendering);
            Assert.Equal(ShaderCompileTarget.Metal, renderer.ShaderCompileTarget);
            Assert.Throws<NotSupportedException>(() => renderer.Draw());
            Assert.Throws<NotSupportedException>(() => renderer.CreateRenderTarget(32, 32));
            Assert.Throws<NotSupportedException>(() => renderer.Render2D.Draw());
            Assert.Throws<NotSupportedException>(() => renderer.Render2D.DrawSprite(null));
        }

        /// <summary>Session disposal retires every remaining resource and rejects shader packages from another target API.</summary>
        [Fact]
        public void DisposalRetiresOwnedAssetsAndTargetMismatchFails() {
            EditorBuildRenderManager3D renderer = new(ShaderCompileTarget.Vulkan);
            using TextureAsset asset = new() {
                Width = 1, Height = 1, Colors = [1, 2, 3, 255], ColorFormat = TextureAssetColorFormat.Rgba32
            };
            using ModelAsset geometry = new() { Positions = [new float3(1, 2, 3)] };
            using ShaderMaterialAsset materialAsset = new() { ShaderAssetId = "shader/test", VertexProgram = "VS", PixelProgram = "PS" };
            using ShaderAsset shader = CreateShader(0);
            RuntimeTexture texture = renderer.Render2D.BuildTextureFromRaw(asset);
            EditorBuildRuntimeModel model = Assert.IsType<EditorBuildRuntimeModel>(renderer.BuildModelFromRaw(geometry));
            ShaderRuntimeMaterial material = Assert.IsType<ShaderRuntimeMaterial>(renderer.BuildMaterialFromRaw(materialAsset, shader));
            shader.TargetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.Metal);
            Assert.Throws<InvalidOperationException>(() => renderer.BuildMaterialFromRaw(materialAsset, shader));

            renderer.Dispose();
            renderer.Dispose();

            Assert.True(texture.IsDisposed);
            Assert.True(model.IsDisposed);
            Assert.Null(material.Layout);
            Assert.NotNull(geometry.Positions);
            Assert.Throws<ObjectDisposedException>(() => renderer.BuildModelFromRaw(geometry));
        }

        /// <summary>Native build targets retain their actual shader package API without changing legacy platform selections.</summary>
        /// <param name="platform">Public native build target.</param>
        /// <param name="expected">Shader package API required by that target.</param>
        [Theory]
        [InlineData("linux", ShaderCompileTarget.Vulkan)]
        [InlineData("macos", ShaderCompileTarget.Metal)]
        [InlineData("ios", ShaderCompileTarget.Metal)]
        [InlineData("windows", ShaderCompileTarget.DirectX11)]
        public void BuildTargetSelectsActualShaderApi(string platform, ShaderCompileTarget expected) {
            Assert.Equal(expected, EditorCliBuildRunner.ResolveShaderCompileTarget(platform));
        }

        /// <summary>Creates valid CPU shader binding metadata whose slot can change during hot reload.</summary>
        /// <param name="slot">Reflected constant-buffer slot.</param>
        /// <returns>Shader package metadata owned by the caller.</returns>
        static ShaderAsset CreateShader(int slot) {
            return new ShaderAsset {
                Id = "shader/test", TargetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan),
                Programs = [
                    new ShaderProgramAsset { Name = "VS", Stage = ShaderStage.Vertex, Bindings = [] },
                    new ShaderProgramAsset { Name = "PS", Stage = ShaderStage.Pixel, Bindings = [
                        new ShaderBindingAsset { Name = "Params", Type = ShaderResourceType.ConstantBuffer, Set = 0, Slot = slot, Size = 16, Members = [] }
                    ] }
                ]
            };
        }
    }
}

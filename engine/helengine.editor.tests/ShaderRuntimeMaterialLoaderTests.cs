using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shader-backed packaged material loading rebuilds runtime texture bindings from imported cooked texture assets.
    /// </summary>
    public sealed class ShaderRuntimeMaterialLoaderTests : IDisposable {
        /// <summary>
        /// Temporary packaged content root used by shader runtime material loader tests.
        /// </summary>
        readonly string ContentRootPath;

        /// <summary>
        /// Test render manager used to rebuild packaged shader-backed materials.
        /// </summary>
        readonly TestRenderManager3D RenderManager3DValue;

        /// <summary>
        /// Test render manager used to rebuild packaged imported textures.
        /// </summary>
        readonly TestRenderManager2D RenderManager2DValue;

        /// <summary>
        /// Core instance required by runtime texture materialization paths.
        /// </summary>
        readonly Core CoreValue;

        /// <summary>
        /// Initializes one isolated packaged-content workspace plus the shared core services required by shader-backed runtime loading.
        /// </summary>
        public ShaderRuntimeMaterialLoaderTests() {
            ContentRootPath = Path.Combine(Path.GetTempPath(), "helengine-shader-runtime-loader-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ContentRootPath);
            RenderManager3DValue = new TestRenderManager3D();
            RenderManager2DValue = new TestRenderManager2D();
            CoreValue = new Core();
            CoreValue.Initialize(RenderManager3DValue, RenderManager2DValue, new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases the temporary packaged-content workspace and the initialized core after each test.
        /// </summary>
        public void Dispose() {
            CoreValue.Dispose();
            if (Directory.Exists(ContentRootPath)) {
                Directory.Delete(ContentRootPath, true);
            }
        }

        /// <summary>
        /// Ensures packaged shader-backed materials rebuild their imported diffuse texture binding from the packaged imported texture asset path.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRawAsset_WhenMaterialReferencesImportedDiffuseTexture_BindsRuntimeTexture() {
            string diffuseTextureAssetId = "Textures/GeneratedChecker";
            WriteShaderPackage();
            WriteMaterialAsset(diffuseTextureAssetId);
            WriteImportedTextureAsset(diffuseTextureAssetId);

            ContentManager contentManager = new ContentManager(ContentRootPath);
            RuntimeMaterial runtimeMaterial = RenderManager3DValue.BuildMaterialFromRawAsset(
                contentManager,
                ContentRootPath,
                Path.Combine(ContentRootPath, "cooked", "materials", "TestMaterial.hasset"));

            RuntimeTexture runtimeTexture = ShaderRuntimeMaterialAccess.Require(runtimeMaterial).ResolveTexture();

            Assert.NotNull(runtimeTexture);
            Assert.Equal(4, runtimeTexture.Width);
            Assert.Equal(2, runtimeTexture.Height);
            Assert.True(RenderManager2DValue.BuildTextureFromRawCallCount > 0);
        }

        /// <summary>
        /// Writes one packaged shader asset exposing the standard diffuse texture binding required by the runtime loader.
        /// </summary>
        void WriteShaderPackage() {
            string shaderPath = Path.Combine(ContentRootPath, "cooked", "shaders", "ForwardStandardShader.vulkan.hasset");
            string shaderDirectoryPath = Path.GetDirectoryName(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderDirectoryPath)) {
                throw new InvalidOperationException("Could not resolve a shader directory path for the packaged shader test payload.");
            }

            Directory.CreateDirectory(shaderDirectoryPath);
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "ForwardStandardShader",
                TargetName = "vulkan",
                Programs = [
                    CreateProgram("ForwardStandardShader.vs", ShaderStage.Vertex),
                    CreateProgram(
                        "ForwardStandardShader.ps",
                        ShaderStage.Pixel,
                        CreateBinding(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, ShaderResourceType.Texture2D, 0, 0, 0))
                ],
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };

            using FileStream stream = File.Create(shaderPath);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Writes one packaged shader-backed material asset that references the supplied imported diffuse texture asset id.
        /// </summary>
        /// <param name="diffuseTextureAssetId">Imported texture asset id that should be rebound by the runtime loader.</param>
        void WriteMaterialAsset(string diffuseTextureAssetId) {
            string materialPath = Path.Combine(ContentRootPath, "cooked", "materials", "TestMaterial.hasset");
            string materialDirectoryPath = Path.GetDirectoryName(materialPath);
            if (string.IsNullOrWhiteSpace(materialDirectoryPath)) {
                throw new InvalidOperationException("Could not resolve a material directory path for the packaged material test payload.");
            }

            Directory.CreateDirectory(materialDirectoryPath);
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = "Materials/TestMaterial",
                ShaderAssetId = "ForwardStandardShader",
                VertexProgram = "ForwardStandardShader.vs",
                PixelProgram = "ForwardStandardShader.ps",
                Variant = "default",
                DiffuseTextureAssetId = diffuseTextureAssetId,
                RenderState = new MaterialRenderState(),
                ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>(),
                CastsShadows = true,
                ReceivesShadows = true
            };

            using FileStream stream = File.Create(materialPath);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Writes one packaged imported texture asset that can be rebuilt by the runtime material loader.
        /// </summary>
        /// <param name="diffuseTextureAssetId">Imported texture asset id that determines the packaged texture path.</param>
        void WriteImportedTextureAsset(string diffuseTextureAssetId) {
            string texturePath = Path.Combine(ContentRootPath, "cooked", "imported", diffuseTextureAssetId);
            string textureDirectoryPath = Path.GetDirectoryName(texturePath);
            if (string.IsNullOrWhiteSpace(textureDirectoryPath)) {
                throw new InvalidOperationException("Could not resolve a texture directory path for the packaged imported texture test payload.");
            }

            Directory.CreateDirectory(textureDirectoryPath);
            TextureAsset textureAsset = new TextureAsset {
                Width = 4,
                Height = 2,
                Colors = new byte[] {
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                    0, 0, 255, 255,
                    255, 255, 0, 255,
                    255, 0, 255, 255,
                    0, 255, 255, 255,
                    255, 255, 255, 255,
                    0, 0, 0, 255
                }
            };

            using FileStream stream = File.Create(texturePath);
            AssetSerializer.Serialize(stream, textureAsset);
        }

        /// <summary>
        /// Creates one minimal shader program asset with the supplied binding list.
        /// </summary>
        /// <param name="name">Program name stored in the packaged shader asset.</param>
        /// <param name="stage">Shader stage implemented by the program.</param>
        /// <param name="bindings">Bindings exposed by the program.</param>
        /// <returns>Minimal shader program asset.</returns>
        static ShaderProgramAsset CreateProgram(string name, ShaderStage stage, params ShaderBindingAsset[] bindings) {
            return new ShaderProgramAsset {
                Name = name,
                Stage = stage,
                Bindings = bindings ?? Array.Empty<ShaderBindingAsset>()
            };
        }

        /// <summary>
        /// Creates one minimal shader binding asset for the supplied binding contract.
        /// </summary>
        /// <param name="name">Binding name.</param>
        /// <param name="resourceType">Binding resource type.</param>
        /// <param name="set">Binding set index.</param>
        /// <param name="slot">Binding slot index.</param>
        /// <param name="sizeInBytes">Binding size in bytes.</param>
        /// <returns>Minimal shader binding asset.</returns>
        static ShaderBindingAsset CreateBinding(string name, ShaderResourceType resourceType, int set, int slot, int sizeInBytes) {
            return new ShaderBindingAsset {
                Name = name,
                Type = resourceType,
                Set = set,
                Slot = slot,
                Size = sizeInBytes,
                Members = Array.Empty<ShaderConstantMemberAsset>()
            };
        }
    }
}

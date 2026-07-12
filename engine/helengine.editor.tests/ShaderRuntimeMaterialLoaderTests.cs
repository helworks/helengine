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
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(ContentRootPath)
            });
            CoreValue.Initialize(RenderManager3DValue, RenderManager2DValue, new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(ContentRootPath)
            });
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
            WriteMaterialAsset(diffuseTextureAssetId, string.Empty, string.Empty);
            WriteImportedTextureAsset(diffuseTextureAssetId);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ContentRootPath));
            RuntimeMaterial runtimeMaterial = RenderManager3DValue.BuildMaterialFromRawAsset(
                contentManager,
                Path.Combine(ContentRootPath, "cooked", "materials", "TestMaterial.hasset"));

            RuntimeTexture runtimeTexture = ShaderRuntimeMaterialAccess.Require(runtimeMaterial).ResolveTexture();

            Assert.NotNull(runtimeTexture);
            Assert.Equal(4, runtimeTexture.Width);
            Assert.Equal(2, runtimeTexture.Height);
            Assert.True(RenderManager2DValue.BuildTextureFromRawCallCount > 0);
        }

        /// <summary>
        /// Ensures packaged shader-backed materials rebuild both diffuse and roughness imported texture bindings from the packaged imported texture asset paths.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRawAsset_WhenMaterialReferencesImportedDiffuseAndRoughnessTextures_BindsBothRuntimeTextures() {
            string diffuseTextureAssetId = "Textures/GeneratedChecker";
            string roughnessTextureAssetId = "Textures/GeneratedRoughness";
            WriteShaderPackage();
            WriteMaterialAsset(diffuseTextureAssetId, string.Empty, roughnessTextureAssetId);
            WriteImportedTextureAsset(diffuseTextureAssetId, 4, 2);
            WriteImportedTextureAsset(roughnessTextureAssetId, 2, 4);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ContentRootPath));
            RuntimeMaterial runtimeMaterial = RenderManager3DValue.BuildMaterialFromRawAsset(
                contentManager,
                Path.Combine(ContentRootPath, "cooked", "materials", "TestMaterial.hasset"));

            ShaderRuntimeMaterial shaderRuntimeMaterial = ShaderRuntimeMaterialAccess.Require(runtimeMaterial);
            int diffuseBindingIndex = shaderRuntimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            int roughnessBindingIndex = shaderRuntimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName);
            RuntimeTexture diffuseTexture = shaderRuntimeMaterial.Properties.GetTexture(diffuseBindingIndex);
            RuntimeTexture roughnessTexture = shaderRuntimeMaterial.Properties.GetTexture(roughnessBindingIndex);

            Assert.NotNull(diffuseTexture);
            Assert.NotNull(roughnessTexture);
            Assert.Equal(4, diffuseTexture.Width);
            Assert.Equal(2, diffuseTexture.Height);
            Assert.Equal(2, roughnessTexture.Width);
            Assert.Equal(4, roughnessTexture.Height);
        }

        /// <summary>
        /// Ensures packaged shader-backed materials rebuild their imported emissive texture binding from the packaged imported texture asset path.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRawAsset_WhenMaterialReferencesImportedEmissiveTexture_BindsRuntimeTexture() {
            string emissiveTextureAssetId = "Textures/GeneratedEmissive";
            WriteShaderPackage();
            WriteMaterialAsset(string.Empty, emissiveTextureAssetId, string.Empty);
            WriteImportedTextureAsset(emissiveTextureAssetId, 3, 5);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ContentRootPath));
            RuntimeMaterial runtimeMaterial = RenderManager3DValue.BuildMaterialFromRawAsset(
                contentManager,
                Path.Combine(ContentRootPath, "cooked", "materials", "TestMaterial.hasset"));

            ShaderRuntimeMaterial shaderRuntimeMaterial = ShaderRuntimeMaterialAccess.Require(runtimeMaterial);
            int emissiveBindingIndex = shaderRuntimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName);
            RuntimeTexture emissiveTexture = shaderRuntimeMaterial.Properties.GetTexture(emissiveBindingIndex);

            Assert.NotNull(emissiveTexture);
            Assert.Equal(3, emissiveTexture.Width);
            Assert.Equal(5, emissiveTexture.Height);
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
                        CreateBinding(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, ShaderResourceType.Texture2D, 0, 0, 0),
                        CreateBinding(StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName, ShaderResourceType.Texture2D, 0, 7, 0),
                        CreateBinding(StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName, ShaderResourceType.Texture2D, 0, 6, 0))
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
        /// <param name="emissiveTextureAssetId">Imported emissive texture asset id that should be rebound by the runtime loader.</param>
        /// <param name="roughnessTextureAssetId">Imported roughness texture asset id that should be rebound by the runtime loader.</param>
        void WriteMaterialAsset(string diffuseTextureAssetId, string emissiveTextureAssetId, string roughnessTextureAssetId) {
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
                EmissiveTextureAssetId = emissiveTextureAssetId,
                RoughnessTextureAssetId = roughnessTextureAssetId,
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
        /// <param name="textureAssetId">Imported texture asset id that determines the packaged texture path.</param>
        /// <param name="width">Authored width used by the packaged texture payload.</param>
        /// <param name="height">Authored height used by the packaged texture payload.</param>
        void WriteImportedTextureAsset(string textureAssetId, int width = 4, int height = 2) {
            string texturePath = Path.Combine(ContentRootPath, "cooked", "imported", textureAssetId);
            string textureDirectoryPath = Path.GetDirectoryName(texturePath);
            if (string.IsNullOrWhiteSpace(textureDirectoryPath)) {
                throw new InvalidOperationException("Could not resolve a texture directory path for the packaged imported texture test payload.");
            }

            Directory.CreateDirectory(textureDirectoryPath);
            TextureAsset textureAsset = new TextureAsset {
                Width = (ushort)width,
                Height = (ushort)height,
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
                Set = (ushort)set,
                Slot = (ushort)slot,
                Size = sizeInBytes,
                Members = Array.Empty<ShaderConstantMemberAsset>()
            };
        }
    }
}


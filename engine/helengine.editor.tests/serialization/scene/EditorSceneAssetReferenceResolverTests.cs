using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies generated material references resolve through the generated-asset registry.
    /// </summary>
    public class EditorSceneAssetReferenceResolverTests : IDisposable {
        /// <summary>
        /// Temporary project root used by generated material resolver tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the resolver.
        /// </summary>
        public EditorSceneAssetReferenceResolverTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-material-resolver-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state and clears provider registrations.
        /// </summary>
        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated material references resolve through the generated-asset provider registry.
        /// </summary>
        [Fact]
        public void ResolveMaterial_WhenReferenceIsGenerated_UsesGeneratedAssetProviderRegistry() {
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
                },
                new TestRuntimeModel(),
                runtimeMaterial));
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(new ContentManager(TempProjectRootPath), TempProjectRootPath);

            RuntimeMaterial resolvedMaterial = resolver.ResolveMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Materials/Standard",
                ProviderId = "engine",
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            });

            Assert.Same(runtimeMaterial, resolvedMaterial);
        }

        /// <summary>
        /// Ensures filesystem-backed model references resolve through the processed model cache instead of deserializing the raw source file.
        /// </summary>
        [Fact]
        public void ResolveModel_WhenReferenceIsFileSystem_UsesImportedModelAsset() {
            string modelPath = Path.Combine(TempProjectRootPath, "assets", "Models", "Sponza.obj");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            File.WriteAllText(modelPath, "raw obj source");
            AssetImportManager assetImportManager = CreateAssetImportManager();
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
                new ContentManager(TempProjectRootPath),
                TempProjectRootPath,
                new EditorFileSystemModelResolver(assetImportManager));

            RuntimeModel runtimeModel = resolver.ResolveModel(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Models/Sponza.obj"
            });

            Assert.NotNull(runtimeModel);
            ModelAsset builtModelAsset = Assert.Single(renderManager.BuiltModelAssets);
            Assert.Equal(new ushort[] { 0, 1, 2 }, builtModelAsset.Indices16);
        }

        /// <summary>
        /// Ensures filesystem-backed source font references resolve through the imported font cache instead of loading raw source bytes as a packaged font.
        /// </summary>
        [Fact]
        public void ResolveFont_WhenReferenceIsSourceFont_UsesImportedFontAsset() {
            string fontPath = Path.Combine(TempProjectRootPath, "assets", "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(fontPath));
            File.WriteAllBytes(fontPath, new byte[] { 1, 2, 3, 4 });
            AssetImportManager assetImportManager = CreateAssetImportManager();
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
                new ContentManager(TempProjectRootPath),
                TempProjectRootPath,
                new EditorFileSystemModelResolver(assetImportManager),
                new EditorFileSystemFontResolver(assetImportManager));

            FontAsset font = resolver.ResolveFont(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Fonts/DemoDiscTitle.ttf"
            });

            Assert.Equal("ImportedTestFont", font.FontInfo.Name);
        }

        /// <summary>
        /// Ensures file-backed materials that point at built-in shaders can still resolve in the editor when no cached shader package exists yet.
        /// </summary>
        [Fact]
        public void ResolveMaterial_WhenFileSystemMaterialUsesBuiltInShader_LoadsBuiltInShaderWithoutCachedPackage() {
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.helmat";
            string materialFullPath = WriteMaterialAsset(materialRelativePath, new MaterialAsset {
                Id = "Materials.rendering.colored_cube_grid.Cube00",
                ShaderAssetId = "ForwardStandardShader",
                VertexProgram = "ForwardStandardShader.vs",
                PixelProgram = "ForwardStandardShader.ps",
                Variant = "default",
                RenderState = new MaterialRenderState()
            });
            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            EditorProjectPaths.Initialize(TempProjectRootPath);
            using ShaderModuleManager shaderModuleManager = CreateShaderModuleManager();
            EditorShaderPackageService.Initialize(shaderModuleManager, ShaderCompileTarget.DirectX11, contentManager);
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(contentManager, TempProjectRootPath);

            RuntimeMaterial material = resolver.ResolveMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = materialRelativePath
            });

            Assert.NotNull(material);
            Assert.True(File.Exists(materialFullPath));
        }

        /// <summary>
        /// Ensures file-backed schema-driven standard materials hydrate their authored base-color buffer before the runtime material is built for editor scene loading.
        /// </summary>
        [Fact]
        public void ResolveMaterial_WhenFileSystemMaterialHasStandardShaderBaseColorSettings_AppliesBaseColorBuffer() {
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.helmat";
            WriteMaterialAsset(materialRelativePath, new MaterialAsset {
                Id = "Materials.rendering.colored_cube_grid.Cube00",
                ShaderAssetId = "ForwardStandardShader",
                VertexProgram = "ForwardStandardShader.vs",
                PixelProgram = "ForwardStandardShader.ps",
                Variant = "default",
                RenderState = new MaterialRenderState()
            });
            WriteMaterialSettings(materialRelativePath, CreateStandardMaterialSettings("#336699"));
            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            EditorProjectPaths.Initialize(TempProjectRootPath);
            using ShaderModuleManager shaderModuleManager = CreateShaderModuleManager();
            EditorShaderPackageService.Initialize(shaderModuleManager, ShaderCompileTarget.DirectX11, contentManager);
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(contentManager, TempProjectRootPath);

            RuntimeMaterial material = resolver.ResolveMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = materialRelativePath
            });

            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            MaterialAsset builtMaterialAsset = Assert.Single(renderManager.BuiltMaterialAssets);

            Assert.NotNull(material);
            MaterialConstantBufferAsset baseColorBuffer = Assert.Single(builtMaterialAsset.ConstantBuffers, buffer => buffer.Name == StandardMaterialBaseColorDefaults.BaseColorBufferName);
            Assert.Equal(
                StandardMaterialBaseColorDefaults.CreateConstantBufferData(new float4(0x33 / 255f, 0x66 / 255f, 0x99 / 255f, 1f)),
                baseColorBuffer.Data);
        }

        /// <summary>
        /// Creates one asset import manager that can import `.obj` source files for the current resolver test project.
        /// </summary>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateAssetImportManager() {
            ContentManager contentManager = new ContentManager(Path.Combine(TempProjectRootPath, "assets"));
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }));
            return assetImportManager;
        }

        /// <summary>
        /// Creates a shader module manager that can be disposed safely during tests without requiring any authored shader files.
        /// </summary>
        /// <returns>Disposable shader module manager for the test project.</returns>
        ShaderModuleManager CreateShaderModuleManager() {
            string shaderRootPath = Path.Combine(TempProjectRootPath, "assets", "Shaders");
            string packageOutputPath = Path.Combine(TempProjectRootPath, "cache", "shader-cache");
            Directory.CreateDirectory(shaderRootPath);
            Directory.CreateDirectory(packageOutputPath);

            ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
            ShaderPackageBuildOptions buildOptions = new ShaderPackageBuildOptions(
                new[] { targetOptions },
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                Array.Empty<ShaderDefine>());
            ShaderModuleManagerOptions options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                ShaderCompileTarget.DirectX11,
                100);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Writes one serialized material asset under the temporary project assets root.
        /// </summary>
        /// <param name="relativePath">Project-relative material path to create.</param>
        /// <param name="materialAsset">Serialized material payload to write.</param>
        /// <returns>Absolute path to the written material asset.</returns>
        string WriteMaterialAsset(string relativePath, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            string fullPath = Path.Combine(TempProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Material directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = File.Create(fullPath);
            global::helengine.editor.AssetSerializer.Serialize(stream, materialAsset);
            return fullPath;
        }

        /// <summary>
        /// Writes one material settings sidecar under the temporary project assets root.
        /// </summary>
        /// <param name="relativePath">Project-relative material path whose sidecar should be written.</param>
        /// <param name="settings">Material settings payload to persist.</param>
        void WriteMaterialSettings(string relativePath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string materialFullPath = Path.Combine(TempProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialFullPath, settings);
        }

        /// <summary>
        /// Creates one standard-shader material settings sidecar payload with the supplied authored base color.
        /// </summary>
        /// <param name="baseColor">Authored base color in editor HTML hex form.</param>
        /// <returns>Material settings payload for the standard shader schema.</returns>
        AssetImportSettings CreateStandardMaterialSettings(string baseColor) {
            if (string.IsNullOrWhiteSpace(baseColor)) {
                throw new ArgumentException("Base color must be provided.", nameof(baseColor));
            }

            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "helengine.material";
            settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Material = new MaterialAssetProcessorSettings {
                    SchemaId = "standard-shader",
                    FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                        ["use-custom-shader"] = "false",
                        ["texture-id"] = string.Empty,
                        ["casts-shadow"] = "true",
                        ["receives-shadow"] = "true",
                        ["base-color"] = baseColor
                    }
                }
            };
            return settings;
        }
    }
}

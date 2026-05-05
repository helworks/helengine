using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies Windows scene packaging collects referenced shader ids from packaged material assets.
    /// </summary>
    public class EditorPlatformBuildScenePackagerTests : IDisposable {
        /// <summary>
        /// Temporary project root used for scene-packager tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used for scene-packager tests.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated project workspace for scene packaging verification.
        /// </summary>
        public EditorPlatformBuildScenePackagerTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-packager-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache", "shader-cache"));
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the temporary project workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a packaged scene referencing one material reports one deduplicated shader id.
        /// </summary>
        [Fact]
        public void Package_WhenTwoMeshesReferenceTheSameShader_ReportsOneReferencedShaderId() {
            string sceneId = "Scenes/TestScene.helen";
            string materialRelativePath = "Materials/TestMaterial.helmat";
            string shaderAssetId = "ForwardStandardShader";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteMaterialAsset(materialRelativePath, shaderAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.Equal(new[] { shaderAssetId }, result.ReferencedShaderAssetIds);
        }

        /// <summary>
        /// Ensures packaged scenes preserve FPS overlay components for the player runtime loader.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/FpsScene.helen";

            WriteSceneAsset(sceneId, "Helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = Path.Combine(
                BuildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("helengine.FPSComponent", packagedScene.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/fonts/default.hefont", packagedScene.AssetReferences[0].RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont")));
        }

        /// <summary>
        /// Ensures packaged scenes rewrite text component font references into file-backed assets.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsTextComponent_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/TextScene.helen";

            WriteSceneAsset(sceneId, "Helengine.TextComponent", WriteTextComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = Path.Combine(
                BuildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("helengine.TextComponent", packagedScene.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/fonts/default.hefont", packagedScene.AssetReferences[0].RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont")));

            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            TextComponent loadedTextComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is TextComponent));

            Assert.Equal("Hello world", loadedTextComponent.Text);
            Assert.NotNull(loadedTextComponent.Font);
            Assert.Equal(defaultFont.FontInfo.Name, loadedTextComponent.Font.FontInfo.Name);
        }

        /// <summary>
        /// Ensures packaged scenes preserve baked demo menu components and their file-backed font dependencies for the player runtime loader.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsDemoMenuBuildComponent_LeavesPackagedComponentLoadable() {
            string menuSceneId = "Scenes/MenuScene.helen";
            string playableSceneId = "Scenes/TestPlayableScene.helen";

            WriteFontAsset("fonts/title.hefont", CreatePackagedFontAsset());
            WriteFontAsset("fonts/body.hefont", CreatePackagedFontAsset());
            WriteSceneAsset(menuSceneId, BuildDemoMenuSceneAsset(menuSceneId));
            WriteEmptySceneAsset(playableSceneId);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { menuSceneId, playableSceneId }, BuildRootPath);

            string packagedMenuScenePath = Path.Combine(
                BuildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string packagedPlayableScenePath = Path.Combine(
                BuildRootPath,
                "scenes",
                "Scenes",
                "TestPlayableScene.hasset");
            Assert.True(File.Exists(packagedMenuScenePath));
            Assert.True(File.Exists(packagedPlayableScenePath));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "fonts", "title.hefont")));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "fonts", "body.hefont")));

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedMenuScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = BuildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());

            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            Assert.Equal(2, loadedRoots.Count);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is DemoMenuBuildComponent));
            DemoMenuBuildComponent menuHostComponent = Assert.IsType<DemoMenuBuildComponent>(
                Assert.Single(loadedRoot.Components, component => component is DemoMenuBuildComponent));

            Assert.False(menuHostComponent.IsInitialized);
            Assert.Single(loadedRoot.Children);
            Assert.NotEmpty(loadedRoot.Children[0].Children);
        }

        /// <summary>
        /// Ensures packaged lights and rounded rectangles are rewritten into strict runtime payloads that still load correctly.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsLightsAndRoundedRect_LeavesPackagedComponentsLoadable() {
            string sceneId = "Scenes/LightingUiScene.helen";
            WriteSceneAsset(sceneId, BuildLightingAndUiSceneAsset(sceneId));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = Path.Combine(
                BuildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            DirectionalLightComponent directionalLightComponent = Assert.IsType<DirectionalLightComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is DirectionalLightComponent));
            PointLightComponent pointLightComponent = Assert.IsType<PointLightComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is PointLightComponent));
            SpotLightComponent spotLightComponent = Assert.IsType<SpotLightComponent>(
                Assert.Single(loadedRoots[2].Components, component => component is SpotLightComponent));
            RoundedRectComponent roundedRectComponent = Assert.IsType<RoundedRectComponent>(
                Assert.Single(loadedRoots[3].Components, component => component is RoundedRectComponent));

            Assert.Equal(72f, directionalLightComponent.ShadowDistance);
            Assert.Equal(18f, pointLightComponent.Range);
            Assert.Equal(22f, spotLightComponent.Range);
            Assert.Equal(18f, spotLightComponent.InnerConeAngleDegrees);
            Assert.Equal(31f, spotLightComponent.OuterConeAngleDegrees);
            Assert.Equal(14f, roundedRectComponent.Radius);
            Assert.Equal(new byte4(4, 8, 12, 255), roundedRectComponent.FillColor);
            Assert.Equal(new byte4(80, 120, 160, 255), roundedRectComponent.BorderColor);
        }

        /// <summary>
        /// Ensures file-system model references in the scene manifest are imported instead of copied raw.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesFileSystemModel_ImportsModelAssetInsteadOfCopyingObj() {
            string sceneId = "Scenes/SponzaScene.helen";
            string sourceModelRelativePath = "Models/Sponza.obj";
            string sourceModelPath = Path.Combine(ProjectRootPath, "assets", sourceModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath));
            File.WriteAllText(sourceModelPath, "o Sponza\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

            IAssetImporterRegistration importerRegistration = new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" });

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                AssetReferences = new[] {
                    new SceneAssetReference {
                        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                        RelativePath = sourceModelRelativePath,
                        ProviderId = string.Empty,
                        AssetId = string.Empty
                    }
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                new[] { importerRegistration },
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string importedModelPath = Path.Combine(BuildRootPath, "cooked", "imported", "Models", "Sponza.hasset");
            Assert.True(File.Exists(importedModelPath));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));

            using FileStream packagedSceneStream = File.OpenRead(Path.Combine(
                BuildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/imported/Models/Sponza.hasset", packagedScene.AssetReferences[0].RelativePath);
        }

        /// <summary>
        /// Ensures the committed point-shadow smoke scene packages without failing the component rewrite pipeline.
        /// </summary>
        [Fact]
        public void Package_WhenUsingCommittedPointShadowScene_PackagesSuccessfully() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string sourceScenePath = Path.Combine(repositoryRootPath, "test-project", "assets", "Scenes", "rendering", "point-shadow.helen");
            string sceneId = "Scenes/rendering/point-shadow.helen";
            string targetScenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "rendering", "point-shadow.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(targetScenePath));
            File.Copy(sourceScenePath, targetScenePath, true);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(result);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        /// <summary>
        /// Ensures the build packager exports a default packaged font asset that the runtime content pipeline can reload.
        /// </summary>
        [Fact]
        public void Package_WhenDefaultFontAssetIsAvailable_WritesPackagedDefaultFontThatCanBeReloaded() {
            string sceneId = "Scenes/FontScene.helen";
            WriteSceneAsset(sceneId, "Helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont");
            Assert.True(File.Exists(packagedFontPath));

            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            FontAsset loadedFont = runtimeContentManager.Load<FontAsset>(packagedFontPath, RuntimeContentProcessorIds.FontAsset);

            Assert.Equal(defaultFont.FontInfo.Name, loadedFont.FontInfo.Name);
            Assert.Equal(defaultFont.FontInfo.LineSpacing, loadedFont.FontInfo.LineSpacing);
            Assert.Equal(defaultFont.FontInfo.SpaceWidth, loadedFont.FontInfo.SpaceWidth);
            Assert.Equal(defaultFont.LineHeight, loadedFont.LineHeight);
            Assert.Equal(defaultFont.AtlasWidth, loadedFont.AtlasWidth);
            Assert.Equal(defaultFont.AtlasHeight, loadedFont.AtlasHeight);
            Assert.NotNull(loadedFont.Texture);
        }

        /// <summary>
        /// Ensures platform-provided compatibility metadata can reject unsupported components with a clear reason.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformMarksComponentUnsupported_FailsWithTheBuilderReason() {
            PlatformDefinition definition = new(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [])
                ],
                [
                    new PlatformAssetRequirementDefinition(
                        "texture",
                        "Texture",
                        true,
                        ["png", "tga"])
                ],
                [
                    new PlatformComponentCompatibilityDefinition(
                        "helengine.BadComponent",
                        PlatformComponentCompatibilityKind.Unsupported,
                        "This platform does not support the component.",
                        "Remove the component before building.")
                ]);

            string sceneId = "Scenes/Bad.helen";
            WriteSceneAsset(sceneId, "helengine.BadComponent", Array.Empty<byte>());

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                definition);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                packager.Package(new[] { sceneId }, BuildRootPath));

            Assert.Contains("This platform does not support the component.", ex.Message);
            Assert.Contains("Remove the component before building.", ex.Message);
        }

        /// <summary>
        /// Writes one serialized scene asset that references the supplied material path from a mesh component payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="materialRelativePath">Project-relative material path to reference.</param>
        void WriteSceneAsset(string sceneId, string materialRelativePath) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "second-root-entity",
                        Name = "SecondRoot",
                        LocalPosition = new float3(1f, 0f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one serialized scene asset that contains a single component record with the supplied type id and payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="componentTypeId">Serialized component type id to encode.</param>
        /// <param name="payload">Serialized component payload to attach.</param>
        void WriteSceneAsset(string sceneId, string componentTypeId, byte[] payload, SceneAssetReference[] assetReferences = null) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = componentTypeId,
                                ComponentIndex = 0,
                                Payload = payload
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one complete serialized scene asset to the test project.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="sceneAsset">Complete scene asset to serialize.</param>
        void WriteSceneAsset(string sceneId, SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates the generated scene reference used for the editor's built-in font asset.
        /// </summary>
        /// <returns>Generated editor font scene reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }

        /// <summary>
        /// Creates the file-backed font reference used by packaged demo-disc menu scenes.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <returns>File-backed scene reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Writes one serialized scene asset containing a single empty root entity.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        void WriteEmptySceneAsset(string sceneId) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one packaged font asset into the source project assets folder.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <param name="fontAsset">Font asset to serialize.</param>
        void WriteFontAsset(string relativePath, FontAsset fontAsset) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Creates a packaged font asset with exportable raw atlas data.
        /// </summary>
        /// <returns>Font asset with export data attached for packaging.</returns>
        FontAsset CreatePackagedFontAsset() {
            byte[] colors = new byte[64];
            for (int index = 0; index < colors.Length; index += 4) {
                byte pixelIndex = (byte)(index / 4);
                colors[index] = (byte)(pixelIndex * 16);
                colors[index + 1] = (byte)(255 - (pixelIndex * 16));
                colors[index + 2] = (byte)(pixelIndex * 8);
                colors[index + 3] = 255;
            }

            TextureAsset textureAsset = new TextureAsset {
                Width = 4,
                Height = 4,
                Colors = colors
            };

            FontAsset font = new FontAsset(
                new FontInfo("PackagedTest", 16, 4f),
                new TestRuntimeTexture {
                    Width = textureAsset.Width,
                    Height = textureAsset.Height
                },
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 1f, 1f), 0f, 1f, 0f, 0f)
                },
                16f,
                textureAsset.Width,
                textureAsset.Height);

            font.SourceTextureAsset = textureAsset;
            return font;
        }

        /// <summary>
        /// Writes one serialized material asset that references the supplied shader asset id.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        /// <param name="shaderAssetId">Shader asset id referenced by the material.</param>
        void WriteMaterialAsset(string materialRelativePath, string shaderAssetId) {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAsset materialAsset = new MaterialAsset {
                Id = materialRelativePath,
                ShaderAssetId = shaderAssetId,
                VertexProgram = string.Concat(shaderAssetId, ".vs"),
                PixelProgram = string.Concat(shaderAssetId, ".ps"),
                Variant = "default",
                RenderState = new MaterialRenderState(),
                ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
            };

            using FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Writes one compiled shader package into the local editor shader cache.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to encode.</param>
        /// <param name="target">Shader compile target to encode.</param>
        void WriteShaderCachePackage(string shaderAssetId, ShaderCompileTarget target) {
            string packagePath = ShaderPackagePaths.GetPackagePath(Path.Combine(ProjectRootPath, "cache", "shader-cache"), shaderAssetId, target);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));

            ShaderAsset shaderAsset = new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = ShaderTargetNames.GetTargetName(target),
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = new[] {
                            new ShaderVariantAsset {
                                Name = "default",
                                Defines = Array.Empty<string>()
                            }
                        }
                    }
                },
                Binaries = new[] {
                    new ShaderBinaryAsset {
                        ProgramName = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        TargetName = ShaderTargetNames.GetTargetName(target),
                        Variant = "default",
                        Bytecode = new byte[] { 1, 2, 3, 4 }
                    }
                }
            };

            using FileStream stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Writes one mesh-component payload that points at the supplied material path.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string materialRelativePath) {
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Material = new TestRuntimeMaterial()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Material", new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = materialRelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            });

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized FPS component payload.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayload() {
            FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();
            FPSComponent fpsComponent = new FPSComponent {
                Font = CreatePackagedFontAsset(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", CreateEditorFontReference());

            SceneComponentAssetRecord record = descriptor.SerializeComponent(fpsComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized text component payload.
        /// </summary>
        /// <returns>Serialized text component payload.</returns>
        byte[] WriteTextComponentPayload() {
            TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", CreateEditorFontReference());

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized directional light component payload.
        /// </summary>
        /// <returns>Serialized directional light payload.</returns>
        byte[] WriteDirectionalLightPayload() {
            DirectionalLightComponentPersistenceDescriptor descriptor = new DirectionalLightComponentPersistenceDescriptor();
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(0.6f, 0.7f, 0.8f, 1f),
                Intensity = 1.8f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.65f,
                ShadowDistance = 72f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized point light component payload.
        /// </summary>
        /// <returns>Serialized point light payload.</returns>
        byte[] WritePointLightPayload() {
            PointLightComponentPersistenceDescriptor descriptor = new PointLightComponentPersistenceDescriptor();
            PointLightComponent lightComponent = new PointLightComponent {
                Color = new float4(1f, 0.9f, 0.7f, 1f),
                Intensity = 2.2f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.5f,
                Range = 18f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized spot light component payload.
        /// </summary>
        /// <returns>Serialized spot light payload.</returns>
        byte[] WriteSpotLightPayload() {
            SpotLightComponentPersistenceDescriptor descriptor = new SpotLightComponentPersistenceDescriptor();
            SpotLightComponent lightComponent = new SpotLightComponent {
                Color = new float4(0.5f, 0.8f, 1f, 1f),
                Intensity = 3.1f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.7f,
                Range = 22f,
                InnerConeAngleDegrees = 18f,
                OuterConeAngleDegrees = 31f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized rounded rectangle component payload.
        /// </summary>
        /// <returns>Serialized rounded rectangle payload.</returns>
        byte[] WriteRoundedRectPayload() {
            RoundedRectComponentPersistenceDescriptor descriptor = new RoundedRectComponentPersistenceDescriptor();
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent {
                RenderOrder2D = 8,
                LayerMask = 3,
                Corners = RoundedRectCorners.All,
                Rotation = 0.45f,
                Color = new byte4(1, 2, 3, 4),
                SourceRect = new float4(0.2f, 0.3f, 0.4f, 0.5f),
                Size = new int2(280, 120),
                Radius = 14f,
                BorderThickness = 3f,
                FillColor = new byte4(4, 8, 12, 255),
                BorderColor = new byte4(80, 120, 160, 255)
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(roundedRectComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Builds one baked demo menu scene asset for packaged runtime-load verification.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the baked asset.</param>
        /// <returns>Baked demo menu scene asset.</returns>
        SceneAsset BuildDemoMenuSceneAsset(string sceneId) {
            DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();
            return factory.BuildSceneAsset(
                sceneId,
                typeof(TestMenuDefinitionProvider).AssemblyQualifiedName,
                new MenuDefinition(
                    "Demo",
                    "Packager",
                    "main",
                    "fonts/title.hefont",
                    "fonts/body.hefont",
                    new byte4(10, 10, 20, 255),
                    new byte4(30, 30, 50, 255),
                    new byte4(60, 60, 90, 255),
                    new byte4(120, 120, 255, 255),
                    new byte4(80, 180, 200, 255),
                    new byte4(255, 255, 255, 255),
                    new byte4(210, 210, 220, 255),
                    new[] {
                        new MenuPanelDefinition(
                            "main",
                            "Main Menu",
                            "Packager test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("select-scene", "Select Scene", "Loads a scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "Scenes/TestPlayableScene.helen")),
                                new MenuItemDefinition("back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            })
                    }));
        }

        /// <summary>
        /// Builds one authored scene containing lights and a rounded-rectangle visual for packaged runtime verification.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the asset.</param>
        /// <returns>Scene asset containing tagged editor payloads for packaged runtime verification.</returns>
        SceneAsset BuildLightingAndUiSceneAsset(string sceneId) {
            return new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "directional-root",
                        Name = "Directional",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.DirectionalLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteDirectionalLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "point-root",
                        Name = "Point",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.PointLightComponent",
                                ComponentIndex = 0,
                                Payload = WritePointLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "spot-root",
                        Name = "Spot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.SpotLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteSpotLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "rounded-root",
                        Name = "RoundedRect",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RoundedRectComponent",
                                ComponentIndex = 0,
                                Payload = WriteRoundedRectPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
        }

        /// <summary>
        /// Writes the stable editor-font reference used by packaged FPS overlays.
        /// </summary>
        /// <param name="writer">Writer receiving the reference payload.</param>
        void WriteEditorFontReference(EngineBinaryWriter writer) {
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.Generated);
            writer.WriteString("generated/editor/fonts/ui.hefont");
            writer.WriteString("editor");
            writer.WriteString("ui-font");
        }
    }
}

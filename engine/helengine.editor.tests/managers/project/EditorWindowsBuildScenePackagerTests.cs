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
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "shader-cache"));
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
            string shaderAssetId = "EditorDefaultMesh";

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

            string packagedScenePath = Path.Combine(BuildRootPath, "scenes", "Scenes", "FpsScene.helen");
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

            string packagedScenePath = Path.Combine(BuildRootPath, "scenes", "Scenes", "TextScene.helen");
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

            string importedModelPath = Path.Combine(BuildRootPath, "cooked", "imported", "Models", "Sponza.model.asset");
            Assert.True(File.Exists(importedModelPath));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));

            using FileStream packagedSceneStream = File.OpenRead(Path.Combine(BuildRootPath, "cooked", "scenes", "Scenes", "SponzaScene.helen"));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/imported/Models/Sponza.model.asset", packagedScene.AssetReferences[0].RelativePath);
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
            string packagePath = ShaderPackagePaths.GetPackagePath(Path.Combine(ProjectRootPath, "shader-cache"), shaderAssetId, target);
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
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(0);
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.FileSystem);
            writer.WriteString(materialRelativePath);
            writer.WriteString(string.Empty);
            writer.WriteString(string.Empty);
            writer.WriteByte(0);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized FPS component payload.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            WriteEditorFontReference(writer);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.5d));
            writer.WriteInt2(new int2(8, 6));
            writer.WriteByte(250);
            return stream.ToArray();
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

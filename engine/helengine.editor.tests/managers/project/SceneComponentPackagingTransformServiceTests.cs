using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies focused scene-component packaging rewrites after build-time text-sprite removal.
    /// </summary>
    public sealed class SceneComponentPackagingTransformServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by each transform-service test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used by each transform-service test.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated workspace for transform-service verification.
        /// </summary>
        public SceneComponentPackagingTransformServiceTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-transform-service-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache"));
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the isolated workspace after the test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures text components still package as runtime text payloads.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsPackaged_KeepsTextComponentPayload() {
            SceneComponentPackagingTransformService service = CreateService();
            SceneComponentAssetRecord record = CreateTextRecord();

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
        }

        /// <summary>
        /// Ensures text packaging no longer writes generated texture outputs even when the builder owns texture cooking.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBuilderOwnedTextureCookIsEnabled_DoesNotEnqueueGeneratedTextureCookWorkItemForText() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedTextureService(workItems);

            bool transformed = service.TryTransform(CreateTextRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Empty(workItems);
        }

        /// <summary>
        /// Ensures authored sprite components that persist their texture field through the automatic editor payload contract still package successfully.
        /// </summary>
        [Fact]
        public void TryTransform_WhenSpriteComponentUsesAuthoredTextureField_RewritesSpritePayload() {
            SceneComponentPackagingTransformService service = CreateService();
            SceneComponentAssetRecord record = CreateSpriteRecord();

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.SpriteComponent", transformedRecord.ComponentTypeId);
            SceneAssetReference textureReference = ReadSpriteTextureReference(transformedRecord);
            Assert.NotNull(textureReference);
            Assert.StartsWith("cooked/imported/", textureReference.RelativePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures DS-authored generated debug-font references rewrite into the packaged DS debug-font path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenDebugComponentUsesNintendoDsGeneratedFont_RewritesFontReference() {
            SceneComponentPackagingTransformService service = CreateService();
            SceneComponentAssetRecord record = CreateDebugRecord(CreateNintendoDsDebugFontReference());

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.DebugComponent", transformedRecord.ComponentTypeId);
            SceneAssetReference fontReference = ReadDebugFontReference(transformedRecord);
            Assert.NotNull(fontReference);
            Assert.Equal("cooked/fonts/ds-debug.hefont", fontReference.RelativePath);
        }

        /// <summary>
        /// Creates one transform service wired to real project dependencies.
        /// </summary>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateService() {
            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "windows");
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned texture cooking.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <returns>Configured transform service that records builder-owned texture cook work items.</returns>
        SceneComponentPackagingTransformService CreateBuilderOwnedTextureService(List<PlatformCookWorkItem> workItems) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "ds",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateBuilderOwnedTexturePlatformDefinition());
        }

        /// <summary>
        /// Creates one automatic reflected text-component record for packaging verification.
        /// </summary>
        /// <returns>Serialized text-component record.</returns>
        SceneComponentAssetRecord CreateTextRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(128, 32),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true,
                Alignment = TextAlignment.Center
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), CreateEditorFontReference());

            return descriptor.SerializeComponent(textComponent, 0, saveState);
        }

        /// <summary>
        /// Creates one automatic reflected sprite-component record for packaging verification.
        /// </summary>
        /// <returns>Serialized sprite-component record.</returns>
        SceneComponentAssetRecord CreateSpriteRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            WriteTextureSourceFile();
            SpriteComponent spriteComponent = new SpriteComponent {
                Texture = new TestRuntimeTexture(),
                Size = new int2(128, 32),
                Color = new byte4(255, 255, 255, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(SpriteComponent.Texture), new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Images/Menu/helengine-logo.png",
                ProviderId = string.Empty,
                AssetId = string.Empty
            });

            return descriptor.SerializeComponent(spriteComponent, 0, saveState);
        }

        /// <summary>
        /// Writes one minimal PNG texture source file expected by the authored sprite packaging path.
        /// </summary>
        void WriteTextureSourceFile() {
            string relativePath = Path.Combine("assets", "Images", "Menu");
            string directoryPath = Path.Combine(ProjectRootPath, relativePath);
            Directory.CreateDirectory(directoryPath);
            string fullPath = Path.Combine(directoryPath, "helengine-logo.png");
            byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=");
            File.WriteAllBytes(fullPath, pngBytes);
        }

        /// <summary>
        /// Creates one generated editor-font reference matching authored text scene payloads.
        /// </summary>
        /// <returns>Generated editor-font reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }

        /// <summary>
        /// Creates one generated Nintendo DS debug-font reference matching authored DS text scene payloads.
        /// </summary>
        /// <returns>Generated Nintendo DS debug-font reference.</returns>
        static SceneAssetReference CreateNintendoDsDebugFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ds-debug.hefont",
                ProviderId = "editor",
                AssetId = "ds-debug-font"
            };
        }

        /// <summary>
        /// Creates one minimal packaged font asset suitable for automatic text serialization.
        /// </summary>
        /// <returns>Minimal font asset.</returns>
        static FontAsset CreatePackagedFontAsset() {
            return new FontAsset(
                new FontInfo("Demo", 16, 8f),
                null,
                new Dictionary<char, FontChar>(),
                16f,
                64,
                64) {
                    SourceTextureAsset = new TextureAsset {
                        Id = "fonts/demo-source",
                        Width = 64,
                        Height = 64,
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8,
                        Colors = new byte[64 * 64 * 4]
                    }
                };
        }

        /// <summary>
        /// Creates one automatic reflected debug-component record for font-reference packaging verification.
        /// </summary>
        /// <param name="fontReference">Generated font reference the authored debug component should carry.</param>
        /// <returns>Serialized debug-component record.</returns>
        SceneComponentAssetRecord CreateDebugRecord(SceneAssetReference fontReference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            DebugComponent debugComponent = new DebugComponent {
                Font = CreatePackagedFontAsset(),
                RefreshIntervalSeconds = 0.5f,
                Padding = new int2(2, 3),
                RenderOrder2D = 17
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(DebugComponent.Font), fontReference);

            return descriptor.SerializeComponent(debugComponent, 0, saveState);
        }

        /// <summary>
        /// Reads the packaged sprite texture reference from one strict runtime sprite payload.
        /// </summary>
        /// <param name="record">Transformed sprite component record to inspect.</param>
        /// <returns>Packaged texture reference stored in the sprite payload.</returns>
        static SceneAssetReference ReadSpriteTextureReference(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(1, reader.ReadByte());
            SceneAssetReference reference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
            return Assert.IsType<SceneAssetReference>(reference);
        }

        /// <summary>
        /// Reads the packaged debug-component font reference from one strict runtime payload.
        /// </summary>
        /// <param name="record">Transformed debug-component record to inspect.</param>
        /// <returns>Packaged font reference stored in the debug payload.</returns>
        static SceneAssetReference ReadDebugFontReference(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(1, reader.ReadByte());
            SceneAssetReference reference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
            return Assert.IsType<SceneAssetReference>(reference);
        }

        /// <summary>
        /// Creates one minimal platform definition whose texture cook is owned by the builder.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned texture cook capability.</returns>
        static PlatformDefinition CreateBuilderOwnedTexturePlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                RuntimeGenerationContract.CreateDefault(),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A4\",\"indexingMethod\":\"QuantizedIndexed\"}")
                });
        }
    }
}

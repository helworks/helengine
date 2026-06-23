using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies focused text-component packaging rewrites in the shared scene-component transform service.
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
        /// Ensures flagged text falls back to the normal runtime text payload when build-time sprite conversion is disabled.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_KeepsTextComponentPayloadWithoutCallingBakeService() {
            StubTextComponentSpriteBakeService bakeService = new StubTextComponentSpriteBakeService();
            SceneComponentPackagingTransformService service = CreateService(bakeService);
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
            Assert.False(bakeService.WasCalled);
        }

        /// <summary>
        /// Ensures unflagged text remains a runtime text component during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsNotFlagged_KeepsTextComponentPayload() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateTextRecord(false);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
        }

        /// <summary>
        /// Ensures flagged text remains a runtime text payload and does not call the bake service.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_KeepsTextComponentPayloadAndDoesNotCallBakeService() {
            StubTextComponentSpriteBakeService bakeService = new StubTextComponentSpriteBakeService();
            SceneComponentPackagingTransformService service = CreateService(bakeService);
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
            Assert.False(bakeService.WasCalled);
        }

        /// <summary>
        /// Ensures flagged text no longer writes one generated texture asset into packaged build output.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_DoesNotWriteGeneratedTextureAssetToCookedOutput() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            string generatedTextureDirectoryPath = Path.Combine(BuildRootPath, "cooked", "generated", "text-sprites");
            Assert.False(Directory.Exists(generatedTextureDirectoryPath));
        }

        /// <summary>
        /// Ensures flagged text no longer enqueues one builder-owned texture cook work item when the selected platform owns texture cooking.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBuilderOwnedTextureCookIsEnabled_DoesNotEnqueueGeneratedTextureCookWorkItem() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedTextureService(workItems, new StubTextComponentSpriteBakeService());

            bool transformed = service.TryTransform(CreateTextRecord(true), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Empty(workItems);
        }

        /// <summary>
        /// Ensures builder-owned font-atlas texture capabilities externalize imported font atlases through the shared generic texture path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenPlatformOwnsFontAtlasTextureCooking_ExternalizesImportedFontAtlasUsingGenericTexturePath() {
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedFontAtlasService(workItems, new StubTextComponentSpriteBakeService());
            WriteSourceFont(fontRelativePath);
            SceneComponentAssetRecord record = CreateDebugRecord(CreateFileFontReference(fontRelativePath));

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            PlatformCookWorkItem workItem = Assert.Single(workItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal(".hetex", Path.GetExtension(workItem.SourceAssetPath));
            Assert.Contains(Path.Combine(ProjectRootPath, "cache", "generated", "platform-fonts"), workItem.SourceAssetPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures rooted packaged-path platforms write rooted runtime font-atlas references while preserving the shared builder-owned texture path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenPlatformOwnsFontAtlasTextureCookingAndAllowsRootedPackagedPaths_WritesRootedAtlasRuntimePath() {
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateRootedBuilderOwnedFontAtlasService(workItems, new StubTextComponentSpriteBakeService());
            WriteSourceFont(fontRelativePath);
            SceneComponentAssetRecord record = CreateDebugRecord(CreateFileFontReference(fontRelativePath));

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            PlatformCookWorkItem workItem = Assert.Single(workItems);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Equal("/cooked/fonts/demodisctitle.hetex", cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures authored sprite components that persist their texture field through the automatic editor payload contract still package successfully.
        /// </summary>
        [Fact]
        public void TryTransform_WhenSpriteComponentUsesAuthoredTextureField_RewritesSpritePayload() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
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
        /// Ensures DS-authored generated debug-font references are rejected after the shared engine path drops the platform-specific font hook.
        /// </summary>
        [Fact]
        public void TryTransform_WhenDebugComponentUsesRemovedNintendoDsGeneratedFont_ThrowsUnsupportedGeneratedReference() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateDebugRecord(CreateNintendoDsDebugFontReference());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.TryTransform(record, BuildRootPath, out _));
            Assert.Contains("Unsupported generated", exception.Message);
        }

        /// <summary>
        /// Creates one transform service wired to real project dependencies and one injected bake-service seam.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateService(ITextComponentSpriteBakeService bakeService) {
            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "windows",
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                bakeService);
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned texture cooking.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records generated texture cook work items.</returns>
        SceneComponentPackagingTransformService CreateBuilderOwnedTextureService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
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
                CreateBuilderOwnedTexturePlatformDefinition(),
                bakeService);
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned font-atlas texture cooking.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records generated font-atlas cook work items.</returns>
        SceneComponentPackagingTransformService CreateBuilderOwnedFontAtlasService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "external-platform",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateBuilderOwnedFontAtlasPlatformDefinition(),
                bakeService);
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned font-atlas cooking and rooted packaged runtime paths.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records rooted font-atlas cook work items.</returns>
        SceneComponentPackagingTransformService CreateRootedBuilderOwnedFontAtlasService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "ps2",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateRootedBuilderOwnedFontAtlasPlatformDefinition(),
                bakeService);
        }

        /// <summary>
        /// Creates one automatic reflected text-component record for packaging verification.
        /// </summary>
        /// <param name="convertTextToSprite">True when the authored text should request build-time sprite conversion.</param>
        /// <returns>Serialized text-component record.</returns>
        SceneComponentAssetRecord CreateTextRecord(bool convertTextToSprite) {
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
                ConvertTextToSprite = convertTextToSprite,
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
        /// Writes one minimal source font file expected by the authored font packaging path.
        /// </summary>
        /// <param name="relativePath">Project-relative source font path.</param>
        void WriteSourceFont(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, [0x00]);
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
        /// Creates one file-backed font reference for authored runtime payloads.
        /// </summary>
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>File-backed font reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
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

        /// <summary>
        /// Creates one minimal platform definition whose texture cook is owned by the builder and reused for font atlas outputs.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned texture cook capability.</returns>
        static PlatformDefinition CreateBuilderOwnedFontAtlasPlatformDefinition() {
            return new PlatformDefinition(
                "external-platform",
                "External Platform",
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
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}")
                });
        }

        /// <summary>
        /// Creates one minimal platform definition whose builder-owned font-atlas texture outputs resolve through rooted packaged runtime paths.
        /// </summary>
        /// <returns>Minimal platform definition with rooted packaged runtime-path support.</returns>
        static PlatformDefinition CreateRootedBuilderOwnedFontAtlasPlatformDefinition() {
            return new PlatformDefinition(
                "ps2",
                "PlayStation 2",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.RootedOrContentRelative),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}")
                });
        }

        /// <summary>
        /// Provides a controllable text-sprite bake result for transform-service tests.
        /// </summary>
        sealed class StubTextComponentSpriteBakeService : ITextComponentSpriteBakeService {
            /// <summary>
            /// Gets whether the bake service has been invoked.
            /// </summary>
            public bool WasCalled { get; private set; }

            /// <summary>
            /// Gets the last bake request received by the stub.
            /// </summary>
            public TextComponentSpriteBakeRequest LastRequest { get; private set; }

            /// <summary>
            /// Returns one deterministic generated texture bake result for the supplied request.
            /// </summary>
            /// <param name="request">Bake request issued by the transform service.</param>
            /// <returns>Generated bake result.</returns>
            public TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request) {
                WasCalled = true;
                LastRequest = request;

                return new TextComponentSpriteBakeResult(
                    new TextureAsset {
                        Id = "generated:text-sprite",
                        Width = 128,
                        Height = 32,
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8,
                        Colors = new byte[128 * 32 * 4]
                    },
                    new TextureAssetProcessorSettings {
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8
                    },
                    "text-scene-0");
            }
        }
    }
}



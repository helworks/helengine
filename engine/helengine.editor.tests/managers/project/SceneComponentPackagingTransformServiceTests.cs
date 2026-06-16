using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

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
        /// Ensures flagged text is rewritten into a sprite payload and routed through the bake service.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_RewritesToSpritePayloadAndCallsBakeService() {
            StubTextComponentSpriteBakeService bakeService = new StubTextComponentSpriteBakeService();
            SceneComponentPackagingTransformService service = CreateService(bakeService);
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.SpriteComponent", transformedRecord.ComponentTypeId);
            Assert.True(bakeService.WasCalled);
            Assert.NotNull(bakeService.LastRequest);
            Assert.Equal("Hello world", bakeService.LastRequest.Text);
            Assert.Equal(new int2(128, 32), bakeService.LastRequest.Size);
            Assert.Equal(TextAlignment.Center, bakeService.LastRequest.Alignment);
        }

        /// <summary>
        /// Ensures flagged text writes one generated texture asset into packaged build output when the editor owns texture cooking.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_WritesGeneratedTextureAssetToCookedOutput() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            SceneAssetReference textureReference = ReadSpriteTextureReference(transformedRecord);
            string generatedTexturePath = Path.Combine(BuildRootPath, textureReference.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(generatedTexturePath));
        }

        /// <summary>
        /// Ensures flagged text enqueues one builder-owned texture cook work item when the selected platform owns texture cooking.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBuilderOwnedTextureCookIsEnabled_EnqueuesGeneratedTextureCookWorkItem() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedTextureService(workItems, new StubTextComponentSpriteBakeService());

            bool transformed = service.TryTransform(CreateTextRecord(true), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            PlatformCookWorkItem workItem = Assert.Single(workItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Contains("cooked/generated/text-sprites/", workItem.OutputRelativePath);
            Assert.True(File.Exists(workItem.SourceAssetPath));
        }

        /// <summary>
        /// Creates one transform service wired to real project dependencies and one injected bake-service seam.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateService(ITextComponentSpriteBakeService bakeService) {
            ContentManager contentManager = new ContentManager(ProjectRootPath);
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
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

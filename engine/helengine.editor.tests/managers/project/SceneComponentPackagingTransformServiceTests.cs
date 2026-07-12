using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using System.Reflection;
using System.Reflection.Emit;

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
        /// Ensures platform-extended text metadata stored in detached DS overrides is emitted into the packaged ordinal runtime payload.
        /// </summary>
        [Fact]
        public void TryTransform_WhenDsTextComponentHasSyntheticBgLayerOverride_WritesSyntheticPlatformMemberIntoPackagedPayload() {
            PlatformDefinition platformDefinition = CreateDsSyntheticTextPlatformDefinition();
            SceneComponentPackagingTransformService service = CreateDsSyntheticTextService(platformDefinition);
            SceneComponentAssetRecord record = CreateWrappedTextRecord(false, "1");

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            TextComponent restored = DeserializePlatformExtendedAutomaticComponent<TextComponent>(transformedRecord, platformDefinition);
            Assert.Equal(1, restored.GetSyntheticInt32MemberOrDefault("BGLayer", -1));
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
        /// Ensures builder-owned font-atlas texture capabilities externalize imported font atlases through the dedicated cook kind while keeping the shared runtime texture path.
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
            Assert.Equal("font-atlas-texture", workItem.SourceAssetKind);
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
        /// Ensures authored automatic audio-source components rewrite their file-backed clip references into cooked packaged audio assets.
        /// </summary>
        [Fact]
        public void TryTransform_WhenAudioSourceComponentUsesAuthoredClipReference_RewritesAudioPayload() {
            const string audioRelativePath = "audio/menu/theme.wav";
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            string sourcePath = WriteSourceAudio(audioRelativePath);
            ConfigureAudioImportSettings(
                sourcePath,
                "windows",
                new AudioAssetProcessorSettings {
                    PlaybackMode = AudioPlaybackMode.Streamed,
                    EncodingFamilyId = "pcm-streamed",
                    DefaultBusId = "music",
                    DefaultLoop = true,
                    StreamChunkByteSize = 4
                });
            SceneComponentAssetRecord record = CreateAudioSourceRecord(audioRelativePath);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            SceneAssetReference clipReference = ReadAutomaticComponentAssetReference<AudioSourceComponent>(transformedRecord, nameof(AudioSourceComponent.Clip));
            Assert.NotNull(clipReference);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, clipReference.SourceKind);
            Assert.Equal("cooked/audio/menu/theme.hasset", clipReference.RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "audio", "menu", "theme.hasset")));
        }

        /// <summary>
        /// Ensures automatic asset-reference rewriting accepts engine asset member types that arrive from another load context but keep the same full name.
        /// </summary>
        [Fact]
        public void RewriteAutomaticComponentReference_WhenAudioTypeMatchesByFullName_RewritesAudioPayload() {
            const string audioRelativePath = "audio/menu/theme.wav";
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            string sourcePath = WriteSourceAudio(audioRelativePath);
            ConfigureAudioImportSettings(
                sourcePath,
                "windows",
                new AudioAssetProcessorSettings {
                    PlaybackMode = AudioPlaybackMode.Streamed,
                    EncodingFamilyId = "pcm-streamed",
                    DefaultBusId = "music",
                    DefaultLoop = true,
                    StreamChunkByteSize = 4
                });

            MethodInfo rewriteMethod = typeof(SceneComponentPackagingTransformService).GetMethod(
                "RewriteAutomaticComponentReference",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(rewriteMethod);

            Type foreignAudioAssetType = CreateForeignEngineType("helengine.AudioAsset");
            SceneAssetReference sourceReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath);

            SceneAssetReference rewrittenReference = Assert.IsType<SceneAssetReference>(rewriteMethod.Invoke(service, [foreignAudioAssetType, sourceReference, BuildRootPath]));

            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, rewrittenReference.SourceKind);
            Assert.Equal("cooked/audio/menu/theme.hasset", rewrittenReference.RelativePath);
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
        /// Ensures a registered static-mesh cook processor can populate the cooked runtime payload during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new StubStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);
            SceneComponentAssetRecord record = CreateStaticMeshColliderRecord();

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.NotNull(restored.CookedRuntimeData);
            Assert.Equal("test.static-mesh", restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader("test.static-mesh", 0x7A01, 3);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal(0.25f, reader.ReadSingle());
        }

        /// <summary>
        /// Ensures the real BEPU static-mesh cook processor can populate a BEPU-owned runtime payload during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenStaticMeshColliderUsesRealBepuCookProcessor_WritesBepuPayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);

            bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader(
                BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
        }

        /// <summary>
        /// Ensures the BEPU cook processor preserves the generic static mesh collision data while adding the cooked payload.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBepuCookProcessorRuns_PreservesGenericCollisionDataAlongsideCookedPayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);

            bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.Equal(3, restored.CollisionData.Vertices.Length);
            Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
            Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader(
                BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
        }

        /// <summary>
        /// Creates one transform service wired to real project dependencies and one injected bake-service seam.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateService(ITextComponentSpriteBakeService bakeService, StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry = null) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
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
                bakeService,
                staticMeshCookProcessorRegistry);
        }

        /// <summary>
        /// Creates one transform service configured with one big-endian codegen profile for static-mesh runtime payload verification.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <param name="staticMeshCookProcessorRegistry">Cook processor registry used by the service.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateBigEndianStaticMeshService(
            ITextComponentSpriteBakeService bakeService,
            StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);
            PlatformDefinition platformDefinition = CreateBigEndianStaticMeshPlatformDefinition();

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                new List<string>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "gamecube",
                null,
                "main",
                string.Empty,
                null,
                null,
                platformDefinition,
                bakeService,
                staticMeshCookProcessorRegistry);
        }

        /// <summary>
        /// Creates one transform service configured for DS text synthetic-member packaging verification.
        /// </summary>
        /// <param name="platformDefinition">Platform definition that exposes the synthetic text member.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateDsSyntheticTextService(PlatformDefinition platformDefinition) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
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
                null,
                platformDefinition,
                new StubTextComponentSpriteBakeService());
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

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
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

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
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

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
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
        /// Creates one wrapped automatic reflected text-component record that carries one detached DS synthetic member override.
        /// </summary>
        /// <param name="convertTextToSprite">True when the authored text should request build-time sprite conversion.</param>
        /// <param name="bgLayerValue">Serialized DS background-layer override value.</param>
        /// <returns>Wrapped serialized text-component record.</returns>
        SceneComponentAssetRecord CreateWrappedTextRecord(bool convertTextToSprite, string bgLayerValue) {
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
            SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(textComponent, 0, saveState);
            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride("ds");
            overrideState.Payload = baseRecord.Payload;
            overrideState.SetPropertyOverride("BGLayer");
            overrideState.SetMemberValue("BGLayer", bgLayerValue);
            return new ComponentPlatformOverridePayloadService().Wrap(baseRecord, saveState);
        }

        /// <summary>
        /// Creates one automatic reflected static-mesh collider record for packaging verification.
        /// </summary>
        /// <returns>Serialized static-mesh collider record.</returns>
        static SceneComponentAssetRecord CreateStaticMeshColliderRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            StaticMeshCollider3DComponent component = new StaticMeshCollider3DComponent {
                CollisionData = new StaticMeshCollisionData3D(
                    [
                        new float3(-1f, 0f, -1f),
                        new float3(1f, 0f, -1f),
                        new float3(-1f, 0f, 1f)
                    ],
                    [0, 1, 2])
            };

            return descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
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
            saveState.SetAssetReference(
                nameof(SpriteComponent.Texture),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemTexture("Images/Menu/helengine-logo.png"));

            return descriptor.SerializeComponent(spriteComponent, 0, saveState);
        }

        /// <summary>
        /// Creates one automatic reflected audio-source component record for packaging verification.
        /// </summary>
        /// <param name="audioRelativePath">Project-relative authored audio path referenced by the component.</param>
        /// <returns>Serialized audio-source component record.</returns>
        SceneComponentAssetRecord CreateAudioSourceRecord(string audioRelativePath) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            AudioSourceComponent audioSourceComponent = new AudioSourceComponent {
                Clip = new AudioAsset(),
                PlayOnStart = true,
                Loop = true,
                BusId = "music",
                Gain = 0.75f
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                nameof(AudioSourceComponent.Clip),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath));

            return descriptor.SerializeComponent(audioSourceComponent, 0, saveState);
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

        static Type CreateForeignEngineType(string fullTypeName) {
            AssemblyName assemblyName = new AssemblyName("SceneComponentPackagingTransformServiceTests.Dynamic." + Guid.NewGuid().ToString("N"));
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("main");
            return moduleBuilder.DefineType(fullTypeName, TypeAttributes.Public | TypeAttributes.Class).CreateType();
        }

        /// <summary>
        /// Writes one minimal source audio file expected by the authored audio packaging path.
        /// </summary>
        /// <param name="relativePath">Project-relative audio path.</param>
        /// <returns>Absolute authored audio source path.</returns>
        string WriteSourceAudio(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, [1, 2, 3, 4]);
            return fullPath;
        }

        /// <summary>
        /// Writes one deterministic audio import-settings sidecar for the requested platform.
        /// </summary>
        /// <param name="sourcePath">Absolute authored audio path whose settings should be updated.</param>
        /// <param name="platformId">Target platform id whose processor settings should be stored.</param>
        /// <param name="processorSettings">Processor settings that should be persisted for the target platform.</param>
        void ConfigureAudioImportSettings(string sourcePath, string platformId, AudioAssetProcessorSettings processorSettings) {
            ContentManager contentManager = new(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager manager = new(ProjectRootPath, contentManager);
            manager.CurrentPlatformId = platformId;
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));

            AudioAssetImportSettings settings = manager.LoadOrCreateAudioImportSettings(sourcePath);
            settings.Processor.Platforms[platformId] = processorSettings;
            manager.SaveAudioImportSettings(sourcePath, settings);
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
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEditorUiFont();
        }

        /// <summary>
        /// Creates one file-backed font reference for authored runtime payloads.
        /// </summary>
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>File-backed font reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemFont(relativePath);
        }

        /// <summary>
        /// Creates one generated Nintendo DS debug-font reference matching authored DS text scene payloads.
        /// </summary>
        /// <returns>Generated Nintendo DS debug-font reference.</returns>
        static SceneAssetReference CreateNintendoDsDebugFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "generated/editor/fonts/ds-debug.hefont",
                "editor",
                "ds-debug-font");
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
        /// Reads one automatic-component asset reference from the serialized payload without requiring one runtime asset resolver.
        /// </summary>
        /// <typeparam name="TComponent">Automatic component type represented by the payload.</typeparam>
        /// <param name="record">Packaged component record being decoded.</param>
        /// <param name="memberName">Stable reflected member name whose scene reference should be restored.</param>
        /// <returns>Restored scene asset reference stored for the requested member.</returns>
        static SceneAssetReference ReadAutomaticComponentAssetReference<TComponent>(SceneComponentAssetRecord record, string memberName) where TComponent : Component, new() {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(typeof(TComponent));
            TComponent component = new TComponent();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(schema.Members.Count, reader.ReadInt32());
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                member.SetValue(component, AutomaticScriptComponentPersistenceDescriptor.ReadSupportedMemberValue(reader, member, component, saveComponent, null));
            }

            Assert.True(saveComponent.TryGetComponentState(component, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference(memberName, out SceneAssetReference reference));
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
        /// Deserializes one automatic reflected component from the supplied transformed record.
        /// </summary>
        /// <typeparam name="TComponent">Expected component type.</typeparam>
        /// <param name="record">Transformed record to deserialize.</param>
        /// <returns>Deserialized component instance.</returns>
        static TComponent DeserializeAutomaticComponent<TComponent>(SceneComponentAssetRecord record) where TComponent : Component {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<TComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), new TestSceneAssetReferenceResolver()));
        }

        /// <summary>
        /// Deserializes one packaged automatic component record using the platform-extended schema expected by the target runtime.
        /// </summary>
        /// <typeparam name="TComponent">Expected component type.</typeparam>
        /// <param name="record">Packaged component record being decoded.</param>
        /// <param name="platformDefinition">Platform definition that owns any synthetic schema members.</param>
        /// <returns>Decoded component instance.</returns>
        static TComponent DeserializePlatformExtendedAutomaticComponent<TComponent>(
            SceneComponentAssetRecord record,
            PlatformDefinition platformDefinition) where TComponent : Component, new() {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            PlatformExtendedScriptComponentSchemaBuilder schemaBuilder = new PlatformExtendedScriptComponentSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(typeof(TComponent), platformDefinition);
            TComponent component = new TComponent();
            TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
            referenceResolver.RegisterFont(
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemFont("cooked/fonts/default.hefont"),
                CreatePackagedFontAsset());
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(schema.Members.Count, reader.ReadInt32());
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                member.SetValue(component, AutomaticScriptComponentPersistenceDescriptor.ReadSupportedMemberValue(reader, member, component, new EntitySaveComponent(), referenceResolver));
            }

            return component;
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
        /// Creates one minimal platform definition whose dedicated font-atlas cook is owned by the builder.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned font-atlas cook capability.</returns>
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
                        "font-atlas-texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}",
                        null,
                        ".hetex")
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
                        "font-atlas-texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}",
                        null,
                        ".hetex")
                });
        }

        /// <summary>
        /// Creates one minimal DS platform definition that exposes the synthetic text background-layer member.
        /// </summary>
        /// <returns>Minimal DS platform definition with one synthetic text member.</returns>
        static PlatformDefinition CreateDsSyntheticTextPlatformDefinition() {
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
                componentMemberDefinitions: [
                    new PlatformComponentMemberDefinition(
                        "helengine.TextComponent",
                        "BGLayer",
                        "BG Layer",
                        PlatformComponentMemberValueKind.Int32,
                        "0",
                        0)
                ]);
        }

        /// <summary>
        /// Creates one minimal platform definition whose selected build profile resolves to one big-endian codegen profile.
        /// </summary>
        /// <returns>Minimal big-endian platform definition.</returns>
        static PlatformDefinition CreateBigEndianStaticMeshPlatformDefinition() {
            return new PlatformDefinition(
                "gamecube",
                "GameCube",
                [
                    new PlatformBuildProfileDefinition(
                        "main",
                        "Main",
                        "Main build profile",
                        "default",
                        "gc-cpp",
                        Array.Empty<PlatformSettingDefinition>())
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "default",
                        "Default",
                        "Default graphics profile",
                        Array.Empty<PlatformSettingDefinition>())
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                [
                    new PlatformCodegenProfileDefinition(
                        "gc-cpp",
                        "GC C++",
                        "GameCube codegen",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.BigEndian,
                        Array.Empty<PlatformSettingDefinition>())
                ],
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>());
        }

        /// <summary>
        /// Imports deterministic audio metadata for authored transform-service tests without relying on one real platform codec.
        /// </summary>
        sealed class TestAudioImporter : IAudioImporter {
            /// <summary>
            /// Produces one stable imported audio payload for the supplied source stream.
            /// </summary>
            /// <param name="stream">Source audio stream being imported.</param>
            /// <returns>Deterministic imported audio payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedAudioSource {
                    Channels = 2,
                    SampleRate = 44100,
                    DurationSeconds = 3.5f,
                    Pcm16Bytes = [1, 2, 3, 4]
                };
            }
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

        /// <summary>
        /// Provides one deterministic cooked runtime payload for static-mesh packaging tests.
        /// </summary>
        sealed class StubStaticMeshCollisionCookProcessor3D : IStaticMeshCollisionCookProcessor3D {
            /// <summary>
            /// Gets the stable test payload format identifier.
            /// </summary>
            public string FormatId => "test.static-mesh";

            /// <summary>
            /// Gets the stable binary payload format identifier written into the HELE header.
            /// </summary>
            public ushort BinaryFormatId => 0x7A01;

            /// <summary>
            /// Gets the binary payload format version written into the HELE header.
            /// </summary>
            public byte BinaryFormatVersion => 3;

            /// <summary>
            /// Writes one deterministic cooked payload for test assertions.
            /// </summary>
            /// <param name="writer">Endian-aware writer owned by Helengine.</param>
            /// <param name="collisionData">Generic collision data passed by the packaging service.</param>
            public void WritePayload(EngineBinaryWriter writer, StaticMeshCollisionData3D collisionData) {
                if (writer == null) {
                    throw new ArgumentNullException(nameof(writer));
                } else if (collisionData == null) {
                    throw new ArgumentNullException(nameof(collisionData));
                }

                writer.WriteInt32(collisionData.TriangleCount);
                writer.WriteSingle(0.25f);
            }
        }
    }
}



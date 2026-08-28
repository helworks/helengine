using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session handling of platform-aware asset import settings.
    /// </summary>
    public class EditorSessionAssetImportSettingsTests : IDisposable {
        /// <summary>
        /// Temporary project root used for asset-settings session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Temporary assets root used by the import manager.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one isolated project root and the UI services required by properties-panel tests.
        /// </summary>
        public EditorSessionAssetImportSettingsTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-asset-settings-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes the temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures applying import settings persists the selected project platform, updates the import manager, and rehydrates the properties panel.
        /// </summary>
        [Fact]
        public void HandleImportSettingsApplyRequested_WhenPlatformSettingsApplied_PersistsPlatformAndReloadsView() {
            string sourcePath = WriteSourceModel("sponza.obj");
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            EditorProjectLocalSettingsService localSettingsService = GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "sponza.obj",
                "Models/sponza.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model);
            AssetProcessorSettings processorSettings = new AssetProcessorSettings();
            processorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = true
                }
            };
            processorSettings.Platforms["android"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = false
                }
            };
            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest("test-model", "windows", processorSettings);

            InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);

            ModelAssetImportSettings savedSettings = manager.LoadOrCreateModelImportSettings(sourcePath);
            AssetImportSettingsView view = GetPrivateField<AssetImportSettingsView>(panel, "importSettingsView");
            Assert.Equal("windows", session.CurrentProjectPlatform);
            Assert.Equal("windows", manager.CurrentPlatformId);
            Assert.Equal("windows", localSettingsService.LoadActivePlatform());
            Assert.Equal("test-model", savedSettings.Importer.ImporterId);
            Assert.True(savedSettings.Processor.Platforms["windows"].FlipWinding);
            Assert.Equal("windows", view.SelectedPlatformId);
            Assert.True(view.CurrentFlipWindingValue);
        }

        /// <summary>
        /// Ensures audio settings selection, apply, and refresh reject editing without reading or rewriting the typed audio sidecar.
        /// </summary>
        [Fact]
        public void AudioImportSettingsSelectionApplyAndRefresh_RejectsWithoutTouchingTypedSidecar() {
            string sourcePath = WriteSourceAudio("music.wav");
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), new[] { ".wav" }));
            AudioAssetImportSettings settings = new AudioAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "test-audio",
                    SourceChecksum = "audio-checksum",
                    AssetId = "audio-id"
                }
            };
            settings.Processor.Platforms["windows"] = new AudioAssetProcessorSettings {
                EncodingFamilyId = "pcm-buffered",
                PlaybackMode = AudioPlaybackMode.Predecoded,
                TargetChannels = 1,
                TargetSampleRate = 22050,
                StreamChunkByteSize = 8192,
                DefaultLoop = true,
                DefaultBusId = "music"
            };
            manager.SaveAudioImportSettings(sourcePath, settings);
            string settingsPath = sourcePath + ".hasset";
            byte[] originalSidecar = File.ReadAllBytes(settingsPath);
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "music.wav",
                "Audio/music.wav",
                sourcePath,
                ".wav",
                AssetEntryKind.Audio);

            InvokePrivate(session, "HandleAssetSelected", entry);

            List<TextComponent> lineTexts = GetPrivateField<List<TextComponent>>(panel, "lineTexts");
            Assert.Contains("Audio import settings are not editable", lineTexts[2].Text, StringComparison.Ordinal);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));

            AssetProcessorSettings requestProcessorSettings = new AssetProcessorSettings();
            requestProcessorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Audio = new AudioAssetProcessorSettings {
                    EncodingFamilyId = "pcm-buffered",
                    PlaybackMode = AudioPlaybackMode.Predecoded
                }
            };
            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest("test-audio", "windows", requestProcessorSettings);
            InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));

            InvokePrivate(session, "HandlePropertiesPanelAssetState", panel, entry);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));
            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                AudioAssetImportSettings restoredSettings = AudioAssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal("test-audio", restoredSettings.Importer.ImporterId);
                Assert.Equal(AudioPlaybackMode.Predecoded, restoredSettings.Processor.Platforms["windows"].PlaybackMode);
            }
        }

        /// <summary>
        /// Ensures a dynamically registered texture extension that the static browser classifier does not know still uses typed settings for selection, apply, and refresh.
        /// </summary>
        [Fact]
        public void CustomTextureImporterExtension_UsesTypedSettingsAcrossSelectionApplyAndRefresh() {
            const string customExtension = ".dynamictexture";
            string sourcePath = WriteSourceTexture("custom.dynamictexture");
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            manager.RegisterTextureImporter(new TextureImporterRegistration(
                "test-dynamic-texture",
                new ConfigurableTextureImporter(new byte[] { 5, 6, 7, 8 }),
                new[] { customExtension }));

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            manager.SaveTextureImportSettings(sourcePath, settings);
            byte[] originalSidecar = File.ReadAllBytes(sourcePath + ".hasset");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "custom.dynamictexture",
                "Textures/custom.dynamictexture",
                sourcePath,
                customExtension,
                AssetEntryKind.File);
            Assert.Equal(AssetEntryKind.File, new EditorAssetPathClassifier(TempProjectRootPath).Classify(sourcePath));
            Assert.True(manager.IsTextureExtension(customExtension));

            InvokePrivate(session, "HandleAssetSelected", entry);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            AssetImportSettingsView view = GetPrivateField<AssetImportSettingsView>(panel, "importSettingsView");
            Assert.True(view.IsTextureProcessorVisible);
            Assert.StartsWith("VRAM:", GetPrivateField<TextComponent>(view, "VramPreviewText").Text, StringComparison.Ordinal);

            AssetProcessorSettings requestProcessorSettings = new AssetProcessorSettings();
            requestProcessorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 128,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                }
            };
            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest(
                "test-dynamic-texture",
                "windows",
                requestProcessorSettings);

            InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.True(view.IsTextureProcessorVisible);
            Assert.Equal(128, view.CurrentTextureMaxResolutionValue);
            InvokePrivate(session, "HandlePropertiesPanelAssetState", panel, entry);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.True(view.IsTextureProcessorVisible);
            Assert.Equal(128, view.CurrentTextureMaxResolutionValue);
            Assert.StartsWith("VRAM:", GetPrivateField<TextComponent>(view, "VramPreviewText").Text, StringComparison.Ordinal);

            using (FileStream stream = new FileStream(sourcePath + ".hasset", FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAssetImportSettings restoredSettings = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal("test-dynamic-texture", restoredSettings.Importer.ImporterId);
                Assert.Equal(128, restoredSettings.Processor.Platforms["windows"].MaxResolution);
            }
            Assert.NotEqual(originalSidecar, File.ReadAllBytes(sourcePath + ".hasset"));
        }

        /// <summary>
        /// Ensures a dynamically registered model extension presents model controls while selection, apply, and refresh continue using the typed model sidecar.
        /// </summary>
        [Fact]
        public void CustomModelImporterExtension_PresentsTypedSettingsAcrossSelectionApplyAndRefresh() {
            const string customExtension = ".dynamicmodel";
            string sourcePath = WriteSourceModel("custom.dynamicmodel");
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            manager.RegisterModelImporter(new ModelImporterRegistration(
                "test-dynamic-model",
                new TestModelImporter(),
                new[] { customExtension }));

            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            manager.SaveModelImportSettings(sourcePath, settings);
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "custom.dynamicmodel",
                "Models/custom.dynamicmodel",
                sourcePath,
                customExtension,
                AssetEntryKind.File);
            Assert.Equal(AssetEntryKind.File, new EditorAssetPathClassifier(TempProjectRootPath).Classify(sourcePath));
            Assert.True(manager.IsModelExtension(customExtension));

            InvokePrivate(session, "HandleAssetSelected", entry);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            AssetImportSettingsView view = GetPrivateField<AssetImportSettingsView>(panel, "importSettingsView");
            Assert.True(view.IsModelProcessorVisible);

            AssetProcessorSettings requestProcessorSettings = new AssetProcessorSettings();
            requestProcessorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = true
                }
            };
            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest(
                "test-dynamic-model",
                "windows",
                requestProcessorSettings);

            InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.True(view.IsModelProcessorVisible);
            Assert.True(view.CurrentFlipWindingValue);

            InvokePrivate(session, "HandlePropertiesPanelAssetState", panel, entry);
            Assert.DoesNotContain("Sectioned import settings sidecar", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.True(view.IsModelProcessorVisible);
            Assert.True(view.CurrentFlipWindingValue);
            using (FileStream stream = new FileStream(sourcePath + ".hasset", FileMode.Open, FileAccess.Read, FileShare.Read)) {
                ModelAssetImportSettings restoredSettings = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal("test-dynamic-model", restoredSettings.Importer.ImporterId);
                Assert.True(restoredSettings.Processor.Platforms["windows"].FlipWinding);
            }
        }

        /// <summary>
        /// Ensures a dynamically registered audio extension that the static browser classifier does not know is rejected by every settings UI route without reading or creating a sectioned sidecar.
        /// </summary>
        [Fact]
        public void CustomAudioImporterExtension_RejectsTypedSettingsWithoutSectionedAccess() {
            const string customExtension = ".dynamicaudio";
            string sourcePath = WriteSourceAudio("custom.dynamicaudio");
            string missingSourcePath = WriteSourceAudio("missing.dynamicaudio");
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-dynamic-audio", new TestAudioImporter(), new[] { customExtension }));

            AudioAssetImportSettings settings = new AudioAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "test-dynamic-audio",
                    SourceChecksum = "audio-checksum",
                    AssetId = "audio-id"
                }
            };
            manager.SaveAudioImportSettings(sourcePath, settings);
            string settingsPath = sourcePath + ".hasset";
            byte[] originalSidecar = File.ReadAllBytes(settingsPath);
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "custom.dynamicaudio",
                "Audio/custom.dynamicaudio",
                sourcePath,
                customExtension,
                AssetEntryKind.File);

            Assert.Equal(AssetEntryKind.File, new EditorAssetPathClassifier(TempProjectRootPath).Classify(sourcePath));
            Assert.True(manager.IsAudioExtension(customExtension));

            InvokePrivate(session, "HandleAssetSelected", entry);
            Assert.Contains("Audio import settings are not editable", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));

            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest(
                "test-dynamic-audio",
                "windows",
                new AssetProcessorSettings());
            InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);
            Assert.Contains("Audio import settings are not editable", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));

            InvokePrivate(session, "HandlePropertiesPanelAssetState", panel, entry);
            Assert.Contains("Audio import settings are not editable", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.Equal(originalSidecar, File.ReadAllBytes(settingsPath));

            AssetBrowserEntry missingEntry = AssetBrowserEntry.CreateFileSystemFile(
                "missing.dynamicaudio",
                "Audio/missing.dynamicaudio",
                missingSourcePath,
                customExtension,
                AssetEntryKind.File);
            string missingSettingsPath = missingSourcePath + ".hasset";
            Assert.False(File.Exists(missingSettingsPath));
            InvokePrivate(session, "HandleAssetSelected", missingEntry);
            Assert.Contains("Audio import settings are not editable", GetImportErrorText(panel), StringComparison.Ordinal);
            Assert.False(File.Exists(missingSettingsPath));
        }

        /// <summary>
        /// Ensures applying model processor settings rebuilds the live scene mesh from the processed cache using the selected platform.
        /// </summary>
        [Fact]
        public void HandleImportSettingsApplyRequested_WhenSceneUsesFileSystemModel_RebuildsTheLiveMeshModel() {
            string sourcePath = WriteSourceModel(Path.Combine("Models", "sponza.obj"));
            EditorSession session = CreateSession();
            AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            EditorEntity entity = new EditorEntity();
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel()
            };
            RuntimeModel originalModel = meshComponent.Model;
            entity.AddComponent(meshComponent);
            GetSaveComponent(entity).SetAssetReference(
                meshComponent,
                "Model",
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemModel("Models/sponza.obj"));
            AssetProcessorSettings processorSettings = new AssetProcessorSettings();
            processorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = true
                }
            };
            AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest("test-model", "windows", processorSettings);

            InvokePrivate(session, "HandleImportSettingsApplyRequested", AssetBrowserEntry.CreateFileSystemFile(
                "sponza.obj",
                "Models/sponza.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model), request);

            ModelAsset rebuiltAsset = Assert.Single(renderManager.BuiltModelAssets);
            Assert.NotSame(originalModel, meshComponent.Model);
            Assert.Equal(new ushort[] { 0, 2, 1 }, rebuiltAsset.Indices16);
            Assert.True(manager.CurrentPlatformId == "windows");
        }

        /// <summary>
        /// Creates one partially initialized editor session containing the collaborators used by import-settings apply handling.
        /// </summary>
        /// <returns>Editor session configured for asset-settings tests.</returns>
        EditorSession CreateSession() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(TempProjectRootPath, contentManager);
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), contentManager);
            IReadOnlyList<string> supportedPlatforms = new List<string> { "windows", "android" };
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, supportedPlatforms);
            localSettingsService.SaveActivePlatform("android");
            manager.CurrentPlatformId = "android";
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("android", "Android", string.Empty, "platforms/android", true),
                    new AvailablePlatformDescriptor("windows", "Windows", string.Empty, "platforms/windows", true)
                ],
                ["android", "windows"]);

            SetPrivateField(session, "assetImportManager", manager);
            SetPrivateField(session, "propertiesPanel", panel);
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", "android");
            SetPrivateField(session, "RequiredEngineVersion", "1.0.0-custom");
            EditorFileSystemModelResolver modelResolver = new EditorFileSystemModelResolver(manager);
            modelResolver.SetRenderManager(Core.Instance.RenderManager3D);
            SetPrivateField(session, "SceneModelRefreshService", new EditorSceneModelRefreshService(modelResolver, Core.Instance.ObjectManager));
            SetPrivateField(session, "availablePlatformProviderResolver", new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(TempProjectRootPath)));

            return session;
        }

        /// <summary>
        /// Writes one engine-level platform manifest for the current temporary toolchain root.
        /// </summary>
        /// <param name="engineVersion">Exact engine version whose platforms should be discoverable.</param>
        /// <param name="platforms">Platform descriptors written into the manifest.</param>
        /// <param name="installedPlatformIds">Platform identifiers whose install roots should exist on disk.</param>
        void WritePlatformManifest(string engineVersion, IReadOnlyList<AvailablePlatformDescriptor> platforms, IReadOnlyList<string> installedPlatformIds) {
            if (string.IsNullOrWhiteSpace(engineVersion)) {
                throw new ArgumentException("Engine version must be provided.", nameof(engineVersion));
            }
            if (platforms == null) {
                throw new ArgumentNullException(nameof(platforms));
            }
            if (installedPlatformIds == null) {
                throw new ArgumentNullException(nameof(installedPlatformIds));
            }

            string sharedToolchainRootPath = TempProjectRootPath;
            string manifestPath = Path.Combine(sharedToolchainRootPath, "platforms.json");
            Directory.CreateDirectory(sharedToolchainRootPath);

            List<string> manifestEntries = new List<string>(platforms.Count);
            for (int index = 0; index < platforms.Count; index++) {
                AvailablePlatformDescriptor platform = platforms[index];
                manifestEntries.Add($$"""
                {
                  "engineVersion": "{{engineVersion}}",
                  "platformId": "{{platform.Id}}",
                  "displayName": "{{platform.DisplayName}}",
                  "builderAssemblyPath": "{{platform.BuilderAssemblyPath}}",
                  "playerSourceRootPath": "{{platform.PlayerSourceRootPath}}"
                }
                """);

                if (installedPlatformIds.Contains(platform.Id)) {
                    string resolvedPlayerSourceRootPath = Path.Combine(sharedToolchainRootPath, platform.PlayerSourceRootPath);
                    Directory.CreateDirectory(resolvedPlayerSourceRootPath);
                }
            }

            string json = """
            {
              "platforms": [
            """ + string.Join(",\n", manifestEntries) + """
              ]
            }
            """;

            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Writes one minimal model source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created model source file.</returns>
        string WriteSourceModel(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            string directoryPath = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(sourcePath, "test model source");
            return sourcePath;
        }

        /// <summary>
        /// Writes one minimal texture source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created texture source file.</returns>
        string WriteSourceTexture(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            string directoryPath = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }

        /// <summary>
        /// Reads the current import-error line from the test properties panel.
        /// </summary>
        /// <param name="panel">Properties panel to inspect.</param>
        /// <returns>Visible error/status line text.</returns>
        string GetImportErrorText(PropertiesPanel panel) {
            List<TextComponent> lineTexts = GetPrivateField<List<TextComponent>>(panel, "lineTexts");
            return lineTexts.Count > 2 ? lineTexts[2].Text : string.Empty;
        }

        /// <summary>
        /// Writes one minimal audio source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created audio source file.</returns>
        string WriteSourceAudio(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be read.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Provides deterministic imported audio metadata for audio-settings UI behavior coverage.
        /// </summary>
        sealed class TestAudioImporter : IAudioImporter {
            /// <summary>
            /// Imports a minimal source stream into deterministic PCM metadata.
            /// </summary>
            /// <param name="stream">Source stream containing authored audio bytes.</param>
            /// <returns>Imported audio metadata and PCM payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedAudioSource {
                    Channels = 1,
                    SampleRate = 22050,
                    DurationSeconds = 1f,
                    Pcm16Bytes = new byte[] { 1, 2, 3, 4 }
                };
            }
        }

        /// <summary>
        /// Creates a small font asset that can satisfy properties-panel layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['W'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}


using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session routes selection changes through the preview source resolver.
    /// </summary>
    public class EditorSessionPreviewSelectionTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the preview selection tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Temporary assets root used by the import manager.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes the core services required by the preview selection tests.
        /// </summary>
        public EditorSessionPreviewSelectionTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-preview-selection-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a selected camera replaces an existing texture preview.
        /// </summary>
        [Fact]
        public void HandleSelectionChanged_WhenCameraIsSelected_ReplacesTexturePreview() {
            EditorSession session = CreateSession();
            AssetBrowserEntry textureEntry = CreateTextureEntry();
            InvokePrivate(session, "HandleAssetSelected", textureEntry);

            PreviewPanel previewPanel = GetPrivateField<PreviewPanel>(session, "previewPanel");
            Assert.IsType<TexturePreviewSource>(previewPanel.ActivePreviewSource);

            EditorEntity cameraEntity = CreateCameraEntity();
            InvokePrivate(session, "HandleSelectionChanged", new EditorSelectionChangedEventArgs(cameraEntity, true));

            Assert.IsType<CameraPreviewSource>(previewPanel.ActivePreviewSource);
        }

        /// <summary>
        /// Ensures selecting one scene entity through the editor session creates a visible component platform tab strip on the properties panel.
        /// </summary>
        [Fact]
        public void HandleSelectionChanged_WhenSceneEntityIsSelected_ShowsPropertiesPlatformTabs() {
            EditorSession session = CreateSession();
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            InvokePrivate(session, "HandleSelectionChanged", new EditorSelectionChangedEventArgs(entity, true));

            PropertiesPanel propertiesPanel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(propertiesPanel, "ComponentPlatformTabStrip");
            List<EditorEntity> tabHosts = GetPrivateField<List<EditorEntity>>(tabStrip, "TabHosts");

            Assert.True(tabStrip.Root.Enabled);
            Assert.Equal(2, tabHosts.Count);
            Assert.All(tabHosts, host => Assert.True(host.Enabled));
        }

        /// <summary>
        /// Ensures the live session path builds drawable tab visuals for the properties platform strip instead of only enabling empty hosts.
        /// </summary>
        [Fact]
        public void HandleSelectionChanged_WhenSceneEntityIsSelected_BuildsPropertiesPlatformTabVisuals() {
            EditorSession session = CreateSession();
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            InvokePrivate(session, "HandleSelectionChanged", new EditorSelectionChangedEventArgs(entity, true));

            PropertiesPanel propertiesPanel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(propertiesPanel, "ComponentPlatformTabStrip");
            List<EditorEntity> tabHosts = GetPrivateField<List<EditorEntity>>(tabStrip, "TabHosts");
            EditorEntity firstTabHost = Assert.IsType<EditorEntity>(tabHosts[0]);
            TabComponent firstTab = Assert.Single(firstTabHost.Components.OfType<TabComponent>());
            RoundedRectComponent background = GetPrivateField<RoundedRectComponent>(firstTab, "roundedRect");
            Entity textEntity = GetPrivateField<Entity>(firstTab, "textEntity");
            TextComponent label = Assert.IsType<TextComponent>(Assert.Single(textEntity.Components));

            Assert.NotNull(background);
            Assert.NotNull(textEntity);
            Assert.NotNull(label);
        }

        /// <summary>
        /// Ensures a selected model replaces the preview with a model preview source.
        /// </summary>
        [Fact]
        public void HandleAssetSelected_WhenModelIsSelected_ReplacesTexturePreview() {
            EditorSession session = CreateSession();
            AssetBrowserEntry modelEntry = CreateModelEntry();

            InvokePrivate(session, "HandleAssetSelected", modelEntry);

            PreviewPanel previewPanel = GetPrivateField<PreviewPanel>(session, "previewPanel");
            Assert.IsType<ModelPreviewSource>(previewPanel.ActivePreviewSource);
        }

        /// <summary>
        /// Ensures clearing the asset selection clears the preview when nothing else is previewable.
        /// </summary>
        [Fact]
        public void HandleAssetSelectionCleared_WhenNothingPreviewableRemains_ClearsPreview() {
            EditorSession session = CreateSession();
            AssetBrowserEntry textureEntry = CreateTextureEntry();
            InvokePrivate(session, "HandleAssetSelected", textureEntry);

            PreviewPanel previewPanel = GetPrivateField<PreviewPanel>(session, "previewPanel");
            Assert.IsType<TexturePreviewSource>(previewPanel.ActivePreviewSource);

            InvokePrivate(session, "HandleAssetSelectionCleared");

            Assert.Null(previewPanel.ActivePreviewSource);
        }

        /// <summary>
        /// Creates a partially initialized editor session containing the collaborators used by preview selection.
        /// </summary>
        /// <returns>Editor session instance configured for preview selection tests.</returns>
        EditorSession CreateSession() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempProjectRootPath));
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
            assetImportManager.CurrentPlatformId = "windows";

            PropertiesPanel propertiesPanel = new PropertiesPanel(CreateFont(), contentManager);
            PreviewPanel previewPanel = new PreviewPanel(CreateFont());
            IReadOnlyList<string> supportedPlatforms = new List<string> { "windows" };
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, supportedPlatforms);
            PreviewSourceResolver previewSourceResolver = new PreviewSourceResolver(assetImportManager, Core.Instance.RenderManager2D, Core.Instance.RenderManager3D);

            SetPrivateField(session, "assetImportManager", assetImportManager);
            SetPrivateField(session, "propertiesPanel", propertiesPanel);
            SetPrivateField(session, "previewPanel", previewPanel);
            SetPrivateField(session, "previewSourceResolver", previewSourceResolver);
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", "windows");

            return session;
        }

        /// <summary>
        /// Creates one texture asset entry that can be resolved by the preview pipeline.
        /// </summary>
        /// <returns>Texture asset browser entry.</returns>
        AssetBrowserEntry CreateTextureEntry() {
            string sourcePath = WriteSourceTexture("Preview.png");
            return AssetBrowserEntry.CreateFileSystemFile(
                "Preview.png",
                "Textures/Preview.png",
                sourcePath,
                ".png",
                AssetEntryKind.Image);
        }

        /// <summary>
        /// Creates one model asset entry that can be resolved by the preview pipeline.
        /// </summary>
        /// <returns>Model asset browser entry.</returns>
        AssetBrowserEntry CreateModelEntry() {
            string sourcePath = WriteSourceModel("Preview.obj");
            return AssetBrowserEntry.CreateFileSystemFile(
                "Preview.obj",
                "Models/Preview.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model);
        }

        /// <summary>
        /// Creates one editor entity with a camera component for preview replacement tests.
        /// </summary>
        /// <returns>Editor entity with a camera component.</returns>
        EditorEntity CreateCameraEntity() {
            EditorEntity cameraEntity = new EditorEntity();
            float4 orientation;
            float4.CreateFromYawPitchRoll(0.2f, -0.1f, 0f, out orientation);
            cameraEntity.Position = new float3(5f, 3f, -7f);
            cameraEntity.Orientation = orientation;

            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 9,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(0f, 0f, 128f, 72f)
            };
            cameraEntity.AddComponent(camera);

            return cameraEntity;
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
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Finds one non-public instance field declared on the supplied type or any of its base types.
        /// </summary>
        /// <param name="type">Type whose inheritance chain should be searched.</param>
        /// <param name="fieldName">Field name to resolve.</param>
        /// <returns>Resolved field info.</returns>
        static FieldInfo FindPrivateField(Type type, string fieldName) {
            if (type == null) {
                throw new ArgumentNullException(nameof(type));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            Type currentType = type;
            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Could not resolve private field '{fieldName}' on '{type.FullName}'.");
        }

        /// <summary>
        /// Writes one minimal texture source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created source file.</returns>
        string WriteSourceTexture(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
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
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy properties-panel layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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


using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;
using Xunit.Sdk;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies selecting filesystem model assets does not leak editor UI entities into the scene hierarchy.
    /// </summary>
    public class EditorSessionModelAssetSelectionTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the model-selection session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Temporary assets root used by the import manager.
        /// </summary>
        readonly string AssetsRootPath;
        /// <summary>
        /// Configurable input system used by input-driven picker tests.
        /// </summary>
        readonly TestInputBackend Input;

        /// <summary>
        /// Initializes isolated editor services required by the model-selection tests.
        /// </summary>
        public EditorSessionModelAssetSelectionTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-model-selection-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            Input = new TestInputBackend();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
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
        /// Ensures selecting one filesystem model asset does not add visible scene entities.
        /// </summary>
        [Fact]
        public void HandleAssetSelected_WhenFileSystemModelIsSelected_DoesNotAddVisibleSceneHierarchyNodes() {
            string sourcePath = WriteSourceModel("Sponza.obj");
            EditorSession session = CreateSession();
            SceneHierarchyPanel hierarchyPanel = GetPrivateField<SceneHierarchyPanel>(session, "sceneHierarchyPanel");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Sponza.obj",
                "Models/Sponza.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model);

            hierarchyPanel.RefreshHierarchy();
            int hierarchyCountBeforeSelection = GetHierarchyNodeCount(hierarchyPanel);

            InvokePrivate(session, "HandleAssetSelected", entry);

            hierarchyPanel.RefreshHierarchy();
            int hierarchyCountAfterSelection = GetHierarchyNodeCount(hierarchyPanel);

            Assert.Equal(hierarchyCountBeforeSelection, hierarchyCountAfterSelection);
        }

        /// <summary>
        /// Ensures selecting one filesystem model through the properties panel does not leave one clickable `Up` button from the picker after the modal closes.
        /// </summary>
        [Fact]
        public void RequestModelPick_WhenFileSystemModelIsSelected_DoesNotLeavePickerUpButtonVisible() {
            string sourcePath = WriteSourceModel("Sponza.obj");
            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(meshComponent);

            PropertiesPanel panel = new PropertiesPanel(
                CreateFont(),
                contentManager,
                new EditorFileSystemModelResolver(assetImportManager));
            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow modelRow = FindModelRow(view);
            AssetPickerModal modal = new AssetPickerModal(CreateFont(), TempProjectRootPath);
            Action<AssetPickerRequest> handler = request => modal.Show(request.OnPicked, request.ExtensionFilter);
            EditorAssetPickerService.PickRequested += handler;

            try {
                InvokePrivate(view, "RequestModelPick", modelRow);
                modal.UpdateLayout(1280, 720);

                AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                    "Sponza.obj",
                    "Models/Sponza.obj",
                    sourcePath,
                    ".obj",
                    AssetEntryKind.Model);
                InvokePrivate(modal, "HandleAssetActivated", entry);

                Assert.False(HasVisibleButton("Up"), DescribeVisibleControls());
                Assert.False(HasRegisteredButtonArtifacts("Up"), DescribeRegisteredButtonArtifacts());
            } finally {
                EditorAssetPickerService.PickRequested -= handler;
            }
        }

        /// <summary>
        /// Ensures selecting one filesystem model through a real pointer click closes the picker without leaving the toolbar `Up` button behind.
        /// </summary>
        [Fact]
        public void RequestModelPick_WhenFileSystemModelIsClickedThroughInput_DoesNotLeavePickerUpButtonVisible() {
            WriteSourceModel("Sponza.obj");
            CreateModalCamera(1280, 720);

            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(meshComponent);

            PropertiesPanel panel = new PropertiesPanel(
                CreateFont(),
                contentManager,
                new EditorFileSystemModelResolver(assetImportManager));
            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow modelRow = FindModelRow(view);
            AssetPickerModal modal = new AssetPickerModal(CreateFont(), TempProjectRootPath);
            Action<AssetPickerRequest> handler = request => modal.Show(request.OnPicked, request.ExtensionFilter);
            EditorAssetPickerService.PickRequested += handler;

            try {
                InvokePrivate(view, "RequestModelPick", modelRow);
                modal.UpdateLayout(1280, 720);

                AssetBrowserRow row = FindRowByLabel(modal, "Sponza.obj");
                int2 rowPointer = GetRowPointer(row);

                AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
                AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
                Assert.Same(row.Interactable, Core.Instance.PointerInteractionSystem.Hovering);
                AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
                Assert.Same(row.Interactable, Core.Instance.PointerInteractionSystem.Highlighted);
                AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

                Assert.False(modal.IsVisible);
                Assert.False(HasVisibleButton("Up"), DescribeVisibleControls());
                Assert.False(HasRegisteredButtonArtifacts("Up"), DescribeRegisteredButtonArtifacts());
            } finally {
                EditorAssetPickerService.PickRequested -= handler;
            }
        }

        /// <summary>
        /// Ensures the asset picker backdrop strip reaches the minimize button border instead of leaving a gap.
        /// </summary>
        [Fact]
        public void RequestModelPick_WhenAssetPickerModalIsShown_PositionsBackdropTopFlushToWindowControlCluster() {
            AssetPickerModal modal = new AssetPickerModal(CreateFont(), TempProjectRootPath);
            modal.Show(_ => { }, ".obj");
            modal.UpdateLayout(1280, 720);

            SpriteComponent backdropTopSurface = GetPrivateField<SpriteComponent>(modal, "BackdropTopSurface");

            Assert.Equal(1280 - (EditorDialogBase.CloseButtonWidth * 3), backdropTopSurface.Size.X);
            Assert.Equal(EditorTitleBar.HeightPixels, backdropTopSurface.Size.Y);
        }

        /// <summary>
        /// Ensures selecting one filesystem model from a subdirectory closes the picker without leaving the toolbar `Up` button behind.
        /// </summary>
        [Fact]
        public void RequestModelPick_WhenFileSystemModelInsideSubdirectoryIsClickedThroughInput_DoesNotLeavePickerUpButtonVisible() {
            WriteSourceModel("Models/Sponza.obj");
            CreateModalCamera(1280, 720);

            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(meshComponent);

            PropertiesPanel panel = new PropertiesPanel(
                CreateFont(),
                contentManager,
                new EditorFileSystemModelResolver(assetImportManager));
            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow modelRow = FindModelRow(view);
            AssetPickerModal modal = new AssetPickerModal(CreateFont(), TempProjectRootPath);
            Action<AssetPickerRequest> handler = request => modal.Show(request.OnPicked, request.ExtensionFilter);
            EditorAssetPickerService.PickRequested += handler;

            try {
                InvokePrivate(view, "RequestModelPick", modelRow);
                modal.UpdateLayout(1280, 720);

                ClickRow(modal, "Models");
                Assert.True(HasVisibleButton("Up"), DescribeVisibleControls());

                ClickRow(modal, "Sponza.obj");

                Assert.False(modal.IsVisible);
                Assert.False(HasVisibleButton("Up"), DescribeVisibleControls());
                Assert.False(HasRegisteredButtonArtifacts("Up"), DescribeRegisteredButtonArtifacts());
            } finally {
                EditorAssetPickerService.PickRequested -= handler;
            }
        }

        /// <summary>
        /// Creates one partially initialized editor session containing the collaborators used by asset selection.
        /// </summary>
        /// <returns>Editor session configured for filesystem model asset selection tests.</returns>
        EditorSession CreateSession() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(TempProjectRootPath, contentManager);
            PropertiesPanel propertiesPanel = new PropertiesPanel(CreateFont(), contentManager);
            PreviewPanel previewPanel = new PreviewPanel(CreateFont());
            SceneHierarchyPanel sceneHierarchyPanel = new SceneHierarchyPanel(CreateFont());
            IReadOnlyList<string> supportedPlatforms = new List<string> { "windows" };
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, supportedPlatforms);

            manager.CurrentPlatformId = "windows";
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));

            SetPrivateField(session, "assetImportManager", manager);
            SetPrivateField(session, "propertiesPanel", propertiesPanel);
            SetPrivateField(session, "previewPanel", previewPanel);
            SetPrivateField(session, "sceneHierarchyPanel", sceneHierarchyPanel);
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", "windows");

            return session;
        }

        /// <summary>
        /// Reads the flattened hierarchy node count from one scene hierarchy panel.
        /// </summary>
        /// <param name="panel">Scene hierarchy panel to inspect.</param>
        /// <returns>Current number of visible hierarchy nodes.</returns>
        int GetHierarchyNodeCount(SceneHierarchyPanel panel) {
            FieldInfo nodesField = panel.GetType().GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic);
            ICollection nodes = Assert.IsAssignableFrom<ICollection>(nodesField.GetValue(panel));
            return nodes.Count;
        }

        /// <summary>
        /// Clicks one visible asset-browser row through the input system using its display label.
        /// </summary>
        /// <param name="modal">Modal whose browser row should be clicked.</param>
        /// <param name="label">Visible row label to activate.</param>
        void ClickRow(AssetPickerModal modal, string label) {
            if (modal == null) {
                throw new ArgumentNullException(nameof(modal));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Row label must be provided.", nameof(label));
            }

            AssetBrowserRow row = FindRowByLabel(modal, label);
            int2 rowPointer = GetRowPointer(row);

            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Assert.Same(row.Interactable, Core.Instance.PointerInteractionSystem.Hovering);
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Assert.Same(row.Interactable, Core.Instance.PointerInteractionSystem.Highlighted);
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
        }

        /// <summary>
        /// Creates the UI camera used by modal picker input routing.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        void CreateModalCamera(int width, int height) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorModalUi
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.EditorModalUi,
                CameraDrawOrder = EditorUiCameraDrawOrders.ModalUi,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Advances one complete input frame using the provided mouse state.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
            Input.Update();
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
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            Type currentType = target.GetType();
            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) {
                    return Assert.IsType<T>(field.GetValue(target));
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().FullName}' or its base types.");
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
        /// Creates a small font asset that can satisfy properties and hierarchy layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['z'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f)
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

        /// <summary>
        /// Finds the active model row currently rendered by one component-properties view.
        /// </summary>
        /// <param name="view">View whose active rows should be inspected.</param>
        /// <returns>The active row bound to one model property.</returns>
        ComponentPropertyRow FindModelRow(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Model);
        }

        /// <summary>
        /// Finds one live asset-browser row by its visible label.
        /// </summary>
        /// <param name="modal">Modal whose rows should be inspected.</param>
        /// <param name="label">Visible row label to locate.</param>
        /// <returns>Matching visible row.</returns>
        AssetBrowserRow FindRowByLabel(AssetPickerModal modal, string label) {
            if (modal == null) {
                throw new ArgumentNullException(nameof(modal));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Row label must be provided.", nameof(label));
            }

            AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(modal, "BrowserView");
            FieldInfo rowsField = typeof(AssetBrowserView).GetField("Rows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<AssetBrowserRow> rows = Assert.IsType<List<AssetBrowserRow>>(rowsField.GetValue(browserView));

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                AssetBrowserRow row = rows[rowIndex];
                if (!row.Entity.IsHierarchyEnabled || row.Entry == null) {
                    continue;
                }
                if (string.Equals(row.Entry.Name, label, StringComparison.Ordinal)) {
                    return row;
                }
            }

            throw new XunitException($"Could not find one visible asset-browser row named '{label}'.");
        }

        /// <summary>
        /// Computes one pointer position that lies safely inside the provided row.
        /// </summary>
        /// <param name="row">Row to target.</param>
        /// <returns>Pointer position inside the row bounds.</returns>
        int2 GetRowPointer(AssetBrowserRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            float3 position = row.Entity.Position;
            return new int2(
                (int)MathF.Round(position.X + 12f),
                (int)MathF.Round(position.Y + AssetBrowserView.RowHeight * 0.5f));
        }

        /// <summary>
        /// Builds one readable description of the currently enabled interactable controls.
        /// </summary>
        /// <returns>Readable description of active buttons and interactables.</returns>
        string DescribeVisibleControls() {
            List<string> lines = new List<string>();
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (!entity.IsHierarchyEnabled || entity.Components == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is ButtonComponent button) {
                        lines.Add($"Button:{ReadButtonText(button)} Parent={ReadEntityName(entity)} Pos={entity.Position.X},{entity.Position.Y},{entity.Position.Z}");
                    } else if (component is InteractableComponent interactable) {
                        lines.Add($"Interactable: Parent={ReadEntityName(entity)} Pos={entity.Position.X},{entity.Position.Y},{entity.Position.Z} Size={interactable.Size.X}x{interactable.Size.Y}");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines.OrderBy(line => line, StringComparer.Ordinal));
        }

        /// <summary>
        /// Builds one readable description of registered drawables and interactables owned by button entities.
        /// </summary>
        /// <returns>Readable description of registered button artifacts.</returns>
        string DescribeRegisteredButtonArtifacts() {
            List<string> lines = new List<string>();
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (entity.Components == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is not ButtonComponent button) {
                        continue;
                    }

                    string label = ReadButtonText(button);
                    lines.Add($"Button:{label} Enabled={entity.IsHierarchyEnabled} Interactable={HasRegisteredInteractable(entity)} Drawable={HasRegisteredDrawable(entity)} Parent={ReadEntityName(entity)} Pos={entity.Position.X},{entity.Position.Y},{entity.Position.Z}");
                }
            }

            return string.Join(Environment.NewLine, lines.OrderBy(line => line, StringComparer.Ordinal));
        }

        /// <summary>
        /// Determines whether one enabled button with the provided label remains visible.
        /// </summary>
        /// <param name="label">Button label to search for.</param>
        /// <returns>True when one live button with the provided label remains visible; otherwise false.</returns>
        bool HasVisibleButton(string label) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Button label must be provided.", nameof(label));
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (!entity.IsHierarchyEnabled || entity.Components == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is not ButtonComponent button) {
                        continue;
                    }

                    if (!string.Equals(ReadButtonText(button), label, StringComparison.Ordinal)) {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one button with the provided label still owns registered render or input artifacts.
        /// </summary>
        /// <param name="label">Button label to search for.</param>
        /// <returns>True when one matching button still owns one registered artifact; otherwise false.</returns>
        bool HasRegisteredButtonArtifacts(string label) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Button label must be provided.", nameof(label));
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (entity.Components == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is not ButtonComponent button) {
                        continue;
                    }
                    if (!string.Equals(ReadButtonText(button), label, StringComparison.Ordinal)) {
                        continue;
                    }
                    if (HasRegisteredInteractable(entity) || HasRegisteredDrawable(entity)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one entity still owns one interactable registered with the object manager.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when one interactable component remains registered; otherwise false.</returns>
        bool HasRegisteredInteractable(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<IInteractable2D> interactables = Core.Instance.ObjectManager.Interactables;
            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is not InteractableComponent interactable) {
                    continue;
                }
                if (interactables.Contains(interactable)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one entity still owns one drawable registered with the object manager.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when one text or rounded-rectangle drawable remains registered; otherwise false.</returns>
        bool HasRegisteredDrawable(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<IDrawable2D> drawables = Core.Instance.ObjectManager.Drawables2D;
            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                Component component = entity.Components[componentIndex];
                if (component is not IDrawable2D drawable) {
                    continue;
                }
                if (drawables.Contains(drawable)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the configured button label from one button component using reflection.
        /// </summary>
        /// <param name="button">Button whose label should be read.</param>
        /// <returns>Configured label text.</returns>
        string ReadButtonText(ButtonComponent button) {
            FieldInfo textField = typeof(ButtonComponent).GetField("text", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<string>(textField.GetValue(button));
        }

        /// <summary>
        /// Returns one readable entity name for diagnostics.
        /// </summary>
        /// <param name="entity">Entity to describe.</param>
        /// <returns>Entity name or type when unnamed.</returns>
        string ReadEntityName(Entity entity) {
            if (entity is EditorEntity editorEntity && !string.IsNullOrWhiteSpace(editorEntity.Name)) {
                return editorEntity.Name;
            }

            return entity.GetType().Name;
        }
    }
}


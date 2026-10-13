using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies that saving an editor scene persists the authored layout of viewport-scaled subtrees instead of the
    /// editor-window-sized preview that a [RunInEditor] <see cref="ViewportComponent"/> writes into the live entities.
    /// </summary>
    public sealed class ViewportAuthoredLayoutSaveTests : IDisposable {
        /// <summary>
        /// Editor main-window client size used by the investigation (maximized on 1920x1080).
        /// </summary>
        static readonly int2 EditorWindowSize = new int2(1904, 1041);

        /// <summary>
        /// Temporary project root used for scene save outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Editor core whose execution mode enables [RunInEditor] viewport scaling.
        /// </summary>
        readonly EditorCore CoreValue;

        /// <summary>
        /// Generated asset graph that supplies the authoring session and renderer resources.
        /// </summary>
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

        /// <summary>
        /// Persistence registry shared by save and load.
        /// </summary>
        readonly ComponentPersistenceRegistry Registry;

        /// <summary>
        /// Initializes an editor core whose main window has the editor's real size.
        /// </summary>
        public ViewportAuthoredLayoutSaveTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-viewport-authored-layout-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
            CoreValue = new EditorCore(new Project {
                Name = "Viewport Authored Layout",
                Path = TempProjectRootPath
            });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
            Registry = new ComponentPersistenceRegistry();
            ((TestRenderManager3D)CoreValue.RenderManager3D).OnWindowResize(IntPtr.Zero, EditorWindowSize.X, EditorWindowSize.Y);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Save, reload, scale and save again repeatedly: the persisted layout stays at the authored 1280x720 values instead of
        /// compounding by 1904/1280 x 1041/720 per pass, while the live preview stays stretched to the editor window.
        /// </summary>
        [Fact]
        public void SaveReloadSave_WhenViewportStretchesToEditorWindow_PersistsAuthoredLayoutWithoutDrift() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            ViewportComponent viewport = GetComponent<ViewportComponent>(root);
            viewport.Update();
            Assert.Equal(new int2(762, 139), GetComponent<RoundedRectComponent>(FindChild(root, "Panel")).Size);

            using SceneSaveService saveService = CreateSceneSaveService();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Menu.helen");
            List<float> savedFooterY = new List<float>();
            for (int cycle = 0; cycle < 3; cycle++) {
                saveService.Save(scenePath);
                savedFooterY.Add(ReadSavedChildPosition(scenePath, "Footer").Y);
                AssertScaledPreview(root);
                root.Dispose();

                root = LoadSingleRoot(scenePath);
                AssertAuthoredLayout(root);
                GetComponent<ViewportComponent>(root).Update();
                AssertScaledPreview(root);
            }

            saveService.Save(scenePath);
            savedFooterY.Add(ReadSavedChildPosition(scenePath, "Footer").Y);
            root.Dispose();
            AssertAuthoredLayout(LoadSingleRoot(scenePath));
            Assert.All(savedFooterY, footerY => Assert.Equal(678f, footerY));
        }

        /// <summary>
        /// The saved scene does not depend on the editor window: saving the same scene at 1904x1041, at 1600x900 and after a
        /// reload at 1600x900 produces byte-identical files, including the anchor-derived footer position.
        /// </summary>
        [Fact]
        public void Save_WhenEditorWindowSizeDiffers_WritesIdenticalScene() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            GetComponent<ViewportComponent>(root).Update();
            using SceneSaveService saveService = CreateSceneSaveService();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "WindowInvariant.helen");
            saveService.Save(scenePath);
            byte[] savedAtEditorSize = File.ReadAllBytes(scenePath);

            ((TestRenderManager3D)CoreValue.RenderManager3D).OnWindowResize(IntPtr.Zero, 1600, 900);
            GetComponent<ViewportComponent>(root).Update();
            Assert.Equal(new int2(640, 120), GetComponent<RoundedRectComponent>(FindChild(root, "Panel")).Size);
            saveService.Save(scenePath);
            byte[] savedAtSmallerSize = File.ReadAllBytes(scenePath);

            root.Dispose();
            root = LoadSingleRoot(scenePath);
            GetComponent<ViewportComponent>(root).Update();
            saveService.Save(scenePath);
            byte[] savedAfterReload = File.ReadAllBytes(scenePath);

            Assert.Equal(678f, ReadSavedChildPosition(scenePath, "Footer").Y);
            Assert.Equal(savedAtEditorSize, savedAtSmallerSize);
            Assert.Equal(savedAtEditorSize, savedAfterReload);
        }

        /// <summary>
        /// A reference-canvas fit subtree, which also scales in the editor, is saved at its authored values across repeated
        /// save/reload cycles at the editor window size.
        /// </summary>
        [Fact]
        public void SaveReloadSave_WhenReferenceCanvasFitScalesToEditorWindow_PersistsAuthoredLayoutWithoutDrift() {
            EditorEntity root = CreateMenu(new ReferenceCanvasFitComponent { ReferenceWidth = 1280, ReferenceHeight = 720 });
            GetComponent<ReferenceCanvasFitComponent>(root).Update();
            Assert.Equal(new int2(740, 139), GetComponent<RoundedRectComponent>(FindChild(root, "Panel")).Size);

            using SceneSaveService saveService = CreateSceneSaveService();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "FitMenu.helen");
            List<float> savedFooterY = new List<float>();
            for (int cycle = 0; cycle < 3; cycle++) {
                saveService.Save(scenePath);
                savedFooterY.Add(ReadSavedChildPosition(scenePath, "Footer").Y);
                Assert.Equal(new int2(740, 139), GetComponent<RoundedRectComponent>(FindChild(root, "Panel")).Size);
                root.Dispose();

                root = LoadSingleRoot(scenePath);
                AssertAuthoredLayout(root);
                GetComponent<ReferenceCanvasFitComponent>(root).Update();
                Assert.Equal(new int2(740, 139), GetComponent<RoundedRectComponent>(FindChild(root, "Panel")).Size);
            }

            saveService.Save(scenePath);
            savedFooterY.Add(ReadSavedChildPosition(scenePath, "Footer").Y);
            root.Dispose();
            AssertAuthoredLayout(LoadSingleRoot(scenePath));
            Assert.All(savedFooterY, footerY => Assert.Equal(678f, footerY));
        }

        /// <summary>
        /// Platform override payloads are shipped as complete component payloads, so an override created while the subtree is
        /// scaled must be seeded from the authored values, and persisting the live common component must not leak the preview scale.
        /// </summary>
        [Fact]
        public void PlatformOverride_WhenComponentIsInsideScaledViewport_PersistsAuthoredValues() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            GetComponent<ViewportComponent>(root).Update();
            EditorEntity panel = FindChild(root, "Panel");
            RoundedRectComponent commonRect = GetComponent<RoundedRectComponent>(panel);
            EntitySaveComponent save = Assert.IsType<EntitySaveComponent>(Assert.Single(panel.Components, component => component is EntitySaveComponent));
            ComponentPlatformEditingService platformEditing = new ComponentPlatformEditingService();
            EditorOverrideScope scope = EditorOverrideScope.ForPlatform("dc");

            RoundedRectComponent editable = Assert.IsType<RoundedRectComponent>(platformEditing.EnsureScopeOverrideComponent(commonRect, save, scope));
            Assert.Equal(new int2(512, 96), editable.Size);
            Assert.Equal(new int2(762, 139), commonRect.Size);
            editable.Radius = 20f;
            platformEditing.MarkScopePropertyOverride(commonRect, save, scope, nameof(RoundedRectComponent.Radius));
            platformEditing.PersistScopeOverride(commonRect, editable, save, scope);
            RoundedRectComponent payload = ReadOverridePayload(commonRect, save, scope);
            Assert.Equal(new int2(512, 96), payload.Size);
            Assert.Equal(20f, payload.Radius);

            platformEditing.PersistScopeOverride(commonRect, commonRect, save, scope);
            Assert.Equal(new int2(512, 96), ReadOverridePayload(commonRect, save, scope).Size);
            AssertScaledPreview(root);
        }

        /// <summary>
        /// A value edited in the stretched preview is mapped back into authored space on save, and the preview keeps showing the edit.
        /// </summary>
        [Fact]
        public void Save_WhenChildIsEditedInScaledPreview_PersistsTheEditInAuthoredSpace() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            GetComponent<ViewportComponent>(root).Update();
            EditorEntity panel = FindChild(root, "Panel");
            RoundedRectComponent panelRect = GetComponent<RoundedRectComponent>(panel);
            panelRect.Size = new int2(900, panelRect.Size.Y);
            panel.LocalPosition = new float3(595f, panel.LocalPosition.Y, panel.LocalPosition.Z);

            using SceneSaveService saveService = CreateSceneSaveService();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Edited.helen");
            saveService.Save(scenePath);

            Assert.Equal(new int2(900, 139), panelRect.Size);
            Assert.Equal(595f, panel.LocalPosition.X, 0.01f);
            root.Dispose();
            EditorEntity reloaded = LoadSingleRoot(scenePath);
            EditorEntity reloadedPanel = FindChild(reloaded, "Panel");
            Assert.Equal(new int2(605, 96), GetComponent<RoundedRectComponent>(reloadedPanel).Size);
            Assert.Equal(400f, reloadedPanel.LocalPosition.X, 0.01f);
            Assert.Equal(90f, reloadedPanel.LocalPosition.Y);
        }

        /// <summary>
        /// Undo/redo history captures serialize the authored layout too, so restoring an entity from history never re-injects scaled values.
        /// </summary>
        [Fact]
        public void CaptureEntityState_WhenEntityIsInsideScaledViewport_CapturesAuthoredValuesAndKeepsPreview() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            GetComponent<ViewportComponent>(root).Update();
            EditorEntity panel = FindChild(root, "Panel");
            using SceneSaveService saveService = CreateSceneSaveService();

            SerializedEditorEntityState state = saveService.CaptureEntityState(panel);

            Assert.Equal(new float3(384f, 90f, 0f), state.EntityAsset.LocalPosition);
            AssertScaledPreview(root);
        }

        /// <summary>
        /// When serialization throws, the scaled preview is still re-applied so the editor never keeps the restored layout.
        /// </summary>
        [Fact]
        public void Save_WhenSerializationThrows_ReappliesTheScaledPreview() {
            EditorEntity root = CreateMenu(CreateStretchViewport());
            GetComponent<ViewportComponent>(root).Update();
            FindChild(root, "Panel").AddComponent(new UnsupportedScriptComponent());
            using SceneSaveService saveService = CreateSceneSaveService();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Failing.helen");

            Assert.Throws<InvalidOperationException>(() => saveService.Save(scenePath));

            AssertScaledPreview(root);
        }

        /// <summary>
        /// Creates the screen-bound stretch viewport used by the DemoDisc main menu (1280x720 reference).
        /// </summary>
        /// <returns>A detached viewport component.</returns>
        static ViewportComponent CreateStretchViewport() {
            return new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            };
        }

        /// <summary>
        /// Builds the authored menu below one scaling layout owner: a panel, a nested label and a bottom-anchored footer.
        /// </summary>
        /// <param name="layoutOwner">Viewport or reference-canvas fit component attached to the menu root.</param>
        /// <returns>The menu root entity.</returns>
        EditorEntity CreateMenu(Component layoutOwner) {
            EditorEntity root = CreateEntity("MenuRoot", new float3(0f, 0f, 0f));
            root.AddComponent(layoutOwner);

            EditorEntity panel = CreateEntity("Panel", new float3(384f, 90f, 0f));
            panel.AddComponent(new RoundedRectComponent { Size = new int2(512, 96), Radius = 12f, BorderThickness = 2f });
            root.AddChild(panel);

            EditorEntity label = CreateEntity("Label", new float3(20f, 10f, 0f));
            label.AddComponent(new ClipRectComponent { Size = new int2(200, 40) });
            panel.AddChild(label);

            EditorEntity footer = CreateEntity("Footer", new float3(0f, 678f, 0f));
            footer.AddComponent(new RoundedRectComponent { Size = new int2(1280, 42) });
            LayoutComponent footerLayout = new LayoutComponent();
            footer.AddComponent(footerLayout);
            root.AddChild(footer);
            footerLayout.SetAnchorDistances(left: 0f, bottom: 0f);
            return root;
        }

        /// <summary>
        /// Asserts that the menu holds exactly the authored 1280x720 layout values. The bottom-anchored footer's position is
        /// derived by anchoring at load time, so it is checked for stability on the saved data instead.
        /// </summary>
        /// <param name="root">Menu root entity.</param>
        static void AssertAuthoredLayout(EditorEntity root) {
            EditorEntity panel = FindChild(root, "Panel");
            EditorEntity label = FindChild(panel, "Label");
            EditorEntity footer = FindChild(root, "Footer");
            Assert.Equal(new float3(0f, 0f, 0f), root.LocalPosition);
            Assert.Equal(new float3(384f, 90f, 0f), panel.LocalPosition);
            RoundedRectComponent panelRect = GetComponent<RoundedRectComponent>(panel);
            Assert.Equal(new int2(512, 96), panelRect.Size);
            Assert.Equal(12f, panelRect.Radius);
            Assert.Equal(2f, panelRect.BorderThickness);
            Assert.Equal(new float3(20f, 10f, 0f), label.LocalPosition);
            Assert.Equal(new int2(200, 40), GetComponent<ClipRectComponent>(label).Size);
            Assert.Equal(new int2(1280, 42), GetComponent<RoundedRectComponent>(footer).Size);
            Assert.Equal(new float4(0f, 0f, 0f, 0f), GetComponent<LayoutComponent>(footer).AnchorDistances);
        }

        /// <summary>
        /// Asserts that the live menu is stretched to the editor window (1904/1280 horizontally, 1041/720 vertically).
        /// </summary>
        /// <param name="root">Menu root entity.</param>
        static void AssertScaledPreview(EditorEntity root) {
            EditorEntity panel = FindChild(root, "Panel");
            EditorEntity footer = FindChild(root, "Footer");
            Assert.Equal(new int2(762, 139), GetComponent<RoundedRectComponent>(panel).Size);
            Assert.Equal(571.2f, panel.LocalPosition.X, 0.01f);
            Assert.Equal(130.125f, panel.LocalPosition.Y, 0.01f);
            Assert.Equal(new int2(1904, 61), GetComponent<RoundedRectComponent>(footer).Size);
            Assert.Equal(980f, footer.LocalPosition.Y, 0.01f);
        }

        /// <summary>
        /// Loads one saved scene file and returns its single root entity.
        /// </summary>
        /// <param name="scenePath">Saved scene path.</param>
        /// <returns>The loaded root entity.</returns>
        EditorEntity LoadSingleRoot(string scenePath) {
            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneLoadService loader = new SceneLoadService(Registry, new TestSceneAssetReferenceResolver(), GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.RendererResources);
            return Assert.Single(loader.Load(asset));
        }

        /// <summary>
        /// Deserializes the stored platform override payload of one component as a detached component.
        /// </summary>
        /// <param name="commonComponent">Common component that owns the override.</param>
        /// <param name="save">Hidden save component holding the override state.</param>
        /// <param name="scope">Override scope to read.</param>
        /// <returns>The detached component decoded from the stored payload.</returns>
        RoundedRectComponent ReadOverridePayload(RoundedRectComponent commonComponent, EntitySaveComponent save, EditorOverrideScope scope) {
            Assert.True(save.GetOrCreateComponentState(commonComponent).TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState overrideState));
            IComponentPersistenceDescriptor descriptor = Registry.GetDescriptor(commonComponent);
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor is AutomaticScriptComponentPersistenceDescriptor
                    ? AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(commonComponent.GetType())
                    : descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = overrideState.Payload
            };
            return Assert.IsType<RoundedRectComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), new TestSceneAssetReferenceResolver()));
        }

        /// <summary>
        /// Reads the persisted local position of one direct child of the saved scene's single root.
        /// </summary>
        /// <param name="scenePath">Saved scene path.</param>
        /// <param name="name">Child entity name.</param>
        /// <returns>The serialized local position.</returns>
        static float3 ReadSavedChildPosition(string scenePath, string name) {
            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset root = Assert.Single(asset.RootEntities);
            return Assert.Single(root.Children, child => child.Name == name).LocalPosition;
        }

        /// <summary>
        /// Creates the scene save service used by the tests.
        /// </summary>
        /// <returns>A save service bound to the temporary project.</returns>
        SceneSaveService CreateSceneSaveService() {
            return new SceneSaveService(GeneratedAssetGraph.CreateAuthoringSession(TempProjectRootPath), Registry);
        }

        /// <summary>
        /// Creates one scene-owned editor entity at the supplied local position.
        /// </summary>
        /// <param name="name">Entity name.</param>
        /// <param name="position">Authored local position.</param>
        /// <returns>The created entity.</returns>
        static EditorEntity CreateEntity(string name, float3 position) {
            EditorEntity entity = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(name));
            entity.LocalPosition = position;
            return entity;
        }

        /// <summary>
        /// Finds one direct child by name.
        /// </summary>
        /// <param name="parent">Parent entity.</param>
        /// <param name="name">Child name.</param>
        /// <returns>The matching child.</returns>
        static EditorEntity FindChild(Entity parent, string name) {
            return Assert.IsType<EditorEntity>(Assert.Single(parent.Children, child => child is EditorEntity editorChild && editorChild.Name == name));
        }

        /// <summary>
        /// Returns the single attached component of one type.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>The attached component.</returns>
        static T GetComponent<T>(Entity entity) where T : Component {
            return Assert.IsType<T>(Assert.Single(entity.Components, component => component is T));
        }
    }
}

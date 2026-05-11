using helengine.demo_disc_scene_writer;
using helengine.editor.tests.testing;
using helengine.files;
using Xunit;
using System.Text.Json.Nodes;

namespace helengine.editor.tests.tools {
    /// <summary>
    /// Verifies the demo-disc scene writer emits authored scene payloads the editor can deserialize.
    /// </summary>
    public class DemoDiscSceneWriterTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the scene-writer regression test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root for the scene-writer test.
        /// </summary>
        public DemoDiscSceneWriterTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-demo-disc-writer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "user_settings"));
            File.WriteAllText(Path.Combine(ProjectRootPath, "project.heproj"), """
{
  "projectFormatVersion": 1,
  "name": "Neon City",
  "version": "0.1.0",
  "requiredEngineVersion": "0.0.0",
  "supportedPlatforms": [
    "windows"
  ],
  "created": "2026-05-04T00:00:00Z",
  "lastOpened": "2026-05-04T00:00:00Z",
  "description": "Temporary test project."
}
""");
            File.WriteAllText(Path.Combine(ProjectRootPath, "user_settings", "build_config.json"), """
{
  "platforms": [
    {
      "platformId": "windows",
      "selectedSceneIds": [],
      "sceneOrders": [],
      "outputDirectoryPath": "",
      "debugBuild": false,
      "selectedBuildProfileId": "",
      "selectedGraphicsProfileId": "",
      "selectedBuildOptionValues": {},
      "selectedGraphicsOptionValues": {},
      "selectedCodegenProfileId": "",
      "selectedStorageProfileId": "",
      "selectedMediaProfileId": "",
      "selectedCodegenOptionValues": {},
      "selectedCodeModuleIds": []
    }
  ],
  "queueItems": []
}
""");
        }

        /// <summary>
        /// Deletes the temporary project root after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the generated menu scene contains an authored camera payload the editor can deserialize.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_ProducesCameraPayloadTheEditorCanDeserialize() {
            InitializeCore();
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset;
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
            using (FileStream stream = File.OpenRead(scenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            }

            SceneEntityAsset cameraEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscCamera");
            SceneComponentAssetRecord cameraRecord = Assert.Single(cameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");
            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();

            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(
                descriptor.DeserializeComponent(cameraRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal((byte)0, cameraComponent.CameraDrawOrder);
            Assert.Equal((ushort)1, cameraComponent.LayerMask);
            Assert.Equal(new float4(0f, 0f, 1f, 1f), cameraComponent.Viewport);
        }

        /// <summary>
        /// Ensures the baked menu root persists the root gameplay module assembly name when no authored module boundary owns the menu code.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuCodeLivesOutsideManifestBoundary_UsesGameplayAssemblyName() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay", ReadProviderTypeName());
            Assert.Equal(new[] { "gameplay" }, ReadSelectedCodeModuleIds());
        }

        /// <summary>
        /// Ensures the baked menu root persists the authored owning module assembly name when one folder-scoped module boundary owns the generated menu code.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuCodeLivesInsideManifestBoundary_UsesOwningModuleAssemblyName() {
            string menuModuleRootPath = Path.Combine(ProjectRootPath, "assets", "codebase", "menu");
            Directory.CreateDirectory(menuModuleRootPath);
            File.WriteAllText(Path.Combine(menuModuleRootPath, "code.module.json"), """
{
  "moduleId": "gameplay.menu",
  "dependencyModuleIds": [
    "gameplay"
  ],
  "loadScopes": [
    "always-loaded"
  ]
}
""");
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay.menu", ReadProviderTypeName());
            Assert.Equal(new[] { "gameplay.menu" }, ReadSelectedCodeModuleIds());
        }

        /// <summary>
        /// Ensures the generated demo-disc menu scene is baked into authored child entities instead of relying on a runtime menu host.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_BakesTheMenuHierarchyIntoTheScene() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset;
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
            using (FileStream stream = File.OpenRead(scenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            }

            Assert.Equal(2, sceneAsset.RootEntities.Length);

            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneComponentAssetRecord buildRecord = Assert.Single(menuEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.MenuComponent", StringComparison.Ordinal));
            Assert.NotNull(buildRecord);
            Assert.NotEmpty(menuEntity.Children);
        }

        /// <summary>
        /// Ensures the generated menu scene bakes the responsive viewport and panel anchor metadata.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_BakesViewportAndPanelAnchorComponentsForResponsiveLayout() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
            SceneEntityAsset panelEntity = Assert.Single(generatedRoot.Children, entity => string.Equals(entity.Id, "panel-main", StringComparison.Ordinal));
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            string viewportTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(ViewportComponent));
            string anchorTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(AnchorComponent));

            SceneComponentAssetRecord viewportRecord = Assert.Single(menuEntity.Components, component => string.Equals(component.ComponentTypeId, viewportTypeId, StringComparison.Ordinal));
            SceneComponentAssetRecord panelAnchorRecord = Assert.Single(panelEntity.Components, component => string.Equals(component.ComponentTypeId, anchorTypeId, StringComparison.Ordinal));

            ViewportComponent viewportComponent = Assert.IsType<ViewportComponent>(
                descriptor.DeserializeComponent(viewportRecord, null, new TestSceneAssetReferenceResolver()));
            AnchorComponent panelAnchorComponent = Assert.IsType<AnchorComponent>(
                descriptor.DeserializeComponent(panelAnchorRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(ViewportComponent.ScreenBindingMode, viewportComponent.BindingMode);
            Assert.Equal(new int2(DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight), viewportComponent.FixedSize);
            Assert.Equal((byte)(AnchorComponent.LeftAnchorFlag | AnchorComponent.TopAnchorFlag), panelAnchorComponent.AnchorFlags);
            Assert.Equal(new float4(88f, 0f, 190f, 0f), panelAnchorComponent.AnchorDistances);
        }

        /// <summary>
        /// Ensures the generated menu scene persists the authored reference canvas and a runtime fit component that can scale the subtree into smaller windows.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_BakesReferenceCanvasFitMetadata() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            string referenceCanvasFitTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(ReferenceCanvasFitComponent));
            SceneComponentAssetRecord referenceCanvasFitRecord = Assert.Single(
                menuEntity.Components,
                component => string.Equals(component.ComponentTypeId, referenceCanvasFitTypeId, StringComparison.Ordinal));

            ReferenceCanvasFitComponent referenceCanvasFitComponent = Assert.IsType<ReferenceCanvasFitComponent>(
                descriptor.DeserializeComponent(referenceCanvasFitRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.NotNull(sceneAsset.SceneSettings);
            Assert.NotNull(sceneAsset.SceneSettings.CanvasProfile);
            Assert.Equal(DemoMenuLayout.CanvasWidth, sceneAsset.SceneSettings.CanvasProfile.Width);
            Assert.Equal(DemoMenuLayout.CanvasHeight, sceneAsset.SceneSettings.CanvasProfile.Height);
            Assert.Equal(DemoMenuLayout.CanvasWidth, referenceCanvasFitComponent.ReferenceWidth);
            Assert.Equal(DemoMenuLayout.CanvasHeight, referenceCanvasFitComponent.ReferenceHeight);
        }

        /// <summary>
        /// Ensures generated demo-disc source files are written beneath the codebase folder instead of the asset root.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSourcesAreGenerated_WritesThemUnderCodebaseMenu() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            string codebaseMenuRootPath = Path.Combine(ProjectRootPath, "assets", "codebase", "menu");
            Assert.True(File.Exists(Path.Combine(codebaseMenuRootPath, "DemoDiscSceneCatalog.cs")));
            Assert.True(File.Exists(Path.Combine(codebaseMenuRootPath, "DemoDiscMenuTheme.cs")));
            Assert.True(File.Exists(Path.Combine(codebaseMenuRootPath, "DemoDiscMenuDefinitionProvider.cs")));
            Assert.False(Directory.Exists(Path.Combine(ProjectRootPath, "assets", "Menu")));
        }

        /// <summary>
        /// Ensures generated demo-disc provider source no longer emits the removed title and subtitle copy.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSourcesAreGenerated_DoesNotEmitRemovedTitleOrSubtitleCopy() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            string providerSourcePath = Path.Combine(ProjectRootPath, "assets", "codebase", "menu", "DemoDiscMenuDefinitionProvider.cs");
            string providerSource = File.ReadAllText(providerSourcePath);

            Assert.DoesNotContain("Helengine Demo Disc", providerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Lilac nights, bright experiments, and a little street grit.", providerSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated demo-disc assets author source font files and source-theme references.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuFontsAreGenerated_WritesSourceFontsAndSourceThemeReferences() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());
            string fontsRootPath = Path.Combine(ProjectRootPath, "assets", "Fonts");

            Directory.CreateDirectory(fontsRootPath);
            File.WriteAllText(Path.Combine(fontsRootPath, "DemoDiscTitle.hefont"), "placeholder");
            File.WriteAllText(Path.Combine(fontsRootPath, "DemoDiscBody.hefont"), "placeholder");

            writer.WriteAll(ProjectRootPath);

            string titleFontPath = Path.Combine(fontsRootPath, "DemoDiscTitle.ttf");
            string bodyFontPath = Path.Combine(fontsRootPath, "DemoDiscBody.ttf");
            string themeSource = File.ReadAllText(Path.Combine(ProjectRootPath, "assets", "codebase", "menu", "DemoDiscMenuTheme.cs"));

            Assert.True(File.Exists(titleFontPath));
            Assert.True(File.Exists(bodyFontPath));
            Assert.Contains("Fonts/DemoDiscTitle.ttf", themeSource, StringComparison.Ordinal);
            Assert.Contains("Fonts/DemoDiscBody.ttf", themeSource, StringComparison.Ordinal);
            Assert.DoesNotContain(".hefont", themeSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated build configuration no longer includes the stale missing sandbox scene entry.
        /// </summary>
        [Fact]
        public void WriteAll_WhenBuildConfigIsGenerated_DoesNotIncludeMissingSandboxScene() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            Assert.DoesNotContain("NewScene.helen", ReadSelectedSceneIds(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Ensures the generated build configuration stores concise scene ids instead of authored scene paths.
        /// </summary>
        [Fact]
        public void WriteAll_WhenBuildConfigIsGenerated_StoresConciseSceneIds() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            string[] sceneIds = ReadSelectedSceneIds();
            Assert.Contains("DemoDiscMainMenu", sceneIds, StringComparer.Ordinal);
            Assert.Contains("cube_test", sceneIds, StringComparer.Ordinal);
            Assert.Contains("colored_cube_grid", sceneIds, StringComparer.Ordinal);
            Assert.Contains("textured_cube_grid", sceneIds, StringComparer.Ordinal);
            Assert.DoesNotContain("Scenes/DemoDiscMainMenu.helen", sceneIds, StringComparer.Ordinal);
            Assert.DoesNotContain("scenes/rendering/cube_test.helen", sceneIds, StringComparer.Ordinal);
        }

        /// <summary>
        /// Ensures the generated demo-disc scene catalog source emits scene ids instead of authored scene paths.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneCatalogSourceIsGenerated_UsesSceneIdsInLoadSceneActions() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            string sceneCatalogSource = File.ReadAllText(Path.Combine(ProjectRootPath, "assets", "codebase", "menu", "DemoDiscSceneCatalog.cs"));
            Assert.Contains("\"cube_test\"", sceneCatalogSource, StringComparison.Ordinal);
            Assert.Contains("\"colored_cube_grid\"", sceneCatalogSource, StringComparison.Ordinal);
            Assert.Contains("\"textured_cube_grid\"", sceneCatalogSource, StringComparison.Ordinal);
            Assert.DoesNotContain("\"scenes/rendering/cube_test.helen\"", sceneCatalogSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated baked scene no longer contains dedicated title or subtitle text entities.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeTitleOrSubtitleEntities() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            Assert.DoesNotContain(generatedRoot.Children, child => string.Equals(child.Name, "demo-disc-menu-title", StringComparison.Ordinal));
            Assert.DoesNotContain(generatedRoot.Children, child => string.Equals(child.Name, "demo-disc-menu-subtitle", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the generated baked scene no longer contains the decorative left-side root accent bar.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeRootAccentBarEntity() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            Assert.DoesNotContain(generatedRoot.Children, child => string.Equals(child.Id, "demo-disc-menu-accent", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures regenerated demo menu panels no longer author the static subtitle entity beneath the heading.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeStaticPanelDescriptionEntities() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
                Assert.DoesNotContain(
                    panelEntity.Children,
                    child => child.Id.Contains("-description", StringComparison.Ordinal) && !child.Id.StartsWith("selected-description-", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Ensures regenerated demo menu panels no longer author the decorative left accent bar and still keep the selected-description entity.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_RemovesPanelAccentBarAndKeepsSelectedDescriptionEntity() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
                Assert.DoesNotContain(panelEntity.Children, child => child.Id.EndsWith("-accent", StringComparison.Ordinal));
                Assert.Contains(panelEntity.Children, child => child.Id.StartsWith("selected-description-", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Ensures regenerated demo menu panels still keep the dynamic selected-description text target after static cleanup.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_KeepsSelectedDescriptionEntityPerPanel() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
                Assert.Single(panelEntity.Children.Where(child => child.Id.StartsWith("selected-description-", StringComparison.Ordinal)));
            }
        }

        /// <summary>
        /// Ensures generated demo menu panels bake a clipped item viewport and scrolling root instead of parenting item rows directly under the panel root.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_BakesClippedSceneListViewportAndScrollingRoot() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
            SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));

            SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
            SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));

            Assert.DoesNotContain(sceneSelectPanel.Children, child => child.Id.StartsWith("item-scene-select-", StringComparison.Ordinal));
            Assert.Contains(itemsRoot.Children, child => child.Id.StartsWith("item-scene-select-", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the generated scene-list viewport owns a clip component and the scrolling root owns a scroll component.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_BakesClipAndScrollComponentsForSceneList() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
            SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));
            SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
            SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));

            string clipTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(ClipRectComponent));
            string scrollTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(ScrollComponent));

            Assert.Contains(itemsViewport.Components, component => string.Equals(component.ComponentTypeId, clipTypeId, StringComparison.Ordinal));
            Assert.Contains(itemsRoot.Components, component => string.Equals(component.ComponentTypeId, scrollTypeId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the scene-select panel shows four full rows and only a partial fifth row so scrolling remains visually obvious.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_LimitsSceneSelectViewportToFourAndAHalfRows() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
            SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));
            SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
            SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord clipRecord = Assert.Single(itemsViewport.Components);
            SceneComponentAssetRecord scrollRecord = Assert.Single(itemsRoot.Components);

            ClipRectComponent clipComponent = Assert.IsType<ClipRectComponent>(descriptor.DeserializeComponent(clipRecord, null, new TestSceneAssetReferenceResolver()));
            ScrollComponent scrollComponent = Assert.IsType<ScrollComponent>(descriptor.DeserializeComponent(scrollRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(4, scrollComponent.VisibleItemCount);
            Assert.Equal(DemoMenuLayout.ButtonWidth, clipComponent.Size.X);
            Assert.Equal(272, clipComponent.Size.Y);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate their render queues during deserialization.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Reads the persisted provider type name from the generated demo-disc menu build component.
        /// </summary>
        /// <returns>Persisted provider type name.</returns>
        string ReadProviderTypeName() {
            SceneAsset sceneAsset = ReadGeneratedSceneAsset();
            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneComponentAssetRecord menuRecord = Assert.Single(menuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
            MenuComponentPersistenceDescriptor descriptor = new MenuComponentPersistenceDescriptor();
            MenuComponent menuHostComponent = Assert.IsType<MenuComponent>(
                descriptor.DeserializeComponent(menuRecord, null, new TestSceneAssetReferenceResolver()));
            return menuHostComponent.ProviderTypeName;
        }

        /// <summary>
        /// Reads the generated demo-disc scene asset from disk.
        /// </summary>
        /// <returns>Generated scene asset.</returns>
        SceneAsset ReadGeneratedSceneAsset() {
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>
        /// Reads the selected Windows code-module ids from the generated build config.
        /// </summary>
        /// <returns>Selected Windows code-module ids.</returns>
        string[] ReadSelectedCodeModuleIds() {
            JsonNode rootNode = JsonNode.Parse(File.ReadAllText(Path.Combine(ProjectRootPath, "user_settings", "build_config.json")))
                ?? throw new InvalidOperationException("Build config JSON could not be parsed.");
            JsonArray platforms = rootNode["platforms"]?.AsArray()
                ?? throw new InvalidOperationException("Build config is missing the platforms array.");
            JsonObject windowsPlatform = Assert.IsType<JsonObject>(Assert.Single(
                platforms,
                platformNode => string.Equals(platformNode?["platformId"]?.GetValue<string>(), "windows", StringComparison.OrdinalIgnoreCase)));
            JsonArray selectedCodeModuleIdsNode = windowsPlatform["selectedCodeModuleIds"]?.AsArray()
                ?? throw new InvalidOperationException("Build config is missing selected code-module ids.");
            return [.. selectedCodeModuleIdsNode.Select(moduleIdNode => moduleIdNode?.GetValue<string>() ?? string.Empty)];
        }

        /// <summary>
        /// Reads the selected Windows scene ids from the generated build config.
        /// </summary>
        /// <returns>Selected Windows scene ids.</returns>
        string[] ReadSelectedSceneIds() {
            JsonNode rootNode = JsonNode.Parse(File.ReadAllText(Path.Combine(ProjectRootPath, "user_settings", "build_config.json")))
                ?? throw new InvalidOperationException("Build config JSON could not be parsed.");
            JsonArray platforms = rootNode["platforms"]?.AsArray()
                ?? throw new InvalidOperationException("Build config is missing the platforms array.");
            JsonObject windowsPlatform = Assert.IsType<JsonObject>(Assert.Single(
                platforms,
                platformNode => string.Equals(platformNode?["platformId"]?.GetValue<string>(), "windows", StringComparison.OrdinalIgnoreCase)));
            JsonArray selectedSceneIdsNode = windowsPlatform["selectedSceneIds"]?.AsArray()
                ?? throw new InvalidOperationException("Build config is missing selected scene ids.");
            return [.. selectedSceneIdsNode.Select(sceneIdNode => sceneIdNode?.GetValue<string>() ?? string.Empty)];
        }
    }
}

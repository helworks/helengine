using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the viewport selection order across screen-space 2D, world-preview 2D, and generic 3D scene picking.
    /// </summary>
    public sealed class EditorViewportPicker2DSelectionTests : IDisposable {
        EditorSessionInteractionServices InteractionServices => GeneratedAssetGraph.InteractionServices;
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Initializes the core services required by the viewport 2D selection tests.
        /// </summary>
        public EditorViewportPicker2DSelectionTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            CoreValue = core;
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures direct viewport selection resolves the underlying 2D scene entity under the pointer.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenSelectableScene2DExists_ReturnsTheUnderlying2DEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            InteractableComponent interactable = CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50),
                GeneratedAssetGraph.ObjectManager);

            Assert.Same(interactable.Parent, selectedEntity);
        }

        /// <summary>
        /// Ensures direct viewport selection returns null when no selectable 2D scene entity lies under the pointer so the picker can fall back to 3D.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenNoSelectableScene2DExists_ReturnsNull() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(250, 150),
                GeneratedAssetGraph.ObjectManager);

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Ensures clicking one rendered preview proxy resolves selection back to the authored 2D source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenPreviewProxyIsClicked_SelectsTheUnderlying2DEntity() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(48, 24),
                Texture = CoreValue.RenderManager2D.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources));
            InteractionServices.WorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures clicking one rendered text preview proxy resolves selection back to the authored text source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenTextPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            TextComponent textComponent = new TextComponent {
                Text = "Preview Text",
                Size = new int2(120, 32)
            };
            sourceEntity.AddComponent(textComponent);

            EditorEntity previewEntity = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, textComponent));
            previewEntity.AddComponent(new EditorTextWorldPreviewComponent(sourceEntity, textComponent, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources));
            InteractionServices.WorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures clicking one rendered rounded-rectangle preview proxy resolves selection back to the authored rounded-rectangle source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenRoundedRectPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent {
                Size = new int2(96, 48),
                FillColor = new byte4(255, 255, 255, 255)
            };
            sourceEntity.AddComponent(roundedRectComponent);

            EditorEntity previewEntity = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, roundedRectComponent));
            previewEntity.AddComponent(new EditorRoundedRectWorldPreviewComponent(sourceEntity, roundedRectComponent, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources));
            InteractionServices.WorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures 2D selection resolves before any later 3D fallback when authored 2D content overlaps other scene geometry.
        /// </summary>
        [Fact]
        public void ResolveSelection_When2DPreviewAnd3DOverlap_PrefersThe2DSourceEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            InteractableComponent interactable = CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity overlappingMeshEntity = new Entity(CoreValue) {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            overlappingMeshEntity.InitComponents();
            overlappingMeshEntity.InitChildren();
            overlappingMeshEntity.AddComponent(new MeshComponent {
                Model = GeneratedAssetGraph.GetRuntimeModel(EngineGeneratedModelCache.PlaneAssetId),
                Materials = new RuntimeMaterial[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedAssetGraph.MaterialCache) }
            });

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50),
                GeneratedAssetGraph.ObjectManager);

            Assert.Same(interactable.Parent, selectedEntity);
        }

        /// <summary>
        /// Ensures viewport-owned supported 2D scene entities bypass the screen-space 2D pick path so the visible world-preview proxy can own selection resolution.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenViewportOwnedEntityUsesWorldPreview_ReturnsNullForScreenSpace2DPath() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));

            Entity viewportEntity = new Entity(CoreValue);
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(320, 180)
            });

            Entity contentEntity = new Entity(CoreValue) {
                Position = new float3(20f, 30f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            viewportEntity.AddChild(contentEntity);
            contentEntity.AddComponent(new SpriteComponent {
                Texture = CoreValue.RenderManager2D.PixelTexture,
                Size = new int2(100, 60),
            });
            contentEntity.AddComponent(new InteractableComponent {
                Size = new int2(100, 60)
            });

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50),
                GeneratedAssetGraph.ObjectManager);

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Ensures one world-preview sprite proxy can be resolved directly from the scene-view pointer before generic 3D picking runs.
        /// </summary>
        [Fact]
        public void ResolveSelectableWorldPreviewEntityAtPointer_WhenViewportOwnedPreviewIsUnderPointer_ReturnsTheUnderlyingSourceEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 500f, 400f));

            Entity viewportEntity = new Entity(CoreValue);
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(500, 400)
            });

            Entity sourceEntity = new Entity(CoreValue) {
                LocalPosition = new float3(-50f, -30f, 0f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(100, 60),
                Texture = CoreValue.RenderManager2D.PixelTexture,
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources));
            InteractionServices.WorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(250, 200),
                GeneratedAssetGraph.ObjectManager);

            Assert.Same(sourceEntity, selectedEntity);
        }

        /// <summary>
        /// Ensures overlapping preview selection follows ray depth and authored sibling order after transforms change.
        /// </summary>
        [Fact]
        public void ResolveWorldPreview_UsesDistanceThenSourceHierarchy() {
            CameraComponent camera = CreateSceneCamera(new float4(0f, 0f, 500f, 400f));
            camera.Parent.LocalPosition = new float3(0f, 0f, 100f);
            Entity parent = new Entity(CoreValue);
            parent.InitComponents();
            parent.InitChildren();
            Entity first = CreateWorldPreviewSource(parent, 0f);
            Entity second = CreateWorldPreviewSource(parent, -10f);
            int2 pointer = new int2(250, 200);

            Assert.Same(first, EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                camera, camera.Viewport, pointer, GeneratedAssetGraph.ObjectManager));
            second.LocalPosition = new float3(-50f, -30f, 10f);
            Assert.Same(second, EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                camera, camera.Viewport, pointer, GeneratedAssetGraph.ObjectManager));
            second.LocalPosition = new float3(-50f, -30f, 0f);
            Assert.Same(second, EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                camera, camera.Viewport, pointer, GeneratedAssetGraph.ObjectManager));
            parent.RemoveChild(first);
            parent.AddChild(first);
            Assert.Same(first, EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                camera, camera.Viewport, pointer, GeneratedAssetGraph.ObjectManager));
        }

        /// <summary>Ensures GPU alpha misses and deeper IDs are not replaced by a rectangular world-preview hit.</summary>
        [Fact]
        public void ResolveSelectionPick_UsesGpuCoverageInsteadOfWorldPreviewBounds() {
            CameraComponent camera = CreateSceneCamera(new float4(0, 0, 500, 400));
            camera.Parent.LocalPosition = new float3(0, 0, 100);
            Entity parent = new Entity(CoreValue);
            parent.InitComponents();
            parent.InitChildren();
            Entity source = CreateWorldPreviewSource(parent, 0);
            Assert.Same(source, EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                camera, camera.Viewport, new int2(250, 200), GeneratedAssetGraph.ObjectManager));
            EditorEntity pickerOwner = new EditorEntity(CoreValue, InteractionServices);
            CameraComponent pickerCamera = new CameraComponent { LayerMask = EditorLayerMasks.SceneObjects };
            pickerOwner.AddComponent(pickerCamera);
            EditorViewportGizmoDrawableCollector gizmos = new EditorViewportGizmoDrawableCollector(
                ResolveNoAdditionalOwnedEntities,
                new EditorEntity(CoreValue, InteractionServices),
                new EditorEntity(CoreValue, InteractionServices),
                new EditorEntity(CoreValue, InteractionServices));
            EditorViewportPicker picker = new EditorViewportPicker(camera, camera, gizmos, pickerOwner, pickerCamera,
                new TestEditorPickingBackend(), GeneratedAssetGraph.RendererResources);
            pickerOwner.AddComponent(picker);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EditorViewportPicker).GetField("PendingPointer", flags).SetValue(picker, new int2(250, 200));
            typeof(EditorViewportPicker).GetField("PendingViewport", flags).SetValue(picker, camera.Viewport);
            typeof(EditorViewportPicker).GetMethod("BuildPickColors", flags).Invoke(picker, new object[] { 1 });
            Dictionary<int, Entity> entities = (Dictionary<int, Entity>)typeof(EditorViewportPicker).GetField("PickEntitiesById", flags).GetValue(picker);
            int sourceId = Assert.Single(entities.Where(pair => ReferenceEquals(pair.Value, source))).Key;
            MethodInfo resolve = typeof(EditorViewportPicker).GetMethod("ResolveSelectionPick", flags);
            InteractionServices.Selection.SetSelectedEntity(source);
            resolve.Invoke(picker, new object[] { 0 });
            Assert.Null(InteractionServices.Selection.SelectedEntity);
            resolve.Invoke(picker, new object[] { sourceId });
            Assert.Same(source, InteractionServices.Selection.SelectedEntity);
            Entity behind = new Entity(CoreValue);
            behind.InitComponents();
            entities.Add(0xFFFF, behind);
            resolve.Invoke(picker, new object[] { 0xFFFF });
            Assert.Same(behind, InteractionServices.Selection.SelectedEntity);
        }

        /// <summary>Provides an empty gizmo extension list for a picker isolated from viewport tool controls.</summary>
        /// <returns>An empty collection of additional gizmo entities.</returns>
        static IReadOnlyList<EditorEntity> ResolveNoAdditionalOwnedEntities() {
            return Array.Empty<EditorEntity>();
        }

        /// <summary>Creates a selectable sprite and its registered world-preview proxy.</summary>
        /// <param name="parent">Authored parent controlling sibling order.</param>
        /// <param name="depth">Local depth of the source plane.</param>
        /// <returns>The authored sprite entity.</returns>
        Entity CreateWorldPreviewSource(Entity parent, float depth) {
            Entity source = new Entity(CoreValue) {
                LocalPosition = new float3(-50f, -30f, depth)
            };
            source.InitComponents();
            parent.AddChild(source);
            SpriteComponent sprite = new SpriteComponent {
                Size = new int2(100, 60),
                Texture = CoreValue.RenderManager2D.PixelTexture
            };
            source.AddComponent(sprite);
            EditorEntity proxy = new EditorEntity(CoreValue, InteractionServices) { InternalEntity = true };
            proxy.AddComponent(new Editor2DPreviewSourceTagComponent(source, sprite));
            proxy.AddComponent(new EditorSpriteWorldPreviewComponent(source, sprite, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources));
            InteractionServices.WorldSpace2DPreviewRegistry.Register(source, proxy);
            return source;
        }

        /// <summary>
        /// Creates one active scene camera with the supplied viewport rectangle.
        /// </summary>
        /// <param name="viewport">Viewport rectangle used by direct scene selection.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float4 viewport) {
            Entity cameraEntity = new Entity(CoreValue) {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 255,
                Viewport = viewport
            };
            cameraEntity.AddComponent(camera);
            return camera;
        }

        /// <summary>
        /// Creates one selectable scene 2D entity with a visible sprite and interactable bounds.
        /// </summary>
        /// <param name="position">Top-left entity position in window-space coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="depth">2D depth assigned to the visible sprite.</param>
        /// <returns>Interactable component registered for hit resolution.</returns>
        InteractableComponent CreateSceneInteractableEntity(float3 position, int2 size, byte depth) {
            Entity entity = new Entity(CoreValue) {
                LayerMask = EditorLayerMasks.SceneObjects,
                Position = new float3(position.X, position.Y, depth)
            };
            entity.InitComponents();
            entity.InitChildren();

            SpriteComponent sprite = new SpriteComponent {
                Texture = CoreValue.RenderManager2D.PixelTexture,
                Size = size,
            };
            entity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = size
            };
            entity.AddComponent(interactable);
            return interactable;
        }
    }
}

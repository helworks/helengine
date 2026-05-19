using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the viewport selection order across screen-space 2D, world-preview 2D, and generic 3D scene picking.
    /// </summary>
    public sealed class EditorViewportPicker2DSelectionTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the viewport 2D selection tests.
        /// </summary>
        public EditorViewportPicker2DSelectionTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            EditorWorldSpace2DPreviewRegistry.Clear();
            EditorWorldSpace2DPreviewMeshResources.ResetForTests();
            Core.Instance?.Dispose();
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
                new int2(60, 50));

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
                new int2(250, 150));

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Ensures clicking one rendered preview proxy resolves selection back to the authored 2D source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenPreviewProxyIsClicked_SelectsTheUnderlying2DEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(48, 24),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent));
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures clicking one rendered text preview proxy resolves selection back to the authored text source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenTextPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            TextComponent textComponent = new TextComponent {
                Text = "Preview Text",
                Size = new int2(120, 32)
            };
            sourceEntity.AddComponent(textComponent);

            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, textComponent));
            previewEntity.AddComponent(new EditorTextWorldPreviewComponent(sourceEntity, textComponent));
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures clicking one rendered rounded-rectangle preview proxy resolves selection back to the authored rounded-rectangle source entity.
        /// </summary>
        [Fact]
        public void ResolveSelection_WhenRoundedRectPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent {
                Size = new int2(96, 48),
                FillColor = new byte4(255, 255, 255, 255)
            };
            sourceEntity.AddComponent(roundedRectComponent);

            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, roundedRectComponent));
            previewEntity.AddComponent(new EditorRoundedRectWorldPreviewComponent(sourceEntity, roundedRectComponent));
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(previewEntity));
        }

        /// <summary>
        /// Ensures 2D selection resolves before any later 3D fallback when authored 2D content overlaps other scene geometry.
        /// </summary>
        [Fact]
        public void ResolveSelection_When2DPreviewAnd3DOverlap_PrefersThe2DSourceEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            InteractableComponent interactable = CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity overlappingMeshEntity = new Entity {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            overlappingMeshEntity.InitComponents();
            overlappingMeshEntity.InitChildren();
            overlappingMeshEntity.AddComponent(new MeshComponent {
                Model = EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.PlaneAssetId),
                Material = helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial()
            });

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50));

            Assert.Same(interactable.Parent, selectedEntity);
        }

        /// <summary>
        /// Ensures viewport-owned supported 2D scene entities bypass the screen-space 2D pick path so the visible world-preview proxy can own selection resolution.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenViewportOwnedEntityUsesWorldPreview_ReturnsNullForScreenSpace2DPath() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(320, 180)
            });

            Entity contentEntity = new Entity {
                Position = new float3(20f, 30f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            viewportEntity.AddChild(contentEntity);
            contentEntity.AddComponent(new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = new int2(100, 60),
                RenderOrder2D = 4
            });
            contentEntity.AddComponent(new InteractableComponent {
                Size = new int2(100, 60)
            });

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50));

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Ensures one world-preview sprite proxy can be resolved directly from the scene-view pointer before generic 3D picking runs.
        /// </summary>
        [Fact]
        public void ResolveSelectableWorldPreviewEntityAtPointer_WhenViewportOwnedPreviewIsUnderPointer_ReturnsTheUnderlyingSourceEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 500f, 400f));

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(500, 400)
            });

            Entity sourceEntity = new Entity {
                LocalPosition = new float3(-50f, -30f, 0f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(100, 60),
                Texture = TextureUtils.PixelTexture,
                RenderOrder2D = 4
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent));
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(250, 200));

            Assert.Same(sourceEntity, selectedEntity);
        }

        /// <summary>
        /// Creates one active scene camera with the supplied viewport rectangle.
        /// </summary>
        /// <param name="viewport">Viewport rectangle used by direct scene selection.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float4 viewport) {
            Entity cameraEntity = new Entity {
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
        /// <param name="renderOrder">2D render order assigned to the visible sprite.</param>
        /// <returns>Interactable component registered for hit resolution.</returns>
        InteractableComponent CreateSceneInteractableEntity(float3 position, int2 size, byte renderOrder) {
            Entity entity = new Entity {
                LayerMask = EditorLayerMasks.SceneObjects,
                Position = position
            };
            entity.InitComponents();
            entity.InitChildren();

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = size,
                RenderOrder2D = renderOrder
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

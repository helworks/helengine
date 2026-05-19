using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the editor-owned authored-viewport border synchronizer creates and updates world-space border gizmos correctly.
    /// </summary>
    public sealed class EditorViewportBorderGizmoSyncComponentTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by authored-viewport border gizmo tests.
        /// </summary>
        public EditorViewportBorderGizmoSyncComponentTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            EditorViewportBorderGizmoMeshResources.ResetForTests();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures one authored viewport component creates one internal border gizmo entity.
        /// </summary>
        [Fact]
        public void Update_WhenAuthoredViewportExists_CreatesBorderGizmoEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(320, 180)
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            EditorViewportBorderGizmoComponent gizmoComponent = Assert.Single(
                Core.Instance.ObjectManager.Entities
                    .SelectMany(entity => entity.Components)
                    .OfType<EditorViewportBorderGizmoComponent>());
            Assert.Same(sourceEntity, gizmoComponent.SourceEntity);
            Assert.True(gizmoComponent.Parent is EditorEntity editorEntity && editorEntity.InternalEntity);
        }

        /// <summary>
        /// Ensures viewport entities inside internal editor hierarchy do not create authored border gizmos.
        /// </summary>
        [Fact]
        public void Update_WhenViewportBelongsToInternalEditorHierarchy_DoesNotCreateBorderGizmoEntity() {
            EditorEntity internalRoot = new EditorEntity {
                InternalEntity = true
            };
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(320, 180)
            });
            internalRoot.AddChild(viewportEntity);

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            Assert.Empty(
                Core.Instance.ObjectManager.Entities
                    .SelectMany(entity => entity.Components)
                    .OfType<EditorViewportBorderGizmoComponent>());
        }

        /// <summary>
        /// Ensures one authored viewport border gizmo mirrors the authored viewport transform and resolved size.
        /// </summary>
        [Fact]
        public void Update_WhenAuthoredViewportExists_SynchronizesBorderTransformAndSize() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(42f, 24f, 7f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(640, 360)
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            EditorViewportBorderGizmoComponent gizmoComponent = Assert.Single(
                Core.Instance.ObjectManager.Entities
                    .SelectMany(entity => entity.Components)
                    .OfType<EditorViewportBorderGizmoComponent>());
            Assert.Equal(sourceEntity.Position, gizmoComponent.Parent.LocalPosition);
            Assert.Equal(sourceEntity.Orientation, gizmoComponent.Parent.LocalOrientation);
            Assert.Equal(new float3(640f, 360f, 1f), gizmoComponent.Parent.LocalScale);
        }

        /// <summary>
        /// Ensures the authored viewport border gizmo mesh starts at the local origin instead of being centered around it.
        /// </summary>
        [Fact]
        public void Update_WhenAuthoredViewportExists_BuildsCornerOriginBorderMesh() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(320, 180)
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            ModelAsset builtModelAsset = Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Equal(new float3(0f, 0f, 0f), builtModelAsset.Positions[0]);
            Assert.Equal(new float3(1f, 0f, 0f), builtModelAsset.Positions[1]);
            Assert.Equal(new float3(1f, -1f, 0f), builtModelAsset.Positions[2]);
            Assert.Equal(new float3(0f, -1f, 0f), builtModelAsset.Positions[3]);
        }

        /// <summary>
        /// Ensures authored viewport border gizmos reverse reference-canvas fit offsets so the viewport root presents at its authored origin in scene view.
        /// </summary>
        [Fact]
        public void Update_WhenViewportUsesReferenceCanvasFit_ResolvesPresentedViewportOriginInsteadOfLiveWindowOffset() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager3D.AddWindow(IntPtr.Zero, 1600, 1200);

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });
            sourceEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            EditorViewportBorderGizmoComponent gizmoComponent = Assert.Single(
                Core.Instance.ObjectManager.Entities
                    .SelectMany(entity => entity.Components)
                    .OfType<EditorViewportBorderGizmoComponent>());
            Assert.Equal(float3.Zero, gizmoComponent.Parent.LocalPosition);
            Assert.Equal(new float3(1280f, 720f, 1f), gizmoComponent.Parent.LocalScale);
        }

        /// <summary>
        /// Ensures screen-bound authored viewports that use reference-canvas fit still present their authored reference size in scene view instead of the live host-window size.
        /// </summary>
        [Fact]
        public void Update_WhenScreenBoundViewportUsesReferenceCanvasFit_PresentsReferenceCanvasSize() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager3D.AddWindow(IntPtr.Zero, 1600, 1200);

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });
            sourceEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorViewportBorderGizmoSyncComponent syncComponent = new EditorViewportBorderGizmoSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();

            EditorViewportBorderGizmoComponent gizmoComponent = Assert.Single(
                Core.Instance.ObjectManager.Entities
                    .SelectMany(entity => entity.Components)
                    .OfType<EditorViewportBorderGizmoComponent>());
            Assert.Equal(float3.Zero, gizmoComponent.Parent.LocalPosition);
            Assert.Equal(new float3(1280f, 720f, 1f), gizmoComponent.Parent.LocalScale);
        }
    }
}

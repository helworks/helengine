using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor visibility suppresses rendering without disabling scene entities.
    /// </summary>
    public sealed class EditorEntityVisibilityTests {
        /// <summary>
        /// Ensures hiding an entity removes its 2D and 3D drawables from camera queues while preserving enabled state.
        /// </summary>
        [Fact]
        public void Hidden_WhenToggled_RemovesDrawablesFromCameraQueuesWithoutDisablingEntity() {
            Core core = CreateCore();
            EditorSessionInteractionServices interactions = new EditorSessionInteractionServices();
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            CameraComponent camera = new CameraComponent { LayerMask = 1, FilterEditorHiddenEntities = true };
            cameraEntity.AddComponent(camera);
            EditorEntity gameCameraEntity = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            CameraComponent gameCamera = new CameraComponent { LayerMask = 1 };
            gameCameraEntity.AddComponent(gameCamera);
            EditorEntity entity = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            DualDrawableComponent drawable = new DualDrawableComponent();
            entity.AddComponent(drawable);
            core.ObjectManager.RegisterForRender2D(drawable);
            core.ObjectManager.RegisterForRender3D(drawable);

            Assert.True(Contains(camera.RenderQueue2D, drawable));
            Assert.True(Contains(camera.RenderQueue3D, drawable));
            Assert.True(Contains(gameCamera.RenderQueue2D, drawable));

            entity.Hidden = true;

            Assert.False(Contains(camera.RenderQueue2D, drawable));
            Assert.False(Contains(camera.RenderQueue3D, drawable));
            Assert.True(Contains(gameCamera.RenderQueue2D, drawable));
            Assert.True(Contains(gameCamera.RenderQueue3D, drawable));
            Assert.True(entity.Enabled);
            Assert.True(entity.IsHierarchyEnabled);
            Assert.Contains(drawable, core.ObjectManager.Drawables2D);
            Assert.Contains(drawable, core.ObjectManager.Drawables3D);

            entity.Hidden = false;

            Assert.True(Contains(camera.RenderQueue2D, drawable));
            Assert.True(Contains(camera.RenderQueue3D, drawable));
            Assert.True(Contains(gameCamera.RenderQueue2D, drawable));
        }

        /// <summary>
        /// Ensures a hidden parent suppresses descendant rendering without changing child enabled state or local visibility.
        /// </summary>
        [Fact]
        public void Hidden_OnParentSuppressesDescendantDrawablesUntilParentIsShown() {
            Core core = CreateCore();
            EditorSessionInteractionServices interactions = new EditorSessionInteractionServices();
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            CameraComponent camera = new CameraComponent { LayerMask = 1, FilterEditorHiddenEntities = true };
            cameraEntity.AddComponent(camera);
            EditorEntity parent = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            EditorEntity child = new EditorEntity(Core.Instance, interactions) { LayerMask = 1 };
            DualDrawableComponent drawable = new DualDrawableComponent();
            child.AddComponent(drawable);
            core.ObjectManager.RegisterForRender2D(drawable);
            core.ObjectManager.RegisterForRender3D(drawable);
            parent.AddChild(child);

            Assert.True(Contains(camera.RenderQueue2D, drawable));
            parent.Hidden = true;

            Assert.False(Contains(camera.RenderQueue2D, drawable));
            Assert.True(child.Enabled);
            Assert.True(child.IsHierarchyEnabled);
            Assert.False(child.Hidden);

            parent.Hidden = false;

            Assert.True(Contains(camera.RenderQueue2D, drawable));
        }

        static bool Contains(IRenderQueue2D queue, IDrawable2D drawable) {
            RenderList2D list = Assert.IsType<RenderList2D>(queue);
            for (int index = 0; index < list.Count; index++) {
                if (ReferenceEquals(list[index], drawable)) {
                    return true;
                }
            }

            return false;
        }

        static bool Contains(IRenderQueue3D queue, IDrawable3D drawable) {
            RenderList3D list = Assert.IsType<RenderList3D>(queue);
            for (int index = 0; index < list.Count; index++) {
                if (ReferenceEquals(list[index], drawable)) {
                    return true;
                }
            }

            return false;
        }

        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            return core;
        }

        sealed class DualDrawableComponent : Component, IDrawable2D, IDrawable3D {
            public byte RenderOrder3D { get; set; }
            public RuntimeModel Model => null;
            public RuntimeMaterial[] Materials { get; set; }

            public void Draw() { }
        }
    }
}

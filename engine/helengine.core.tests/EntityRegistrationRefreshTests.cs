using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the registration refresh that runs after a parent change rebuilds every registration a component owns,
    /// including components that implement more than one of the drawable and camera contracts.
    /// </summary>
    public sealed class EntityRegistrationRefreshTests {
        [Fact]
        public void Reparenting_refreshes_both_render_registrations_of_a_component_that_is_2d_and_3d_drawable() {
            Core core = CreateInitializedCore();
            Entity newParent = CreateEntity(core);
            Entity moved = CreateEntity(core);
            Entity untouched = CreateEntity(core);

            DualDrawableComponent movedDrawable = new DualDrawableComponent();
            moved.AddComponent(movedDrawable);
            DualDrawableComponent untouchedDrawable = new DualDrawableComponent();
            untouched.AddComponent(untouchedDrawable);

            core.ObjectManager.RegisterForRender2D(movedDrawable);
            core.ObjectManager.RegisterForRender3D(movedDrawable);
            core.ObjectManager.RegisterForRender2D(untouchedDrawable);
            core.ObjectManager.RegisterForRender3D(untouchedDrawable);

            Assert.Same(movedDrawable, core.ObjectManager.Drawables2D[0]);
            Assert.Same(movedDrawable, core.ObjectManager.Drawables3D[0]);

            newParent.AddChild(moved);

            Assert.Same(movedDrawable, core.ObjectManager.Drawables2D[core.ObjectManager.Drawables2D.Count - 1]);
            Assert.Same(movedDrawable, core.ObjectManager.Drawables3D[core.ObjectManager.Drawables3D.Count - 1]);
            Assert.Equal(2, core.ObjectManager.Drawables2D.Count);
            Assert.Equal(2, core.ObjectManager.Drawables3D.Count);
        }

        [Fact]
        public void Reparenting_refreshes_both_the_drawable_and_camera_registrations_of_a_component_that_is_both() {
            Core core = CreateInitializedCore();
            Entity newParent = CreateEntity(core);
            Entity moved = CreateEntity(core);
            Entity untouched = CreateEntity(core);

            DrawableCameraComponent movedCamera = new DrawableCameraComponent();
            moved.AddComponent(movedCamera);
            DrawableCameraComponent untouchedCamera = new DrawableCameraComponent();
            untouched.AddComponent(untouchedCamera);

            core.ObjectManager.RegisterForRender2D(movedCamera);
            core.ObjectManager.RegisterCamera(movedCamera);
            core.ObjectManager.RegisterForRender2D(untouchedCamera);
            core.ObjectManager.RegisterCamera(untouchedCamera);

            Assert.Same(movedCamera, core.ObjectManager.Drawables2D[0]);
            Assert.Same(movedCamera, core.ObjectManager.Cameras[0]);

            newParent.AddChild(moved);

            Assert.Same(movedCamera, core.ObjectManager.Drawables2D[core.ObjectManager.Drawables2D.Count - 1]);
            Assert.Same(movedCamera, core.ObjectManager.Cameras[core.ObjectManager.Cameras.Count - 1]);
            Assert.Equal(2, core.ObjectManager.Drawables2D.Count);
            Assert.Equal(2, core.ObjectManager.Cameras.Count);
        }

        /// <summary>
        /// Creates one entity with initialized child and component collections.
        /// </summary>
        /// <param name="core">Owning core.</param>
        /// <returns>Configured entity.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>
        /// Creates one headless core whose object manager is materialized so entities can register against it.
        /// </summary>
        /// <returns>Initialized core instance.</returns>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Component that participates in both the 2D and the 3D render lists so the refresh must rebuild two registrations.
        /// </summary>
        sealed class DualDrawableComponent : Component, IDrawable2D, IDrawable3D {
            /// <summary>
            /// Gets or sets the 2D render order for this drawable.
            /// </summary>
            public byte RenderOrder2D { get; set; }

            /// <summary>
            /// Gets or sets the 3D render order for this drawable.
            /// </summary>
            public byte RenderOrder3D { get; set; }

            /// <summary>
            /// Gets the runtime model; this probe draws nothing and therefore owns no model.
            /// </summary>
            public RuntimeModel Model {
                get { return null; }
            }

            /// <summary>
            /// Gets or sets the runtime materials bound to each drawable submesh slot.
            /// </summary>
            public RuntimeMaterial[] Materials { get; set; }

            /// <summary>
            /// Performs no drawing; registration bookkeeping is the only behavior under test.
            /// </summary>
            public void Draw() {
            }
        }

        /// <summary>
        /// Component that is both a 2D drawable and a camera so the refresh must rebuild the render and the camera registration.
        /// </summary>
        sealed class DrawableCameraComponent : Component, IDrawable2D, ICamera {
            readonly RenderList2D RenderQueue2DValue = new RenderList2D(4);
            readonly RenderList3D RenderQueue3DValue = new RenderList3D(4);

            /// <summary>
            /// Gets or sets the 2D render order for this drawable.
            /// </summary>
            public byte RenderOrder2D { get; set; }

            /// <summary>
            /// Gets or sets the layer mask this camera renders.
            /// </summary>
            public ushort LayerMask { get; set; } = 1;

            /// <summary>
            /// Gets or sets the draw order applied when the camera is inserted into the ordered camera list.
            /// </summary>
            public byte CameraDrawOrder { get; set; }

            /// <summary>
            /// Gets or sets the viewport rectangle of this camera.
            /// </summary>
            public float4 Viewport { get; set; }

            /// <summary>
            /// Gets or sets the near clip-plane distance of this camera.
            /// </summary>
            public float FieldOfView { get; set; } = CameraProjectionUtils.DefaultFieldOfView;

            public float NearPlaneDistance { get; set; } = 0.1f;

            /// <summary>
            /// Gets or sets the far clip-plane distance of this camera.
            /// </summary>
            public float FarPlaneDistance { get; set; } = 100f;

            /// <summary>
            /// Gets or sets the render target receiving this camera's output.
            /// </summary>
            public RenderTarget RenderTarget { get; set; }

            /// <summary>
            /// Gets or sets the clear settings applied before this camera renders.
            /// </summary>
            public CameraClearSettings ClearSettings { get; set; }

            /// <summary>
            /// Gets or sets the authored render intent for this camera.
            /// </summary>
            public CameraRenderSettings RenderSettings { get; set; }

            /// <summary>
            /// Gets the 2D render queue owned by this camera.
            /// </summary>
            public IRenderQueue2D RenderQueue2D {
                get { return RenderQueue2DValue; }
            }

            /// <summary>
            /// Gets the 3D render queue owned by this camera.
            /// </summary>
            public IRenderQueue3D RenderQueue3D {
                get { return RenderQueue3DValue; }
            }

            /// <summary>
            /// Performs no drawing; registration bookkeeping is the only behavior under test.
            /// </summary>
            public void Draw() {
            }
        }
    }
}

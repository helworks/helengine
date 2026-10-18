using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>Owns a small real runtime and framebuffer for layout lifecycle tests.</summary>
    internal sealed class ViewportLayoutTestScene : IDisposable {
        /// <summary>Render manager publishing real resize events.</summary>
        public MultiWindowTestRenderManager Meshes { get; }
        /// <summary>Runtime that owns all test entities.</summary>
        public Core Core { get; }
        /// <summary>Initializes a 320 by 240 presentation target.</summary>
        public ViewportLayoutTestScene() {
            Meshes = new MultiWindowTestRenderManager();
            Core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            Core.Initialize(Meshes, new Batch2DTestRenderManager(), null, new PlatformInfo("test", "test"));
            Meshes.AddWindow(IntPtr.Zero, 320, 240);
        }
        /// <summary>Creates a fully allocated but not yet initialized scene entity.</summary>
        public Entity CreateEntity() {
            Entity entity = new Entity(Core);
            entity.InitChildren();
            entity.InitComponents();
            return entity;
        }
        /// <summary>Creates a stretch owner with an authored canvas twice the framebuffer.</summary>
        public ViewportComponent AddViewport(Entity root, ViewportLayoutProcessingMode mode) {
            ViewportComponent viewport = new ViewportComponent {
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                ReferenceWidth = 640, ReferenceHeight = 480, LayoutProcessingMode = mode
            };
            root.AddComponent(viewport);
            return viewport;
        }
        /// <summary>Releases test entities and renderer resources in their real ownership order.</summary>
        public void Dispose() { Core.Dispose(); }
    }
}

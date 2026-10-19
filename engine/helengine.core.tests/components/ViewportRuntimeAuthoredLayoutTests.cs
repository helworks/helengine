using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>
    /// Pins the runtime (player) behavior of viewport reference-canvas scaling: the authored layout captured at load stays the
    /// only source of truth, and editor-only preview-edit tracking never runs outside the editor.
    /// </summary>
    public sealed class ViewportRuntimeAuthoredLayoutTests {
        /// <summary>
        /// At runtime a stretch viewport scales from the captured authored values on every pass; values written by gameplay code
        /// between passes are replaced on the next resize instead of being mapped back into authored space.
        /// </summary>
        [Fact]
        public void RuntimeStretch_WhenLiveValuesChangeBetweenPasses_RescalesFromTheCapturedAuthoredLayout() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test-version"));
            meshes.AddWindow(IntPtr.Zero, 1904, 1041);
            Assert.Equal(ComponentExecutionMode.Runtime, core.ExecutionMode);

            Entity root = new Entity(core);
            root.InitComponents();
            root.InitChildren();
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            };
            root.AddComponent(viewport);
            Entity panel = new Entity(core) { LocalPosition = new float3(384f, 90f, 0f) };
            panel.InitComponents();
            panel.InitChildren();
            RoundedRectComponent rect = new RoundedRectComponent { Size = new int2(512, 96) };
            panel.AddComponent(rect);
            root.AddChild(panel);

            viewport.Update();
            Assert.Equal(new int2(762, 139), rect.Size);
            Assert.Equal((double)(571.2f),(double)(panel.LocalPosition.X), 0.01f);

            rect.Size = new int2(900, 139);
            panel.LocalPosition = new float3(10f, 10f, 0f);
            meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            viewport.Update();

            Assert.Equal(new int2(256, 64), rect.Size);
            Assert.Equal(new float3(192f, 60f, 0f), panel.LocalPosition);
        }

        /// <summary>
        /// At runtime a reference-canvas fit subtree rescales from the captured authored values; live writes between passes are
        /// replaced on the next resize, and restoring the authored layout outside the editor changes nothing.
        /// </summary>
        [Fact]
        public void RuntimeReferenceCanvasFit_WhenLiveValuesChangeBetweenPasses_RescalesFromTheCapturedAuthoredLayout() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test-version"));
            meshes.AddWindow(IntPtr.Zero, 1280, 720);

            Entity root = new Entity(core);
            root.InitComponents();
            root.InitChildren();
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 1280, ReferenceHeight = 720 };
            root.AddComponent(fit);
            Entity panel = new Entity(core) { LocalPosition = new float3(384f, 90f, 0f) };
            panel.InitComponents();
            panel.InitChildren();
            RoundedRectComponent rect = new RoundedRectComponent { Size = new int2(512, 96) };
            panel.AddComponent(rect);
            root.AddChild(panel);
            fit.Update();
            meshes.OnWindowResize(IntPtr.Zero, 1920, 1080);
            fit.Update();
            Assert.Equal(new int2(768, 144), rect.Size);

            rect.Size = new int2(900, 139);
            panel.LocalPosition = new float3(10f, 10f, 0f);
            fit.RestoreAuthoredLayout();
            Assert.Equal(new int2(900, 139), rect.Size);
            meshes.OnWindowResize(IntPtr.Zero, 640, 360);
            fit.Update();

            Assert.Equal(new int2(256, 48), rect.Size);
            Assert.Equal(new float3(192f, 45f, 0f), panel.LocalPosition);
        }

        /// <summary>
        /// Restoring the authored layout outside the editor has no recorded preview pass to undo, so it leaves live runtime values untouched.
        /// </summary>
        [Fact]
        public void RestoreAuthoredLayout_WhenRunningOutsideTheEditor_LeavesLiveValuesUntouched() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test-version"));
            meshes.AddWindow(IntPtr.Zero, 640, 480);
            Entity root = new Entity(core);
            root.InitComponents();
            root.InitChildren();
            RoundedRectComponent rect = new RoundedRectComponent { Size = new int2(512, 96) };
            root.AddComponent(rect);
            ViewportComponent viewport = new ViewportComponent {
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            };
            root.AddComponent(viewport);
            viewport.Update();
            Assert.Equal(new int2(256, 64), rect.Size);

            viewport.RestoreAuthoredLayout();

            Assert.Equal(new int2(256, 64), rect.Size);
            viewport.ReapplyLayout();
            Assert.Equal(new int2(256, 64), rect.Size);
        }
    }
}

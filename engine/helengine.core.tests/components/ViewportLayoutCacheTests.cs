using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>Verifies typed retention independently of the viewport's lifecycle coordinator.</summary>
    public sealed class ViewportLayoutCacheTests {
        /// <summary>New components are captured by identity even when hierarchy population stays constant.</summary>
        [Fact]
        public void SameCountComponentReplacement_DropsOldReference() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test"));
            Entity root = new Entity(core);
            root.InitChildren();
            root.InitComponents();
            RoundedRectComponent old = new RoundedRectComponent { Size = new int2(100, 40) };
            root.AddComponent(old);
            ViewportLayoutCache cache = ViewportLayoutCache.Capture(root);
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(2, new int2(640, 480), new int2(320, 240), 1, 1, 160, 160);
            cache.Apply(metrics);
            Assert.Equal(new int2(50, 20), old.Size);
            cache.Remove(old);
            root.RemoveComponent(old);
            RoundedRectComponent replacement = new RoundedRectComponent { Size = new int2(200, 80) };
            root.AddComponent(replacement);
            cache.Reconcile(root);
            cache.Apply(metrics);
            Assert.Equal(new int2(100, 40), replacement.Size);
            Assert.Equal(1, cache.PositionCount);
            cache.Release();
        }

        /// <summary>Originals survive repeated scale passes and deliberate writes to resolved values.</summary>
        [Fact]
        public void RuntimeWrites_KeepOriginalResizeBaseline() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test"));
            Entity root = new Entity(core);
            root.InitChildren();
            root.InitComponents();
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            root.AddComponent(rectangle);
            ViewportLayoutCache cache = ViewportLayoutCache.Capture(root);
            cache.Apply(ViewportLayoutMath.Resolve(2, new int2(640, 480), new int2(320, 240), 1, 1, 160, 160));
            rectangle.Size = new int2(900, 900);
            cache.Apply(ViewportLayoutMath.Resolve(2, new int2(640, 480), new int2(1280, 960), 1, 1, 160, 160));
            Assert.Equal(new int2(200, 80), rectangle.Size);
            cache.Release();
        }
    }
}

using System.Reflection;
using helengine;

namespace helengine.core.tests.components {
    /// <summary>Verifies camera queue replacement preserves borrowed drawables and disposes old containers.</summary>
    public sealed class CameraRenderListOwnershipTests {
        /// <summary>Checks queue growth copies membership before releasing each previous queue.</summary>
        [Fact]
        public void Attachment_GrowsQueuesPreservesMembershipAndDisposesPreviousContainers() {
            using Core core = CreateCore();
            CameraComponent camera = new CameraComponent();
            InitializeLists(camera);
            RenderList2D previous2D = Assert.IsType<RenderList2D>(camera.RenderQueue2D);
            RenderList3D previous3D = Assert.IsType<RenderList3D>(camera.RenderQueue3D);
            Drawable drawable = new Drawable();
            Drawable secondDrawable = new Drawable { RenderOrder3D = 3 };
            previous2D.Add(drawable);
            previous2D.Add(secondDrawable);
            previous3D.Add(drawable);
            previous3D.Add(secondDrawable);
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();

            entity.AddComponent(camera);

            RenderList2D current2D = Assert.IsType<RenderList2D>(camera.RenderQueue2D);
            RenderList3D current3D = Assert.IsType<RenderList3D>(camera.RenderQueue3D);
            Assert.NotSame(previous2D, current2D);
            Assert.NotSame(previous3D, current3D);
            Assert.Equal(0, previous2D.Count);
            Assert.Equal(0, previous3D.Count);
            Assert.Equal(2, current2D.Count);
            Assert.Equal(2, current3D.Count);
            Assert.Same(drawable, current2D[0]);
            Assert.Same(drawable, current3D[0]);
            Assert.Same(secondDrawable, current2D[1]);
            Assert.Same(secondDrawable, current3D[1]);
            Assert.True(current2D.Capacity >= 32);
            Assert.True(current3D.Capacity >= 32);
            Assert.False(drawable.WasDisposed);
            Assert.False(secondDrawable.WasDisposed);
        }

        /// <summary>Checks repeated initialization keeps queues that already satisfy the requested capacity.</summary>
        [Fact]
        public void Initialization_WithSufficientCapacityPreservesQueueIdentity() {
            using Core core = CreateCore();
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            CameraComponent camera = new CameraComponent();
            entity.AddComponent(camera);
            IRenderQueue2D queue2D = camera.RenderQueue2D;
            IRenderQueue3D queue3D = camera.RenderQueue3D;

            InitializeLists(camera);

            Assert.Same(queue2D, camera.RenderQueue2D);
            Assert.Same(queue3D, camera.RenderQueue3D);
        }

        /// <summary>Checks repeated initialization before attachment preserves the already allocated detached queues.</summary>
        [Fact]
        public void Initialization_WhileDetachedPreservesQueueIdentity() {
            CameraComponent camera = new CameraComponent();
            InitializeLists(camera);
            IRenderQueue2D queue2D = camera.RenderQueue2D;
            IRenderQueue3D queue3D = camera.RenderQueue3D;

            InitializeLists(camera);

            Assert.Same(queue2D, camera.RenderQueue2D);
            Assert.Same(queue3D, camera.RenderQueue3D);
            camera.Dispose();
        }

        /// <summary>Checks a failure copying borrowed drawables retains the old queue and its usable membership.</summary>
        [Fact]
        public void Attachment_WhenCopyFailsPreservesPreviousQueue() {
            using Core core = CreateCore();
            CameraComponent camera = new CameraComponent();
            InitializeLists(camera);
            RenderList3D previous = Assert.IsType<RenderList3D>(camera.RenderQueue3D);
            Drawable drawable = new Drawable();
            previous.Add(drawable);
            drawable.ThrowOnRenderOrder = true;
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();

            Assert.Throws<InvalidOperationException>(() => entity.AddComponent(camera));

            drawable.ThrowOnRenderOrder = false;
            Assert.Same(previous, camera.RenderQueue3D);
            Assert.Equal(1, previous.Count);
            Assert.Same(drawable, previous[0]);
            Assert.False(drawable.WasDisposed);
        }

        /// <summary>Creates a headless core with capacities larger than detached camera queues.</summary>
        /// <returns>Initialized core that owns the test entities.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                RenderList2DInitialCapacity = 32,
                RenderList3DInitialCapacity = 32
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>Runs the same private queue initialization used by camera lifecycle registration.</summary>
        /// <param name="camera">Camera whose render lists are being initialized.</param>
        static void InitializeLists(CameraComponent camera) {
            MethodInfo method = typeof(CameraComponent).GetMethod("InitializeLists", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(camera, null);
        }

        /// <summary>Borrowed drawable with no GPU resources; queue cleanup must leave it alive.</summary>
        sealed class Drawable : Component, IDrawable2D, IDrawable3D {
            /// <summary>Stores the ordering value used by the queue.</summary>
            byte RenderOrderValue;
            /// <summary>Gets or sets whether reading order should fail during queue copying.</summary>
            public bool ThrowOnRenderOrder { get; set; }
            /// <summary>Gets or sets the test render ordering.</summary>
            public byte RenderOrder3D {
                get {
                    if (ThrowOnRenderOrder) { throw new InvalidOperationException("Injected queue copy failure."); }
                    return RenderOrderValue;
                }
                set { RenderOrderValue = value; }
            }
            /// <summary>Gets the absent model for this bookkeeping-only drawable.</summary>
            public RuntimeModel Model => null;
            /// <summary>Gets or sets the unused materials.</summary>
            public RuntimeMaterial[] Materials { get; set; }
            /// <summary>Gets whether a queue incorrectly disposed this borrowed component.</summary>
            public bool WasDisposed { get; private set; }
            /// <summary>Records disposal so queue ownership can be checked.</summary>
            public override void Dispose() {
                WasDisposed = true;
                base.Dispose();
            }
            /// <summary>Performs no drawing.</summary>
            public void Draw() { }
        }
    }
}

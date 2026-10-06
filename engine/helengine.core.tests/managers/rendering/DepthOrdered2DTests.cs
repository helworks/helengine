using helengine;

namespace helengine.core.tests {
    /// <summary>Verifies physical 2D stacking stays synchronized with hierarchy and pointer precedence.</summary>
    public sealed class DepthOrdered2DTests : IDisposable {
        /// <summary>Core owning the fixture's entities and render registrations.</summary>
        readonly Core CoreValue;

        /// <summary>Initializes a renderer-independent scene for ordering tests.</summary>
        public DepthOrdered2DTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            CoreValue.Initialize(null, null, null, new PlatformInfo("test", "test"));
        }

        /// <summary>Releases the fixture's scene and renderer resources.</summary>
        public void Dispose() {
            CoreValue.Dispose();
        }

        /// <summary>Depth changes reorder existing registrations and invalidate consumers of queue revisions.</summary>
        [Fact]
        public void Queue_MovingDepth_ReordersWithoutReregistering() {
            SpriteComponent front = CreateSprite(5);
            SpriteComponent back = CreateSprite(-3);
            using RenderList2D queue = new RenderList2D(2);
            queue.Add(front);
            queue.Add(back);
            Assert.Same(back, queue[0]);
            int version = queue.Version;
            front.Parent.LocalPosition = new float3(0, 0, -8);
            Assert.Same(front, queue[0]);
            Assert.NotEqual(version, queue.Version);
        }

        /// <summary>Equal-depth content follows sibling order even when creation and registration differ.</summary>
        [Fact]
        public void Queue_EqualDepth_FollowsHierarchyAfterReparenting() {
            Entity root = CreateEntity(0);
            SpriteComponent laterSibling = CreateSprite(0);
            SpriteComponent earlierSibling = CreateSprite(0);
            root.AddChild(earlierSibling.Parent);
            root.AddChild(laterSibling.Parent);
            using RenderList2D queue = new RenderList2D(2);
            queue.Add(laterSibling);
            queue.Add(earlierSibling);
            Assert.Same(earlierSibling, queue[0]);
            root.RemoveChild(earlierSibling.Parent);
            root.AddChild(earlierSibling.Parent);
            Assert.Same(laterSibling, queue[0]);
        }

        /// <summary>Components sharing a transform use their authored component order instead of queue insertion order.</summary>
        [Fact]
        public void Queue_SameEntity_UsesComponentOrder() {
            Entity entity = CreateEntity(0);
            SpriteComponent background = new SpriteComponent();
            SpriteComponent foreground = new SpriteComponent();
            entity.AddComponent(background);
            entity.AddComponent(foreground);
            using RenderList2D queue = new RenderList2D(2);
            queue.Add(foreground);
            queue.Add(background);
            Assert.Same(background, queue[0]);
            Assert.Same(foreground, queue[1]);
        }

        /// <summary>The visible front entity receives input, including after only its parent's depth changes.</summary>
        [Fact]
        public void Pointer_MovingParentDepth_FollowsTheVisibleFront() {
            Entity cameraEntity = CreateEntity(0);
            CameraComponent camera = new CameraComponent { Viewport = new float4(0, 0, 100, 100) };
            cameraEntity.AddComponent(camera);
            SpriteComponent front = CreateSprite(5);
            SpriteComponent back = CreateSprite(0);
            InteractableComponent firstHit = new InteractableComponent { Size = new int2(40, 40) };
            InteractableComponent secondHit = new InteractableComponent { Size = new int2(40, 40) };
            front.Parent.AddComponent(firstHit);
            back.Parent.AddComponent(secondHit);
            Assert.Same(firstHit, ResolveHit(camera));
            Entity group = CreateEntity(10);
            group.AddChild(back.Parent);
            Assert.Same(secondHit, ResolveHit(camera));
        }

        /// <summary>Transparent frame and immediate queues agree when the camera looks from opposite sides.</summary>
        /// <param name="reverse">Whether the camera looks along positive rather than negative Z.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TransparentPreview_UsesCameraDepthInBothRenderPaths(bool reverse) {
            Entity cameraEntity = CreateEntity(10);
            cameraEntity.LocalOrientation = reverse ? new float4(0, 1, 0, 0) : float4.Identity;
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);
            MeshComponent near = CreateTransparentMesh(5);
            MeshComponent far = CreateTransparentMesh(-5);
            RenderFrameDrawableSubmission[] submissions = new[] {
                new RenderFrameDrawableSubmission(near, true, new RenderFrameBatchingMetadata(false, false, false)),
                new RenderFrameDrawableSubmission(far, true, new RenderFrameBatchingMetadata(false, false, false))
            };
            using RenderFrame frame = new RenderFrame(camera, submissions, Array.Empty<RenderFrameLightSubmission>(), Array.Empty<RenderFrameShadowCasterSubmission>());
            Assert.Same(reverse ? near : far, frame.DrawableSubmissions[0].Drawable);
            Assert.Same(reverse ? far : near, frame.DrawableSubmissions[1].Drawable);
            using RenderList3D queue = new RenderList3D(2);
            queue.Add(near);
            queue.Add(far);
            RecordingDrawableVisitor3D visitor = new RecordingDrawableVisitor3D();
            queue.VisitCameraOrdered(visitor, camera);
            Assert.Equal(2, visitor.Drawables.Count);
            Assert.Same(frame.DrawableSubmissions[0].Drawable, visitor.Drawables[0]);
            Assert.Same(frame.DrawableSubmissions[1].Drawable, visitor.Drawables[1]);
        }

        /// <summary>Disabling and re-enabling an equal-depth sibling does not bring it in front of later siblings.</summary>
        [Fact]
        public void Queue_Reenable_KeepsHierarchyOrder() {
            Entity cameraEntity = CreateEntity(0);
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);
            Entity root = CreateEntity(0);
            SpriteComponent first = CreateSprite(0);
            SpriteComponent second = CreateSprite(0);
            root.AddChild(first.Parent);
            root.AddChild(second.Parent);
            first.Parent.Enabled = false;
            first.Parent.Enabled = true;
            RenderList2D queue = Assert.IsType<RenderList2D>(camera.RenderQueue2D);
            Assert.Same(first, queue[0]);
            Assert.Same(second, queue[1]);
        }

        /// <summary>Creates an alpha-blended mesh carrying a physical position for composition tests.</summary>
        MeshComponent CreateTransparentMesh(float depth) {
            Entity entity = CreateEntity(depth);
            RuntimeMaterial material = new RuntimeMaterial();
            material.SetRenderState(new MaterialRenderState { BlendMode = MaterialBlendMode.AlphaBlend });
            MeshComponent mesh = new MeshComponent { Materials = new[] { material } };
            entity.AddComponent(mesh);
            return mesh;
        }

        /// <summary>Resolves a pointer shared by all overlapping fixture sprites.</summary>
        IInteractable2D ResolveHit(CameraComponent camera) {
            return PointerInteractableHitResolver.ResolveTopInteractableAt(CoreValue.ObjectManager.Interactables,
                CoreValue.ObjectManager.Drawables2D, camera, 10, 10);
        }

        /// <summary>Creates one registered drawable at the requested local depth.</summary>
        SpriteComponent CreateSprite(float depth) {
            Entity entity = CreateEntity(depth);
            SpriteComponent sprite = new SpriteComponent { Size = new int2(40, 40) };
            entity.AddComponent(sprite);
            return sprite;
        }

        /// <summary>Creates an initialized entity with no hidden render priority.</summary>
        Entity CreateEntity(float depth) {
            Entity entity = new Entity(CoreValue) { LocalPosition = new float3(0, 0, depth) };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}

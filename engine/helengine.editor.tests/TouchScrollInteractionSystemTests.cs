using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>Exercises opt-in scrolling through the production contact tracker and pointer router.</summary>
    public sealed class TouchScrollInteractionSystemTests : IDisposable {
        /// <summary>Publishes physical contact samples without a native window.</summary>
        readonly TestTouchInputBackend Backend = new TestTouchInputBackend();
        /// <summary>Owns registered controls, cameras, and the production pointer router.</summary>
        readonly Core TestCore;
        /// <summary>Scopes touch routing to a fixed physical viewport.</summary>
        readonly CameraComponent Camera;

        /// <summary>Initializes a real router with headless graphics services.</summary>
        public TouchScrollInteractionSystemTests() {
            TestRenderManager3D renderer = new TestRenderManager3D();
            TestCore = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            TestCore.Initialize(renderer, new TestRenderManager2D(), Backend, new PlatformInfo("test", "test"));
            renderer.OnWindowResize(renderer.MainWindowHandle, 320, 240);
            Camera = new CameraComponent { Viewport = new float4(0, 0, 320, 240), LayerMask = EditorLayerMasks.EditorUi };
            CreateEntity(0, 0).AddComponent(Camera);
        }

        /// <summary>Dragging a pressed descendant cancels its activation and preserves the original drag origin outside the viewport.</summary>
        [Fact]
        public void DragTransfersFromButtonAndContinuesOutside() {
            ScrollComponent scroll = CreateScroll();
            int clicks = 0;
            EditorEntity buttonHost = CreateEntity(10, 20);
            scroll.Parent.AddChild(buttonHost);
            buttonHost.LocalPosition = new float3(10, 20, 1);
            buttonHost.AddComponent(new ButtonComponent("", new int2(80, 80), null, () => clicks++) { SystemTextStyle = new SystemTextStyle(SystemTextRole.Body) });
            InteractableComponent buttonInput = Assert.Single(buttonHost.Components.OfType<InteractableComponent>());
            List<InputTouchPhase> phases = new List<InputTouchPhase>();
            buttonInput.TouchEvent += sample => phases.Add(sample.Phase);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 50, 1f); Advance();
            Assert.Equal(3, scroll.ScrollOffset);
            Assert.Equal(new[] { InputTouchPhase.Began, InputTouchPhase.Canceled }, phases);
            Backend.Touches.MoveTouch(1, 30, -20, 1f); Advance();
            Assert.Equal(10, scroll.ScrollOffset);
            Backend.Touches.EndTouch(1, 30, -20, 0f); Advance();
            Assert.Equal(0, clicks);
        }

        /// <summary>A tap and exact-threshold movement remain button clicks; perpendicular movement cannot start a vertical scroll.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, -8)]
        [InlineData(15, 0)]
        public void TapAndSubthresholdMovementPreserveButtonClick(int deltaX, int deltaY) {
            ScrollComponent scroll = CreateScroll();
            int clicks = 0;
            CreateButton(scroll, () => clicks++);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30 + deltaX, 80 + deltaY, 1f); Advance();
            Backend.Touches.EndTouch(1, 30 + deltaX, 80 + deltaY, 0f); Advance();
            Assert.Equal(1, clicks);
            Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>A final position arriving without a separate move still claims the gesture and suppresses a descendant click.</summary>
        [Fact]
        public void EndedSampleCanTransferWithoutIntermediateMove() {
            ScrollComponent scroll = CreateScroll(); int clicks = 0;
            CreateButton(scroll, () => clicks++);
            Backend.Touches.BeginTouch(1, 30, 80, 1f);
            Backend.Touches.EndTouch(1, 30, 40, 0f); Advance();
            Assert.Equal(4, scroll.ScrollOffset); Assert.Equal(0, clicks);
        }

        /// <summary>Opt-in registration adds only a touch background and opt-out cancels capture without changing wheel configuration.</summary>
        [Fact]
        public void BackgroundIsOptInAndDoesNotInterceptMouse() {
            ScrollComponent scroll = CreateScroll(); scroll.TouchScrollingEnabled = false;
            Assert.DoesNotContain(scroll, TestCore.ObjectManager.Interactables);
            Backend.Touches.BeginTouch(1, 120, 100, 1f); Advance();
            scroll.TouchScrollingEnabled = true;
            Backend.Touches.MoveTouch(1, 120, 50, 1f); Advance();
            Assert.Equal(0, scroll.ScrollOffset);
            Assert.Null(PointerInteractableHitResolver.ResolveTopInteractableAt(TestCore.ObjectManager.Interactables, TestCore.ObjectManager.Drawables2D, Camera, 120, 100));
            Assert.Same(scroll, PointerInteractableHitResolver.ResolveTopInteractableAt(TestCore.ObjectManager.Interactables, TestCore.ObjectManager.Drawables2D, Camera, 120, 100, true));
            Backend.Touches.EndTouch(1, 120, 50, 0f); Advance();
            Backend.Touches.BeginTouch(2, 120, 100, 1f); Advance();
            Backend.Touches.MoveTouch(2, 120, 70, 1f); Advance();
            Assert.Equal(3, scroll.ScrollOffset);
            scroll.TouchScrollingEnabled = false;
            Backend.Touches.MoveTouch(2, 120, 40, 1f); Advance();
            Assert.Equal(3, scroll.ScrollOffset);
            Assert.True(scroll.RequiresPointerInside);
        }

        /// <summary>Invalid physical-pixel thresholds fail explicitly, and zero permits any nonzero axial movement.</summary>
        [Fact]
        public void ThresholdRejectsInvalidValuesAndAllowsZero() {
            ScrollComponent scroll = CreateScroll();
            Assert.Throws<ArgumentOutOfRangeException>(() => scroll.TouchDragThreshold = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => scroll.TouchDragThreshold = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => scroll.TouchDragThreshold = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => scroll.TouchDragThreshold = float.NegativeInfinity);
            scroll.TouchDragThreshold = 0f; int clicks = 0;
            CreateButton(scroll, () => clicks++);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.EndTouch(1, 30, 79, 0f); Advance();
            Assert.Equal(0, clicks); Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>Cancellation releases the owning finger, while an already-held second contact never adopts its gesture.</summary>
        [Fact]
        public void OnlyOneFingerDrivesScrollAndCancellationReleasesIt() {
            ScrollComponent scroll = CreateScroll();
            Backend.Touches.BeginTouch(1, 120, 100, 1f);
            Backend.Touches.BeginTouch(2, 150, 100, 1f); Advance();
            Backend.Touches.MoveTouch(2, 150, 20, 1f); Advance(); Assert.Equal(0, scroll.ScrollOffset);
            Backend.Touches.MoveTouch(1, 120, 60, 1f); Advance(); Assert.Equal(4, scroll.ScrollOffset);
            Backend.Touches.CancelAll(); Advance();
            Backend.Touches.BeginTouch(3, 120, 100, 1f); Advance();
            Backend.Touches.MoveTouch(3, 120, 70, 1f); Advance(); Assert.Equal(7, scroll.ScrollOffset);
        }

        /// <summary>A second finger dragging a child cancels that child's click without stealing the first finger's scroll.</summary>
        [Fact]
        public void BusyScrollCancelsSecondDescendantDragWithoutStealingDriver() {
            ScrollComponent scroll = CreateScroll(); int clicks = 0; CreateButton(scroll, () => clicks++);
            Backend.Touches.BeginTouch(1, 120, 100, 1f); Advance();
            Backend.Touches.MoveTouch(1, 120, 70, 1f); Advance(); Assert.Equal(3, scroll.ScrollOffset);
            Backend.Touches.BeginTouch(2, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(2, 30, 40, 1f); Advance(); Assert.Equal(3, scroll.ScrollOffset);
            Backend.Touches.EndTouch(2, 30, 40, 0f); Advance(); Assert.Equal(0, clicks);
            Backend.Touches.MoveTouch(1, 120, 50, 1f); Advance(); Assert.Equal(5, scroll.ScrollOffset);
        }

        /// <summary>Touch displacement uses the configured horizontal axis and symmetric item quantization around a nonzero initial offset.</summary>
        [Theory]
        [InlineData(ScrollOrientation.Vertical)]
        [InlineData(ScrollOrientation.Horizontal)]
        public void DragKeepsInitialOffsetAndUsesConfiguredAxis(ScrollOrientation axis) {
            ScrollComponent scroll = CreateScroll(); scroll.Orientation = axis; scroll.ScrollTo(10);
            Backend.Touches.BeginTouch(1, 120, 100, 1f); Advance();
            Backend.Touches.MoveTouch(1, axis == ScrollOrientation.Horizontal ? 90 : 120, axis == ScrollOrientation.Vertical ? 70 : 100, 1f); Advance();
            Assert.Equal(13, scroll.ScrollOffset);
            Backend.Touches.MoveTouch(1, axis == ScrollOrientation.Horizontal ? 129 : 120, axis == ScrollOrientation.Vertical ? 109 : 100, 1f); Advance();
            Assert.Equal(10, scroll.ScrollOffset);
            Backend.Touches.MoveTouch(1, axis == ScrollOrientation.Horizontal ? 150 : 120, axis == ScrollOrientation.Vertical ? 130 : 100, 1f); Advance();
            Assert.Equal(7, scroll.ScrollOffset);
        }

        /// <summary>Range bounds and empty ranges still cancel the pressed button after a drag.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(100, 84)]
        public void BoundedAndEmptyScrollRangesSuppressAccidentalClicks(int count, int initialOffset) {
            ScrollComponent scroll = CreateScroll(); scroll.ItemCount = count; scroll.ScrollTo(initialOffset);
            int clicks = 0; CreateButton(scroll, () => clicks++);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 40, 1f); Advance();
            Backend.Touches.EndTouch(1, 30, 40, 0f); Advance();
            Assert.Equal(initialOffset, scroll.ScrollOffset); Assert.Equal(0, clicks);
        }

        /// <summary>The nearest matching axis wins, and an orthogonal inner scroll permits transfer to the outer viewport.</summary>
        [Theory]
        [InlineData(ScrollOrientation.Vertical, 3, 0)]
        [InlineData(ScrollOrientation.Horizontal, 0, 3)]
        public void NestedScrollChoosesNearestApplicableAxis(ScrollOrientation innerAxis, int innerOffset, int outerOffset) {
            ScrollComponent outer = CreateScroll(); ScrollComponent inner = CreateScroll();
            outer.Parent.AddChild(inner.Parent); inner.Parent.LocalPosition = float3.Zero; inner.Orientation = innerAxis;
            CreateButton(inner, () => { });
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 50, 1f); Advance();
            Assert.Equal(innerOffset, inner.ScrollOffset); Assert.Equal(outerOffset, outer.ScrollOffset);
        }

        /// <summary>A scroll attached to the translated content measures touches against the fixed ancestor clip rather than moving its own origin.</summary>
        [Fact]
        public void MovingContentHostKeepsFixedTouchOrigin() {
            EditorEntity clipHost = CreateEntity(20, 20); clipHost.AddComponent(new ClipRectComponent { Size = new int2(200, 160) });
            ScrollComponent scroll = CreateScroll(); clipHost.AddChild(scroll.Parent); scroll.Parent.LocalPosition = float3.Zero;
            scroll.ContentRoot = scroll.Parent;
            Backend.Touches.BeginTouch(1, 140, 120, 1f); Advance();
            Backend.Touches.MoveTouch(1, 140, 90, 1f); Advance(); Assert.Equal(3, scroll.ScrollOffset);
            Advance(); Assert.Equal(3, scroll.ScrollOffset);
            Backend.Touches.MoveTouch(1, 140, 60, 1f); Advance(); Assert.Equal(6, scroll.ScrollOffset);
            Assert.Equal(new float4(20, 20, 200, 160), scroll.GetClipRect());
        }

        /// <summary>Removing, disabling, or unregistering an ancestor prevents late promotion and leaves no active child click.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void CancellationCallbackCanInvalidateTransferTarget(int action) {
            ScrollComponent scroll = CreateScroll(); int clicks = 0;
            InteractableComponent input = CreateButton(scroll, () => clicks++);
            input.TouchEvent += sample => {
                if (sample.Phase != InputTouchPhase.Canceled) { return; }
                if (action == 0) { scroll.Parent.RemoveComponent(scroll); }
                else if (action == 1) { scroll.Parent.Enabled = false; }
                else { TestCore.ObjectManager.RemoveInteractable(scroll); }
            };
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 40, 1f); Advance();
            Backend.Touches.EndTouch(1, 30, 40, 0f); Advance();
            Assert.Equal(0, clicks); Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>Camera deletion from the old target's cancellation callback cannot dispatch through the stale camera.</summary>
        [Fact]
        public void CancellationCallbackCanRemoveCamera() {
            ScrollComponent scroll = CreateScroll(); int clicks = 0;
            InteractableComponent input = CreateButton(scroll, () => clicks++);
            input.TouchEvent += sample => { if (sample.Phase == InputTouchPhase.Canceled) { Camera.Parent.Dispose(); } };
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 40, 1f); Advance();
            Backend.Touches.EndTouch(1, 30, 40, 0f); Advance();
            Assert.Equal(0, clicks); Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>Direct invalidation of a held scroll releases it immediately and later samples cannot change its offset.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ActiveScrollSafelyStopsOnInvalidation(int action) {
            ScrollComponent scroll = CreateScroll();
            Backend.Touches.BeginTouch(1, 120, 100, 1f); Advance();
            Backend.Touches.MoveTouch(1, 120, 70, 1f); Advance(); Assert.Equal(3, scroll.ScrollOffset);
            if (action == 0) { scroll.Parent.RemoveComponent(scroll); }
            else if (action == 1) { scroll.Parent.Enabled = false; }
            else { TestCore.ObjectManager.RemoveInteractable(scroll); }
            Backend.Touches.MoveTouch(1, 120, 40, 1f); Advance(); Assert.Equal(3, scroll.ScrollOffset);
        }

        /// <summary>Canceling another contact from a promotion callback does not corrupt capture indices or skip the new scroll capture.</summary>
        [Fact]
        public void ReentrantCancellationOfAnotherContactKeepsTransferStable() {
            ScrollComponent scroll = CreateScroll(); int clicks = 0;
            InteractableComponent first = CreateButton(scroll, () => clicks++);
            EditorEntity otherHost = CreateEntity(230, 20);
            InteractableComponent other = new InteractableComponent { Size = new int2(60, 80) }; otherHost.AddComponent(other);
            List<InputTouchPhase> phases = new List<InputTouchPhase>(); other.TouchEvent += touch => phases.Add(touch.Phase);
            first.TouchEvent += touch => { if (touch.Phase == InputTouchPhase.Canceled) { otherHost.Dispose(); } };
            Backend.Touches.BeginTouch(2, 250, 60, 1f);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 50, 1f); Advance();
            Assert.Equal(3, scroll.ScrollOffset); Assert.Equal(0, clicks);
            Assert.Equal(new[] { InputTouchPhase.Began, InputTouchPhase.Canceled }, phases);
        }

        /// <summary>Structural ancestors need no component collection for a descendant press to reach the scroll viewport.</summary>
        [Fact]
        public void ComponentlessAncestorDoesNotInterruptScrollTransfer() {
            ScrollComponent scroll = CreateScroll(); InteractableComponent input = CreateButton(scroll, () => { });
            Entity host = input.Parent; scroll.Parent.RemoveChild(host);
            Entity bridge = new Entity(TestCore) { LayerMask = EditorLayerMasks.EditorUi }; bridge.InitChildren();
            scroll.Parent.AddChild(bridge); bridge.AddChild(host); host.LocalPosition = new float3(10, 20, 1);
            Backend.Touches.BeginTouch(1, 30, 80, 1f); Advance();
            Backend.Touches.MoveTouch(1, 30, 50, 1f); Advance();
            Assert.Equal(3, scroll.ScrollOffset);
        }

        /// <summary>Releases the router before its borrowed backend storage.</summary>
        public void Dispose() { TestCore.Dispose(); Backend.Dispose(); }

        /// <summary>Captures one input frame and invokes actual pointer routing.</summary>
        void Advance() { TestCore.Input.EarlyUpdate(); TestCore.Input.Update(); TestCore.PointerInteractionSystem.Update(); }

        /// <summary>Creates a scene-owned entity on the test camera layer.</summary>
        EditorEntity CreateEntity(int x, int y) {
            return new EditorEntity(TestCore, new helengine.editor.EditorSessionInteractionServices()) {
                InternalEntity = true, LayerMask = EditorLayerMasks.EditorUi, Position = new float3(x, y, 0)
            };
        }

        /// <summary>Builds a viewport with ten-pixel items and explicitly enabled physical-pixel touch scrolling.</summary>
        ScrollComponent CreateScroll() {
            ScrollComponent scroll = new ScrollComponent { ShowScrollBar = false, Size = new int2(200, 160), ItemCount = 100, ItemExtent = 10, TouchScrollingEnabled = true, TouchDragThreshold = 8f };
            CreateEntity(0, 0).AddComponent(scroll);
            return scroll;
        }

        /// <summary>Creates an actual button whose existing interactable exposes cancellation and activation behavior.</summary>
        InteractableComponent CreateButton(ScrollComponent scroll, Action click) {
            EditorEntity host = CreateEntity(10, 20); scroll.Parent.AddChild(host); host.LocalPosition = new float3(10, 20, 1);
            host.AddComponent(new ButtonComponent("", new int2(80, 80), null, click) { SystemTextStyle = new SystemTextStyle(SystemTextRole.Body) });
            return Assert.Single(host.Components.OfType<InteractableComponent>());
        }
    }
}

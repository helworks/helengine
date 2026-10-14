using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Verifies independent control capture, viewport routing and safe cancellation of multitouch interactions.</summary>
    public sealed class TouchInteractionSystemTests : IDisposable {
        /// <summary>Feeds native-style events through the shared tracker.</summary>
        readonly TestTouchInputBackend Backend;
        /// <summary>Owns the interaction router and registered test controls.</summary>
        readonly Core TestCore;
        /// <summary>Defines the window bounds and allows testing input view switches.</summary>
        readonly TestRenderManager3D Renderer;
        /// <summary>Scopes all controls to one UI camera.</summary>
        readonly CameraComponent Camera;

        /// <summary>Creates a headless core with a real pointer router and a 320 by 240 input viewport.</summary>
        public TouchInteractionSystemTests() {
            Backend = new TestTouchInputBackend();
            Renderer = new TestRenderManager3D();
            TestCore = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            TestCore.Initialize(Renderer, new TestRenderManager2D(), Backend, new PlatformInfo("test", "test-version"));
            Renderer.OnWindowResize(Renderer.MainWindowHandle, 320, 240);
            EditorEntity cameraEntity = CreateEntity(new float3(0, 0, 0));
            Camera = new CameraComponent { Viewport = new float4(0, 0, 320, 240), LayerMask = EditorLayerMasks.EditorUi };
            cameraEntity.AddComponent(Camera);
        }

        /// <summary>Two controls retain different fingers while movement outside their hit regions remains captured.</summary>
        [Fact]
        public void Update_CapturesTwoControlsIndependently() {
            InteractableComponent left = CreateControl(10, 20, 60, 60);
            InteractableComponent right = CreateControl(200, 20, 60, 60);
            List<InputTouchState> leftSamples = new List<InputTouchState>();
            List<InputTouchState> rightSamples = new List<InputTouchState>();
            left.TouchEvent += leftSamples.Add;
            right.TouchEvent += rightSamples.Add;
            Backend.Touches.BeginTouch(9, 20, 30, 1f);
            Backend.Touches.BeginTouch(2, 210, 30, 1f);
            Advance();
            Backend.Touches.MoveTouch(9, 180, 200, 0.7f);
            Backend.Touches.EndTouch(2, 210, 30, 0f);
            Advance();
            Assert.Equal(9, leftSamples[0].Id);
            Assert.Equal(10, leftSamples[0].X);
            Assert.Equal(InputTouchPhase.Moved, leftSamples[1].Phase);
            Assert.Equal(170, leftSamples[1].X);
            Assert.Equal(InputTouchPhase.Ended, rightSamples[1].Phase);
            Advance();
            Assert.Equal(InputTouchPhase.Stationary, leftSamples[2].Phase);
            Assert.Equal(2, rightSamples.Count);
            Backend.Touches.EndTouch(9, 180, 200, 0f);
            Advance();
            Assert.Equal(InputTouchPhase.Ended, leftSamples[3].Phase);
        }

        /// <summary>A complete tap in one native event drain reaches legacy buttons once, without projected pointer duplication.</summary>
        [Fact]
        public void Update_FastTapInvokesLegacyCursorSequenceOnce() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<PointerInteraction> interactions = new List<PointerInteraction>();
            control.CursorEvent += (position, delta, interaction) => interactions.Add(interaction);
#if DESKTOP_PLATFORM
            Backend.Mouse = new MouseState(20, 30, 0, ButtonState.Pressed, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released);
#endif
            Backend.Touches.BeginTouch(4, 20, 30, 1f);
            Backend.Touches.EndTouch(4, 20, 30, 0f);
            Advance();
            Advance();
            Assert.Equal(new[] { PointerInteraction.Hover, PointerInteraction.Press, PointerInteraction.Release, PointerInteraction.Leave }, interactions);
        }

        /// <summary>A second finger on the same control cannot move or release the first finger's cursor-based press.</summary>
        [Fact]
        public void Update_SecondFingerCannotReleaseAnotherFingersButton() {
            InteractableComponent control = CreateControl(10, 20, 100, 100);
            List<PointerInteraction> interactions = new List<PointerInteraction>();
            List<InputTouchState> samples = new List<InputTouchState>();
            control.CursorEvent += (position, delta, interaction) => interactions.Add(interaction);
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(7, 20, 30, 1f);
            Backend.Touches.BeginTouch(8, 40, 50, 1f);
            Advance();
            Backend.Touches.EndTouch(8, 40, 50, 0f);
            Advance();
            Assert.DoesNotContain(PointerInteraction.Release, interactions);
            Assert.Contains(samples, sample => sample.Id == 8 && sample.Phase == InputTouchPhase.Ended);
            Backend.Touches.EndTouch(7, 20, 30, 0f);
            Advance();
            Assert.Single(interactions, interaction => interaction == PointerInteraction.Release);
        }

        /// <summary>Focus cancellation clears all buttons without invoking their normal release actions.</summary>
        [Fact]
        public void Update_CancellationDoesNotActivateControls() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<PointerInteraction> interactions = new List<PointerInteraction>();
            List<InputTouchState> samples = new List<InputTouchState>();
            control.CursorEvent += (position, delta, interaction) => interactions.Add(interaction);
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(4, 20, 30, 1f);
            Advance();
            Backend.Touches.CancelAll();
            Advance();
            Assert.Equal(InputTouchPhase.Canceled, samples[1].Phase);
            Assert.DoesNotContain(PointerInteraction.Release, interactions);
            Assert.Equal(PointerInteraction.Leave, interactions[2]);
        }

        /// <summary>Removing a captured control cancels only its finger; another control continues receiving movement.</summary>
        [Fact]
        public void Update_RemovingOneControlPreservesOtherCaptures() {
            InteractableComponent left = CreateControl(10, 20, 60, 60);
            InteractableComponent right = CreateControl(200, 20, 60, 60);
            List<InputTouchState> leftSamples = new List<InputTouchState>();
            List<InputTouchState> rightSamples = new List<InputTouchState>();
            left.TouchEvent += leftSamples.Add;
            right.TouchEvent += rightSamples.Add;
            Backend.Touches.BeginTouch(1, 20, 30, 1f);
            Backend.Touches.BeginTouch(2, 210, 30, 1f);
            Advance();
            left.Parent.Dispose();
            Assert.Equal(InputTouchPhase.Canceled, leftSamples[1].Phase);
            Backend.Touches.MoveTouch(2, 220, 40, 1f);
            Advance();
            Assert.Equal(2, leftSamples.Count);
            Assert.Equal(InputTouchPhase.Moved, rightSamples[1].Phase);
            Assert.Equal(20, rightSamples[1].X);
        }

        /// <summary>The object manager cancels a capture even when a custom control unregisters directly.</summary>
        [Fact]
        public void Update_UnregisteringAControlCancelsBeforeItsLifetimeEnds() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, 20, 30, 1f);
            Advance();
            TestCore.ObjectManager.RemoveInteractable(control);
            Assert.Equal(InputTouchPhase.Canceled, samples[1].Phase);
            Backend.Touches.EndTouch(0, 20, 30, 0f);
            Advance();
            Assert.Equal(2, samples.Count);
        }

        /// <summary>Camera removal abandons captures while the borrowed camera is still alive.</summary>
        [Fact]
        public void Update_RemovingCameraCancelsCapturedContacts() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, 20, 30, 1f);
            Advance();
            Camera.Parent.Dispose();
            Assert.Equal(InputTouchPhase.Canceled, samples[1].Phase);
            Backend.Touches.EndTouch(0, 20, 30, 0f);
            Advance();
            Assert.Equal(2, samples.Count);
        }

        /// <summary>A finger that started outside a control cannot acquire that control merely by sliding into it.</summary>
        [Fact]
        public void Update_DoesNotCaptureContactsThatSlideIntoAControl() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, 200, 200, 1f);
            Advance();
            Backend.Touches.MoveTouch(0, 20, 30, 1f);
            Advance();
            Backend.Touches.EndTouch(0, 20, 30, 0f);
            Advance();
            Assert.Empty(samples);
        }

        /// <summary>Touch IDs reused in one frame acquire the new target only after the preceding contact releases its target.</summary>
        [Fact]
        public void Update_ReusedIdStartsAnIndependentCapture() {
            InteractableComponent left = CreateControl(10, 20, 60, 60);
            InteractableComponent right = CreateControl(200, 20, 60, 60);
            List<InputTouchState> leftSamples = new List<InputTouchState>();
            List<InputTouchState> rightSamples = new List<InputTouchState>();
            left.TouchEvent += leftSamples.Add;
            right.TouchEvent += rightSamples.Add;
            Backend.Touches.BeginTouch(3, 20, 30, 1f);
            Backend.Touches.EndTouch(3, 20, 30, 0f);
            Backend.Touches.BeginTouch(3, 210, 30, 1f);
            Advance();
            Assert.Equal(2, leftSamples.Count);
            Assert.Equal(InputTouchPhase.Ended, leftSamples[1].Phase);
            Assert.Single(rightSamples);
            Assert.Equal(InputTouchPhase.Began, rightSamples[0].Phase);
        }

        /// <summary>An offset camera-bound viewport uses the same local coordinates for touch hit testing and event delivery.</summary>
        [Fact]
        public void Update_ConvertsCameraBoundViewportCoordinates() {
            Camera.Viewport = new float4(100, 50, 180, 160);
            EditorEntity viewportRoot = CreateEntity(float3.Zero);
            Camera.Parent.AddChild(viewportRoot);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode,
                FixedSize = new int2(180, 160)
            };
            viewportRoot.AddComponent(viewport);
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            viewportRoot.AddChild(control.Parent);
            control.Parent.LocalPosition = new float3(10, 20, 0);
            viewport.Update();
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, 125, 85, 1f);
            Advance();
            Assert.Single(samples);
            Assert.Equal(15, samples[0].X);
            Assert.Equal(15, samples[0].Y);
        }

        /// <summary>Touch hit regions follow the engine viewport's DPI and user scale instead of applying a second Android scale.</summary>
        [Theory]
        [InlineData(160, 0.75f)]
        [InlineData(320, 1.5f)]
        public void Update_UsesDensityScaledControlBounds(int dpi, float expectedScale) {
            Renderer.SetWindowDensityDpi(Renderer.MainWindowHandle, dpi);
            EditorEntity root = CreateEntity(float3.Zero);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                ScalingMode = ViewportComponent.DensityScalingMode,
                PixelsPerCentimeter = (float)(160d / 2.54d), UiScale = 0.75f
            };
            root.AddComponent(viewport);
            InteractableComponent control = CreateControl(20, 30, 40, 40);
            root.AddChild(control.Parent);
            control.Parent.LocalPosition = new float3(20, 30, 1);
            viewport.ReapplyLayout();
            Assert.Equal((int)Math.Round(40 * expectedScale), control.Size.X);
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, (int)Math.Round(50 * expectedScale), (int)Math.Round(60 * expectedScale), 1f);
            Advance();
            Assert.Single(samples);
            Assert.InRange(Math.Abs(samples[0].X - 30 * expectedScale), 0f, 0.5f);
            Assert.InRange(Math.Abs(samples[0].Y - 30 * expectedScale), 0f, 0.5f);
        }

        /// <summary>Control callbacks may disable themselves during begin without leaving a phantom legacy press.</summary>
        [Fact]
        public void Update_ReentrantRemovalCancelsBeforeLegacyPress() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<InputTouchPhase> phases = new List<InputTouchPhase>();
            List<PointerInteraction> interactions = new List<PointerInteraction>();
            control.TouchEvent += touch => {
                phases.Add(touch.Phase);
                if (touch.Phase == InputTouchPhase.Began) { control.Parent.Dispose(); }
            };
            control.CursorEvent += (position, delta, interaction) => interactions.Add(interaction);
            Backend.Touches.BeginTouch(0, 20, 30, 1f);
            Advance();
            Assert.Equal(new[] { InputTouchPhase.Began, InputTouchPhase.Canceled }, phases);
            Assert.DoesNotContain(PointerInteraction.Press, interactions);
            Assert.DoesNotContain(PointerInteraction.Release, interactions);
        }

        /// <summary>A host input-view switch cancels contacts; stationary fingers cannot activate controls in the new view.</summary>
        [Fact]
        public void Update_WindowSwitchCancelsWithoutRecapturingHeldContacts() {
            InteractableComponent control = CreateControl(10, 20, 60, 60);
            List<InputTouchState> samples = new List<InputTouchState>();
            control.TouchEvent += samples.Add;
            Backend.Touches.BeginTouch(0, 20, 30, 1f);
            Advance();
            Renderer.AddWindow(new IntPtr(984), 320, 240);
            Renderer.SelectInputWindow(new IntPtr(984));
            Advance();
            Assert.Equal(2, samples.Count);
            Assert.Equal(InputTouchPhase.Canceled, samples[1].Phase);
            Backend.Touches.EndTouch(0, 20, 30, 0f);
            Advance();
            Assert.Equal(2, samples.Count);
        }

        /// <summary>Disposes the router before releasing its borrowed backend buffers.</summary>
        public void Dispose() {
            TestCore.Dispose();
            Backend.Dispose();
        }

        /// <summary>Captures input once and runs the production touch and pointer routing phase.</summary>
        void Advance() {
            TestCore.Input.EarlyUpdate();
            TestCore.Input.Update();
            TestCore.PointerInteractionSystem.Update();
        }

        /// <summary>Creates a registered entity on the same UI layer as the test camera.</summary>
        EditorEntity CreateEntity(float3 position) {
            return new EditorEntity(TestCore, new helengine.editor.EditorSessionInteractionServices()) {
                InternalEntity = true, LayerMask = EditorLayerMasks.EditorUi, Position = position
            };
        }

        /// <summary>Creates a visible hit region using the production entity registration and depth ordering.</summary>
        InteractableComponent CreateControl(int x, int y, int width, int height) {
            EditorEntity entity = CreateEntity(new float3(x, y, 1));
            entity.AddComponent(new SpriteComponent { Texture = TestCore.RenderManager2D.PixelTexture, Size = new int2(width, height) });
            InteractableComponent control = new InteractableComponent { Size = new int2(width, height) };
            entity.AddComponent(control);
            return control;
        }
    }
}

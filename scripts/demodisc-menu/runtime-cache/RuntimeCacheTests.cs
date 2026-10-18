namespace DemoDisc.MenuContractTests {
    /// <summary>Checks cache lifetimes through reference fixups, resizing and scroll hierarchy changes.</summary>
    public sealed class RuntimeCacheTests {
        /// <summary>Pointer selection after scrolling uses prepared row spacing and prepared hit rectangles.</summary>
        [Fact]
        public void ScrolledPointer_UsesResolvedRowStep() {
            using MenuContractFixture fixture = new MenuContractFixture("empty", false);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(200, 120),
                ReferenceWidth = 400, ReferenceHeight = 240, ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                LayoutProcessingMode = ViewportLayoutProcessingMode.OnLoad
            };
            fixture.Root.AddComponent(viewport);
            viewport.Update();
            fixture.Menu.ScenePrepared(fixture.Root);
            fixture.Invoke("ActivatePanel", "scene-select", false);
            MenuPanelRuntime panel = fixture.Panels["scene-select"];
            panel.ItemsScrollComponent.ScrollTo(1);
            Assert.Equal(-15f, panel.ItemsRootEntity.LocalPosition.Y);
            FrameInputBackend backend = new FrameInputBackend();
            InputSystem input = fixture.Core.Input;
            input.SetBackend(backend);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 5, 17, ButtonState.Pressed);
            fixture.Invoke("HandlePointerInput", input);
            Assert.Equal(2, panel.SelectedItemIndex);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 5, 17, ButtonState.Released);
            fixture.Invoke("HandlePointerInput", input);
            Assert.Equal(panel.Items[2].Definition.TargetId, fixture.Core.SceneManager.TransitionTargetSceneId);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>The full authored button tree survives every processing policy with an empty scene catalog.</summary>
        [Theory]
        [InlineData(ViewportLayoutProcessingMode.Dynamic)]
        [InlineData(ViewportLayoutProcessingMode.OnLoad)]
        [InlineData(ViewportLayoutProcessingMode.Cook)]
        public void MenuModes_KeepAll44Buttons(ViewportLayoutProcessingMode mode) {
            using MenuContractFixture fixture = new MenuContractFixture("empty", false);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(1280, 720),
                ReferenceWidth = 1280, ReferenceHeight = 720, ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                LayoutProcessingMode = mode
            };
            if (mode == ViewportLayoutProcessingMode.Cook) {
                viewport.CookedLayout = new ViewportCookedLayout { AlgorithmVersion = 1, TargetWidth = 1280, TargetHeight = 720,
                    PixelAspect = 1, ScaleX = 1, ScaleY = 1, GlyphScale = 1, AnchorSize = new int2(1280, 720) };
            }
            fixture.Root.AddComponent(viewport);
            viewport.Update();
            fixture.Menu.ScenePrepared(fixture.Root);
            int count = 0;
            foreach (MenuPanelRuntime panel in fixture.Panels.Values) { count += panel.Items.Length; }
            Assert.Equal(44, count);
        }
        /// <summary>Cooked density uses prepared factors once while custom marquee dimensions remain authored.</summary>
        [Fact]
        public void CookedFooter_DoesNotDoubleScale() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            Entity canvas = fixture.CreateNode();
            fixture.Root.AddChild(canvas);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(200, 100),
                ScalingMode = ViewportComponent.DensityScalingMode, ReferenceWidth = 200, ReferenceHeight = 100,
                LayoutProcessingMode = ViewportLayoutProcessingMode.Cook,
                CookedLayout = new ViewportCookedLayout { AlgorithmVersion = 1, TargetWidth = 200, TargetHeight = 100,
                    PixelAspect = 1, HasDpi = true, Dpi = 320, ScaleX = 2, ScaleY = 2, GlyphScale = 2, AnchorSize = new int2(200, 100) }
            };
            canvas.AddComponent(viewport);
            Entity text = fixture.CreateNode();
            text.LocalPosition = new float3(400, 0, 0);
            text.AddComponent(new TextComponent { Size = new int2(400, 40) });
            canvas.AddChild(text);
            SceneEntityReference reference = new SceneEntityReference { EntityId = 42u };
            typeof(SceneEntityReference).GetProperty(nameof(SceneEntityReference.ResolvedEntity)).SetValue(reference, text);
            FooterIdentityMarqueeComponent footer = new FooterIdentityMarqueeComponent {
                TextEntityReference = reference, StripWidth = 200f, PixelsPerSecond = 10f
            };
            canvas.AddComponent(footer);
            viewport.Update();
            footer.ScenePrepared(canvas);
            Assert.Equal(400, text.LocalPosition.X);
            Assert.Equal(new int2(400, 40), ((TextComponent)text.Components[0]).Size);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }
        /// <summary>The footer consumes the loader's fixup without global runtime-id discovery, and invalidates its restart coordinate on resize.</summary>
        [Fact]
        public void PreparedFooterBindsResolvedReferenceAndFollowsResize() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            Entity canvas = fixture.CreateNode();
            fixture.Root.AddChild(canvas);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                ReferenceWidth = 200,
                ReferenceHeight = 100,
                FixedSize = new int2(200, 100)
            };
            canvas.AddComponent(viewport);
            Entity text = fixture.CreateNode();
            canvas.AddChild(text);
            text.AddComponent(new TextComponent());
            SceneEntityReference reference = new SceneEntityReference { EntityId = 42u };
            typeof(SceneEntityReference).GetProperty(nameof(SceneEntityReference.ResolvedEntity)).SetValue(reference, text);
            FooterIdentityMarqueeComponent footer = new FooterIdentityMarqueeComponent {
                TextEntityReference = reference, StripWidth = 200f, PixelsPerSecond = 10f
            };
            canvas.AddComponent(footer);
            viewport.Update();
            footer.ScenePrepared(canvas);
            Assert.Equal(200f, text.LocalPosition.X);
            Assert.StartsWith("MADE BY HELENA", ((TextComponent)text.Components[0]).Text);
            viewport.FixedSize = new int2(400, 200);
            viewport.Update();
            typeof(FooterIdentityMarqueeComponent).GetMethod("PositionTextAtStripStart", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(footer, null);
            Assert.Equal(400f, text.LocalPosition.X);
            canvas.RemoveComponent(footer);
            viewport.FixedSize = new int2(600, 300);
            viewport.Update();
            footer.Dispose();
        }

        /// <summary>A press on a second controller remains an edge while another holds that button; axis history and disconnects stay frame-local.</summary>
        [Fact]
        public void NavigationSnapshotPreservesPerDeviceEdgesAndAxisHistory() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            FrameInputBackend backend = new FrameInputBackend();
            InputSystem input = fixture.Core.Input;
            input.SetBackend(backend);
            ulong down = 1UL << (int)InputGamepadButton.DPadDown;
            Capture(input, backend, new InputGamepadState[] {
                new InputGamepadState { Connected = true, Buttons = down, LeftStickY = 10000 },
                new InputGamepadState { Connected = true, LeftStickY = -24000 }
            }, 0, 0, ButtonState.Released);
            Capture(input, backend, new InputGamepadState[] {
                new InputGamepadState { Connected = true, Buttons = down },
                new InputGamepadState { Connected = true, Buttons = down, LeftStickY = 20000 }
            }, 0, 0, ButtonState.Released);
            ulong pressed = DemoDiscGamepadInput.ReadMenuNavigationState(input, out short current, out short previous);
            Assert.Equal(down, pressed);
            Assert.Equal((short)20000, current);
            Assert.Equal((short)-24000, previous);
            Capture(input, backend, new InputGamepadState[] {
                new InputGamepadState { Connected = true, Buttons = down },
                new InputGamepadState { Connected = true, Buttons = down, LeftStickY = 20000 }
            }, 0, 0, ButtonState.Released);
            Assert.Equal(0UL, DemoDiscGamepadInput.ReadMenuNavigationState(input, out current, out previous));
            Capture(input, backend, new InputGamepadState[] {
                new InputGamepadState(), new InputGamepadState()
            }, 0, 0, ButtonState.Released);
            Assert.Equal(0UL, DemoDiscGamepadInput.ReadMenuNavigationState(input, out current, out previous));
            Assert.Equal((short)0, current);
            Assert.Equal((short)20000, previous);
        }

        /// <summary>Idle pointer frames preserve keyboard selection, while a stationary press/release still activates the current row after a panel switch.</summary>
        [Fact]
        public void PointerIdleDoesNotRetargetAndStationaryClickStillWorks() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            FrameInputBackend backend = new FrameInputBackend();
            InputSystem input = fixture.Core.Input;
            input.SetBackend(backend);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 10, 35, ButtonState.Released);
            fixture.Invoke("HandlePointerInput", input);
            Assert.Equal(1, fixture.Panels["main"].SelectedItemIndex);
            fixture.Invoke("MoveSelection", -1);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 10, 35, ButtonState.Released);
            fixture.Invoke("HandlePointerInput", input);
            Assert.Equal(0, fixture.Panels["main"].SelectedItemIndex);
            fixture.Invoke("ActivatePanel", "scene-select", false);
            fixture.Panels["scene-select"].ItemsScrollComponent.ScrollTo(1);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 10, 35, ButtonState.Pressed);
            fixture.Invoke("HandlePointerInput", input);
            Assert.Equal(2, fixture.Panels["scene-select"].SelectedItemIndex);
            Capture(input, backend, Array.Empty<InputGamepadState>(), 10, 35, ButtonState.Released);
            fixture.Invoke("HandlePointerInput", input);
            Assert.True(fixture.Core.SceneManager.IsSceneTransitionActive);
            Assert.Equal(fixture.Panels["scene-select"].Items[2].Definition.TargetId, fixture.Core.SceneManager.TransitionTargetSceneId);
        }

        /// <summary>Commits the previous capture and supplies fresh arrays so device history matches an actual backend frame boundary.</summary>
        /// <param name="input">Real input system under test.</param>
        /// <param name="backend">Raw device source.</param>
        /// <param name="gamepads">Current controller slots.</param>
        /// <param name="x">Pointer X coordinate.</param>
        /// <param name="y">Pointer Y coordinate.</param>
        /// <param name="primary">Primary mouse button state.</param>
        static void Capture(InputSystem input, FrameInputBackend backend, InputGamepadState[] gamepads, int x, int y, ButtonState primary) {
            input.Update();
            backend.Frame = new InputFrameState {
                Gamepads = gamepads, GamepadCount = gamepads.Length, Keyboard = new KeyboardState(),
                Mouse = new MouseState(x, y, 0, primary, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released)
            };
            input.EarlyUpdate();
        }

        /// <summary>Resizing, replacing the clip, and moving content to another viewport never retain old bounds.</summary>
        [Fact]
        public void ScrollClipFollowsResizeReplacementAndReparenting() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            Entity viewport = fixture.CreateNode();
            Entity content = fixture.CreateNode();
            fixture.Root.AddChild(viewport);
            viewport.AddChild(content);
            ClipRectComponent clip = new ClipRectComponent { Size = new int2(160, 100) };
            viewport.AddComponent(clip);
            ScrollComponent scroll = new ScrollComponent { ShowScrollBar = false };
            content.AddComponent(scroll);
            Assert.Equal(160f, scroll.GetClipRect().Z);
            clip.Size = new int2(240, 150);
            Assert.Equal(240f, scroll.GetClipRect().Z);
            viewport.RemoveComponent(clip);
            clip.Dispose();
            viewport.AddComponent(new ClipRectComponent { Size = new int2(320, 200) });
            Assert.Equal(320f, scroll.GetClipRect().Z);
            Entity otherViewport = fixture.CreateNode();
            fixture.Root.AddChild(otherViewport);
            otherViewport.AddComponent(new ClipRectComponent { Size = new int2(480, 300) });
            viewport.RemoveChild(content);
            otherViewport.AddChild(content);
            Assert.Equal(480f, scroll.GetClipRect().Z);
        }
    }
}

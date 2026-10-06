using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.dock {
    /// <summary>Verifies exclusive scrollbar and dock-resize gestures when their pointer regions overlap.</summary>
    public sealed class DockingManagerPointerInteractionTests : IDisposable {
        /// <summary>Core owning the simulated input, camera, and dock entities.</summary>
        readonly Core CoreValue;
        /// <summary>Raw mouse backend used to exercise actual pointer capture.</summary>
        readonly TestInputBackend Input;
        /// <summary>Editor interaction graph shared by the docked panels and manager.</summary>
        readonly EditorSessionInteractionServices InteractionServices = new EditorSessionInteractionServices();
        /// <summary>Host bounds used by both the UI camera and dock layout.</summary>
        readonly int2 HostSize = new int2(1200, 900);
        /// <summary>Manager coordinating resize gestures for the test layout.</summary>
        readonly DockingManager Manager;
        /// <summary>First panel whose scrollbar sits alongside the split boundary.</summary>
        readonly DockableEntity FirstDock;
        /// <summary>Second panel separated from the scrollbar panel by the resize boundary.</summary>
        readonly DockableEntity SecondDock;
        /// <summary>Scroll controller bound to the first panel's body.</summary>
        readonly ScrollComponent Scroll;

        /// <summary>Initializes a real pointer router, UI camera, and two panels with a reusable scrollbar.</summary>
        public DockingManagerPointerInteractionTests() {
            Input = new TestInputBackend();
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(null, new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
            CoreValue.SessionInteractionGraph = InteractionServices;
            EditorEntity cameraHost = new EditorEntity(CoreValue, InteractionServices) { InternalEntity = true };
            cameraHost.AddComponent(new CameraComponent {
                LayerMask = EditorLayerMasks.EditorUi,
                CameraDrawOrder = EditorUiCameraDrawOrders.SharedUi,
                Viewport = new float4(0f, 0f, HostSize.X, HostSize.Y)
            });

            FontAsset font = CreateFont();
            FirstDock = new DockableEntity(CoreValue, InteractionServices, font);
            SecondDock = new DockableEntity(CoreValue, InteractionServices, font);
            Manager = new DockingManager(CoreValue.RenderManager2D, CoreValue.ObjectManager, InteractionServices);
            EditorEntity scrollHost = new EditorEntity(CoreValue, InteractionServices) {
                LayerMask = EditorLayerMasks.EditorUi,
                Position = new float3(0f, FirstDock.TitleBarHeightPixels, 0f)
            };
            FirstDock.AddChild(scrollHost);
            Scroll = new ScrollComponent { ItemCount = 200, ItemExtent = 10 };
            scrollHost.AddComponent(Scroll);
        }

        /// <summary>Releases the test interaction graph and all core-owned entities.</summary>
        public void Dispose() {
            InteractionServices.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>Checks that scrollbar hover and captured dragging never resize a nearby split, even after the pointer leaves the bar.</summary>
        /// <param name="vertical">Whether the panels and scrollbar are arranged along a vertical split.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ScrollBarDrag_NearResizeHandle_DoesNotResize(bool vertical) {
            ConfigureLayout(vertical);
            int2 pointer = GetScrollBarPoint(vertical);
            Assert.True(Manager.Layout.TryGetResizeAxis(pointer, HostSize, float3.Zero, out _));
            AdvancePointer(pointer, ButtonState.Released);
            Assert.Equal(DockingCursorState.Default, Manager.CursorState);
            Assert.Equal(PointerCursorKind.Hand, CoreValue.PointerInteractionSystem.HoverCursor);
            AdvancePointer(pointer, ButtonState.Pressed);
            Assert.NotNull(CoreValue.PointerInteractionSystem.Highlighted);
            Assert.False(Manager.Layout.IsResizing);

            int2 originalSize = FirstDock.Size;
            int originalOffset = Scroll.ScrollOffset;
            int2 moved = vertical ? new int2(pointer.X + 40, pointer.Y + 100) : new int2(pointer.X + 100, pointer.Y + 40);
            AdvancePointer(moved, ButtonState.Pressed);
            Assert.True(Scroll.ScrollOffset > originalOffset);
            Assert.False(Manager.Layout.IsResizing);
            Assert.Equal(originalSize, FirstDock.Size);
            AdvancePointer(moved, ButtonState.Released);
            Assert.Null(CoreValue.PointerInteractionSystem.Highlighted);
            Assert.False(Manager.Layout.IsResizing);
        }

        /// <summary>Checks that a resize begun outside the bar continues when the held pointer crosses the scrollbar, without scrolling it.</summary>
        /// <param name="vertical">Whether the active splitter separates panels horizontally.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ResizeDrag_CrossingScrollBar_DoesNotScroll(bool vertical) {
            ConfigureLayout(vertical);
            int2 barPoint = GetScrollBarPoint(vertical);
            int2 resizePoint = vertical ? new int2(barPoint.X + 3, barPoint.Y) : new int2(barPoint.X, barPoint.Y + 3);
            AdvancePointer(resizePoint, ButtonState.Released);
            Assert.Equal(vertical ? DockingCursorState.VerticalSplit : DockingCursorState.HorizontalSplit, Manager.CursorState);
            AdvancePointer(resizePoint, ButtonState.Pressed);
            Assert.True(Manager.Layout.IsResizing);

            int2 originalSize = FirstDock.Size;
            int2 moved = vertical ? new int2(barPoint.X - 3, barPoint.Y) : new int2(barPoint.X, barPoint.Y - 3);
            AdvancePointer(moved, ButtonState.Pressed);
            Assert.True(Manager.Layout.IsResizing);
            Assert.NotEqual(originalSize, FirstDock.Size);
            Assert.Equal(0, Scroll.ScrollOffset);
            AdvancePointer(moved, ButtonState.Released);
            Assert.False(Manager.Layout.IsResizing);
            Assert.Equal(0, Scroll.ScrollOffset);
        }

        /// <summary>Arranges panels so the scrollbar overlaps the five-pixel resize hit band beside their split.</summary>
        /// <param name="vertical">Whether to create a left/right split with a vertical scrollbar.</param>
        void ConfigureLayout(bool vertical) {
            Manager.Layout.DockAsRoot(FirstDock);
            Manager.Layout.DockRelative(SecondDock, FirstDock, vertical ? DockInsertDirection.Right : DockInsertDirection.Bottom);
            Manager.Layout.Layout(HostSize, float3.Zero);
            FirstDock.SetTitleBarInteractableEnabled(false);
            SecondDock.SetTitleBarInteractableEnabled(false);
            Scroll.Orientation = vertical ? ScrollOrientation.Vertical : ScrollOrientation.Horizontal;
            Scroll.Size = FirstDock.Size;
        }

        /// <summary>Returns a point on the scrollbar two pixels inside the adjacent split, away from its thumb.</summary>
        /// <param name="vertical">Whether the scrollbar runs vertically.</param>
        /// <returns>Screen coordinate inside both the scrollbar and the split resize hit band.</returns>
        int2 GetScrollBarPoint(bool vertical) {
            float3 position = Scroll.ScrollBar.Parent.Position;
            int2 size = Scroll.ScrollBar.Size;
            return vertical
                ? new int2((int)position.X + size.X - 2, (int)position.Y + size.Y / 2)
                : new int2((int)position.X + size.X / 2, (int)position.Y + size.Y - 2);
        }

        /// <summary>Runs pointer routing before docking, matching the editor frame's input order, and applies any resize.</summary>
        /// <param name="pointer">Mouse coordinate to expose for the frame.</param>
        /// <param name="leftButton">Primary mouse-button state for the frame.</param>
        void AdvancePointer(int2 pointer, ButtonState leftButton) {
            Input.SetMouseState(new MouseState(pointer.X, pointer.Y, 0, leftButton, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            CoreValue.Update();
            if (Manager.Update(pointer, leftButton, HostSize, float3.Zero)) {
                Manager.Layout.Layout(HostSize, float3.Zero);
                Scroll.Size = FirstDock.Size;
            }
        }

        /// <summary>Creates a deterministic font for the panels' title and menu chrome.</summary>
        /// <returns>Small atlas with the glyphs used by dock chrome.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "Panel".Distinct()) {
                characters.Add(character, new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f));
            }

            return new FontAsset(new FontInfo("Test", 16, 4f), new TestRuntimeTexture { Width = 64, Height = 64 }, characters, 16f, 64, 64);
        }
    }
}

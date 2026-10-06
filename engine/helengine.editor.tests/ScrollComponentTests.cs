using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies wheel-driven scroll behavior for reusable scroll components.
    /// </summary>
    public class ScrollComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate the core instance for each test.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Raw input backend used to simulate mouse movement and wheel scrolling.
        /// </summary>
        readonly TestInputBackend Input;

        /// <summary>
        /// Initializes the core services required by the scroll-component tests.
        /// </summary>
        public ScrollComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scrollcomponent-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            Input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases shared editor state and temporary directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>Verifies that disabling scrollbar creation allocates no subtree or input regions while wheel scrolling still works.</summary>
        [Fact]
        public void ScrollComponent_WithScrollBarDisabled_CreatesNoObjectsAndStillScrolls() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            ScrollComponent scroll = new ScrollComponent {
                ShowScrollBar = false,
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            int entityCount = Core.Instance.ObjectManager.Entities.Count;
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();

            Assert.Null(scroll.ScrollBar);
            Assert.Empty(viewport.Children);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Assert.Equal(1, scroll.ScrollOffset);
            Assert.Null(scroll.ScrollBar);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
        }

        /// <summary>Checks default creation, axis-specific track layout and pointer input, and range refresh without an offset-change event.</summary>
        /// <param name="orientation">Scrolling axis used by the viewport and scrollbar.</param>
        [Theory]
        [InlineData(ScrollOrientation.Vertical)]
        [InlineData(ScrollOrientation.Horizontal)]
        public void ScrollComponent_DefaultScrollBar_FollowsViewportAndScrollAxis(ScrollOrientation orientation) {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Orientation = orientation,
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();

            Assert.True(scroll.ShowScrollBar);
            ScrollBarComponent bar = Assert.IsType<ScrollBarComponent>(scroll.ScrollBar);
            Assert.True(bar.IsVisible);
            Assert.Equal(orientation == ScrollOrientation.Horizontal ? new int2(160, 8) : new int2(8, 100), bar.Size);
            Assert.Equal(orientation == ScrollOrientation.Horizontal ? new float3(20f, 122f, 1f) : new float3(172f, 30f, 1f), bar.Parent.Position);
            InteractableComponent interactable = Assert.Single(bar.Parent.Children[0].Components.OfType<InteractableComponent>());
            interactable.OnCursor(new int2(159, 99), int2.Zero, PointerInteraction.Press);
            interactable.OnCursor(new int2(159, 99), int2.Zero, PointerInteraction.Release);
            Assert.Equal(scroll.MaximumScrollOffset, scroll.ScrollOffset);

            scroll.ResetScrollOffset();
            RoundedRectComponent thumb = Assert.Single(bar.Parent.Children[0].Children[0].Components.OfType<RoundedRectComponent>());
            Assert.Equal(new float3(0f, 0f, 0.1f), thumb.Parent.LocalPosition);
            scroll.ItemCount = 2;
            Assert.False(bar.IsVisible);
            scroll.ItemCount = 24;
            Assert.True(bar.IsVisible);
            scroll.Size = new int2(240, 200);
            Assert.Equal(orientation == ScrollOrientation.Horizontal ? new int2(240, 8) : new int2(8, 200), bar.Size);
        }

        /// <summary>Verifies that toggling and removing the component disposes all owned entities and recreates exactly one scrollbar when requested.</summary>
        [Fact]
        public void ScrollComponent_TogglingScrollBar_ReleasesAndRecreatesOwnedObjects() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            ScrollComponent scroll = new ScrollComponent {
                ShowScrollBar = false,
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();
            int entityCount = Core.Instance.ObjectManager.Entities.Count;
            scroll.ScrollTo(4);
            scroll.ShowScrollBar = true;
            Entity firstHost = scroll.ScrollBar.Parent;
            int enabledEntityCount = Core.Instance.ObjectManager.Entities.Count;
            Assert.Equal(entityCount + 3, enabledEntityCount);
            scroll.ShowScrollBar = true;
            Assert.Equal(enabledEntityCount, Core.Instance.ObjectManager.Entities.Count);

            scroll.ShowScrollBar = false;
            Assert.True(firstHost.IsDisposed);
            Assert.Null(scroll.ScrollBar);
            Assert.Empty(viewport.Children);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
            Assert.Equal(4, scroll.ScrollOffset);

            scroll.ShowScrollBar = true;
            Assert.NotSame(firstHost, scroll.ScrollBar.Parent);
            Assert.Equal(enabledEntityCount, Core.Instance.ObjectManager.Entities.Count);
            viewport.RemoveComponent(scroll);
            Assert.Null(scroll.ScrollBar);
            Assert.Empty(viewport.Children);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
        }

        /// <summary>Checks scrollbar initialization on disabled viewports and complete cleanup when the owning viewport is disposed.</summary>
        [Fact]
        public void ScrollComponent_OnInitiallyDisabledViewport_InitializesAndDisposesScrollBar() {
            int entityCount = Core.Instance.ObjectManager.Entities.Count;
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Enabled = false
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();
            Assert.NotNull(scroll.ScrollBar);
            Assert.False(scroll.ScrollBar.Parent.IsHierarchyEnabled);
            viewport.Enabled = true;
            Assert.True(scroll.ScrollBar.IsVisible);
            Assert.True(scroll.ScrollBar.Parent.IsHierarchyEnabled);
            viewport.Dispose();
            Assert.Null(scroll.ScrollBar);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
        }

        /// <summary>
        /// Ensures wheel scrolling advances the offset when the pointer is inside the viewport.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelMovesInsideBounds_AdvancesOffset() {
            EditorEntity host = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            host.AddComponent(scroll);
            host.InitializeHierarchy();

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures wheel scrolling is ignored when the pointer lies outside the viewport bounds.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelMovesOutsideBounds_DoesNotAdvanceOffset() {
            EditorEntity host = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            host.AddComponent(scroll);
            host.InitializeHierarchy();

            AdvanceInput(new MouseState(5, 5, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(5, 5, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures scroll offsets stop at the last available item window.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelExceedsAvailableRange_ClampsOffset() {
            EditorEntity host = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 3,
                VisibleItemCount = 1
            };
            host.AddComponent(scroll);
            host.InitializeHierarchy();

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -240, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -360, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(2, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures scrolling uses the viewport bounds exposed by the scroll component itself.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenViewportIsOwnedByTheScrollComponent_UsesItsOwnClipBoundsForWheelHitTesting() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };

            EditorEntity itemsRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            viewport.AddChild(itemsRoot);

            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            viewport.AddComponent(scroll);
            scroll.ContentRoot = itemsRoot;
            viewport.InitializeHierarchy();

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures the scroll component can derive its visible item count and translate a bound content root automatically.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenVisibleCountIsAuto_UsesViewportAndItemExtent() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };

            EditorEntity itemsRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            viewport.AddChild(itemsRoot);

            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 12
            };
            viewport.AddComponent(scroll);
            scroll.ContentRoot = itemsRoot;
            viewport.InitializeHierarchy();

            Assert.Equal(8, scroll.VisibleItemCount);
            float4 clipRect = scroll.GetClipRect();
            Assert.Equal(20f, clipRect.X);
            Assert.Equal(30f, clipRect.Y);
            Assert.Equal(160f, clipRect.Z);
            Assert.Equal(100f, clipRect.W);

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, scroll.ScrollOffset);
            Assert.Equal(-12f, itemsRoot.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures a partially fitting trailing item does not reduce the scroll range required to show the final item completely.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenViewportEndsMidItem_ExcludesThePartialItemFromVisibleCount() {
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 95),
                ItemCount = 8,
                ItemExtent = 24
            };

            Assert.Equal(3, scroll.VisibleItemCount);
            Assert.Equal(5, scroll.MaximumScrollOffset);
        }

        /// <summary>
        /// Advances the simulated raw input state by one engine frame.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }
    }
}


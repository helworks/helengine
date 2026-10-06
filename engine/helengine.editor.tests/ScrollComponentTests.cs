using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies wheel and middle-button autoscroll behavior for reusable scroll components.
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
            TempRootPath = Path.Combine(AppContext.BaseDirectory, "test-artifacts", "scroll-component", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            Input = new TestInputBackend();
            core.Initialize(
                null,
                new TestRenderManager2D(),
                Input,
                new PlatformInfo("test", "test-version"));
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
        /// Ensures a scroll viewport inherits the fixed origin of its ancestor clip when its parent content moves.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenParentContentMoves_AnchorsClipBoundsToAncestorClipRect() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            viewport.AddComponent(new ClipRectComponent {
                Size = new int2(160, 100)
            });

            EditorEntity movingContentRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            viewport.AddChild(movingContentRoot);
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            movingContentRoot.AddComponent(scroll);
            viewport.InitializeHierarchy();

            movingContentRoot.Position = new float3(0f, -40f, 0f);

            float4 clipRect = scroll.GetClipRect();

            Assert.Equal(20f, clipRect.X);
            Assert.Equal(30f, clipRect.Y);
            Assert.Equal(160f, clipRect.Z);
            Assert.Equal(100f, clipRect.W);
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
        /// Ensures a middle-button click released inside the viewport activates browser-style autoscroll.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenMiddleButtonClickIsReleased_EntersAutoScrollModeAndTracksPointerDirection() {
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 60, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 0.25d);

            Assert.Equal(1, scroll.ScrollOffset);
            Assert.Equal(PointerCursorKind.AutoScrollVertical, Core.Instance.PointerInteractionSystem.HoverCursor);
            Assert.Equal(new int2(40, 50), Core.Instance.PointerInteractionSystem.CursorOverridePosition);

            AdvanceInput(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 0.25d);
            Assert.Equal(0, scroll.ScrollOffset);

            AdvanceInput(new MouseState(40, 60, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 60, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            Assert.Equal(PointerCursorKind.Default, Core.Instance.PointerInteractionSystem.HoverCursor);
            Assert.Equal(new int2(0, 0), Core.Instance.PointerInteractionSystem.CursorOverridePosition);
            AdvanceInput(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d);
            Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures a middle-button click outside the viewport does not activate autoscroll when the pointer later moves inside.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenMiddleButtonClickStartsOutsideViewport_DoesNotActivateAutoScroll() {
            EditorEntity host = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            host.AddComponent(scroll);
            host.InitializeHierarchy();

            AdvanceInput(new MouseState(5, 5, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(5, 5, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(5, 5, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 30, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d);

            Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures horizontal scrolling uses horizontal extent, pointer distance, and content translation while retaining the click anchor.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenHorizontalAutoScrollIsActive_UsesHorizontalAxisAndKeepsIndicatorAnchorFixed() {
            using EditorAutoScrollIndicatorOverlay overlay = new EditorAutoScrollIndicatorOverlay(
                Core.Instance,
                new helengine.editor.EditorSessionInteractionServices());
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            EditorEntity contentRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(0f, 7f, 3f)
            };
            viewport.AddChild(contentRoot);

            ScrollComponent scroll = new ScrollComponent {
                Orientation = ScrollOrientation.Horizontal,
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            scroll.ContentRoot = contentRoot;
            viewport.InitializeHierarchy();

            int initialCameraCount = Core.Instance.ObjectManager.Cameras.Count;
            Assert.Equal(16, scroll.VisibleItemCount);
            Assert.Equal(8, scroll.MaximumScrollOffset);
            overlay.Update();
            Assert.False(overlay.IsVisible);
            Assert.Equal(initialCameraCount, Core.Instance.ObjectManager.Cameras.Count);

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 60, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 0.5d);

            Assert.Equal(0, scroll.ScrollOffset);
            Assert.Equal(new int2(40, 50), Core.Instance.PointerInteractionSystem.CursorOverridePosition);
            overlay.Update();
            Assert.True(overlay.IsVisible);
            Assert.Equal(ScrollOrientation.Horizontal, overlay.VisibleOrientation);
            Assert.Equal(new int2(24, 34), overlay.IndicatorTopLeft);
            Assert.Equal(initialCameraCount + 1, Core.Instance.ObjectManager.Cameras.Count);

            AdvanceInput(new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 0.25d);

            Assert.Equal(1, scroll.ScrollOffset);
            Assert.Equal(PointerCursorKind.AutoScrollHorizontal, Core.Instance.PointerInteractionSystem.HoverCursor);
            Assert.Equal(-10f, contentRoot.LocalPosition.X);
            Assert.Equal(7f, contentRoot.LocalPosition.Y);
            Assert.Equal(3f, contentRoot.LocalPosition.Z);
            Assert.Equal(new int2(40, 50), Core.Instance.PointerInteractionSystem.CursorOverridePosition);

            overlay.Update();
            Assert.Equal(new int2(24, 34), overlay.IndicatorTopLeft);

            AdvanceInput(new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.False(overlay.IsVisible);
            Assert.Equal(initialCameraCount, Core.Instance.ObjectManager.Cameras.Count);
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

        /// <summary>Checks arrow feedback against the scroll axis, neutral position, content limits, and cancellation.</summary>
        [Theory]
        [InlineData(ScrollOrientation.Vertical)]
        [InlineData(ScrollOrientation.Horizontal)]
        public void AutoScrollIndicator_HighlightsOnlyTheDirectionThatCanScroll(ScrollOrientation orientation) {
            using EditorAutoScrollIndicatorOverlay overlay = new EditorAutoScrollIndicatorOverlay(
                Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Orientation = orientation,
                Size = new int2(160, 100),
                ItemCount = 40,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();
            scroll.ScrollTo(5);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();

            string backwardName = orientation == ScrollOrientation.Vertical ? "Autoscroll Up Arrow" : "Autoscroll Left Arrow";
            string forwardName = orientation == ScrollOrientation.Vertical ? "Autoscroll Down Arrow" : "Autoscroll Right Arrow";
            SpriteComponent backward = FindAutoScrollSprite(backwardName);
            SpriteComponent forward = FindAutoScrollSprite(forwardName);
            _ = FindAutoScrollSprite("Autoscroll Center Dot");
            byte4 neutral = backward.Color;
            Assert.Equal(neutral, forward.Color);

            AdvanceInput(new MouseState(orientation == ScrollOrientation.Vertical ? 70 : 40,
                orientation == ScrollOrientation.Horizontal ? 80 : 50, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.Equal(neutral, backward.Color);
            Assert.Equal(neutral, forward.Color);

            AdvanceInput(new MouseState(orientation == ScrollOrientation.Horizontal ? 30 : 40,
                orientation == ScrollOrientation.Vertical ? 40 : 50, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.NotEqual(neutral, backward.Color);
            Assert.Equal(neutral, forward.Color);

            scroll.ScrollTo(0);
            overlay.Update();
            Assert.Equal(neutral, backward.Color);

            AdvanceInput(new MouseState(orientation == ScrollOrientation.Horizontal ? 50 : 40,
                orientation == ScrollOrientation.Vertical ? 60 : 50, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.Equal(neutral, backward.Color);
            Assert.NotEqual(neutral, forward.Color);

            scroll.ScrollTo(scroll.MaximumScrollOffset);
            overlay.Update();
            Assert.Equal(neutral, forward.Color);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.Equal(neutral, backward.Color);
            Assert.Equal(neutral, forward.Color);

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            overlay.Update();
            Assert.False(overlay.IsVisible);
        }

        /// <summary>Ensures indicator updates reuse one atlas and teardown releases it and the entities exactly once.</summary>
        [Fact]
        public void AutoScrollIndicator_ReusesAndReleasesItsOwnedAtlas() {
            TestRenderManager2D renderer = (TestRenderManager2D)Core.Instance.RenderManager2D;
            int textureCount = renderer.BuildTextureFromRawCallCount;
            int releasedCount = renderer.ReleasedTextures.Count;
            int entityCount = Core.Instance.ObjectManager.Entities.Count;
            EditorAutoScrollIndicatorOverlay overlay = new EditorAutoScrollIndicatorOverlay(
                Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            overlay.Update();
            overlay.Update();
            Assert.Equal(textureCount + 1, renderer.BuildTextureFromRawCallCount);
            overlay.Dispose();
            overlay.Dispose();
            Assert.Equal(releasedCount + 1, renderer.ReleasedTextures.Count);
            Assert.Equal(entityCount, Core.Instance.ObjectManager.Entities.Count);
        }

        /// <summary>Finds a rendered indicator sprite by its semantic direction name.</summary>
        /// <param name="name">Indicator element whose currently rendered sprite is required.</param>
        /// <returns>The visible sprite submitted by the overlay.</returns>
        SpriteComponent FindAutoScrollSprite(string name) {
            return Assert.Single(Core.Instance.ObjectManager.Drawables2D.OfType<SpriteComponent>(),
                sprite => sprite.Parent is EditorEntity entity && entity.Name == name);
        }

        /// <summary>Checks that either primary mouse button cancels autoscroll immediately, even outside its viewport.</summary>
        [Theory]
        [InlineData(ScrollOrientation.Vertical, true)]
        [InlineData(ScrollOrientation.Vertical, false)]
        [InlineData(ScrollOrientation.Horizontal, true)]
        [InlineData(ScrollOrientation.Horizontal, false)]
        public void ScrollComponent_LeftOrRightClickOutsideViewport_StopsAutoScroll(ScrollOrientation orientation, bool leftButton) {
            using EditorAutoScrollIndicatorOverlay overlay = new EditorAutoScrollIndicatorOverlay(
                Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            EditorEntity viewport = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Orientation = orientation,
                Size = new int2(160, 100),
                ItemCount = 100,
                ItemExtent = 10
            };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d / 60d);
            AdvanceInput(new MouseState(orientation == ScrollOrientation.Horizontal ? 50 : 40,
                orientation == ScrollOrientation.Vertical ? 60 : 50, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 0.25d);
            overlay.Update();
            Assert.True(overlay.IsVisible);
            Assert.Equal(1, scroll.ScrollOffset);

            AdvanceInput(new MouseState(500, 500, 0,
                leftButton ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                leftButton ? ButtonState.Released : ButtonState.Pressed, ButtonState.Released, ButtonState.Released), 0.25d);
            overlay.Update();
            Assert.False(overlay.IsVisible);
            Assert.Equal(PointerCursorKind.Default, Core.Instance.PointerInteractionSystem.HoverCursor);
            Assert.Equal(0, scroll.AutoScrollDirection);
            Assert.Equal(1, scroll.ScrollOffset);

            AdvanceInput(new MouseState(400, 400, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), 1d);
            Assert.Equal(1, scroll.ScrollOffset);
        }

        /// <summary>
        /// Advances the simulated raw input state by one engine frame.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }

        /// <summary>
        /// Advances the simulated raw input state by one engine frame with a deterministic elapsed time.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        /// <param name="elapsedSeconds">Frame time in seconds.</param>
        void AdvanceInput(MouseState mouseState, double elapsedSeconds) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update(elapsedSeconds);
        }
    }
}


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
            EditorInputCaptureService.Reset();

            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures wheel scrolling advances the offset when the pointer is inside the viewport.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelMovesInsideBounds_AdvancesOffset() {
            EditorEntity host = new EditorEntity {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            host.AddComponent(scroll);

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures wheel scrolling is ignored when the pointer lies outside the viewport bounds.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelMovesOutsideBounds_DoesNotAdvanceOffset() {
            EditorEntity host = new EditorEntity {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            host.AddComponent(scroll);

            AdvanceInput(new MouseState(5, 5, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(5, 5, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(0, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures scroll offsets stop at the last available item window.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenWheelExceedsAvailableRange_ClampsOffset() {
            EditorEntity host = new EditorEntity {
                Position = new float3(20f, 30f, 0f)
            };
            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 3,
                VisibleItemCount = 1
            };
            host.AddComponent(scroll);

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
            EditorEntity viewport = new EditorEntity {
                Position = new float3(20f, 30f, 0f)
            };

            EditorEntity itemsRoot = new EditorEntity();
            viewport.AddChild(itemsRoot);

            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                VisibleItemCount = 8
            };
            viewport.AddComponent(scroll);
            scroll.ContentRoot = itemsRoot;

            AdvanceInput(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, scroll.ScrollOffset);
        }

        /// <summary>
        /// Ensures the scroll component can derive its visible item count and translate a bound content root automatically.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenVisibleCountIsAuto_UsesViewportAndItemExtent() {
            EditorEntity viewport = new EditorEntity {
                Position = new float3(20f, 30f, 0f)
            };

            EditorEntity itemsRoot = new EditorEntity();
            viewport.AddChild(itemsRoot);

            ScrollComponent scroll = new ScrollComponent {
                Size = new int2(160, 100),
                ItemCount = 24,
                ItemExtent = 12
            };
            viewport.AddComponent(scroll);
            scroll.ContentRoot = itemsRoot;

            Assert.Equal(9, scroll.VisibleItemCount);
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
        /// Advances the simulated raw input state by one engine frame.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }
    }
}


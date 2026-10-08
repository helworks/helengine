using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>
    /// Verifies viewport scaling keeps text effects proportional and restores authored values after resizing.
    /// </summary>
    public sealed class ViewportLayoutSnapshotTests {
        /// <summary>
        /// Keeps glyphs and effects proportional to the canvas scalar across aspect changes, repeated resizing and restoration.
        /// </summary>
        /// <param name="stretchCanvas">Whether other layout components stretch independently by axis.</param>
        /// <param name="viewportWidth">Live width available to the authored text layout.</param>
        /// <param name="viewportHeight">Live height available to the authored text layout.</param>
        /// <param name="expectedScale">Expected text and effect scale for the supplied viewport bounds.</param>
        [Theory]
        [InlineData(false, 320, 360, 0.25f)]
        [InlineData(true, 320, 360, 0.5f)]
        [InlineData(false, 640, 480, 0.5f)]
        [InlineData(true, 640, 480, 0.6666667f)]
        [InlineData(true, 1280, 720, 1f)]
        [InlineData(true, 2560, 720, 1f)]
        [InlineData(true, 640, 1440, 2f)]
        [InlineData(true, 2560, 360, 0.5f)]
        public void Apply_WhenViewportResizes_ScalesTextEffectsFromAuthoredValues(bool stretchCanvas, int viewportWidth, int viewportHeight, float expectedScale) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            Entity entity = new Entity(core);
            entity.InitComponents();
            TextComponent text = new TextComponent {
                Size = new int2(400, 100),
                FontScale = 2f,
                OutlineScale = 2f,
                ShadowOffset = new float2(-4f, 6f)
            };
            entity.AddComponent(text);
            ViewportLayoutSnapshot snapshot = new ViewportLayoutSnapshot(entity, false);
            AnchorSpace reduced = new AnchorSpace(new int2(viewportWidth, viewportHeight), new float2(0f, 0f));

            snapshot.Apply(reduced, new float2(0f, 0f), 1280, 720, stretchCanvas);
            snapshot.Apply(reduced, new float2(0f, 0f), 1280, 720, stretchCanvas);

            Assert.Equal(2f * expectedScale, text.FontScale);
            Assert.Equal(2f * expectedScale, text.OutlineScale);
            Assert.Equal(-4f * expectedScale, text.ShadowOffset.X);
            Assert.Equal(6f * expectedScale, text.ShadowOffset.Y);
            Assert.Equal((int)Math.Round(400d * viewportWidth / 1280d), text.Size.X);
            Assert.Equal((int)Math.Round(100d * viewportHeight / 720d), text.Size.Y);

            snapshot.Apply(new AnchorSpace(new int2(1280, 720), new float2(0f, 0f)),
                new float2(0f, 0f), 1280, 720, stretchCanvas);

            Assert.Equal(2f, text.FontScale);
            Assert.Equal(2f, text.OutlineScale);
            Assert.Equal(new float2(-4f, 6f), text.ShadowOffset);
            Assert.Equal(new int2(400, 100), text.Size);
        }
    }
}

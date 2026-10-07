using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>
    /// Verifies viewport scaling keeps text effects proportional and restores authored values after resizing.
    /// </summary>
    public sealed class ViewportLayoutSnapshotTests {
        /// <summary>
        /// Applies the same scale to glyphs, outlines and signed shadow offsets without accumulating resize drift.
        /// </summary>
        /// <param name="stretchCanvas">Whether the viewport uses its height scale or the minimum axis scale.</param>
        /// <param name="expectedScale">Expected text and effect scale for the supplied viewport bounds.</param>
        [Theory]
        [InlineData(false, 0.25f)]
        [InlineData(true, 0.5f)]
        public void Apply_WhenViewportResizes_ScalesTextEffectsFromAuthoredValues(bool stretchCanvas, float expectedScale) {
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
            AnchorSpace reduced = new AnchorSpace(new int2(320, 360), new float2(0f, 0f));

            snapshot.Apply(reduced, new float2(0f, 0f), 1280, 720, stretchCanvas);
            snapshot.Apply(reduced, new float2(0f, 0f), 1280, 720, stretchCanvas);

            Assert.Equal(2f * expectedScale, text.FontScale);
            Assert.Equal(2f * expectedScale, text.OutlineScale);
            Assert.Equal(-4f * expectedScale, text.ShadowOffset.X);
            Assert.Equal(6f * expectedScale, text.ShadowOffset.Y);
            Assert.Equal(new int2(100, 50), text.Size);

            snapshot.Apply(new AnchorSpace(new int2(1280, 720), new float2(0f, 0f)),
                new float2(0f, 0f), 1280, 720, stretchCanvas);

            Assert.Equal(2f, text.FontScale);
            Assert.Equal(2f, text.OutlineScale);
            Assert.Equal(new float2(-4f, 6f), text.ShadowOffset);
            Assert.Equal(new int2(400, 100), text.Size);
        }
    }
}

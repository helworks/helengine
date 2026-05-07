using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.rendering {
    /// <summary>
    /// Verifies rectangular clip-owner components expose deterministic clip bounds for the 2D command builder.
    /// </summary>
    public sealed class ClipRectComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate the shared core instance for the clip-owner tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the clip-owner tests.
        /// </summary>
        public ClipRectComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-clip-rect-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(null, null, new TestInputBackend());
        }

        /// <summary>
        /// Releases temporary test directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a clip component resolves one rectangle from its parent position and configured size.
        /// </summary>
        [Fact]
        public void GetClipRect_WhenParentAndSizeAreConfigured_ReturnsScreenSpaceBounds() {
            EditorEntity entity = new EditorEntity {
                Position = new float3(48f, 96f, 0f)
            };
            ClipRectComponent clip = new ClipRectComponent {
                Size = new int2(320, 120)
            };
            entity.AddComponent(clip);

            Assert.Equal(new float4(48f, 96f, 320f, 120f), clip.GetClipRect());
        }

        /// <summary>
        /// Ensures clip components reject negative clip sizes instead of silently correcting them.
        /// </summary>
        [Fact]
        public void Size_WhenNegative_ThrowsArgumentOutOfRangeException() {
            ClipRectComponent clip = new ClipRectComponent();

            Assert.Throws<ArgumentOutOfRangeException>(
                delegate {
                    clip.Size = new int2(-1, 24);
                });
        }
    }
}

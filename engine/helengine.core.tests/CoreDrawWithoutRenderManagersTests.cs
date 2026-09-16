using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the draw cycle tolerates a host that has no render managers yet.
    /// </summary>
    /// <remarks>
    /// Initialize accepts null render managers and the rest of Draw already
    /// treats a null 3D manager as "nothing was drawn", so a host bringing a new
    /// platform up reasonably expects to run the loop before it has a renderer.
    /// The measurement helper was the one place that dereferenced the manager
    /// unconditionally, which turned that into a null reference on the first
    /// draw; on a console target it faults with no diagnostic at all.
    /// </remarks>
    public sealed class CoreDrawWithoutRenderManagersTests {
        /// <summary>
        /// Ensures Draw completes and reports no work when no render managers are attached.
        /// </summary>
        [Fact]
        public void Draw_WhenRenderManagersAreAbsent_CompletesAndReportsNoDraw() {
            Core core = CreateCore();

            core.Draw();

            Assert.Equal(0d, core.LastRenderManager3DDrawMilliseconds);
            Assert.Equal(0, core.LastRenderManager3DDrawCallCount);
            Assert.Equal("DrawEnd", core.LastSceneTransitionStage);
        }

        /// <summary>
        /// Ensures repeated draws stay stable rather than only surviving the first one.
        /// </summary>
        [Fact]
        public void Draw_WhenCalledRepeatedlyWithoutRenderManagers_StaysStable() {
            Core core = CreateCore();

            for (int frame = 0; frame < 3; frame++) {
                core.Draw();
            }

            Assert.Equal(0d, core.LastRenderManager3DDrawMilliseconds);
            Assert.Equal("DrawEnd", core.LastSceneTransitionStage);
        }

        /// <summary>
        /// Creates one initialized core with no render, input or audio backend.
        /// </summary>
        /// <returns>Core instance used by the draw tests.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}

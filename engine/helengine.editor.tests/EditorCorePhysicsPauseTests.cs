using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor host pauses physics simulation so authoring transforms are never overwritten by body poses.
    /// </summary>
    public sealed class EditorCorePhysicsPauseTests {
        /// <summary>
        /// Ensures a freshly constructed editor core starts with physics simulation paused.
        /// </summary>
        [Fact]
        public void EditorCore_WhenConstructed_PausesPhysicsSimulation() {
            EditorCore core = new EditorCore(new Project {
                Name = "Physics Pause",
                Path = "physics-pause-test"
            });

            Assert.True(core.PhysicsSimulationIsPaused);
        }
    }
}

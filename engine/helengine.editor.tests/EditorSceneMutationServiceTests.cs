using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared editor scene-mutation notification service.
    /// </summary>
    public class EditorSceneMutationServiceTests {
        /// <summary>
        /// Ensures scene-mutation notifications raise the shared event.
        /// </summary>
        [Fact]
        public void MarkSceneMutated_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;

                EditorSceneMutationService.MarkSceneMutated();

                Assert.True(raised);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
        }

        /// <summary>
        /// Ensures reset clears subscribers between tests.
        /// </summary>
        [Fact]
        public void Reset_ClearsSubscribers() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            EditorSceneMutationService.SceneMutated += handleSceneMutated;

            EditorSceneMutationService.Reset();
            EditorSceneMutationService.MarkSceneMutated();

            Assert.False(raised);
        }
    }
}

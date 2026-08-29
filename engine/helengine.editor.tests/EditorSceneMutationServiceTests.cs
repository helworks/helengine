using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared editor scene-mutation notification service.
    /// </summary>
    public class EditorSceneMutationServiceTests {
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        /// <summary>
        /// Ensures scene-mutation notifications raise the shared event.
        /// </summary>
        [Fact]
        public void MarkSceneMutated_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;

            try {
                InteractionServices.SceneMutation.SceneMutated += handleSceneMutated;

                InteractionServices.SceneMutation.MarkSceneMutated();

                Assert.True(raised);
            } finally {
                InteractionServices.SceneMutation.SceneMutated -= handleSceneMutated;
            }
        }

        /// <summary>
        /// Ensures disposal clears subscribers between uses.
        /// </summary>
        [Fact]
        public void Dispose_ClearsSubscribers() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            InteractionServices.SceneMutation.SceneMutated += handleSceneMutated;
            InteractionServices.SceneMutation.Dispose();
            InteractionServices.SceneMutation.MarkSceneMutated();

            Assert.False(raised);
        }
    }
}

using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies custom editor tools can capture and record component mutations through the static history bridge without reaching into editor-session internals.
    /// </summary>
    public sealed class EditorComponentHistoryMutationServiceTests : IDisposable {
        /// <summary>
        /// Clears shared component-history bridge callbacks after each test.
        /// </summary>
        public void Dispose() {
            EditorComponentHistoryMutationService.Reset();
        }

        /// <summary>
        /// Ensures the bridge captures the owning editor entity state when the component belongs to one live editor entity.
        /// </summary>
        [Fact]
        public void Try_capture_entity_state_when_component_belongs_to_a_live_editor_entity_returns_true() {
            EditorEntity entity = new EditorEntity();
            SpriteComponent component = new SpriteComponent();
            entity.AddComponent(component);
            SerializedEditorEntityState expectedState = new SerializedEditorEntityState(7u, "Sprite Entity", 0u, Array.Empty<byte>());
            int captureCount = 0;
            EditorComponentHistoryMutationService.CaptureEntityState = editorEntity => {
                captureCount++;
                Assert.Same(entity, editorEntity);
                return expectedState;
            };

            bool captured = EditorComponentHistoryMutationService.TryCaptureEntityState(component, out SerializedEditorEntityState previousEntityState);

            Assert.True(captured);
            Assert.Same(expectedState, previousEntityState);
            Assert.Equal(1, captureCount);
        }

        /// <summary>
        /// Ensures the bridge records one component mutation when all required callbacks and editor state are available.
        /// </summary>
        [Fact]
        public void Try_record_component_mutation_when_callbacks_are_available_invokes_the_session_bridge() {
            EditorEntity entity = new EditorEntity();
            SpriteComponent component = new SpriteComponent();
            entity.AddComponent(component);
            SerializedEditorEntityState previousEntityState = new SerializedEditorEntityState(3u, "Before", 0u, Array.Empty<byte>());
            int recordCount = 0;
            EditorComponentHistoryMutationService.RecordComponentMutation = (editorEntity, historyComponent, historyState) => {
                recordCount++;
                Assert.Same(entity, editorEntity);
                Assert.Same(component, historyComponent);
                Assert.Same(previousEntityState, historyState);
            };

            bool recorded = EditorComponentHistoryMutationService.TryRecordComponentMutation(component, previousEntityState);

            Assert.True(recorded);
            Assert.Equal(1, recordCount);
        }

        /// <summary>
        /// Ensures detached or unsupported components fail gracefully instead of throwing when the bridge cannot resolve one live editor entity.
        /// </summary>
        [Fact]
        public void Try_record_component_mutation_when_component_is_not_owned_by_a_live_editor_entity_returns_false() {
            SpriteComponent component = new SpriteComponent();
            SerializedEditorEntityState previousEntityState = new SerializedEditorEntityState(5u, "Detached", 0u, Array.Empty<byte>());
            int recordCount = 0;
            EditorComponentHistoryMutationService.RecordComponentMutation = (editorEntity, historyComponent, historyState) => recordCount++;

            bool recorded = EditorComponentHistoryMutationService.TryRecordComponentMutation(component, previousEntityState);

            Assert.False(recorded);
            Assert.Equal(0, recordCount);
        }
    }
}

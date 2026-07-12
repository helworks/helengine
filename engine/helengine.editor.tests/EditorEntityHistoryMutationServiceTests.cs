using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies global editor tools can capture and record entity-scoped history through the static entity-history bridge.
    /// </summary>
    public sealed class EditorEntityHistoryMutationServiceTests : IDisposable {
        /// <summary>
        /// Clears shared entity-history bridge callbacks after each test.
        /// </summary>
        public void Dispose() {
            EditorEntityHistoryMutationService.Reset();
        }

        /// <summary>
        /// Ensures the bridge captures one detached history snapshot for a live editor entity when the active editor session exposes the callback.
        /// </summary>
        [Fact]
        public void Try_capture_entity_state_when_entity_is_live_and_callback_exists_returns_true() {
            EditorEntity entity = new EditorEntity();
            SerializedEditorEntityState expectedState = new SerializedEditorEntityState {
                EntityId = 7u,
                ParentEntityId = 0u,
                EntityAsset = new SceneEntityAsset {
                    Id = 7u,
                    Name = "History Entity"
                }
            };
            int captureCount = 0;
            EditorEntityHistoryMutationService.CaptureEntityState = editorEntity => {
                captureCount++;
                Assert.Same(entity, editorEntity);
                return expectedState;
            };

            bool captured = EditorEntityHistoryMutationService.TryCaptureEntityState(entity, out SerializedEditorEntityState entityState);

            Assert.True(captured);
            Assert.Same(expectedState, entityState);
            Assert.Equal(1, captureCount);
        }

        /// <summary>
        /// Ensures the bridge records one entity mutation when the target entity is live and the active session callback exists.
        /// </summary>
        [Fact]
        public void Try_record_entity_state_change_when_entity_is_live_and_callback_exists_returns_true() {
            EditorEntity entity = new EditorEntity();
            SerializedEditorEntityState previousEntityState = new SerializedEditorEntityState {
                EntityId = 3u,
                ParentEntityId = 0u,
                EntityAsset = new SceneEntityAsset {
                    Id = 3u,
                    Name = "Before"
                }
            };
            int recordCount = 0;
            EditorEntityHistoryMutationService.RecordEntityStateChange = (editorEntity, historyState) => {
                recordCount++;
                Assert.Same(entity, editorEntity);
                Assert.Same(previousEntityState, historyState);
            };

            bool recorded = EditorEntityHistoryMutationService.TryRecordEntityStateChange(entity, previousEntityState);

            Assert.True(recorded);
            Assert.Equal(1, recordCount);
        }

        /// <summary>
        /// Ensures detached entities fail gracefully instead of throwing when no active live editor entity is available.
        /// </summary>
        [Fact]
        public void Try_record_entity_state_change_when_entity_is_not_one_live_editor_entity_returns_false() {
            Entity entity = new Entity();
            SerializedEditorEntityState previousEntityState = new SerializedEditorEntityState {
                EntityId = 9u,
                ParentEntityId = 0u,
                EntityAsset = new SceneEntityAsset {
                    Id = 9u,
                    Name = "Detached"
                }
            };
            int recordCount = 0;
            EditorEntityHistoryMutationService.RecordEntityStateChange = (editorEntity, historyState) => recordCount++;

            bool recorded = EditorEntityHistoryMutationService.TryRecordEntityStateChange(entity, previousEntityState);

            Assert.False(recorded);
            Assert.Equal(0, recordCount);
        }
    }
}

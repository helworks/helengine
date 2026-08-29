using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies blueprint-inherited entities stay read-only across history capture and recording.
    /// </summary>
    public sealed class BlueprintInheritedEntityProtectionTests : IDisposable {
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        /// <summary>
        /// Initializes one core host for inherited-entity protection tests.
        /// </summary>
        public BlueprintInheritedEntityProtectionTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears the static history bridge callbacks after each test.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures inherited blueprint entities are never captured into undo history snapshots.
        /// </summary>
        [Fact]
        public void TryCaptureEntityState_WhenEntityIsBlueprintInherited_ReturnsFalse() {
            InteractionServices.EntityHistory.CaptureEntityState = entity => new SerializedEditorEntityState();
            EditorEntity inheritedEntity = CreateInheritedEntity();

            bool captured = InteractionServices.EntityHistory.TryCaptureEntityState(inheritedEntity, out SerializedEditorEntityState state);

            Assert.False(captured);
            Assert.Null(state);
        }

        /// <summary>
        /// Ensures inherited blueprint entities are never recorded as entity-state mutations.
        /// </summary>
        [Fact]
        public void TryRecordEntityStateChange_WhenEntityIsBlueprintInherited_ReturnsFalse() {
            bool recorded = false;
            InteractionServices.EntityHistory.RecordEntityStateChange = (entity, previousState) => recorded = true;
            EditorEntity inheritedEntity = CreateInheritedEntity();

            bool result = InteractionServices.EntityHistory.TryRecordEntityStateChange(inheritedEntity, new SerializedEditorEntityState());

            Assert.False(result);
            Assert.False(recorded);
        }

        /// <summary>
        /// Ensures the shared inherited-entity check recognizes the inherited marker on any entity type.
        /// </summary>
        [Fact]
        public void IsInheritedEntity_RecognizesTheInheritedMarker() {
            EditorEntity inheritedEntity = CreateInheritedEntity();
            EditorEntity plainEntity = new EditorEntity { IsSceneOwned = true };

            Assert.True(BlueprintSceneSaveFilterService.IsInheritedEntity(inheritedEntity));
            Assert.False(BlueprintSceneSaveFilterService.IsInheritedEntity(plainEntity));
            Assert.False(BlueprintSceneSaveFilterService.IsInheritedEntity(null));
        }

        /// <summary>
        /// Creates one scene-owned entity carrying the inherited blueprint marker.
        /// </summary>
        /// <returns>Inherited blueprint entity.</returns>
        static EditorEntity CreateInheritedEntity() {
            EditorEntity entity = new EditorEntity { IsSceneOwned = true };
            entity.AddComponent(new BlueprintInheritedEntityComponent {
                BlueprintAssetReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateCurrentFileSystem("blueprints/games/split_play/GoldenCoin.hblueprint"),
                SourceEntityId = 7u
            });
            return entity;
        }
    }
}

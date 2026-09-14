using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the inspector mutation service retargets rows onto the platform-specific component, persists platform
    /// overrides only where they are meaningful, and falls back to marking the scene dirty when no history service is
    /// attached to the session.
    /// </summary>
    public sealed class ComponentPropertyMutationServiceTests {
        /// <summary>
        /// Platform id used by the tests for a non-common override scope.
        /// </summary>
        const string PlatformId = "windows";

        /// <summary>
        /// Confirms retargeting rewrites only the rows bound to the same component and the same platform.
        /// </summary>
        [Fact]
        public void RetargetRowsForEditableComponent_WhenRowsShareComponentAndPlatform_RetargetsOnlyThoseRows() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent commonComponent = new TestInspectorComponent();
            TestInspectorComponent otherComponent = new TestInspectorComponent();
            TestInspectorComponent editableComponent = new TestInspectorComponent();

            ComponentPropertyRow matchingRow = CreateRow(commonComponent, commonComponent, PlatformId);
            ComponentPropertyRow otherPlatformRow = CreateRow(commonComponent, commonComponent, "ds");
            ComponentPropertyRow otherComponentRow = CreateRow(otherComponent, otherComponent, PlatformId);
            List<ComponentPropertyRow> rows = new List<ComponentPropertyRow> { matchingRow, otherPlatformRow, otherComponentRow };

            service.RetargetRowsForEditableComponent(rows, commonComponent, PlatformId, editableComponent);

            Assert.Same(editableComponent, matchingRow.TargetComponent);
            Assert.Same(commonComponent, otherPlatformRow.TargetComponent);
            Assert.Same(otherComponent, otherComponentRow.TargetComponent);
        }

        /// <summary>
        /// Confirms retargeting matches the platform id without regard to casing, mirroring the scope lookups.
        /// </summary>
        [Fact]
        public void RetargetRowsForEditableComponent_WhenPlatformIdCasingDiffers_StillRetargetsRow() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent commonComponent = new TestInspectorComponent();
            TestInspectorComponent editableComponent = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(commonComponent, commonComponent, "Windows");
            List<ComponentPropertyRow> rows = new List<ComponentPropertyRow> { row };

            service.RetargetRowsForEditableComponent(rows, commonComponent, PlatformId, editableComponent);

            Assert.Same(editableComponent, row.TargetComponent);
        }

        /// <summary>
        /// Confirms a row without a save component or platform context persists nothing, because the value it edits
        /// already lives on the shared component.
        /// </summary>
        /// <param name="platformId">Platform id assigned to the row under test.</param>
        [Theory]
        [InlineData("")]
        [InlineData(ComponentPlatformEditingService.CommonPlatformId)]
        public void PersistPlatformOverrideIfNeeded_WhenRowHasNoPlatformOverrideContext_PersistsNothing(string platformId) {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent component = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(component, component, platformId);

            bool persisted = service.PersistPlatformOverrideIfNeeded(row, nameof(TestInspectorComponent.Value));

            Assert.False(persisted);
        }

        /// <summary>
        /// Confirms a platform row without an entity save component cannot persist an override.
        /// </summary>
        [Fact]
        public void PersistPlatformOverrideIfNeeded_WhenRowHasNoSaveComponent_PersistsNothing() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent component = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(component, component, PlatformId);
            row.SaveComponent = null;

            bool persisted = service.PersistPlatformOverrideIfNeeded(row, nameof(TestInspectorComponent.Value));

            Assert.False(persisted);
        }

        /// <summary>
        /// Confirms a platform row backed by an entity save component records the property path as an explicit
        /// override and reports that the row chrome should refresh.
        /// </summary>
        [Fact]
        public void PersistPlatformOverrideIfNeeded_WhenRowHasPlatformContext_MarksPropertyOverride() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent component = new TestInspectorComponent();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            ComponentPropertyRow row = CreateRow(component, component, PlatformId);
            row.SaveComponent = saveComponent;

            bool persisted = service.PersistPlatformOverrideIfNeeded(row, nameof(TestInspectorComponent.Value));

            Assert.True(persisted);
            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreateScopedPlatformOverride(new EditorOverrideScope(PlatformId));
            Assert.True(overrideState.HasPropertyOverride(nameof(TestInspectorComponent.Value)));
        }

        /// <summary>
        /// Confirms the service marks the scene dirty instead of throwing when the session has no history service.
        /// </summary>
        [Fact]
        public void RecordRowMutation_WhenNoHistoryServiceIsAttached_MarksSceneMutated() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent component = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(component, component, PlatformId);

            service.RecordRowMutation(row, null);

            Assert.Equal(1, sceneMutationCount);
        }

        /// <summary>
        /// Confirms an entity-scoped record without a snapshot also degrades to marking the scene dirty.
        /// </summary>
        [Fact]
        public void RecordCurrentEntityMutation_WhenNoSnapshotWasCaptured_MarksSceneMutated() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);

            service.RecordCurrentEntityMutation(null);

            Assert.Equal(1, sceneMutationCount);
        }

        /// <summary>
        /// Confirms no snapshot is captured, and the scene is left untouched, when history recording is unavailable.
        /// </summary>
        [Fact]
        public void CaptureCurrentEntityHistoryState_WhenNoHistoryServiceIsAttached_ReturnsNull() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);

            SerializedEditorEntityState state = service.CaptureCurrentEntityHistoryState();

            Assert.Null(state);
            Assert.Equal(0, sceneMutationCount);
        }

        /// <summary>
        /// Confirms a row whose target already differs from the common component is left pointing at its override.
        /// </summary>
        [Fact]
        public void EnsureEditableComponentForRow_WhenRowAlreadyTargetsOverride_LeavesTargetUnchanged() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent commonComponent = new TestInspectorComponent();
            TestInspectorComponent overrideComponent = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(commonComponent, overrideComponent, PlatformId);
            row.SaveComponent = new EntitySaveComponent();
            List<ComponentPropertyRow> rows = new List<ComponentPropertyRow> { row };

            service.EnsureEditableComponentForRow(row, rows);

            Assert.Same(overrideComponent, row.TargetComponent);
        }

        /// <summary>
        /// Confirms a common-scope row is never retargeted, since the common component is the editable component.
        /// </summary>
        [Fact]
        public void EnsureEditableComponentForRow_WhenRowEditsCommonScope_LeavesTargetUnchanged() {
            int sceneMutationCount = 0;
            ComponentPropertyMutationService service = CreateService(() => null, () => sceneMutationCount++);
            TestInspectorComponent commonComponent = new TestInspectorComponent();
            ComponentPropertyRow row = CreateRow(commonComponent, commonComponent, ComponentPlatformEditingService.CommonPlatformId);
            row.SaveComponent = new EntitySaveComponent();
            List<ComponentPropertyRow> rows = new List<ComponentPropertyRow> { row };

            service.EnsureEditableComponentForRow(row, rows);

            Assert.Same(commonComponent, row.TargetComponent);
        }

        /// <summary>
        /// Builds a mutation service with no inspected entity, so history recording always takes the fallback path
        /// unless a test supplies its own history resolver.
        /// </summary>
        /// <param name="historyMutationServiceResolver">Resolver for the session history service.</param>
        /// <param name="markSceneMutated">Callback invoked when detached history is unavailable.</param>
        /// <returns>Service under test.</returns>
        static ComponentPropertyMutationService CreateService(
            Func<EditorMutationService> historyMutationServiceResolver,
            Action markSceneMutated) {
            return new ComponentPropertyMutationService(
                new ComponentPlatformEditingService(),
                platformId => new EditorOverrideScope(platformId),
                () => null,
                historyMutationServiceResolver,
                markSceneMutated);
        }

        /// <summary>
        /// Builds a bare property row carrying only the binding metadata the mutation service reads.
        /// </summary>
        /// <param name="commonComponent">Common component the row edits.</param>
        /// <param name="targetComponent">Component the row currently writes to.</param>
        /// <param name="platformId">Platform context the row edits.</param>
        /// <returns>Row usable by the mutation service without any UI hosting.</returns>
        static ComponentPropertyRow CreateRow(Component commonComponent, Component targetComponent, string platformId) {
            ComponentPropertyRow row = new ComponentPropertyRow(ComponentPropertyRowKind.Scalar, null, null, null);
            row.CommonComponent = commonComponent;
            row.TargetComponent = targetComponent;
            row.EditingPlatformId = platformId;
            return row;
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Owns the non-presentation half of inspector property editing: resolving which component a row may actually
    /// write to on the active platform, keeping the visible rows pointed at that component, persisting the resulting
    /// platform override, and recording the matching undo/redo history entry. The inspector view keeps building and
    /// laying out rows and forwards edits here, so entity mutation no longer lives in a UI class.
    /// </summary>
    public sealed class ComponentPropertyMutationService {
        /// <summary>
        /// Platform override persistence service used for non-common edits.
        /// </summary>
        readonly ComponentPlatformEditingService PlatformEditingService;
        /// <summary>
        /// Resolves the override scope for one platform id, including the active environment.
        /// </summary>
        readonly Func<string, EditorOverrideScope> ScopeResolver;
        /// <summary>
        /// Resolves the editor entity currently shown by the inspector, or null when nothing is inspected.
        /// </summary>
        readonly Func<EditorEntity> CurrentEntityResolver;
        /// <summary>
        /// Resolves the session history service after the view has been composed.
        /// </summary>
        readonly Func<EditorMutationService> HistoryMutationServiceResolver;
        /// <summary>
        /// Marks the scene dirty when no entity history service is available.
        /// </summary>
        readonly Action MarkSceneMutated;

        /// <summary>
        /// Initializes the mutation service with the narrow session services one inspector edit needs.
        /// </summary>
        /// <param name="platformEditingService">Service that materializes and persists scoped component overrides.</param>
        /// <param name="scopeResolver">Resolver that turns a platform id into the active override scope.</param>
        /// <param name="currentEntityResolver">Resolver for the editor entity currently shown by the inspector.</param>
        /// <param name="historyMutationServiceResolver">Resolver for the session-owned history service.</param>
        /// <param name="markSceneMutated">Callback used when detached history is unavailable.</param>
        public ComponentPropertyMutationService(
            ComponentPlatformEditingService platformEditingService,
            Func<string, EditorOverrideScope> scopeResolver,
            Func<EditorEntity> currentEntityResolver,
            Func<EditorMutationService> historyMutationServiceResolver,
            Action markSceneMutated) {
            PlatformEditingService = platformEditingService ?? throw new ArgumentNullException(nameof(platformEditingService));
            ScopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
            CurrentEntityResolver = currentEntityResolver ?? throw new ArgumentNullException(nameof(currentEntityResolver));
            HistoryMutationServiceResolver = historyMutationServiceResolver ?? throw new ArgumentNullException(nameof(historyMutationServiceResolver));
            MarkSceneMutated = markSceneMutated ?? throw new ArgumentNullException(nameof(markSceneMutated));
        }

        /// <summary>
        /// Makes sure a row that still targets the shared common component is retargeted onto the platform-specific
        /// override component before it is written to, so a platform edit never mutates the common value. Rows in the
        /// common scope, rows already retargeted and rows without platform context are left alone.
        /// </summary>
        /// <param name="row">Row whose editable component should be resolved.</param>
        /// <param name="rows">Rows currently visible in the inspector, retargeted together with the supplied row.</param>
        public void EnsureEditableComponentForRow(ComponentPropertyRow row, IReadOnlyList<ComponentPropertyRow> rows) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (rows == null) {
                throw new ArgumentNullException(nameof(rows));
            }
            if (row.CommonComponent == null || row.TargetComponent == null) {
                return;
            }
            if (row.SaveComponent == null || string.IsNullOrWhiteSpace(row.EditingPlatformId)) {
                return;
            }
            if (string.Equals(row.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            if (!ReferenceEquals(row.TargetComponent, row.CommonComponent)) {
                return;
            }

            Component editableOverrideComponent = PlatformEditingService.EnsureScopeOverrideComponent(row.CommonComponent, row.SaveComponent, ScopeResolver(row.EditingPlatformId));
            RetargetRowsForEditableComponent(rows, row.CommonComponent, row.EditingPlatformId, editableOverrideComponent);
        }

        /// <summary>
        /// Points every row that edits the same common component on the same platform at the supplied editable
        /// component, so sibling rows do not keep writing to the component the first edit replaced.
        /// </summary>
        /// <param name="rows">Rows currently visible in the inspector.</param>
        /// <param name="commonComponent">Common live component whose rows should be retargeted.</param>
        /// <param name="platformId">Platform context whose rows should be retargeted.</param>
        /// <param name="editableComponent">Effective editable component that should back those rows.</param>
        public void RetargetRowsForEditableComponent(
            IReadOnlyList<ComponentPropertyRow> rows,
            Component commonComponent,
            string platformId,
            Component editableComponent) {
            if (rows == null) {
                throw new ArgumentNullException(nameof(rows));
            }
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }

            for (int index = 0; index < rows.Count; index++) {
                ComponentPropertyRow activeRow = rows[index];
                if (!ReferenceEquals(activeRow.CommonComponent, commonComponent)) {
                    continue;
                }
                if (!string.Equals(activeRow.EditingPlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                activeRow.TargetComponent = editableComponent;
            }
        }

        /// <summary>
        /// Marks and persists the platform override for one edited row. Rows without platform context, and rows in
        /// the common scope, persist nothing because their value already lives on the shared component.
        /// </summary>
        /// <param name="row">Row whose editable component should be persisted.</param>
        /// <param name="propertyPath">Override property path for the row, or empty when the row marks no path.</param>
        /// <returns>True when an override was persisted and the row override chrome should be refreshed.</returns>
        public bool PersistPlatformOverrideIfNeeded(ComponentPropertyRow row, string propertyPath) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.CommonComponent == null || row.TargetComponent == null) {
                return false;
            }
            if (row.SaveComponent == null || string.IsNullOrWhiteSpace(row.EditingPlatformId)) {
                return false;
            }
            if (string.Equals(row.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            EditorOverrideScope scope = ScopeResolver(row.EditingPlatformId);
            if (!string.IsNullOrWhiteSpace(propertyPath)) {
                PlatformEditingService.MarkScopePropertyOverride(row.CommonComponent, row.SaveComponent, scope, propertyPath);
            }
            PlatformEditingService.PersistScopeOverride(row.CommonComponent, row.TargetComponent, row.SaveComponent, scope);
            return true;
        }

        /// <summary>
        /// Captures one detached history snapshot of the inspected entity before a mutation is applied.
        /// </summary>
        /// <returns>Detached entity snapshot when history recording is available; otherwise null.</returns>
        public SerializedEditorEntityState CaptureCurrentEntityHistoryState() {
            EditorEntity editorEntity = CurrentEntityResolver();
            EditorMutationService historyMutationService = HistoryMutationServiceResolver();
            if (historyMutationService == null || editorEntity == null || editorEntity.IsDisposed) {
                return null;
            }

            return historyMutationService.CaptureEntityState(editorEntity);
        }

        /// <summary>
        /// Records one entity-scoped mutation, falling back to marking the scene dirty when no snapshot or history
        /// service is available.
        /// </summary>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        public void RecordCurrentEntityMutation(SerializedEditorEntityState previousEntityState) {
            EditorEntity editorEntity = CurrentEntityResolver();
            EditorMutationService historyMutationService = HistoryMutationServiceResolver();
            if (previousEntityState == null || historyMutationService == null || editorEntity == null || editorEntity.IsDisposed) {
                MarkSceneMutated();
                return;
            }

            historyMutationService.RecordEntityStateChange(editorEntity, previousEntityState);
        }

        /// <summary>
        /// Records one component-scoped mutation for the row owner, degrading to an entity-scoped record when the row
        /// names no component and to marking the scene dirty when history recording is unavailable.
        /// </summary>
        /// <param name="row">Row whose owning component mutation should be recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        public void RecordRowMutation(ComponentPropertyRow row, SerializedEditorEntityState previousEntityState) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            EditorEntity editorEntity = CurrentEntityResolver();
            EditorMutationService historyMutationService = HistoryMutationServiceResolver();
            if (previousEntityState == null || historyMutationService == null || editorEntity == null || editorEntity.IsDisposed) {
                MarkSceneMutated();
                return;
            }

            Component historyComponent = row.CommonComponent;
            if (historyComponent == null) {
                historyComponent = row.TargetComponent;
            }
            if (historyComponent == null) {
                historyMutationService.RecordEntityStateChange(editorEntity, previousEntityState);
                return;
            }

            historyMutationService.RecordComponentMutation(editorEntity, historyComponent, previousEntityState);
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Owns the mutable editor interaction state for one live editor session.
    /// Every UI, tool, and scene component resolves this graph from its owning
    /// entity rather than publishing state through process-wide services.
    /// </summary>
    public sealed class EditorSessionInteractionServices : IDisposable {
        /// <summary>
        /// Resolves the interaction graph captured by an entity's owning editor core.
        /// This is an explicit owner lookup; it never consults ambient process state.
        /// </summary>
        public static EditorSessionInteractionServices From(global::helengine.Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            global::helengine.Entity current = entity;
            while (current != null) {
                if (current is global::helengine.EditorEntity editorEntity && editorEntity.InteractionServices != null) {
                    return editorEntity.InteractionServices;
                }
                current = current.Parent;
            }

            throw new InvalidOperationException("The entity is not attached to an editor session interaction graph.");
        }

        public EditorKeyboardFocusService KeyboardFocus { get; } = new EditorKeyboardFocusService();
        public EditorInputCaptureService InputCapture { get; } = new EditorInputCaptureService();
        public EditorSelectionService Selection { get; } = new EditorSelectionService();
        public EditorSceneMutationService SceneMutation { get; } = new EditorSceneMutationService();
        public EntityPlatformExistenceEditingService EntityExistence { get; } = new EntityPlatformExistenceEditingService();
        public ComponentEditorRegistry ComponentEditors { get; } = new ComponentEditorRegistry();
        public EditorAssetPickerService AssetPicker { get; } = new EditorAssetPickerService();
        public EditorMeshModifierPickerService MeshModifierPicker { get; } = new EditorMeshModifierPickerService();
        public EditorEntityHistoryMutationService EntityHistory { get; } = new EditorEntityHistoryMutationService();
        public EditorComponentHistoryMutationService ComponentHistory { get; } = new EditorComponentHistoryMutationService();
        public EditorGizmoHoverService GizmoHover { get; } = new EditorGizmoHoverService();
        public EditorGizmoDragService GizmoDrag { get; } = new EditorGizmoDragService();
        public EditorTranslationGizmoFollowRegistry TranslationGizmoFollow { get; } = new EditorTranslationGizmoFollowRegistry();
        public EditorViewportToolService ViewportTool { get; } = new EditorViewportToolService();
        public TransformGizmoSnapSettingsService TransformSnap { get; } = new TransformGizmoSnapSettingsService();
        public EditorWorldSpace2DPreviewRegistry WorldSpace2DPreviewRegistry { get; } = new EditorWorldSpace2DPreviewRegistry();
        public EditorContextMenuService ContextMenus { get; } = new EditorContextMenuService();

        bool IsDisposed;

        /// <summary>
        /// Releases every mutable interaction state owned by this session.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            List<Exception> failures = new List<Exception>();
            DisposeOne(WorldSpace2DPreviewRegistry, failures);
            DisposeOne(ContextMenus, failures);
            DisposeOne(TransformSnap, failures);
            DisposeOne(ViewportTool, failures);
            DisposeOne(GizmoDrag, failures);
            DisposeOne(TranslationGizmoFollow, failures);
            DisposeOne(GizmoHover, failures);
            DisposeOne(ComponentHistory, failures);
            DisposeOne(EntityHistory, failures);
            DisposeOne(MeshModifierPicker, failures);
            DisposeOne(AssetPicker, failures);
            DisposeOne(SceneMutation, failures);
            DisposeOne(EntityExistence, failures);
            DisposeOne(ComponentEditors, failures);
            DisposeOne(Selection, failures);
            DisposeOne(InputCapture, failures);
            DisposeOne(KeyboardFocus, failures);
            if (failures.Count != 0) {
                throw failures.Count == 1
                    ? failures[0]
                    : new AggregateException("Editor interaction state disposal failed.", failures);
            }

            IsDisposed = true;
        }

        static void DisposeOne(IDisposable service, List<Exception> failures) {
            try {
                service.Dispose();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
    }
}

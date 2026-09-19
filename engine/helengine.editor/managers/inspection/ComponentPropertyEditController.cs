namespace helengine.editor {
    /// <summary>
    /// Owns validation, persistence and history recording for reflected component property edits.
    /// </summary>
    public sealed class ComponentPropertyEditController {
        /// <summary>
        /// Platform override persistence service used for non-common edits.
        /// </summary>
        readonly ComponentPlatformEditingService PlatformEditingService;
        /// <summary>
        /// Resolves the current session history service after the view has been composed.
        /// </summary>
        readonly Func<EditorMutationService> HistoryMutationServiceResolver;
        /// <summary>
        /// Marks the scene dirty when no entity history service is available.
        /// </summary>
        readonly Action MarkSceneMutated;
        /// <summary>
        /// Refreshes runtime presentation after a committed value.
        /// </summary>
        readonly Action<ComponentPropertyEditRequest> RefreshPresentation;

        /// <summary>
        /// Initializes a property edit controller with the narrow services required by one mutation.
        /// </summary>
        /// <param name="platformEditingService">Service that persists scoped component overrides.</param>
        /// <param name="historyMutationServiceResolver">Resolver for the session-owned history service.</param>
        /// <param name="markSceneMutated">Callback used when detached history is unavailable.</param>
        /// <param name="refreshPresentation">Callback that refreshes presentation after persistence and history.</param>
        public ComponentPropertyEditController(
            ComponentPlatformEditingService platformEditingService,
            Func<EditorMutationService> historyMutationServiceResolver,
            Action markSceneMutated,
            Action<ComponentPropertyEditRequest> refreshPresentation) {
            PlatformEditingService = platformEditingService ?? throw new ArgumentNullException(nameof(platformEditingService));
            HistoryMutationServiceResolver = historyMutationServiceResolver ?? throw new ArgumentNullException(nameof(historyMutationServiceResolver));
            MarkSceneMutated = markSceneMutated ?? throw new ArgumentNullException(nameof(markSceneMutated));
            RefreshPresentation = refreshPresentation ?? throw new ArgumentNullException(nameof(refreshPresentation));
        }

        /// <summary>
        /// Applies one validated reflected property edit, persists its override and records one history mutation.
        /// </summary>
        /// <param name="request">Edit request containing the exact target and scope captured by the inspector.</param>
        public void Apply(ComponentPropertyEditRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateRequest(request);
            object currentValue = request.Property.GetValue(request.TargetComponent);
            if (Equals(currentValue, request.Value)) {
                return;
            }

            EditorMutationService historyMutationService = HistoryMutationServiceResolver();
            SerializedEditorEntityState previousEntityState = CaptureHistoryState(request, historyMutationService);
            request.Property.SetValue(request.TargetComponent, request.Value);
            PersistOverride(request);
            RecordMutation(request, historyMutationService, previousEntityState);
            RefreshPresentation(request);
        }

        /// <summary>
        /// Validates request lifetime, writability and value compatibility before any state is captured or changed.
        /// </summary>
        /// <param name="request">Request to validate.</param>
        void ValidateRequest(ComponentPropertyEditRequest request) {
            if (request.IsReadOnly) {
                throw new InvalidOperationException("Read-only inspector requests cannot mutate component properties.");
            }
            if (request.TargetComponent.IsDisposed) {
                throw new InvalidOperationException("The target component has been disposed.");
            }
            if (request.OwnerEntity != null && request.OwnerEntity.IsDisposed) {
                throw new InvalidOperationException("The owning editor entity has been disposed.");
            }
            if (!request.Property.CanWrite || request.Property.SetMethod == null || !request.Property.SetMethod.IsPublic) {
                throw new InvalidOperationException($"Component member '{request.MemberName}' is not writable.");
            }
            if (!request.Property.DeclaringType.IsAssignableFrom(request.TargetComponent.GetType())) {
                throw new InvalidOperationException($"Component member '{request.MemberName}' does not belong to the target component.");
            }
            if (request.Value == null) {
                if (request.Property.PropertyType.IsValueType && Nullable.GetUnderlyingType(request.Property.PropertyType) == null) {
                    throw new ArgumentException($"Component member '{request.MemberName}' does not accept null.", nameof(request));
                }
            } else if (!request.Property.PropertyType.IsInstanceOfType(request.Value)) {
                throw new ArgumentException($"Value for component member '{request.MemberName}' is not assignable to {request.Property.PropertyType.Name}.", nameof(request));
            }
            bool isCommonScope = request.Scope.IsCommon;
            if (!isCommonScope
                && (request.CommonComponent == null || request.SaveComponent == null || string.IsNullOrWhiteSpace(request.PropertyPath))) {
                throw new InvalidOperationException("Scoped property edits require a common component, save component and serialized property path.");
            }
        }

        /// <summary>
        /// Captures the detached entity state when the request belongs to a live editor entity.
        /// </summary>
        /// <param name="request">Request whose owner should be captured.</param>
        /// <param name="historyMutationService">Current history service.</param>
        /// <returns>Detached state, or null when history is unavailable.</returns>
        SerializedEditorEntityState CaptureHistoryState(ComponentPropertyEditRequest request, EditorMutationService historyMutationService) {
            if (historyMutationService == null || request.OwnerEntity == null || request.OwnerEntity.IsDisposed) {
                return null;
            }

            return historyMutationService.CaptureEntityState(request.OwnerEntity);
        }

        /// <summary>
        /// Persists the edited target into its captured non-common override scope.
        /// </summary>
        /// <param name="request">Request describing the edited target and scope.</param>
        void PersistOverride(ComponentPropertyEditRequest request) {
            if (request.Scope.IsCommon) {
                return;
            }

            PlatformEditingService.MarkScopePropertyOverride(request.CommonComponent, request.SaveComponent, request.Scope, request.PropertyPath);
            PlatformEditingService.PersistScopeOverride(request.CommonComponent, request.TargetComponent, request.SaveComponent, request.Scope);
        }

        /// <summary>
        /// Records one component-scoped mutation or emits the plain dirty notification when no history service is attached.
        /// </summary>
        /// <param name="request">Request that was applied.</param>
        /// <param name="historyMutationService">Current history service.</param>
        /// <param name="previousEntityState">Detached state captured before the edit.</param>
        void RecordMutation(
            ComponentPropertyEditRequest request,
            EditorMutationService historyMutationService,
            SerializedEditorEntityState previousEntityState) {
            if (historyMutationService == null || previousEntityState == null || request.OwnerEntity == null || request.OwnerEntity.IsDisposed) {
                MarkSceneMutated();
                return;
            }

            Component historyComponent = request.CommonComponent ?? request.TargetComponent;
            historyMutationService.RecordComponentMutation(request.OwnerEntity, historyComponent, previousEntityState);
        }
    }
}

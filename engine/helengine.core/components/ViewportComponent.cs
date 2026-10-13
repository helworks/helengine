namespace helengine {
    /// <summary>
    /// Exposes a reusable logical viewport that can follow the current screen or a camera and can optionally scale one authored subtree against a reference canvas.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public class ViewportComponent : UpdateComponent, IAnchorBoundsProvider, ICameraBoundViewportOwner, IAuthoredLayoutOwner {
        /// <summary>
        /// Binding mode that resolves the viewport from the current screen size.
        /// </summary>
        public const byte ScreenBindingMode = 0;

        /// <summary>
        /// Binding mode that resolves the viewport from the nearest ancestor camera component.
        /// </summary>
        public const byte AncestorCameraBindingMode = 1;

        /// <summary>
        /// Binding mode that resolves the viewport from the authored fixed size.
        /// </summary>
        public const byte FixedBindingMode = 2;

        /// <summary>
        /// Binding mode that resolves the viewport from one explicitly assigned camera component.
        /// </summary>
        public const byte ExplicitCameraBindingMode = 3;

        /// <summary>
        /// Scaling mode that leaves authored positions and sizes unchanged.
        /// </summary>
        public const byte NoScalingMode = 0;

        /// <summary>
        /// Scaling mode that fits the authored subtree into the resolved viewport using one reference canvas.
        /// </summary>
        public const byte ReferenceCanvasScalingMode = 1;

        /// <summary>
        /// Scaling mode that fills the resolved viewport and scales the authored subtree independently on each axis.
        /// </summary>
        public const byte ReferenceCanvasStretchScalingMode = 2;

        /// <summary>Scaling mode that converts authored pixels to display pixels using the host DPI and the viewport's pixels per centimeter.</summary>
        public const byte DensityScalingMode = 3;

        /// <summary>Authored pixel density; the initial value corresponds to a 160 DPI reference canvas.</summary>
        float PixelsPerCentimeterValue = (float)(160d / 2.54d);

        /// <summary>User-selected size multiplier applied after physical density conversion.</summary>
        float UiScaleValue = 1f;

        /// <summary>
        /// Stores the selected viewport binding mode.
        /// </summary>
        byte BindingModeValue;

        /// <summary>
        /// Stores the authored fixed viewport size used by fixed and fallback bindings.
        /// </summary>
        int2 FixedSizeValue;

        /// <summary>
        /// Stores the active camera currently driving this viewport.
        /// </summary>
        CameraComponent ActiveCameraComponentValue;

        /// <summary>The renderer currently publishing layout changes; detached object initializers have no host subscription.</summary>
        RenderManager3D LayoutRenderer;

        /// <summary>
        /// Stores the explicitly assigned camera used by the explicit camera binding mode.
        /// </summary>
        CameraComponent ExplicitBoundCameraComponentValue;

        /// <summary>
        /// Stores the selected scaling mode.
        /// </summary>
        byte ScalingModeValue;

        /// <summary>
        /// Stores the authored reference canvas width used by viewport-owned scaling.
        /// </summary>
        int ReferenceWidthValue;

        /// <summary>
        /// Stores the authored reference canvas height used by viewport-owned scaling.
        /// </summary>
        int ReferenceHeightValue;

        /// <summary>
        /// Stores the authored layout snapshots captured for the current subtree.
        /// </summary>
        readonly List<ViewportLayoutSnapshot> LayoutSnapshotsValue;

        /// <summary>
        /// Stores, in editor authoring only, the live layout values written by the last scale pass so edits made in the scaled
        /// preview can be told apart from viewport output and mapped back into authored space.
        /// </summary>
        readonly List<ViewportLayoutSnapshot> AppliedLayoutSnapshotsValue;

        /// <summary>
        /// Stores the root displacement used by the last recorded editor scale pass.
        /// </summary>
        float2 AppliedCanvasOriginValue;

        /// <summary>
        /// Stores the horizontal scale used by the last recorded editor scale pass.
        /// </summary>
        double AppliedWidthScaleValue;

        /// <summary>
        /// Stores the vertical scale used by the last recorded editor scale pass.
        /// </summary>
        double AppliedHeightScaleValue;

        /// <summary>
        /// Stores the uniform glyph and decoration scale used by the last recorded editor scale pass.
        /// </summary>
        double AppliedScalarScaleValue;

        /// <summary>
        /// True while <see cref="RestoreAuthoredLayout"/> re-anchors descendants; the full-viewport anchor space then resolves
        /// to the authored reference canvas instead of the live editor window.
        /// </summary>
        bool RestoringAuthoredLayoutValue;

        /// <summary>
        /// Tracks whether the component is subscribed to screen resize events.
        /// </summary>
        bool IsSubscribedToWindowResizeValue;

        /// <summary>
        /// Tracks whether the authored subtree should be re-evaluated on the next update.
        /// </summary>
        bool PendingScaleApplyValue;

        /// <summary>
        /// Tracks the number of entities captured in the current viewport scaling snapshot set.
        /// </summary>
        int SnapshotEntityCountValue;

        /// <summary>
        /// Tracks the current anchor space exposed to descendants when viewport scaling is active.
        /// </summary>
        AnchorSpace CurrentAnchorSpaceValue;

        /// <summary>
        /// Tracks the current full-viewport anchor space exposed to descendants that explicitly answer to the camera viewport rect.
        /// </summary>
        AnchorSpace CurrentViewportAnchorSpaceValue;

        /// <summary>
        /// Tracks the current origin applied to the scaled subtree root.
        /// </summary>
        float2 CurrentCanvasOriginValue;

        /// <summary>
        /// Raises when the resolved viewport bounds change.
        /// </summary>
        public event Action AnchorBoundsChanged;

        /// <summary>
        /// Initializes a new viewport component with screen binding and a 720p authored fallback size.
        /// </summary>
        public ViewportComponent() {
            BindingModeValue = ScreenBindingMode;
            FixedSizeValue = new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight);
            ReferenceWidthValue = SceneCanvasProfile.DefaultWidth;
            ReferenceHeightValue = SceneCanvasProfile.DefaultHeight;
            LayoutSnapshotsValue = new List<ViewportLayoutSnapshot>();
            AppliedLayoutSnapshotsValue = new List<ViewportLayoutSnapshot>();
            CurrentAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentViewportAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentCanvasOriginValue = new float2(0f, 0f);
        }

        /// <summary>
        /// Gets or sets the active viewport binding mode.
        /// </summary>
        public byte BindingMode {
            get { return BindingModeValue; }
            set {
                if (BindingModeValue != value) {
                    BindingModeValue = value;
                    RefreshSubscriptions();
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the fixed viewport size used by fixed bindings and fallbacks.
        /// </summary>
        public int2 FixedSize {
            get { return FixedSizeValue; }
            set {
                if (FixedSizeValue.X != value.X || FixedSizeValue.Y != value.Y) {
                    FixedSizeValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the scaling mode used to adapt the authored subtree to the resolved viewport.
        /// </summary>
        public byte ScalingMode {
            get { return ScalingModeValue; }
            set {
                if (ScalingModeValue != value) {
                    ScalingModeValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>Gets or sets how many authored viewport pixels represent one centimeter in density mode; host DPI determines the approximate physical conversion.</summary>
        public float PixelsPerCentimeter {
            get { return PixelsPerCentimeterValue; }
            set {
                if (!(value > 0f) || float.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Pixels per centimeter must be finite and positive.");
                }
                if (PixelsPerCentimeterValue != value) {
                    PixelsPerCentimeterValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>Gets or sets an additional positive UI size multiplier for density scaling.</summary>
        public float UiScale {
            get { return UiScaleValue; }
            set {
                if (!(value > 0f) || float.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value), "UI scale must be finite and positive.");
                }
                if (UiScaleValue != value) {
                    UiScaleValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>Gets physical pixels per authored pixel in density mode, using the screen or bound camera's named output view.</summary>
        [ScenePersistenceIgnore]
        public float DensityScale {
            get {
                RefreshSubscriptions();
                string tag = ActiveCameraComponentValue == null || ActiveCameraComponentValue.OutputWindowTag.Length == 0
                    ? "main" : ActiveCameraComponentValue.OutputWindowTag;
                return (float)(OwnerCore.RenderManager3D.GetWindowDensityDpi(tag) / (PixelsPerCentimeterValue * 2.54d) * UiScaleValue);
            }
        }

        /// <summary>
        /// Gets or sets the authored reference canvas width used by viewport-owned scaling.
        /// </summary>
        public int ReferenceWidth {
            get { return ReferenceWidthValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Reference width must be at least one.");
                }
                if (ReferenceWidthValue != value) {
                    ReferenceWidthValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the authored reference canvas height used by viewport-owned scaling.
        /// </summary>
        public int ReferenceHeight {
            get { return ReferenceHeightValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Reference height must be at least one.");
                }
                if (ReferenceHeightValue != value) {
                    ReferenceHeightValue = value;
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the explicitly assigned camera used by the explicit camera binding mode.
        /// This live binding is runtime-only and is excluded from reflected scene persistence.
        /// </summary>
        [ScenePersistenceIgnore]
        public CameraComponent BoundCameraComponent {
            get { return ExplicitBoundCameraComponentValue; }
            set {
                if (!ReferenceEquals(ExplicitBoundCameraComponentValue, value)) {
                    ExplicitBoundCameraComponentValue = value;
                    RefreshSubscriptions();
                    PendingScaleApplyValue = true;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets the resolved viewport anchor space in local pixels.
        /// </summary>
        public AnchorSpace AnchorSpace {
            get {
                RefreshSubscriptions();
                if (IsReferenceCanvasScalingActive()) {
                    return CurrentAnchorSpaceValue;
                }

                CurrentAnchorSpaceValue.Update(ResolveAnchorBounds(), new float2(0f, 0f));
                return CurrentAnchorSpaceValue;
            }
        }

        /// <summary>
        /// Gets the resolved full viewport anchor space in local pixels regardless of any reference-canvas fitting applied to the subtree.
        /// </summary>
        public AnchorSpace ViewportAnchorSpace {
            get {
                RefreshSubscriptions();
                CurrentViewportAnchorSpaceValue.Update(ResolveAnchorBounds(), ResolveViewportAnchorOrigin());
                return CurrentViewportAnchorSpaceValue;
            }
        }

        /// <summary>
        /// Gets the resolved viewport rectangle in pixel-space coordinates.
        /// </summary>
        public float4 ResolvedViewportBounds {
            get {
                RefreshSubscriptions();
                return ResolveViewportBounds();
            }
        }

        /// <summary>
        /// Gets the resolved viewport size in pixels.
        /// </summary>
        public int2 ResolvedViewportSize {
            get {
                float4 viewport = ResolvedViewportBounds;
                return new int2(
                    Math.Max(1, (int)Math.Round(viewport.Z)),
                    Math.Max(1, (int)Math.Round(viewport.W)));
            }
        }

        /// <summary>
        /// Rebinds the viewport listeners when the component is attached to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RefreshSubscriptions();
            RebuildSnapshots();
            PendingScaleApplyValue = true;
        }

        /// <summary>
        /// Releases viewport subscriptions when the component is removed from its entity.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DetachFromCamera();
            DetachFromWindowResize();
            DetachFromWindowLayout();
            ReleaseLayoutSnapshotItems();
            SnapshotEntityCountValue = 0;
            PendingScaleApplyValue = false;
        }

        /// <summary>
        /// Releases viewport-owned snapshot records and subscriptions before the native backend deletes this component.
        /// </summary>
        public override void Dispose() {
            DetachFromCamera();
            DetachFromWindowResize();
            DetachFromWindowLayout();
            ReleaseLayoutSnapshots();
            NativeOwnership.Delete(CurrentAnchorSpaceValue);
            NativeOwnership.Delete(CurrentViewportAnchorSpaceValue);
            ActiveCameraComponentValue = null;
            ExplicitBoundCameraComponentValue = null;
            CurrentAnchorSpaceValue = null;
            CurrentViewportAnchorSpaceValue = null;
            base.Dispose();
        }

        /// <summary>
        /// Rebinds the viewport listeners when the parent entity changes enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                RefreshSubscriptions();
                PendingScaleApplyValue = true;
                RaiseAnchorBoundsChanged();
            } else {
                DetachFromCamera();
                DetachFromWindowResize();
                DetachFromWindowLayout();
            }
        }

        /// <summary>Captures the fully initialized scene subtree before its first published frame, including components added after this viewport attached.</summary>
        internal void PrepareSceneLayout() {
            RebuildSnapshots();
            PendingScaleApplyValue = true;
            ApplyPendingSceneLayout();
        }

        /// <summary>Rebuilds viewport scaling snapshots after subtree changes and reapplies pending layout changes.</summary>
        public override void Update() {
            base.Update();
            ApplyPendingSceneLayout();
        }

        /// <summary>Runs builtin viewport layout without dispatching a derived gameplay Update during scene preparation.</summary>
        void ApplyPendingSceneLayout() {
            RefreshSubscriptions();

            if (Parent == null || !IsReferenceCanvasScalingActive()) {
                return;
            }

            bool tracksAuthoredEdits = IsEditorAuthoring();
            bool subtreeChanged = tracksAuthoredEdits
                ? MatchSnapshotsRecursive(Parent, 0) != LayoutSnapshotsValue.Count
                : CountEntitiesRecursive(Parent) != SnapshotEntityCountValue;
            if (!subtreeChanged && !PendingScaleApplyValue) {
                return;
            }

            if (tracksAuthoredEdits) {
                RestoreAuthoredLayout();
            }

            if (subtreeChanged) {
                RebuildSnapshots();
                PendingScaleApplyValue = true;
            }

            if (PendingScaleApplyValue) {
                ApplyCurrentScale();
                PendingScaleApplyValue = false;
                if (tracksAuthoredEdits) {
                    RecordAppliedLayout();
                }
            }
        }

        /// <summary>
        /// Writes the authored, unscaled layout back onto the scaled subtree so it can be serialized as authored data, and
        /// re-captures the authored snapshots from it. Contract: any tracked live value that differs from what the last
        /// recorded scale pass wrote ("live != last applied") counts as a user edit made in the scaled editor preview and is
        /// mapped back into authored space; unchanged values resolve to the captured authored values. Anchored descendants are
        /// then re-anchored against the reference canvas so their derived positions do not depend on the editor window. Entities that no recorded scale pass has touched already hold authored values
        /// and are only re-captured. Runtime hosts never record a preview pass, so outside the editor this does nothing.
        /// Call <see cref="ReapplyLayout"/> afterwards to restore the scaled preview.
        /// </summary>
        public void RestoreAuthoredLayout() {
            if (!IsEditorAuthoring() || Parent == null || !IsReferenceCanvasScalingActive() || LayoutSnapshotsValue.Count == 0) {
                return;
            }

            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                ViewportLayoutSnapshot snapshot = LayoutSnapshotsValue[snapshotIndex];
                ViewportLayoutSnapshot applied = FindAppliedSnapshot(snapshot.Entity);
                if (applied != null && IsInScaledSubtree(snapshot.Entity)) {
                    snapshot.RestoreAuthoredComponents(
                        applied,
                        AppliedWidthScaleValue,
                        AppliedHeightScaleValue,
                        AppliedScalarScaleValue);
                }
            }

            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                ViewportLayoutSnapshot snapshot = LayoutSnapshotsValue[snapshotIndex];
                ViewportLayoutSnapshot applied = FindAppliedSnapshot(snapshot.Entity);
                if (applied != null && IsInScaledSubtree(snapshot.Entity)) {
                    snapshot.RestoreAuthoredPosition(
                        applied,
                        AppliedCanvasOriginValue,
                        AppliedWidthScaleValue,
                        AppliedHeightScaleValue);
                }
            }

            if (ScalingModeValue != DensityScalingMode) {
                // Anchored axes are derived from the anchor space, and whatever the editor window or a load-time fallback
                // provider produced must not be persisted: re-anchor against the authored reference canvas so the saved
                // positions and stretched sizes are identical for every editor window size.
                // Anchors answering to the full viewport rect resolve through ViewportAnchorSpace, which follows the same
                // reference canvas while RestoringAuthoredLayoutValue is set.
                CurrentAnchorSpaceValue.Update(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
                RestoringAuthoredLayoutValue = true;
                try {
                    for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                        ViewportLayoutSnapshot snapshot = LayoutSnapshotsValue[snapshotIndex];
                        if (!snapshot.IsRootEntity && IsInScaledSubtree(snapshot.Entity)) {
                            snapshot.RefreshAnchoring();
                        }
                    }
                } finally {
                    RestoringAuthoredLayoutValue = false;
                }
            }

            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                ViewportLayoutSnapshot snapshot = LayoutSnapshotsValue[snapshotIndex];
                if (IsInScaledSubtree(snapshot.Entity)) {
                    LayoutSnapshotsValue[snapshotIndex] = new ViewportLayoutSnapshot(snapshot.Entity, snapshot.IsRootEntity);
                }
            }

            AppliedLayoutSnapshotsValue.Clear();
            PendingScaleApplyValue = true;
        }

        /// <summary>
        /// Re-applies the viewport scale to the subtree, typically after <see cref="RestoreAuthoredLayout"/> exposed the authored values for serialization.
        /// </summary>
        public void ReapplyLayout() {
            PendingScaleApplyValue = true;
            ApplyPendingSceneLayout();
        }

        /// <summary>
        /// Returns whether this viewport runs inside the editor, where the scaled subtree is authored data that must stay
        /// recoverable in authored space. Runtime hosts never record or absorb preview edits.
        /// </summary>
        /// <returns>True when the owning core authors scenes in the editor.</returns>
        bool IsEditorAuthoring() {
            return OwnerCore != null && OwnerCore.ExecutionMode == ComponentExecutionMode.Editor;
        }

        /// <summary>
        /// Records the live values and scales written by the scale pass that just ran, so later preview edits can be detected and unscaled.
        /// The scales mirror the ones <see cref="ViewportLayoutSnapshot"/> derives from the same anchor space and reference canvas.
        /// </summary>
        void RecordAppliedLayout() {
            AppliedLayoutSnapshotsValue.Clear();
            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                ViewportLayoutSnapshot snapshot = LayoutSnapshotsValue[snapshotIndex];
                AppliedLayoutSnapshotsValue.Add(new ViewportLayoutSnapshot(snapshot.Entity, snapshot.IsRootEntity));
            }

            if (ScalingModeValue == DensityScalingMode) {
                double densityScale = DensityScale;
                AppliedCanvasOriginValue = new float2(0f, 0f);
                AppliedWidthScaleValue = densityScale;
                AppliedHeightScaleValue = densityScale;
                AppliedScalarScaleValue = densityScale;
                return;
            }

            AppliedCanvasOriginValue = CurrentCanvasOriginValue;
            AppliedWidthScaleValue = (double)CurrentAnchorSpaceValue.Size.X / ReferenceWidthValue;
            AppliedHeightScaleValue = (double)CurrentAnchorSpaceValue.Size.Y / ReferenceHeightValue;
            AppliedScalarScaleValue = ScalingModeValue == ReferenceCanvasStretchScalingMode
                ? AppliedHeightScaleValue
                : Math.Min(AppliedWidthScaleValue, AppliedHeightScaleValue);
        }

        /// <summary>
        /// Finds the live values recorded for one entity by the last editor scale pass.
        /// </summary>
        /// <param name="entity">Entity whose recorded values are requested.</param>
        /// <returns>The recorded values owned by this viewport, or null when the entity was not part of the last recorded pass.</returns>
        [NativeBorrowedReturn]
        ViewportLayoutSnapshot FindAppliedSnapshot(Entity entity) {
            for (int index = 0; index < AppliedLayoutSnapshotsValue.Count; index++) {
                if (ReferenceEquals(AppliedLayoutSnapshotsValue[index].Entity, entity)) {
                    return AppliedLayoutSnapshotsValue[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether one snapshotted entity is still attached below this viewport's owner, so removed or replaced entities are never written.
        /// </summary>
        /// <param name="entity">Entity captured by a snapshot.</param>
        /// <returns>True when the entity is the owner or one of its current descendants.</returns>
        bool IsInScaledSubtree(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (ReferenceEquals(current, Parent)) {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Walks the captured subtree in snapshot order and checks that every entity still matches its snapshot, so the editor
        /// notices replaced entities (for example after undo) even when the entity count did not change.
        /// </summary>
        /// <param name="entity">Current entity to match.</param>
        /// <param name="snapshotIndex">Snapshot index expected for the entity.</param>
        /// <returns>The next snapshot index after the matched subtree, or -1 when the subtree differs from the snapshots.</returns>
        int MatchSnapshotsRecursive(Entity entity, int snapshotIndex) {
            if (snapshotIndex >= LayoutSnapshotsValue.Count || !ReferenceEquals(LayoutSnapshotsValue[snapshotIndex].Entity, entity)) {
                return -1;
            }

            int nextIndex = snapshotIndex + 1;
            if (entity.Children == null) {
                return nextIndex;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (ShouldCaptureChild(entity.Children[childIndex])) {
                    nextIndex = MatchSnapshotsRecursive(entity.Children[childIndex], nextIndex);
                    if (nextIndex < 0) {
                        return -1;
                    }
                }
            }

            return nextIndex;
        }

        /// <summary>
        /// Updates event subscriptions for the current binding mode.
        /// </summary>
        void RefreshSubscriptions() {
            if (BindingModeValue == ScreenBindingMode) {
                DetachFromCamera();
                AttachToWindowResize();
            } else if (BindingModeValue == AncestorCameraBindingMode) {
                DetachFromWindowResize();
                CameraComponent nextCameraComponent = ResolveAncestorCameraComponent();
                if (!ReferenceEquals(ActiveCameraComponentValue, nextCameraComponent)) {
                    DetachFromCamera();
                    ActiveCameraComponentValue = nextCameraComponent;
                    AttachToCamera();
                }
            } else if (BindingModeValue == ExplicitCameraBindingMode) {
                DetachFromWindowResize();
                if (!ReferenceEquals(ActiveCameraComponentValue, ExplicitBoundCameraComponentValue)) {
                    DetachFromCamera();
                    ActiveCameraComponentValue = ExplicitBoundCameraComponentValue;
                    AttachToCamera();
                }
            } else {
                DetachFromCamera();
                DetachFromWindowResize();
            }
            RefreshWindowLayoutSubscription();
        }

        /// <summary>Registers named layout notifications once both camera binding and component ownership have been initialized.</summary>
        void RefreshWindowLayoutSubscription() {
            RenderManager3D renderer = OwnerCore != null && (ActiveCameraComponentValue != null || BindingModeValue == ScreenBindingMode)
                ? OwnerCore.RenderManager3D : null;
            if (!ReferenceEquals(LayoutRenderer, renderer)) {
                if (LayoutRenderer != null) {
                    LayoutRenderer.WindowLayoutChanged -= HandleCameraWindowLayoutChanged;
                }
                LayoutRenderer = renderer;
                if (LayoutRenderer != null) {
                    LayoutRenderer.WindowLayoutChanged += HandleCameraWindowLayoutChanged;
                }
            }
        }

        /// <summary>Releases host layout notifications when this viewport leaves its scene or is disposed.</summary>
        void DetachFromWindowLayout() {
            if (LayoutRenderer != null) {
                LayoutRenderer.WindowLayoutChanged -= HandleCameraWindowLayoutChanged;
                LayoutRenderer = null;
            }
        }

        /// <summary>
        /// Attaches to the current ancestor camera viewport changes when one exists.
        /// </summary>
        void AttachToCamera() {
            if (ActiveCameraComponentValue == null) {
                return;
            }

            ActiveCameraComponentValue.ViewportChanged += HandleCameraViewportChanged;
            ActiveCameraComponentValue.OutputWindowTargetChanged += HandleCameraViewportChanged;
        }

        /// <summary>
        /// Detaches from the current ancestor camera viewport changes when one is active.
        /// </summary>
        void DetachFromCamera() {
            if (ActiveCameraComponentValue != null) {
                ActiveCameraComponentValue.ViewportChanged -= HandleCameraViewportChanged;
                ActiveCameraComponentValue.OutputWindowTargetChanged -= HandleCameraViewportChanged;
                ActiveCameraComponentValue = null;
            }
            RefreshWindowLayoutSubscription();
        }

        /// <summary>
        /// Attaches the screen resize fallback when it is not already active.
        /// </summary>
        void AttachToWindowResize() {
            if (IsSubscribedToWindowResizeValue) {
                return;
            }

            OwnerCore.RenderManager3D.WindowResized += HandleWindowResized;
            IsSubscribedToWindowResizeValue = true;
        }

        /// <summary>
        /// Detaches the screen resize fallback when it is active.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResizeValue) {
                return;
            }

            OwnerCore.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResizeValue = false;
        }

        /// <summary>
        /// Handles ancestor camera viewport changes.
        /// </summary>
        void HandleCameraViewportChanged() {
            PendingScaleApplyValue = true;
            RaiseAnchorBoundsChanged();
        }

        /// <summary>Invalidates only the viewport whose camera uses the named view that changed size.</summary>
        /// <param name="tag">The named view with updated usable layout dimensions.</param>
        void HandleCameraWindowLayoutChanged(string tag) {
            string target = BindingModeValue == ScreenBindingMode ? "main" : CameraWindowLayoutUtils.ResolveWindowTag(this, ActiveCameraComponentValue);
            if (string.Equals(target, tag, StringComparison.OrdinalIgnoreCase)) {
                PendingScaleApplyValue = true;
                RaiseAnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Handles window resize notifications for the screen binding mode.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated window width.</param>
        /// <param name="newHeight">Updated window height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            if (handle != OwnerCore.RenderManager3D.MainWindowHandle) {
                return;
            }
            PendingScaleApplyValue = true;
            RaiseAnchorBoundsChanged();
        }

        /// <summary>
        /// Resolves the active viewport bounds without forcing another subscription refresh.
        /// </summary>
        /// <returns>Resolved viewport bounds in local pixels.</returns>
        int2 ResolveAnchorBounds() {
            if (RestoringAuthoredLayoutValue) {
                return new int2(ReferenceWidthValue, ReferenceHeightValue);
            }

            float4 viewport = ResolveViewportBounds();
            return new int2(
                Math.Max(1, (int)Math.Round(viewport.Z)),
                Math.Max(1, (int)Math.Round(viewport.W)));
        }

        /// <summary>
        /// Resolves the active viewport rectangle without forcing another subscription refresh.
        /// </summary>
        /// <returns>Resolved viewport rectangle in pixel-space coordinates.</returns>
        float4 ResolveViewportBounds() {
            if (BindingModeValue == ScreenBindingMode) {
                int2 screenSize = OwnerCore.RenderManager3D.MainWindowSize;
                if (screenSize.X > 0 && screenSize.Y > 0) {
                    return new float4(0f, 0f, screenSize.X, screenSize.Y);
                }

                return new float4(0f, 0f, FixedSizeValue.X, FixedSizeValue.Y);
            }

            if (BindingModeValue == AncestorCameraBindingMode || BindingModeValue == ExplicitCameraBindingMode) {
                CameraComponent cameraComponent = ResolveBoundCameraComponent();
                if (cameraComponent != null) {
                    return ResolveCameraViewportBounds(cameraComponent);
                }

                return new float4(0f, 0f, FixedSizeValue.X, FixedSizeValue.Y);
            }

            return new float4(0f, 0f, FixedSizeValue.X, FixedSizeValue.Y);
        }

        /// <summary>
        /// Resolves the active camera that should currently drive this viewport.
        /// </summary>
        /// <returns>A hierarchy-owned camera borrowed by the viewport, or null when no binding is available.</returns>
        [NativeBorrowedReturn]
        CameraComponent ResolveBoundCameraComponent() {
            if (BindingModeValue == ExplicitCameraBindingMode) {
                return ExplicitBoundCameraComponentValue;
            }

            if (BindingModeValue == AncestorCameraBindingMode) {
                return ResolveAncestorCameraComponent();
            }

            return null;
        }

        /// <summary>
        /// Resolves the camera currently targeted by this viewport when the viewport is camera-bound.
        /// Returns null when the viewport is screen-bound, fixed-size, or does not currently resolve a target camera.
        /// </summary>
        /// <returns>A hierarchy-owned camera borrowed for rendering and layout decisions, or null when no camera is bound.</returns>
        [NativeBorrowedReturn]
        public CameraComponent GetBoundCameraComponent() {
            RefreshSubscriptions();
            return ResolveBoundCameraComponent();
        }

        /// <summary>
        /// Resolves one camera viewport into pixel-space bounds suitable for layout calculations.
        /// </summary>
        /// <param name="cameraComponent">Camera whose viewport should be resolved.</param>
        /// <returns>Viewport rectangle expressed in pixel-space coordinates when possible.</returns>
        float4 ResolveCameraViewportBounds(CameraComponent cameraComponent) {
            if (cameraComponent == null) {
                throw new ArgumentNullException(nameof(cameraComponent));
            }

            if (cameraComponent.RenderTarget != null && cameraComponent.RenderTarget.Width > 0 && cameraComponent.RenderTarget.Height > 0) {
                return new float4(0f, 0f, cameraComponent.RenderTarget.Width, cameraComponent.RenderTarget.Height);
            }

            float4 viewport = cameraComponent.Viewport;
            if (OwnerCore == null || OwnerCore.RenderManager3D == null) {
                return viewport;
            }

            int2 mainWindowSize = OwnerCore.RenderManager3D.GetCameraLayoutSize(cameraComponent);
            if (mainWindowSize.X <= 0 || mainWindowSize.Y <= 0) {
                return viewport;
            }

            return CameraViewportResolver.ResolveViewport(viewport, mainWindowSize.X, mainWindowSize.Y);
        }

        /// <summary>
        /// Resolves the nearest ancestor camera component in the current entity chain.
        /// </summary>
        /// <returns>An entity-owned ancestor camera component borrowed by the viewport when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        CameraComponent ResolveAncestorCameraComponent() {
            return CameraWindowLayoutUtils.FindAncestorCamera(Parent);
        }

        /// <summary>
        /// Rebuilds the authored snapshots for the current entity subtree when viewport-owned scaling is active.
        /// </summary>
        void RebuildSnapshots() {
            if (Parent == null) {
                ReleaseLayoutSnapshotItems();
                SnapshotEntityCountValue = 0;
                return;
            }

            ViewportLayoutSnapshot[] previous = LayoutSnapshotsValue.ToArray();
            LayoutSnapshotsValue.Clear();
            CaptureSnapshotsRecursive(Parent, true, previous);
            NativeOwnership.Delete(previous);
            SnapshotEntityCountValue = LayoutSnapshotsValue.Count;
        }

        /// <summary>
        /// Recursively captures one entity subtree into immutable authored snapshots.
        /// </summary>
        /// <param name="entity">Current entity to capture.</param>
        /// <param name="isRootEntity">True when the entity is the root of the scaled subtree.</param>
        /// <param name="previous">Existing authored records retained across dynamic subtree changes.</param>
        void CaptureSnapshotsRecursive(Entity entity, bool isRootEntity, [NativeNoEscape] ViewportLayoutSnapshot[] previous) {
            ViewportLayoutSnapshot snapshot = null;
            for (int index = 0; index < previous.Length; index++) {
                if (ReferenceEquals(previous[index].Entity, entity)) {
                    snapshot = previous[index];
                    break;
                }
            }
            LayoutSnapshotsValue.Add(snapshot ?? new ViewportLayoutSnapshot(entity, isRootEntity));
            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (ShouldCaptureChild(entity.Children[childIndex])) {
                    CaptureSnapshotsRecursive(entity.Children[childIndex], false, previous);
                }
            }
        }

        /// <summary>
        /// Leaves independently owned viewport and generated performance-overlay subtrees to their respective layout owners.
        /// In the editor, any nested <see cref="IAuthoredLayoutOwner"/> (for example a reference-canvas fit) also owns its
        /// subtree exclusively, so this viewport never writes values that the nested owner would then mistake for preview
        /// edits, and each owner restores only what it wrote. Runtime hosts keep the original shared behavior.
        /// </summary>
        /// <param name="child">Child whose layout ownership is inspected.</param>
        /// <returns>Whether the current viewport should capture this child's layout.</returns>
        bool ShouldCaptureChild(Entity child) {
            if (child.Components != null) {
                bool editorAuthoring = IsEditorAuthoring();
                for (int index = 0; index < child.Components.Count; index++) {
                    if (child.Components[index] is ViewportComponent) {
                        return false;
                    }
                    if (editorAuthoring && child.Components[index] is IAuthoredLayoutOwner) {
                        return false;
                    }
                }
            }
            Entity parent = child.Parent;
            if (parent != null && parent.Components != null) {
                for (int index = 0; index < parent.Components.Count; index++) {
                    if (parent.Components[index] is FPSComponent fps && ReferenceEquals(child, fps.OverlayLayoutRoot)) {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Deletes the current snapshot records while preserving the reusable snapshot list.
        /// </summary>
        void ReleaseLayoutSnapshotItems() {
            LayoutSnapshotsValue.Clear();
            AppliedLayoutSnapshotsValue.Clear();
        }

        /// <summary>
        /// Deletes all snapshot records and the native list wrappers owned by this viewport component.
        /// </summary>
        void ReleaseLayoutSnapshots() {
            ReleaseLayoutSnapshotItems();
            NativeOwnership.Delete(LayoutSnapshotsValue);
            NativeOwnership.Delete(AppliedLayoutSnapshotsValue);
        }

        /// <summary>
        /// Applies the current viewport-owned scale to the captured subtree.
        /// </summary>
        void ApplyCurrentScale() {
            if (Parent == null || LayoutSnapshotsValue.Count == 0) {
                return;
            }

            int2 resolvedAnchorSpaceSize = ResolveCurrentAnchorSpaceSize();
            float2 resolvedAnchorSpaceOrigin = new float2(0f, 0f);
            float2 resolvedCanvasOrigin = ResolveCurrentCanvasOrigin(resolvedAnchorSpaceSize);
            bool anchorSpaceChanged = DidAnchorSpaceChange(CurrentAnchorSpaceValue, resolvedAnchorSpaceSize, resolvedAnchorSpaceOrigin) ||
                                      DidCanvasOriginChange(CurrentCanvasOriginValue, resolvedCanvasOrigin);

            CurrentAnchorSpaceValue.Update(resolvedAnchorSpaceSize, resolvedAnchorSpaceOrigin);
            CurrentCanvasOriginValue = resolvedCanvasOrigin;
            double densityScale = ScalingModeValue == DensityScalingMode ? DensityScale : 1d;
            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                if (ScalingModeValue == DensityScalingMode) {
                    LayoutSnapshotsValue[snapshotIndex].ApplyDensityScale(densityScale);
                    continue;
                }
                LayoutSnapshotsValue[snapshotIndex].Apply(
                    CurrentAnchorSpaceValue,
                    resolvedCanvasOrigin,
                    ReferenceWidthValue,
                    ReferenceHeightValue,
                    ScalingModeValue == ReferenceCanvasStretchScalingMode);
            }

            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                LayoutSnapshotsValue[snapshotIndex].RefreshAnchoring();
            }

            if (anchorSpaceChanged) {
                RaiseAnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Resolves the current scaled anchor-space size from the live viewport bounds and the authored reference canvas.
        /// </summary>
        /// <returns>Anchor-space size that descendants should use for local anchoring.</returns>
        int2 ResolveCurrentAnchorSpaceSize() {
            int2 viewportBounds = ResolveAnchorBounds();
            if (ScalingModeValue == ReferenceCanvasStretchScalingMode || ScalingModeValue == DensityScalingMode) {
                return viewportBounds;
            }

            double liveWidth = viewportBounds.X > 0 ? viewportBounds.X : ReferenceWidthValue;
            double liveHeight = viewportBounds.Y > 0 ? viewportBounds.Y : ReferenceHeightValue;
            if (LiveViewportMatchesReferenceAspect(liveWidth, liveHeight)) {
                return new int2((int)Math.Round(liveWidth), (int)Math.Round(liveHeight));
            }

            double widthScale = liveWidth / ReferenceWidthValue;
            double heightScale = liveHeight / ReferenceHeightValue;
            double scale = Math.Min(widthScale, heightScale);
            if (scale <= 0d) {
                return new int2(ReferenceWidthValue, ReferenceHeightValue);
            }

            int fittedWidth = Math.Max(1, (int)Math.Round(ReferenceWidthValue * scale));
            int fittedHeight = Math.Max(1, (int)Math.Round(ReferenceHeightValue * scale));
            return new int2(fittedWidth, fittedHeight);
        }

        /// <summary>
        /// Resolves the root-entity offset that centers the scaled subtree inside the live viewport.
        /// </summary>
        /// <param name="anchorSpace">Current scaled anchor space.</param>
        /// <returns>Root-entity offset applied to the scaled subtree.</returns>
        float2 ResolveCurrentCanvasOrigin(int2 anchorSpaceSize) {
            int2 viewportBounds = ResolveAnchorBounds();
            double liveWidth = viewportBounds.X > 0 ? viewportBounds.X : ReferenceWidthValue;
            double liveHeight = viewportBounds.Y > 0 ? viewportBounds.Y : ReferenceHeightValue;
            float originX = (float)((liveWidth - anchorSpaceSize.X) * 0.5d);
            float originY = (float)((liveHeight - anchorSpaceSize.Y) * 0.5d);
            return new float2(originX, originY);
        }

        /// <summary>
        /// Determines whether the live viewport aspect matches the authored reference aspect closely enough to skip letterboxing.
        /// </summary>
        /// <param name="liveWidth">Live viewport width.</param>
        /// <param name="liveHeight">Live viewport height.</param>
        /// <returns>True when the viewport aspect is effectively the same as the reference aspect.</returns>
        bool LiveViewportMatchesReferenceAspect(double liveWidth, double liveHeight) {
            double expectedWidth = liveHeight * ReferenceWidthValue / ReferenceHeightValue;
            double expectedHeight = liveWidth * ReferenceHeightValue / ReferenceWidthValue;
            return Math.Abs(liveWidth - expectedWidth) <= 0.5d || Math.Abs(liveHeight - expectedHeight) <= 0.5d;
        }

        /// <summary>
        /// Determines whether the effective anchor space changed between viewport scaling passes.
        /// </summary>
        /// <param name="currentAnchorSpace">Previously applied anchor space.</param>
        /// <param name="resolvedAnchorSpace">Newly resolved anchor space.</param>
        /// <returns>True when the size or origin changed.</returns>
        bool DidAnchorSpaceChange(AnchorSpace currentAnchorSpace, int2 resolvedAnchorSpaceSize, float2 resolvedAnchorSpaceOrigin) {
            if (currentAnchorSpace == null) {
                return true;
            }

            return currentAnchorSpace.Size.X != resolvedAnchorSpaceSize.X ||
                   currentAnchorSpace.Size.Y != resolvedAnchorSpaceSize.Y ||
                   currentAnchorSpace.Origin.X != resolvedAnchorSpaceOrigin.X ||
                   currentAnchorSpace.Origin.Y != resolvedAnchorSpaceOrigin.Y;
        }

        /// <summary>
        /// Determines whether the root origin changed between viewport scaling passes.
        /// </summary>
        /// <param name="currentCanvasOrigin">Previously applied root origin.</param>
        /// <param name="resolvedCanvasOrigin">Newly resolved root origin.</param>
        /// <returns>True when the root origin changed.</returns>
        bool DidCanvasOriginChange(float2 currentCanvasOrigin, float2 resolvedCanvasOrigin) {
            return currentCanvasOrigin.X != resolvedCanvasOrigin.X ||
                   currentCanvasOrigin.Y != resolvedCanvasOrigin.Y;
        }

        /// <summary>
        /// Resolves the origin shift required for descendants that answer to the full viewport instead of one fitted reference-canvas subtree.
        /// </summary>
        /// <returns>Viewport-space origin correction expressed in local pixels.</returns>
        float2 ResolveViewportAnchorOrigin() {
            if (RestoringAuthoredLayoutValue) {
                return new float2(0f, 0f);
            }

            if (IsReferenceCanvasScalingActive()) {
                return new float2(-CurrentCanvasOriginValue.X, -CurrentCanvasOriginValue.Y);
            }

            if (Parent == null || Parent.Components == null) {
                return new float2(0f, 0f);
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                if (Parent.Components[componentIndex] is ReferenceCanvasFitComponent referenceCanvasFitComponent) {
                    return referenceCanvasFitComponent.ViewportAnchorOrigin;
                }
            }

            return new float2(0f, 0f);
        }

        /// <summary>
        /// Returns whether the viewport owns reference-canvas scaling, preserving either fit or stretch behavior.
        /// </summary>
        bool IsReferenceCanvasScalingActive() {
            return ScalingModeValue == ReferenceCanvasScalingMode
                || ScalingModeValue == ReferenceCanvasStretchScalingMode
                || ScalingModeValue == DensityScalingMode;
        }

        /// <summary>
        /// Counts one entity subtree recursively.
        /// </summary>
        /// <param name="entity">Root entity whose subtree should be counted.</param>
        /// <returns>Total entity count including the supplied root.</returns>
        int CountEntitiesRecursive(Entity entity) {
            if (entity == null) {
                return 0;
            }

            int count = 1;
            if (entity.Children == null) {
                return count;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (ShouldCaptureChild(entity.Children[childIndex])) {
                    count += CountEntitiesRecursive(entity.Children[childIndex]);
                }
            }

            return count;
        }

        /// <summary>
        /// Raises the viewport changed event when listeners are present.
        /// </summary>
        void RaiseAnchorBoundsChanged() {
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
        }
    }
}

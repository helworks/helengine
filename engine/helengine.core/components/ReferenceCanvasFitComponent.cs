namespace helengine {
    /// <summary>
    /// Scales one authored 2D subtree from a reference canvas into the camera's layout surface size while preserving the original layout as the source of truth.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public class ReferenceCanvasFitComponent : UpdateComponent, IAnchorBoundsProvider, IAuthoredLayoutOwner {
        /// <summary>
        /// Backing field for the authored reference canvas width.
        /// </summary>
        int ReferenceWidthValue;

        /// <summary>
        /// Backing field for the authored reference canvas height.
        /// </summary>
        int ReferenceHeightValue;

        /// <summary>
        /// Captured authored snapshots for the current entity subtree.
        /// </summary>
        readonly List<ReferenceCanvasFitSnapshot> SnapshotsValue;

        /// <summary>
        /// Stores, in editor authoring only, the live layout values written by the last fit pass so edits made in the scaled
        /// preview can be told apart from fit output and mapped back into authored space.
        /// </summary>
        readonly List<ReferenceCanvasFitSnapshot> AppliedSnapshotsValue;

        /// <summary>
        /// Stores the root displacement used by the last recorded editor fit pass.
        /// </summary>
        float2 AppliedCanvasOriginValue;

        /// <summary>
        /// Stores the horizontal scale used by the last recorded editor fit pass.
        /// </summary>
        double AppliedWidthScaleValue;

        /// <summary>
        /// Stores the vertical scale used by the last recorded editor fit pass.
        /// </summary>
        double AppliedHeightScaleValue;

        /// <summary>
        /// Stores the uniform glyph and decoration scale used by the last recorded editor fit pass.
        /// </summary>
        double AppliedScalarScaleValue;

        /// <summary>
        /// Stores the display pixel aspect the last fit pass used, so the recorded glyph scale matches the one the snapshots applied.
        /// </summary>
        double CurrentPixelAspectValue;

        /// <summary>
        /// Tracks whether the component is currently subscribed to layout-window size events.
        /// </summary>
        bool IsSubscribedToWindowLayoutChanges;

        /// <summary>
        /// Tracks whether the subtree should be re-evaluated on the next update.
        /// </summary>
        bool PendingApplyValue;

        /// <summary>The hierarchy-owned viewport governing this fitted subtree, when one is authored.</summary>
        ViewportComponent LayoutViewport;

        /// <summary>The hierarchy-owned camera supplying layout dimensions when no explicit viewport overrides them.</summary>
        CameraComponent LayoutCamera;

        /// <summary>
        /// Tracks the number of entities captured in the current authored snapshot set.
        /// </summary>
        int SnapshotEntityCountValue;

        /// <summary>
        /// Tracks the current fitted anchor space exposed to anchored descendants.
        /// </summary>
        AnchorSpace CurrentAnchorSpaceValue;

        /// <summary>
        /// Tracks the current fitted origin applied to the root entity of the authored subtree.
        /// </summary>
        float2 CurrentCanvasOriginValue;

        /// <summary>
        /// Raised when the fitted anchor space changes and anchored descendants should refresh.
        /// </summary>
        public event Action AnchorBoundsChanged;

        /// <summary>
        /// Initializes a new fit component using the default scene canvas profile as its authored reference.
        /// </summary>
        public ReferenceCanvasFitComponent() {
            ReferenceWidthValue = SceneCanvasProfile.DefaultWidth;
            ReferenceHeightValue = SceneCanvasProfile.DefaultHeight;
            SnapshotsValue = new List<ReferenceCanvasFitSnapshot>();
            AppliedSnapshotsValue = new List<ReferenceCanvasFitSnapshot>();
            CurrentAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentCanvasOriginValue = new float2(0f, 0f);
            CurrentPixelAspectValue = RenderManager3D.SquarePixelAspect;
        }

        /// <summary>
        /// Gets or sets the authored reference canvas width in logical pixels.
        /// </summary>
        public int ReferenceWidth {
            get { return ReferenceWidthValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Reference canvas width must be at least one.");
                }

                ReferenceWidthValue = value;
                ApplyCurrentScale();
            }
        }

        /// <summary>
        /// Gets or sets the authored reference canvas height in logical pixels.
        /// </summary>
        public int ReferenceHeight {
            get { return ReferenceHeightValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Reference canvas height must be at least one.");
                }

                ReferenceHeightValue = value;
                ApplyCurrentScale();
            }
        }

        /// <summary>
        /// Gets the fitted anchor space exposed to anchored descendants inside the reference-canvas subtree.
        /// </summary>
        public AnchorSpace AnchorSpace => CurrentAnchorSpaceValue;

        /// <summary>
        /// Gets the offset required to translate fitted-subtree local space back into the full live viewport space.
        /// </summary>
        public float2 ViewportAnchorOrigin => new float2(-CurrentCanvasOriginValue.X, -CurrentCanvasOriginValue.Y);

        /// <summary>
        /// Calculates the active horizontal and vertical scale factors from reference-canvas coordinates into the fitted canvas.
        /// </summary>
        /// <returns>Horizontal and vertical scale factors for values expressed in reference-canvas units.</returns>
        public float2 CalculateScale() {
            int2 anchorSpaceSize = ResolveCurrentAnchorSpaceSize();
            return new float2(
                (float)((double)anchorSpaceSize.X / ReferenceWidthValue),
                (float)((double)anchorSpaceSize.Y / ReferenceHeightValue));
        }

        /// <summary>
        /// Calculates the fitted subtree-local position for a position expressed in reference-canvas coordinates.
        /// </summary>
        /// <param name="referencePosition">Position expressed relative to the reference-canvas subtree.</param>
        /// <returns>Position expressed in the active fitted canvas coordinate system.</returns>
        public float3 CalculatePosition(float3 referencePosition) {
            float2 scale = CalculateScale();
            return new float3(
                referencePosition.X * scale.X,
                referencePosition.Y * scale.Y,
                referencePosition.Z);
        }

        /// <summary>
        /// Captures the authored subtree and applies the first fit scale when the component is attached.
        /// </summary>
        /// <param name="entity">Entity that owns the fit component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RebuildSnapshots();
            RefreshLayoutProviders();
            AttachToWindowLayoutChanges();
            PendingApplyValue = true;
        }

        /// <summary>
        /// Reapplies the current fit scale when the parent entity becomes enabled again and disconnects resize handling when disabled.
        /// </summary>
        /// <param name="newEnabled">True when the parent became enabled.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                RefreshLayoutProviders();
                AttachToWindowLayoutChanges();
                PendingApplyValue = true;
                return;
            }

            DetachFromWindowLayoutChanges();
        }

        /// <summary>
        /// Releases resize subscriptions when the fit component is removed.
        /// </summary>
        /// <param name="entity">Entity losing the fit component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DetachFromWindowLayoutChanges();
            ReleaseSnapshotItems();
            SnapshotEntityCountValue = 0;
            PendingApplyValue = false;
        }

        /// <summary>
        /// Releases fit snapshots and resize subscriptions before the native backend deletes this component.
        /// </summary>
        public override void Dispose() {
            DetachFromWindowLayoutChanges();
            ReleaseSnapshots();
            NativeOwnership.Delete(CurrentAnchorSpaceValue);
            CurrentAnchorSpaceValue = null;
            base.Dispose();
        }

        /// <summary>Captures the complete initialized subtree and resolves its initial canvas fit before scene publication.</summary>
        internal void PrepareSceneLayout() {
            RebuildSnapshots();
            PendingApplyValue = true;
            ApplyPendingSceneLayout();
        }

        /// <summary>Rebuilds the subtree snapshot after hierarchy changes and reapplies pending canvas-fit changes.</summary>
        public override void Update() {
            base.Update();
            ApplyPendingSceneLayout();
        }

        /// <summary>Runs builtin canvas layout without dispatching a derived gameplay Update during scene preparation.</summary>
        void ApplyPendingSceneLayout() {

            if (Parent == null) {
                return;
            }

            bool tracksAuthoredEdits = IsEditorAuthoring();
            bool subtreeChanged = tracksAuthoredEdits
                ? MatchSnapshotsRecursive(Parent, 0) != SnapshotsValue.Count
                : CountEntitiesRecursive(Parent) != SnapshotEntityCountValue;
            if (tracksAuthoredEdits && subtreeChanged) {
                RestoreAuthoredLayout();
            }

            if (subtreeChanged) {
                RebuildSnapshots();
                PendingApplyValue = true;
            }

            RefreshLayoutProviders();
            if (PendingApplyValue) {
                if (tracksAuthoredEdits && !subtreeChanged) {
                    RestoreAuthoredLayout();
                }

                ApplyCurrentScale();
                PendingApplyValue = false;
            }
        }

        /// <summary>
        /// Writes the authored, unscaled layout back onto the fitted subtree so it can be serialized as authored data, and
        /// re-captures the authored snapshots from it. Contract: any tracked live value that differs from what the last
        /// recorded fit pass wrote ("live != last applied") counts as a user edit made in the scaled editor preview and is
        /// mapped back into authored space; unchanged values resolve to the captured authored values. Anchored descendants are
        /// then re-anchored against the reference canvas so their derived positions do not depend on the editor window.
        /// Entities that no recorded fit pass has touched already hold authored values and are only re-captured. Runtime hosts
        /// never record a fit pass, so outside the editor this does nothing. Call <see cref="ReapplyLayout"/> afterwards.
        /// </summary>
        public void RestoreAuthoredLayout() {
            if (!IsEditorAuthoring() || Parent == null || SnapshotsValue.Count == 0) {
                return;
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                ReferenceCanvasFitSnapshot snapshot = SnapshotsValue[snapshotIndex];
                ReferenceCanvasFitSnapshot applied = FindAppliedSnapshot(snapshot.Entity);
                if (applied != null && IsInFittedSubtree(snapshot.Entity)) {
                    snapshot.RestoreAuthoredComponents(applied, AppliedWidthScaleValue, AppliedHeightScaleValue, AppliedScalarScaleValue);
                }
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                ReferenceCanvasFitSnapshot snapshot = SnapshotsValue[snapshotIndex];
                ReferenceCanvasFitSnapshot applied = FindAppliedSnapshot(snapshot.Entity);
                if (applied != null && IsInFittedSubtree(snapshot.Entity)) {
                    snapshot.RestoreAuthoredPosition(applied, AppliedCanvasOriginValue, AppliedWidthScaleValue, AppliedHeightScaleValue);
                }
            }

            // Anchored axes are derived from the anchor space; re-anchor against the authored reference canvas so persisted
            // positions and stretched sizes never depend on the editor window or a load-time fallback provider.
            // Layouts answering to the full viewport rect resolve through the governing viewport (or a non-scaling viewport
            // inside this subtree); those must see the same authored canvas with a zero origin while re-anchoring.
            CurrentAnchorSpaceValue.Update(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            SetAuthoredViewportRect(true);
            try {
                for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                    ReferenceCanvasFitSnapshot snapshot = SnapshotsValue[snapshotIndex];
                    if (!snapshot.IsRootEntity && IsInFittedSubtree(snapshot.Entity)) {
                        snapshot.RefreshAnchoring();
                    }
                }
            } finally {
                SetAuthoredViewportRect(false);
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                ReferenceCanvasFitSnapshot snapshot = SnapshotsValue[snapshotIndex];
                if (IsInFittedSubtree(snapshot.Entity)) {
                    SnapshotsValue[snapshotIndex] = new ReferenceCanvasFitSnapshot(snapshot.Entity, snapshot.IsRootEntity);
                }
            }

            AppliedSnapshotsValue.Clear();
            PendingApplyValue = true;
        }

        /// <summary>
        /// Switches the viewport rect seen by this subtree's full-viewport anchors between the authored reference canvas and
        /// the live viewport: the governing viewport and every viewport attached inside the captured subtree.
        /// </summary>
        /// <param name="authored">True to resolve the reference canvas with a zero origin; false to return to live bounds.</param>
        void SetAuthoredViewportRect(bool authored) {
            int2 referenceSize = new int2(ReferenceWidthValue, ReferenceHeightValue);
            if (LayoutViewport != null) {
                if (authored) {
                    LayoutViewport.BeginAuthoredViewportRect(referenceSize);
                } else {
                    LayoutViewport.EndAuthoredViewportRect();
                }
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                Entity entity = SnapshotsValue[snapshotIndex].Entity;
                if (entity.Components == null || !IsInFittedSubtree(entity)) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is ViewportComponent viewport) {
                        if (authored) {
                            viewport.BeginAuthoredViewportRect(referenceSize);
                        } else {
                            viewport.EndAuthoredViewportRect();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Re-applies the fit scale to the subtree, typically after <see cref="RestoreAuthoredLayout"/> exposed the authored values for serialization.
        /// </summary>
        public void ReapplyLayout() {
            PendingApplyValue = true;
            ApplyPendingSceneLayout();
        }

        /// <summary>
        /// Returns whether this component runs inside the editor, where the fitted subtree is authored data that must stay
        /// recoverable in authored space. Runtime hosts never record or absorb preview edits.
        /// </summary>
        /// <returns>True when the owning core authors scenes in the editor.</returns>
        bool IsEditorAuthoring() {
            return OwnerCore != null && OwnerCore.ExecutionMode == ComponentExecutionMode.Editor;
        }

        /// <summary>
        /// Records the live values and scales written by the fit pass that just ran, so later preview edits can be detected and
        /// unscaled. The scales mirror the ones <see cref="ReferenceCanvasFitSnapshot.Apply"/> derives from the same anchor space.
        /// </summary>
        void RecordAppliedLayout() {
            AppliedSnapshotsValue.Clear();
            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                ReferenceCanvasFitSnapshot snapshot = SnapshotsValue[snapshotIndex];
                AppliedSnapshotsValue.Add(new ReferenceCanvasFitSnapshot(snapshot.Entity, snapshot.IsRootEntity));
            }

            AppliedCanvasOriginValue = CurrentCanvasOriginValue;
            AppliedWidthScaleValue = (double)CurrentAnchorSpaceValue.Size.X / ReferenceWidthValue;
            AppliedHeightScaleValue = (double)CurrentAnchorSpaceValue.Size.Y / ReferenceHeightValue;
            AppliedScalarScaleValue = DisplayAspectFitUtils.ResolveGlyphScale(AppliedWidthScaleValue, AppliedHeightScaleValue, CurrentPixelAspectValue);
        }

        /// <summary>
        /// Finds the live values recorded for one entity by the last editor fit pass.
        /// </summary>
        /// <param name="entity">Entity whose recorded values are requested.</param>
        /// <returns>The recorded values owned by this component, or null when the entity was not part of the last recorded pass.</returns>
        [NativeBorrowedReturn]
        ReferenceCanvasFitSnapshot FindAppliedSnapshot(Entity entity) {
            for (int index = 0; index < AppliedSnapshotsValue.Count; index++) {
                if (ReferenceEquals(AppliedSnapshotsValue[index].Entity, entity)) {
                    return AppliedSnapshotsValue[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether one snapshotted entity is still attached below this component's owner, so removed or replaced entities are never written.
        /// </summary>
        /// <param name="entity">Entity captured by a snapshot.</param>
        /// <returns>True when the entity is the owner or one of its current descendants.</returns>
        bool IsInFittedSubtree(Entity entity) {
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
        /// In the editor, leaves a child subtree to a nested owner that actually rescales it (another fit, or a viewport with
        /// reference or density scaling), so this component never writes values that owner would mistake for preview edits and
        /// each owner restores only what it wrote. A non-scaling viewport stays captured and scaled by this fit, as at runtime.
        /// The nested scaling owner writes its fields after this fit at runtime as well, so the preview matches the player.
        /// Runtime hosts capture every child, exactly as before.
        /// </summary>
        /// <param name="child">Child whose layout ownership is inspected.</param>
        /// <returns>Whether this component should capture the child's layout.</returns>
        bool ShouldCaptureChild(Entity child) {
            if (!IsEditorAuthoring() || child.Components == null) {
                return true;
            }

            for (int index = 0; index < child.Components.Count; index++) {
                if (child.Components[index] is ReferenceCanvasFitComponent) {
                    return false;
                }
                if (child.Components[index] is ViewportComponent viewport && viewport.ScalesSubtree) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Walks the captured subtree in snapshot order and checks that every entity still matches its snapshot, so the editor
        /// notices replaced entities (for example after undo) even when the entity count did not change.
        /// </summary>
        /// <param name="entity">Current entity to match.</param>
        /// <param name="snapshotIndex">Snapshot index expected for the entity.</param>
        /// <returns>The next snapshot index after the matched subtree, or -1 when the subtree differs from the snapshots.</returns>
        int MatchSnapshotsRecursive(Entity entity, int snapshotIndex) {
            if (snapshotIndex >= SnapshotsValue.Count || !ReferenceEquals(SnapshotsValue[snapshotIndex].Entity, entity)) {
                return -1;
            }

            int nextIndex = snapshotIndex + 1;
            if (entity.Children == null) {
                return nextIndex;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (!ShouldCaptureChild(entity.Children[childIndex])) {
                    continue;
                }

                nextIndex = MatchSnapshotsRecursive(entity.Children[childIndex], nextIndex);
                if (nextIndex < 0) {
                    return -1;
                }
            }

            return nextIndex;
        }

        /// <summary>
        /// Handles named layout-window size notifications by reapplying the reference-canvas fit scale.
        /// </summary>
        /// <param name="tag">Named window whose usable layout size changed.</param>
        void HandleWindowLayoutChanged(string tag) {
            if (string.Equals(CameraWindowLayoutUtils.ResolveWindowTag(LayoutViewport, LayoutCamera), tag, StringComparison.OrdinalIgnoreCase)) {
                PendingApplyValue = true;
            }
        }

        /// <summary>
        /// Attaches to the named layout-window change event when the component is active.
        /// </summary>
        void AttachToWindowLayoutChanges() {
            if (IsSubscribedToWindowLayoutChanges) {
                return;
            }
            if (OwnerCore == null || OwnerCore.RenderManager3D == null) {
                return;
            }

            OwnerCore.RenderManager3D.WindowLayoutChanged += HandleWindowLayoutChanged;
            IsSubscribedToWindowLayoutChanges = true;
        }

        /// <summary>
        /// Detaches from the named layout-window change event when currently subscribed.
        /// </summary>
        void DetachFromWindowLayoutChanges() {
            DetachFromLayoutProviders();
            if (!IsSubscribedToWindowLayoutChanges) {
                return;
            }
            if (OwnerCore == null || OwnerCore.RenderManager3D == null) {
                IsSubscribedToWindowLayoutChanges = false;
                return;
            }

            OwnerCore.RenderManager3D.WindowLayoutChanged -= HandleWindowLayoutChanged;
            IsSubscribedToWindowLayoutChanges = false;
        }

        /// <summary>Tracks the explicit viewport or nearest camera and subscribes to authored layout-scope changes.</summary>
        void RefreshLayoutProviders() {
            ViewportComponent viewport = CameraWindowLayoutUtils.FindAncestorViewport(Parent);
            CameraComponent camera = viewport != null ? viewport.GetBoundCameraComponent() : CameraWindowLayoutUtils.FindAncestorCamera(Parent);
            if (!ReferenceEquals(LayoutViewport, viewport) || !ReferenceEquals(LayoutCamera, camera)) {
                DetachFromLayoutProviders();
                LayoutViewport = viewport;
                LayoutCamera = camera;
                if (LayoutViewport != null) {
                    LayoutViewport.AnchorBoundsChanged += HandleLayoutBoundsChanged;
                }
                if (LayoutCamera != null) {
                    LayoutCamera.ViewportChanged += HandleLayoutBoundsChanged;
                    LayoutCamera.OutputWindowTargetChanged += HandleLayoutBoundsChanged;
                }
                PendingApplyValue = true;
            }
        }

        /// <summary>Disconnects borrowed viewport and camera callbacks before this component releases its hierarchy scope.</summary>
        void DetachFromLayoutProviders() {
            if (LayoutViewport != null) {
                LayoutViewport.AnchorBoundsChanged -= HandleLayoutBoundsChanged;
                LayoutViewport = null;
            }
            if (LayoutCamera != null) {
                LayoutCamera.ViewportChanged -= HandleLayoutBoundsChanged;
                LayoutCamera.OutputWindowTargetChanged -= HandleLayoutBoundsChanged;
                LayoutCamera = null;
            }
        }

        /// <summary>Invalidates the fitted subtree when its authored viewport or camera target changes.</summary>
        void HandleLayoutBoundsChanged() {
            PendingApplyValue = true;
        }

        /// <summary>Uses the authored viewport's resolved pixels, or the nearest camera's named layout surface.</summary>
        /// <returns>Dimensions of this subtree's own layout reference.</returns>
        int2 ResolveLayoutSurfaceSize() {
            RefreshLayoutProviders();
            return LayoutViewport != null ? LayoutViewport.ResolvedViewportSize
                : CameraWindowLayoutUtils.ResolveCameraSize(OwnerCore.RenderManager3D, LayoutCamera);
        }

        /// <summary>Uses the authored viewport's pixel aspect, or the nearest camera's output pixel aspect.</summary>
        /// <returns>Displayed width of one layout-surface pixel divided by its displayed height.</returns>
        double ResolveLayoutPixelAspect() {
            RefreshLayoutProviders();
            return LayoutViewport != null ? LayoutViewport.ResolvedPixelAspect
                : CameraWindowLayoutUtils.ResolveCameraPixelAspect(OwnerCore.RenderManager3D, LayoutCamera);
        }

        /// <summary>
        /// Rebuilds the authored snapshots for the current entity subtree.
        /// </summary>
        void RebuildSnapshots() {
            ReleaseSnapshotItems();
            if (Parent == null) {
                SnapshotEntityCountValue = 0;
                return;
            }

            CaptureSnapshotsRecursive(Parent, true);
            SnapshotEntityCountValue = SnapshotsValue.Count;
        }

        /// <summary>
        /// Recursively captures one entity subtree into immutable authored snapshots.
        /// </summary>
        /// <param name="entity">Current entity to capture.</param>
        /// <param name="isRootEntity">True when the entity is the root of the fitted subtree.</param>
        void CaptureSnapshotsRecursive(Entity entity, bool isRootEntity) {
            SnapshotsValue.Add(new ReferenceCanvasFitSnapshot(entity, isRootEntity));

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (ShouldCaptureChild(entity.Children[childIndex])) {
                    CaptureSnapshotsRecursive(entity.Children[childIndex], false);
                }
            }
        }

        /// <summary>
        /// Deletes the current snapshot records while preserving the reusable snapshot list.
        /// </summary>
        void ReleaseSnapshotItems() {
            SnapshotsValue.Clear();
            AppliedSnapshotsValue.Clear();
        }

        /// <summary>
        /// Deletes all snapshot records and the native list wrapper owned by this fit component.
        /// </summary>
        void ReleaseSnapshots() {
            ReleaseSnapshotItems();
            NativeOwnership.Delete(SnapshotsValue);
            NativeOwnership.Delete(AppliedSnapshotsValue);
        }

        /// <summary>
        /// Applies the fit scale resolved from the current camera or viewport surface to the captured authored subtree.
        /// </summary>
        void ApplyCurrentScale() {
            if (Parent == null || OwnerCore == null || OwnerCore.RenderManager3D == null || SnapshotsValue.Count == 0) {
                return;
            }

            int2 resolvedAnchorSpaceSize = ResolveCurrentAnchorSpaceSize();
            float2 resolvedAnchorSpaceOrigin = new float2(0f, 0f);
            float2 resolvedCanvasOrigin = ResolveCurrentCanvasOrigin(resolvedAnchorSpaceSize);
            bool anchorSpaceChanged = DidAnchorSpaceChange(CurrentAnchorSpaceValue, resolvedAnchorSpaceSize, resolvedAnchorSpaceOrigin) ||
                                      DidCanvasOriginChange(CurrentCanvasOriginValue, resolvedCanvasOrigin);

            CurrentAnchorSpaceValue.Update(resolvedAnchorSpaceSize, resolvedAnchorSpaceOrigin);
            CurrentCanvasOriginValue = resolvedCanvasOrigin;
            CurrentPixelAspectValue = ResolveLayoutPixelAspect();
            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].Apply(CurrentAnchorSpaceValue, resolvedCanvasOrigin, ReferenceWidthValue, ReferenceHeightValue, CurrentPixelAspectValue);
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].RefreshAnchoring();
            }

            if (anchorSpaceChanged) {
                RaiseAnchorBoundsChanged();
            }

            if (IsEditorAuthoring()) {
                RecordAppliedLayout();
            }
        }

        /// <summary>
        /// Resolves the current fitted anchor space using the camera-scoped layout dimensions and the authored reference canvas.
        /// The fit is computed in display space: the surface's pixel aspect is applied so the canvas keeps its authored shape
        /// on the display, and with square pixels this is the plain uniform letterbox.
        /// </summary>
        /// <returns>Anchor space that descendants should use for local anchoring.</returns>
        int2 ResolveCurrentAnchorSpaceSize() {
            int2 mainWindowSize = ResolveLayoutSurfaceSize();
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            return DisplayAspectFitUtils.ResolveFittedSize(liveWidth, liveHeight, ReferenceWidthValue, ReferenceHeightValue, ResolveLayoutPixelAspect());
        }

        /// <summary>
        /// Resolves the fitted origin applied to the root entity of the authored subtree.
        /// </summary>
        /// <param name="anchorSpace">Anchor space resolved for the current live window.</param>
        /// <returns>Root-entity offset that places the fitted canvas inside the live window.</returns>
        float2 ResolveCurrentCanvasOrigin(int2 anchorSpaceSize) {
            int2 mainWindowSize = ResolveLayoutSurfaceSize();
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            float originX = (float)((liveWidth - anchorSpaceSize.X) * 0.5d);
            float originY = (float)((liveHeight - anchorSpaceSize.Y) * 0.5d);
            return new float2(originX, originY);
        }

        /// <summary>
        /// Determines whether the fitted anchor space changed between layout passes.
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
        /// Determines whether the fitted root origin changed between layout passes.
        /// </summary>
        /// <param name="currentCanvasOrigin">Previously applied root origin.</param>
        /// <param name="resolvedCanvasOrigin">Newly resolved root origin.</param>
        /// <returns>True when the root origin changed.</returns>
        bool DidCanvasOriginChange(float2 currentCanvasOrigin, float2 resolvedCanvasOrigin) {
            return currentCanvasOrigin.X != resolvedCanvasOrigin.X ||
                   currentCanvasOrigin.Y != resolvedCanvasOrigin.Y;
        }

        /// <summary>
        /// Raises anchor-space change notifications when listeners are present.
        /// </summary>
        void RaiseAnchorBoundsChanged() {
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
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
    }
}

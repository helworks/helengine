namespace helengine {
    /// <summary>
    /// Scales one authored 2D subtree from a reference canvas into the camera's layout surface size while preserving the original layout as the source of truth.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public class ReferenceCanvasFitComponent : UpdateComponent, IAnchorBoundsProvider {
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
            CurrentAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentCanvasOriginValue = new float2(0f, 0f);
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

            int currentEntityCount = CountEntitiesRecursive(Parent);
            if (currentEntityCount != SnapshotEntityCountValue) {
                RebuildSnapshots();
                PendingApplyValue = true;
            }

            RefreshLayoutProviders();
            if (PendingApplyValue) {
                ApplyCurrentScale();
                PendingApplyValue = false;
            }
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
                CaptureSnapshotsRecursive(entity.Children[childIndex], false);
            }
        }

        /// <summary>
        /// Deletes the current snapshot records while preserving the reusable snapshot list.
        /// </summary>
        void ReleaseSnapshotItems() {
            SnapshotsValue.Clear();
        }

        /// <summary>
        /// Deletes all snapshot records and the native list wrapper owned by this fit component.
        /// </summary>
        void ReleaseSnapshots() {
            ReleaseSnapshotItems();
            NativeOwnership.Delete(SnapshotsValue);
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
            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].Apply(CurrentAnchorSpaceValue, resolvedCanvasOrigin, ReferenceWidthValue, ReferenceHeightValue);
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].RefreshAnchoring();
            }

            if (anchorSpaceChanged) {
                RaiseAnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Resolves the current fitted anchor space using the camera-scoped layout dimensions and the authored reference canvas.
        /// </summary>
        /// <returns>Anchor space that descendants should use for local anchoring.</returns>
        int2 ResolveCurrentAnchorSpaceSize() {
            int2 mainWindowSize = ResolveLayoutSurfaceSize();
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            if (LiveWindowMatchesReferenceAspect(liveWidth, liveHeight)) {
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
        /// Determines whether the current live window should be treated as the same aspect as the authored reference canvas.
        /// </summary>
        /// <param name="liveWidth">Resolved live window width.</param>
        /// <param name="liveHeight">Resolved live window height.</param>
        /// <returns>True when the live window is within one half-pixel of the authored aspect ratio.</returns>
        bool LiveWindowMatchesReferenceAspect(double liveWidth, double liveHeight) {
            double expectedWidth = liveHeight * ReferenceWidthValue / ReferenceHeightValue;
            double expectedHeight = liveWidth * ReferenceHeightValue / ReferenceWidthValue;
            return Math.Abs(liveWidth - expectedWidth) <= 0.5d || Math.Abs(liveHeight - expectedHeight) <= 0.5d;
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
                count += CountEntitiesRecursive(entity.Children[childIndex]);
            }

            return count;
        }
    }
}

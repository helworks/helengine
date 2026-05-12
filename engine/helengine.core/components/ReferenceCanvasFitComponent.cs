namespace helengine {
    /// <summary>
    /// Scales one authored 2D subtree from a reference canvas into the current main-window size while preserving the original layout as the source of truth.
    /// </summary>
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
        /// Tracks whether the component is currently subscribed to main-window resize events.
        /// </summary>
        bool IsSubscribedToWindowResizeValue;

        /// <summary>
        /// Tracks whether the subtree should be re-evaluated on the next update.
        /// </summary>
        bool PendingApplyValue;

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
        /// Captures the authored subtree and applies the first fit scale when the component is attached.
        /// </summary>
        /// <param name="entity">Entity that owns the fit component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RebuildSnapshots();
            AttachToWindowResize();
            PendingApplyValue = true;
        }

        /// <summary>
        /// Reapplies the current fit scale when the parent entity becomes enabled again and disconnects resize handling when disabled.
        /// </summary>
        /// <param name="newEnabled">True when the parent became enabled.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                AttachToWindowResize();
                PendingApplyValue = true;
                return;
            }

            DetachFromWindowResize();
        }

        /// <summary>
        /// Releases resize subscriptions when the fit component is removed.
        /// </summary>
        /// <param name="entity">Entity losing the fit component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DetachFromWindowResize();
            SnapshotsValue.Clear();
            SnapshotEntityCountValue = 0;
            PendingApplyValue = false;
        }

        /// <summary>
        /// Rebuilds the subtree snapshot after runtime scene loading finishes and reapplies the current fit scale whenever a pending layout invalidation exists.
        /// </summary>
        public override void Update() {
            base.Update();

            if (Parent == null) {
                return;
            }

            int currentEntityCount = CountEntitiesRecursive(Parent);
            if (currentEntityCount != SnapshotEntityCountValue) {
                RebuildSnapshots();
                PendingApplyValue = true;
            }

            if (PendingApplyValue) {
                ApplyCurrentScale();
                PendingApplyValue = false;
            }
        }

        /// <summary>
        /// Handles main-window resize notifications by reapplying the reference-canvas fit scale.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated window width.</param>
        /// <param name="newHeight">Updated window height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            PendingApplyValue = true;
        }

        /// <summary>
        /// Attaches to the shared main-window resize event when the component is active.
        /// </summary>
        void AttachToWindowResize() {
            if (IsSubscribedToWindowResizeValue) {
                return;
            }
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized += HandleWindowResized;
            IsSubscribedToWindowResizeValue = true;
        }

        /// <summary>
        /// Detaches from the shared main-window resize event when currently subscribed.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResizeValue) {
                return;
            }
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                IsSubscribedToWindowResizeValue = false;
                return;
            }

            Core.Instance.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResizeValue = false;
        }

        /// <summary>
        /// Rebuilds the authored snapshots for the current entity subtree.
        /// </summary>
        void RebuildSnapshots() {
            SnapshotsValue.Clear();
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
        /// Applies the fit scale resolved from the current main-window size to the captured authored subtree.
        /// </summary>
        void ApplyCurrentScale() {
            if (Parent == null || Core.Instance == null || Core.Instance.RenderManager3D == null || SnapshotsValue.Count == 0) {
                return;
            }

            AnchorSpace resolvedAnchorSpace = ResolveCurrentAnchorSpace();
            float2 resolvedCanvasOrigin = ResolveCurrentCanvasOrigin(resolvedAnchorSpace);
            bool anchorSpaceChanged = DidAnchorSpaceChange(CurrentAnchorSpaceValue, resolvedAnchorSpace) ||
                                      DidCanvasOriginChange(CurrentCanvasOriginValue, resolvedCanvasOrigin);

            CurrentAnchorSpaceValue = resolvedAnchorSpace;
            CurrentCanvasOriginValue = resolvedCanvasOrigin;
            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].Apply(resolvedAnchorSpace, resolvedCanvasOrigin, ReferenceWidthValue, ReferenceHeightValue);
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].RefreshAnchoring();
            }

            if (anchorSpaceChanged) {
                RaiseAnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Resolves the current fitted anchor space using the live main-window dimensions and the authored reference canvas.
        /// </summary>
        /// <returns>Anchor space that descendants should use for local anchoring.</returns>
        AnchorSpace ResolveCurrentAnchorSpace() {
            int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            if (LiveWindowMatchesReferenceAspect(liveWidth, liveHeight)) {
                return new AnchorSpace(new int2((int)Math.Round(liveWidth), (int)Math.Round(liveHeight)), new float2(0f, 0f));
            }

            double widthScale = liveWidth / ReferenceWidthValue;
            double heightScale = liveHeight / ReferenceHeightValue;
            double scale = Math.Min(widthScale, heightScale);
            if (scale <= 0d) {
                return new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            }

            int fittedWidth = Math.Max(1, (int)Math.Round(ReferenceWidthValue * scale));
            int fittedHeight = Math.Max(1, (int)Math.Round(ReferenceHeightValue * scale));
            return new AnchorSpace(new int2(fittedWidth, fittedHeight), new float2(0f, 0f));
        }

        /// <summary>
        /// Resolves the fitted origin applied to the root entity of the authored subtree.
        /// </summary>
        /// <param name="anchorSpace">Anchor space resolved for the current live window.</param>
        /// <returns>Root-entity offset that places the fitted canvas inside the live window.</returns>
        float2 ResolveCurrentCanvasOrigin(AnchorSpace anchorSpace) {
            int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            float originX = (float)((liveWidth - anchorSpace.Size.X) * 0.5d);
            float originY = (float)((liveHeight - anchorSpace.Size.Y) * 0.5d);
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
        bool DidAnchorSpaceChange(AnchorSpace currentAnchorSpace, AnchorSpace resolvedAnchorSpace) {
            if (currentAnchorSpace == null) {
                return true;
            }

            return currentAnchorSpace.Size.X != resolvedAnchorSpace.Size.X ||
                   currentAnchorSpace.Size.Y != resolvedAnchorSpace.Size.Y ||
                   currentAnchorSpace.Origin.X != resolvedAnchorSpace.Origin.X ||
                   currentAnchorSpace.Origin.Y != resolvedAnchorSpace.Origin.Y;
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

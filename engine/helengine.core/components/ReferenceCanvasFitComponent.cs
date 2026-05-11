namespace helengine {
    /// <summary>
    /// Scales one authored 2D subtree from a reference canvas into the current main-window size while preserving the original layout as the source of truth.
    /// </summary>
    public class ReferenceCanvasFitComponent : UpdateComponent {
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
        /// Initializes a new fit component using the default scene canvas profile as its authored reference.
        /// </summary>
        public ReferenceCanvasFitComponent() {
            ReferenceWidthValue = SceneCanvasProfile.DefaultWidth;
            ReferenceHeightValue = SceneCanvasProfile.DefaultHeight;
            SnapshotsValue = new List<ReferenceCanvasFitSnapshot>();
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

            CaptureSnapshotsRecursive(Parent);
            SnapshotEntityCountValue = SnapshotsValue.Count;
        }

        /// <summary>
        /// Recursively captures one entity subtree into immutable authored snapshots.
        /// </summary>
        /// <param name="entity">Current entity to capture.</param>
        void CaptureSnapshotsRecursive(Entity entity) {
            SnapshotsValue.Add(new ReferenceCanvasFitSnapshot(entity));

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                CaptureSnapshotsRecursive(entity.Children[childIndex]);
            }
        }

        /// <summary>
        /// Applies the fit scale resolved from the current main-window size to the captured authored subtree.
        /// </summary>
        void ApplyCurrentScale() {
            if (Parent == null || Core.Instance == null || Core.Instance.RenderManager3D == null || SnapshotsValue.Count == 0) {
                return;
            }

            double scale = ResolveCurrentScale();
            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].Apply(scale);
            }

            for (int snapshotIndex = 0; snapshotIndex < SnapshotsValue.Count; snapshotIndex++) {
                SnapshotsValue[snapshotIndex].RefreshAnchoring();
            }
        }

        /// <summary>
        /// Resolves the current uniform fit scale using the live main-window dimensions and the authored reference canvas.
        /// </summary>
        /// <returns>Uniform scale that fits the reference canvas inside the current main window.</returns>
        double ResolveCurrentScale() {
            int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
            double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
            double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
            double widthScale = liveWidth / ReferenceWidthValue;
            double heightScale = liveHeight / ReferenceHeightValue;
            double scale = Math.Min(widthScale, heightScale);
            if (scale <= 0d) {
                return 1d;
            }

            return scale;
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

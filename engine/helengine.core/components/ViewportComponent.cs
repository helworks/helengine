namespace helengine {
    /// <summary>
    /// Exposes a reusable logical viewport that can follow the current screen or a camera and can optionally scale one authored subtree against a reference canvas.
    /// </summary>
    public class ViewportComponent : UpdateComponent, IAnchorBoundsProvider, ICameraBoundViewportOwner {
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
                if (ScalingModeValue == ReferenceCanvasScalingMode) {
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
            }
        }

        /// <summary>
        /// Rebuilds viewport scaling snapshots after subtree changes and reapplies scaling when a pending layout invalidation exists.
        /// </summary>
        public override void Update() {
            base.Update();
            RefreshSubscriptions();

            if (Parent == null || ScalingModeValue != ReferenceCanvasScalingMode) {
                return;
            }

            int currentEntityCount = CountEntitiesRecursive(Parent);
            if (currentEntityCount != SnapshotEntityCountValue) {
                RebuildSnapshots();
                PendingScaleApplyValue = true;
            }

            if (PendingScaleApplyValue) {
                ApplyCurrentScale();
                PendingScaleApplyValue = false;
            }
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
        }

        /// <summary>
        /// Attaches to the current ancestor camera viewport changes when one exists.
        /// </summary>
        void AttachToCamera() {
            if (ActiveCameraComponentValue == null) {
                return;
            }

            ActiveCameraComponentValue.ViewportChanged += HandleCameraViewportChanged;
        }

        /// <summary>
        /// Detaches from the current ancestor camera viewport changes when one is active.
        /// </summary>
        void DetachFromCamera() {
            if (ActiveCameraComponentValue != null) {
                ActiveCameraComponentValue.ViewportChanged -= HandleCameraViewportChanged;
                ActiveCameraComponentValue = null;
            }
        }

        /// <summary>
        /// Attaches the screen resize fallback when it is not already active.
        /// </summary>
        void AttachToWindowResize() {
            if (IsSubscribedToWindowResizeValue) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized += HandleWindowResized;
            IsSubscribedToWindowResizeValue = true;
        }

        /// <summary>
        /// Detaches the screen resize fallback when it is active.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResizeValue) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResizeValue = false;
        }

        /// <summary>
        /// Handles ancestor camera viewport changes.
        /// </summary>
        void HandleCameraViewportChanged() {
            RaiseAnchorBoundsChanged();
        }

        /// <summary>
        /// Handles window resize notifications for the screen binding mode.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated window width.</param>
        /// <param name="newHeight">Updated window height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            PendingScaleApplyValue = true;
            RaiseAnchorBoundsChanged();
        }

        /// <summary>
        /// Resolves the active viewport bounds without forcing another subscription refresh.
        /// </summary>
        /// <returns>Resolved viewport bounds in local pixels.</returns>
        int2 ResolveAnchorBounds() {
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
                int2 screenSize = Core.Instance.RenderManager3D.MainWindowSize;
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
        /// <returns>Camera currently driving this viewport, or null when no binding is available.</returns>
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
        /// <returns>Resolved camera binding for rendering and layout decisions, or null when no camera is bound.</returns>
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
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                return viewport;
            }

            int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
            if (mainWindowSize.X <= 0 || mainWindowSize.Y <= 0) {
                return viewport;
            }

            return CameraViewportResolver.ResolveViewport(viewport, mainWindowSize.X, mainWindowSize.Y);
        }

        /// <summary>
        /// Resolves the nearest ancestor camera component in the current entity chain.
        /// </summary>
        /// <returns>Nearest ancestor camera component when one exists; otherwise null.</returns>
        CameraComponent ResolveAncestorCameraComponent() {
            Entity current = Parent;

            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is CameraComponent cameraComponent) {
                            return cameraComponent;
                        }
                    }
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Rebuilds the authored snapshots for the current entity subtree when viewport-owned scaling is active.
        /// </summary>
        void RebuildSnapshots() {
            ReleaseLayoutSnapshotItems();
            if (Parent == null) {
                SnapshotEntityCountValue = 0;
                return;
            }

            CaptureSnapshotsRecursive(Parent, true);
            SnapshotEntityCountValue = LayoutSnapshotsValue.Count;
        }

        /// <summary>
        /// Recursively captures one entity subtree into immutable authored snapshots.
        /// </summary>
        /// <param name="entity">Current entity to capture.</param>
        /// <param name="isRootEntity">True when the entity is the root of the scaled subtree.</param>
        void CaptureSnapshotsRecursive(Entity entity, bool isRootEntity) {
            LayoutSnapshotsValue.Add(new ViewportLayoutSnapshot(entity, isRootEntity));
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
        void ReleaseLayoutSnapshotItems() {
            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                NativeOwnership.Delete(LayoutSnapshotsValue[snapshotIndex]);
            }

            LayoutSnapshotsValue.Clear();
        }

        /// <summary>
        /// Deletes all snapshot records and the native list wrapper owned by this viewport component.
        /// </summary>
        void ReleaseLayoutSnapshots() {
            ReleaseLayoutSnapshotItems();
            NativeOwnership.Delete(LayoutSnapshotsValue);
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
            for (int snapshotIndex = 0; snapshotIndex < LayoutSnapshotsValue.Count; snapshotIndex++) {
                LayoutSnapshotsValue[snapshotIndex].Apply(CurrentAnchorSpaceValue, resolvedCanvasOrigin, ReferenceWidthValue, ReferenceHeightValue);
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
            if (ScalingModeValue == ReferenceCanvasScalingMode) {
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

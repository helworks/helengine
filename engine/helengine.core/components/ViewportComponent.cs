namespace helengine {
    /// <summary>
    /// Exposes a reusable logical viewport that can follow the current screen or a camera and can optionally scale one authored subtree against a reference canvas.
    /// </summary>
    [RunInEditor]
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
        Real PixelsPerCentimeterValue = (RealEvaluation)(160d / 2.54d);

        /// <summary>User-selected size multiplier applied after physical density conversion.</summary>
        Real UiScaleValue = 1f;

        /// <summary>Originals exist only for Dynamic players and editor authoring previews.</summary>
        ViewportLayoutCache LayoutCache;
        /// <summary>Last applied values exist only in the editor to distinguish preview edits.</summary>
        ViewportLayoutCache AppliedLayoutCache;
        /// <summary>Factors shared by presentation consumers after preparation.</summary>
        ViewportLayoutMetrics CurrentMetrics;
        /// <summary>Freezes the single-pass presentation rectangle without retaining participant originals.</summary>
        float4 PreparedViewportBounds;
        /// <summary>Cold hierarchy mutation requires participant discovery on the next active pass.</summary>
        bool LayoutStructureDirty = true;

        /// <summary>Stores the selected preparation policy; old scene payloads retain Dynamic.</summary>
        ViewportLayoutProcessingMode LayoutProcessingModeValue;

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
        /// Stores the display pixel aspect the last scale pass used, so the recorded glyph scale matches the one the snapshots applied.
        /// </summary>
        Real CurrentPixelAspectValue;

        /// <summary>
        /// Authored canvas size that the full-viewport anchor space resolves to while a layout owner re-anchors its subtree in
        /// authored space (editor restore only), with a zero origin; (0, 0) when the live viewport bounds apply.
        /// </summary>
        int2 AuthoredViewportRectSizeValue;

        /// <summary>
        /// Tracks whether the component is subscribed to screen resize events.
        /// </summary>
        bool IsSubscribedToWindowResizeValue;

        /// <summary>
        /// Tracks whether the authored subtree should be re-evaluated on the next update.
        /// </summary>
        bool PendingScaleApplyValue;

        /// <summary>Tracks whether snapshots already contain the authored values used by a completed scale pass since attachment.</summary>
        bool HasAppliedScaleValue;

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
            CurrentAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentViewportAnchorSpaceValue = new AnchorSpace(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            CurrentCanvasOriginValue = new float2(0f, 0f);
            CurrentPixelAspectValue = RenderManager3D.SquarePixelAspect;
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

        /// <summary>Chooses cook, single-load or dynamic processing before scene preparation.</summary>
        [ScenePersistenceAppend(0)]
        public ViewportLayoutProcessingMode LayoutProcessingMode {
            get { return LayoutProcessingModeValue; }
            set {
                if (HasAppliedScaleValue && LayoutProcessingModeValue != value && !IsEditorAuthoring()) {
                    throw new InvalidOperationException("Changing viewport layout processing requires reloading the scene.");
                }
                LayoutProcessingModeValue = value;
            }
        }

        /// <summary>Stores shared preparation metadata only in packaged cooked scenes.</summary>
        [ScenePersistenceAppend(1)]
        [EditorPropertyHidden]
        public ViewportCookedLayout CookedLayout { get; set; }

        /// <summary>Gets the number of retained original UI transform records for memory diagnostics.</summary>
        public int RetainedLayoutPositionCount => LayoutCache == null ? 0 : LayoutCache.PositionCount;

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
                    if (HasAppliedScaleValue && !IsEditorAuthoring() && LayoutProcessingModeValue != ViewportLayoutProcessingMode.Dynamic) {
                        throw new InvalidOperationException("Changing a prepared single-pass viewport scaling mode requires reloading the scene.");
                    }
                    bool boundaryChanged = (ScalingModeValue == NoScalingMode) != (value == NoScalingMode);
                    if (boundaryChanged && ParentUnsafe != null) {
                        RestoreAuthoredLayout();
                        if (LayoutCache != null) { LayoutCache.RestoreAuthoredLayout(); }
                        ReleaseLayoutCache();
                        HasAppliedScaleValue = false;
                        ViewportLayoutInvalidation.BeforeScalingBoundaryChange(ParentUnsafe);
                    }
                    ScalingModeValue = value;
                    PendingScaleApplyValue = true;
                    if (boundaryChanged && ParentUnsafe != null) { ViewportLayoutInvalidation.RefreshBindings(ParentUnsafe); }
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>Gets or sets how many authored viewport pixels represent one centimeter in density mode; host DPI determines the approximate physical conversion.</summary>
        public Real PixelsPerCentimeter {
            get { return PixelsPerCentimeterValue; }
            set {
                if (!(value > 0f) || RealEvaluation.IsInfinity(value)) {
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
        public Real UiScale {
            get { return UiScaleValue; }
            set {
                if (!(value > 0f) || RealEvaluation.IsInfinity(value)) {
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
        public Real DensityScale {
            get {
                if (UsesPreparedSinglePassBounds()) { return (RealEvaluation)CurrentMetrics.ScaleX; }
                string tag = ActiveCameraComponentValue == null || ActiveCameraComponentValue.OutputWindowTag.Length == 0
                    ? "main" : ActiveCameraComponentValue.OutputWindowTag;
                return (RealEvaluation)(OwnerCore.RenderManager3D.GetWindowDensityDpi(tag) / (PixelsPerCentimeterValue * 2.54d) * UiScaleValue);
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
                CurrentViewportAnchorSpaceValue.Update(ResolveAnchorBounds(), ResolveViewportAnchorOrigin());
                return CurrentViewportAnchorSpaceValue;
            }
        }

        /// <summary>
        /// Gets the resolved viewport rectangle in pixel-space coordinates.
        /// </summary>
        public float4 ResolvedViewportBounds {
            get {
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
                    RealMath.Max(1, (int)RealMath.Round(viewport.Z)),
                    RealMath.Max(1, (int)RealMath.Round(viewport.W)));
            }
        }

        /// <summary>
        /// Gets the display pixel aspect of the surface this viewport resolves against: the primary window for screen
        /// binding, the bound camera's output for camera binding, and square pixels for a fixed size or while the authored
        /// reference rect is active.
        /// </summary>
        public Real ResolvedPixelAspect {
            get {
                return ResolvePixelAspect();
            }
        }

        /// <summary>
        /// Rebinds the viewport listeners when the component is attached to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            HasAppliedScaleValue = false;
            LayoutStructureDirty = true;
            RefreshSubscriptions();
            PendingScaleApplyValue = true;
        }

        /// <summary>
        /// Releases viewport subscriptions when the component is removed from its entity.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            RestoreAuthoredLayout();
            if (LayoutCache != null) { LayoutCache.RestoreAuthoredLayout(); }
            base.ComponentRemoved(entity);
            DetachFromCamera();
            DetachFromWindowResize();
            DetachFromWindowLayout();
            ReleaseLayoutCacheItems();
            PendingScaleApplyValue = false;
        }

        /// <summary>
        /// Releases viewport-owned typed records and subscriptions before the native backend deletes this component.
        /// </summary>
        public override void Dispose() {
            DetachFromCamera();
            DetachFromWindowResize();
            DetachFromWindowLayout();
            ReleaseLayoutCache();
            NativeOwnership.Delete(CookedLayout);
            CookedLayout = null;
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
            ApplyPendingSceneLayout();
        }

        /// <summary>Rebuilds viewport optional layout buffers after subtree changes and reapplies pending layout changes.</summary>
        public override void Update() {
            base.Update();
            ApplyPendingSceneLayout();
        }

        /// <summary>Runs builtin viewport layout without dispatching a derived gameplay Update during scene preparation.</summary>
        void ApplyPendingSceneLayout() {
            if (Parent == null || (HasAppliedScaleValue && !PendingScaleApplyValue && !LayoutStructureDirty)) { return; }
            if ((byte)LayoutProcessingModeValue > (byte)ViewportLayoutProcessingMode.Cook) {
                throw new InvalidOperationException("Unknown viewport layout processing policy.");
            }
            bool editor = IsEditorAuthoring();
            if (HasAppliedScaleValue && !editor && LayoutProcessingModeValue != ViewportLayoutProcessingMode.Dynamic) {
                PendingScaleApplyValue = false;
                LayoutStructureDirty = false;
                return;
            }
            RefreshSubscriptions();
            if (editor) { RestoreAuthoredLayout(); }
            if (!editor && LayoutProcessingModeValue == ViewportLayoutProcessingMode.Cook) {
                if (CookedLayout == null || CookedLayout.AlgorithmVersion != ViewportCookedLayout.CurrentAlgorithmVersion) {
                    throw new InvalidOperationException("Cook viewport requires compatible prepared layout metadata.");
                }
                int2 target = ResolveAnchorBounds();
                if (target.X != CookedLayout.TargetWidth || target.Y != CookedLayout.TargetHeight || RealMath.Abs(ResolvePixelAspect() - CookedLayout.PixelAspect) > 0.000001d) {
                    throw new InvalidOperationException("Cooked viewport presentation target changed; load a scene cooked for this target.");
                }
                CurrentMetrics = new ViewportLayoutMetrics {
                    ScaleX = CookedLayout.ScaleX, ScaleY = CookedLayout.ScaleY, GlyphScale = CookedLayout.GlyphScale,
                    AnchorSize = CookedLayout.AnchorSize, Origin = CookedLayout.Origin, PixelAspect = CookedLayout.PixelAspect
                };
                PublishMetrics();
                ViewportLayoutTraversal.NotifyAnimations(Parent);
            } else {
                if (IsReferenceCanvasScalingActive() && (editor || LayoutProcessingModeValue == ViewportLayoutProcessingMode.Dynamic)) {
                    if (LayoutCache == null) { LayoutCache = ViewportLayoutCache.Capture(Parent); }
                    else if (LayoutStructureDirty) { LayoutCache.Reconcile(Parent); }
                }
                ApplyCurrentScale();
                if (editor && LayoutCache != null) { RecordAppliedLayout(); }
            }
            PreparedViewportBounds = ResolveViewportBounds();
            HasAppliedScaleValue = true;
            PendingScaleApplyValue = false;
            LayoutStructureDirty = false;
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
            if (!IsEditorAuthoring() || Parent == null || LayoutCache == null || AppliedLayoutCache == null) { return; }
            LayoutCache.RestoreEdited(AppliedLayoutCache, CurrentMetrics);
            if (ScalingModeValue != DensityScalingMode) {
                CurrentAnchorSpaceValue.Update(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
                BeginAuthoredViewportRect(new int2(ReferenceWidthValue, ReferenceHeightValue));
                try { ViewportLayoutTraversal.RefreshAnchoring(Parent, false); }
                finally { EndAuthoredViewportRect(); }
            }
            LayoutCache.RecaptureValues();
            AppliedLayoutCache.Release();
            NativeOwnership.Delete(AppliedLayoutCache);
            AppliedLayoutCache = null;
            PendingScaleApplyValue = true;
        }

        /// <summary>
        /// Makes this viewport's bounds and full-viewport anchor space resolve to one authored canvas with a zero origin while a
        /// layout owner re-anchors descendants in authored space during an editor restore. Callers must pair it with
        /// <see cref="EndAuthoredViewportRect"/> in a finally block.
        /// </summary>
        /// <param name="authoredCanvasSize">Reference canvas of the owner that is restoring its subtree.</param>
        internal void BeginAuthoredViewportRect(int2 authoredCanvasSize) {
            AuthoredViewportRectSizeValue = authoredCanvasSize;
        }

        /// <summary>
        /// Returns this viewport's bounds and full-viewport anchor space to the live viewport after an authored re-anchor pass.
        /// </summary>
        internal void EndAuthoredViewportRect() {
            AuthoredViewportRectSizeValue = new int2(0, 0);
        }

        /// <summary>
        /// Gets whether this viewport rescales its subtree (reference fit, reference stretch or density). A viewport without
        /// scaling leaves its subtree's values to any enclosing layout owner.
        /// </summary>
        internal bool ScalesSubtree => IsReferenceCanvasScalingActive();

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
        /// The scales mirror the ones the shared layout applier derives from the same anchor space and reference canvas.
        /// </summary>
        void RecordAppliedLayout() {
            if (AppliedLayoutCache != null) { AppliedLayoutCache.Release(); NativeOwnership.Delete(AppliedLayoutCache); }
            AppliedLayoutCache = ViewportLayoutCache.Capture(Parent);
        }

        /// <summary>
        /// Updates event subscriptions for the current binding mode.
        /// </summary>
        void RefreshSubscriptions() {
            if (Parent == null || OwnerCore == null || OwnerCore.RenderManager3D == null) { return; }
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
            if (AuthoredViewportRectSizeValue.X > 0 && AuthoredViewportRectSizeValue.Y > 0) {
                return AuthoredViewportRectSizeValue;
            }

            float4 viewport = ResolveViewportBounds();
            return new int2(
                RealMath.Max(1, (int)RealMath.Round(viewport.Z)),
                RealMath.Max(1, (int)RealMath.Round(viewport.W)));
        }

        /// <summary>
        /// Resolves the active viewport rectangle without forcing another subscription refresh.
        /// </summary>
        /// <returns>Resolved viewport rectangle in pixel-space coordinates.</returns>
        float4 ResolveViewportBounds() {
            if (UsesPreparedSinglePassBounds()) { return PreparedViewportBounds; }
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
        /// Resolves the display pixel aspect of the active viewport surface without forcing another subscription refresh.
        /// </summary>
        /// <returns>Displayed width of one surface pixel divided by its displayed height.</returns>
        RealEvaluation ResolvePixelAspect() {
            if (UsesPreparedSinglePassBounds()) { return CurrentMetrics.PixelAspect; }
            if (AuthoredViewportRectSizeValue.X > 0 && AuthoredViewportRectSizeValue.Y > 0) {
                return RenderManager3D.SquarePixelAspect;
            } else if (OwnerCore == null || OwnerCore.RenderManager3D == null) {
                return RenderManager3D.SquarePixelAspect;
            } else if (BindingModeValue == ScreenBindingMode) {
                return OwnerCore.RenderManager3D.MainWindowPixelAspect;
            } else if (BindingModeValue == AncestorCameraBindingMode || BindingModeValue == ExplicitCameraBindingMode) {
                CameraComponent cameraComponent = ResolveBoundCameraComponent();
                return cameraComponent != null ? OwnerCore.RenderManager3D.GetCameraPixelAspect(cameraComponent) : RenderManager3D.SquarePixelAspect;
            }

            return RenderManager3D.SquarePixelAspect;
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
        /// Deletes the current typed records while preserving the reusable typed cache.
        /// </summary>
        void ReleaseLayoutCacheItems() {
            if (AppliedLayoutCache != null) { AppliedLayoutCache.Release(); NativeOwnership.Delete(AppliedLayoutCache); AppliedLayoutCache = null; }
            if (LayoutCache != null) { LayoutCache.Release(); NativeOwnership.Delete(LayoutCache); LayoutCache = null; }
        }

        /// <summary>
        /// Deletes all typed records and the native list wrappers owned by this viewport component.
        /// </summary>
        void ReleaseLayoutCache() {
            ReleaseLayoutCacheItems();
        }

        /// <summary>
        /// Applies the current viewport-owned scale to the captured subtree.
        /// </summary>
        /// <summary>Publishes shared bounds without touching any participant values.</summary>
        void PublishMetrics() {
            CurrentAnchorSpaceValue.Update(CurrentMetrics.AnchorSize, new float2(0f, 0f));
            CurrentCanvasOriginValue = CurrentMetrics.Origin;
            CurrentPixelAspectValue = CurrentMetrics.PixelAspect;
        }

        /// <summary>Invalidates borrowed participant references before detachment, without an idle scan.</summary>
        internal void InvalidateLayout(Component component, Entity subtree) {
            if (component != null || subtree != null) { RestoreAuthoredLayout(); }
            if (AppliedLayoutCache != null) {
                if (component != null) { AppliedLayoutCache.Remove(component); }
                if (subtree != null) { AppliedLayoutCache.Remove(subtree); }
            }
            if (LayoutCache != null) {
                if (component != null) { LayoutCache.Remove(component); }
                if (subtree != null) { LayoutCache.Remove(subtree); }
            }
            LayoutStructureDirty = true;
            PendingScaleApplyValue = true;
        }

        /// <summary>Clears camera aliases and subscriptions while a removed camera is still alive.</summary>
        internal void LayoutProviderRemoved(Component provider) {
            bool explicitRemoved = ReferenceEquals(ExplicitBoundCameraComponentValue, provider);
            if (explicitRemoved) { ExplicitBoundCameraComponentValue = null; }
            if (explicitRemoved || ReferenceEquals(ActiveCameraComponentValue, provider)) {
                DetachFromCamera();
                PendingScaleApplyValue = true;
            }
        }

        /// <summary>Rebinds camera listeners once when the hierarchy changes rather than on every bounds read.</summary>
        internal void HierarchyBindingChanged() {
            RefreshSubscriptions();
            LayoutStructureDirty = true;
            PendingScaleApplyValue = true;
            RaiseAnchorBoundsChanged();
        }

        /// <summary>Identifies prepared runtime owners whose layout target remains fixed until scene reload.</summary>
        bool UsesPreparedSinglePassBounds() {
            return HasAppliedScaleValue && !IsEditorAuthoring() && LayoutProcessingModeValue != ViewportLayoutProcessingMode.Dynamic;
        }

        /// <summary>Prepares only a newly initialized subtree under an already prepared single-pass owner.</summary>
        internal void PrepareAttachedSubtree(Entity subtree) {
            if (HasAppliedScaleValue && !IsEditorAuthoring() && LayoutProcessingModeValue != ViewportLayoutProcessingMode.Dynamic && IsReferenceCanvasScalingActive()) {
                ViewportLayoutTraversal.ApplyOnce(subtree, Parent, CurrentMetrics);
                ViewportLayoutTraversal.RefreshAnchoring(subtree, true);
                ViewportLayoutTraversal.NotifyAnimations(subtree);
            }
        }

        /// <summary>Gets prepared factors for animation and other layout-space consumers.</summary>
        public ViewportLayoutMetrics PreparedLayoutMetrics => CurrentMetrics;

        void ApplyCurrentScale() {
            if (Parent == null) { return; }
            RealEvaluation referenceDpi = PixelsPerCentimeterValue * 2.54d;
            RealEvaluation dpi = ScalingModeValue == DensityScalingMode ? DensityScale / UiScaleValue * referenceDpi : 0d;
            CurrentMetrics = ViewportLayoutMath.Resolve(ScalingModeValue, new int2(ReferenceWidthValue, ReferenceHeightValue),
                ResolveAnchorBounds(), ResolvePixelAspect(), UiScaleValue, dpi, referenceDpi);
            PublishMetrics();
            if (IsReferenceCanvasScalingActive()) {
                if (LayoutCache != null) { LayoutCache.Apply(CurrentMetrics); }
                else { ViewportLayoutTraversal.ApplyOnce(Parent, Parent, CurrentMetrics); }
                ViewportLayoutTraversal.RefreshAnchoring(Parent, false);
                ViewportLayoutTraversal.NotifyAnimations(Parent);
            }
            RaiseAnchorBoundsChanged();
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

            RealEvaluation liveWidth = viewportBounds.X > 0 ? viewportBounds.X : ReferenceWidthValue;
            RealEvaluation liveHeight = viewportBounds.Y > 0 ? viewportBounds.Y : ReferenceHeightValue;
            return DisplayAspectFitUtils.ResolveFittedSize(liveWidth, liveHeight, ReferenceWidthValue, ReferenceHeightValue, ResolvePixelAspect());
        }

        /// <summary>
        /// Resolves the root-entity offset that centers the scaled subtree inside the live viewport.
        /// </summary>
        /// <param name="anchorSpace">Current scaled anchor space.</param>
        /// <returns>Root-entity offset applied to the scaled subtree.</returns>
        float2 ResolveCurrentCanvasOrigin(int2 anchorSpaceSize) {
            int2 viewportBounds = ResolveAnchorBounds();
            RealEvaluation liveWidth = viewportBounds.X > 0 ? viewportBounds.X : ReferenceWidthValue;
            RealEvaluation liveHeight = viewportBounds.Y > 0 ? viewportBounds.Y : ReferenceHeightValue;
            RealEvaluation originX = (RealEvaluation)((liveWidth - anchorSpaceSize.X) * 0.5d);
            RealEvaluation originY = (RealEvaluation)((liveHeight - anchorSpaceSize.Y) * 0.5d);
            return new float2(originX, originY);
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
            if (AuthoredViewportRectSizeValue.X > 0 && AuthoredViewportRectSizeValue.Y > 0) {
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
        /// Raises the viewport changed event when listeners are present.
        /// </summary>
        void RaiseAnchorBoundsChanged() {
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
        }
    }
}

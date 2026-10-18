namespace helengine {
    /// <summary>
    /// Scales one authored 2D subtree from a reference canvas into the camera's layout surface size while preserving the original layout as the source of truth.
    /// </summary>
    [RunInEditor]
    public class ReferenceCanvasFitComponent : UpdateComponent, IAnchorBoundsProvider, IAuthoredLayoutOwner {
        /// <summary>
        /// Backing field for the authored reference canvas width.
        /// </summary>
        int ReferenceWidthValue;

        /// <summary>
        /// Backing field for the authored reference canvas height.
        /// </summary>
        int ReferenceHeightValue;

        /// <summary>Originals retained only for Dynamic players or editor preview.</summary>
        ViewportLayoutCache LayoutCache;
        /// <summary>Editor-only last applied participant values.</summary>
        ViewportLayoutCache AppliedLayoutCache;
        /// <summary>Factors resolved by the most recent preparation.</summary>
        ViewportLayoutMetrics CurrentMetrics;
        /// <summary>Tracks completion without retaining per-entity processed flags.</summary>
        bool HasAppliedLayout;
        /// <summary>Marks cold participant changes that require discovery.</summary>
        bool LayoutStructureDirty = true;

        /// <summary>Shared cooked bounds and factors for this legacy owner, without original participant data.</summary>
        [ScenePersistenceAppend(0)]
        [EditorPropertyHidden]
        public ViewportCookedLayout CookedLayout { get; set; }

        /// <summary>Gets the number of retained UI transform originals for memory diagnostics.</summary>
        public int RetainedLayoutPositionCount => LayoutCache == null ? 0 : LayoutCache.PositionCount;

        /// <summary>Gets shared prepared factors for animation and other layout-space consumers.</summary>
        public ViewportLayoutMetrics PreparedLayoutMetrics => CurrentMetrics;

        /// <summary>Gets the enclosing viewport policy, preserving Dynamic for a standalone legacy fit.</summary>
        public ViewportLayoutProcessingMode EffectiveLayoutProcessingMode => LayoutViewport == null ? ViewportLayoutProcessingMode.Dynamic : LayoutViewport.LayoutProcessingMode;

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
                PendingApplyValue = true;
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
                PendingApplyValue = true;
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
            int2 anchorSpaceSize = HasAppliedLayout ? CurrentMetrics.AnchorSize : ResolveCurrentAnchorSpaceSize();
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
            LayoutStructureDirty = true;
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
            RestoreAuthoredLayout();
            if (LayoutCache != null) { LayoutCache.RestoreAuthoredLayout(); }
            base.ComponentRemoved(entity);
            DetachFromWindowLayoutChanges();
            ReleaseLayoutCacheItems();
            PendingApplyValue = false;
        }

        /// <summary>
        /// Releases fit snapshots and resize subscriptions before the native backend deletes this component.
        /// </summary>
        public override void Dispose() {
            DetachFromWindowLayoutChanges();
            ReleaseLayoutCache();
            NativeOwnership.Delete(CookedLayout);
            CookedLayout = null;
            NativeOwnership.Delete(CurrentAnchorSpaceValue);
            CurrentAnchorSpaceValue = null;
            base.Dispose();
        }

        /// <summary>Captures the complete initialized subtree and resolves its initial canvas fit before scene publication.</summary>
        internal void PrepareSceneLayout() {
            ApplyPendingSceneLayout();
        }

        /// <summary>Rebuilds the subtree snapshot after hierarchy changes and reapplies pending canvas-fit changes.</summary>
        public override void Update() {
            base.Update();
            ApplyPendingSceneLayout();
        }

        /// <summary>Runs builtin canvas layout without dispatching a derived gameplay Update during scene preparation.</summary>
        void ApplyPendingSceneLayout() {
            if (Parent == null || OwnerCore == null || OwnerCore.RenderManager3D == null || (HasAppliedLayout && !PendingApplyValue && !LayoutStructureDirty)) { return; }
            RefreshLayoutProviders();
            bool editor = IsEditorAuthoring();
            ViewportLayoutProcessingMode mode = LayoutViewport == null ? ViewportLayoutProcessingMode.Dynamic : LayoutViewport.LayoutProcessingMode;
            if (HasAppliedLayout && !editor && mode != ViewportLayoutProcessingMode.Dynamic) {
                PendingApplyValue = false; LayoutStructureDirty = false; return;
            }
            if (editor) { RestoreAuthoredLayout(); }
            if (!editor && mode == ViewportLayoutProcessingMode.Cook) {
                if (CookedLayout == null || CookedLayout.AlgorithmVersion != 1) { throw new InvalidOperationException("Cook legacy fit requires compatible prepared metadata."); }
                CurrentMetrics = new ViewportLayoutMetrics {
                    ScaleX = CookedLayout.ScaleX, ScaleY = CookedLayout.ScaleY, GlyphScale = CookedLayout.GlyphScale,
                    Origin = CookedLayout.Origin, AnchorSize = CookedLayout.AnchorSize, PixelAspect = CookedLayout.PixelAspect
                };
                PublishMetrics();
                ViewportLayoutTraversal.NotifyAnimations(Parent);
            } else {
                if (editor || mode == ViewportLayoutProcessingMode.Dynamic) {
                    if (LayoutCache == null) { LayoutCache = ViewportLayoutCache.Capture(Parent); }
                    else if (LayoutStructureDirty) { LayoutCache.Reconcile(Parent); }
                }
                ApplyCurrentScale();
            }
            HasAppliedLayout = true;
            PendingApplyValue = false;
            LayoutStructureDirty = false;
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
            if (!IsEditorAuthoring() || Parent == null || LayoutCache == null || AppliedLayoutCache == null) { return; }
            LayoutCache.RestoreEdited(AppliedLayoutCache, CurrentMetrics);
            CurrentAnchorSpaceValue.Update(new int2(ReferenceWidthValue, ReferenceHeightValue), new float2(0f, 0f));
            SetAuthoredViewportRect(true);
            try { ViewportLayoutTraversal.RefreshAnchoring(Parent, true); }
            finally { SetAuthoredViewportRect(false); }
            LayoutCache.RecaptureValues();
            AppliedLayoutCache.Release(); NativeOwnership.Delete(AppliedLayoutCache); AppliedLayoutCache = null;
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

            for (int snapshotIndex = 0; snapshotIndex < LayoutCache.PositionCount; snapshotIndex++) {
                Entity entity = LayoutCache.GetPositionEntity(snapshotIndex);
                if (entity.Components == null) {
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
        /// unscaled. The scales mirror the ones the shared layout applier derives from the same anchor space.
        /// </summary>
        void RecordAppliedLayout() {
            if (AppliedLayoutCache != null) { AppliedLayoutCache.Release(); NativeOwnership.Delete(AppliedLayoutCache); }
            AppliedLayoutCache = ViewportLayoutCache.Capture(Parent);
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
        /// Deletes the current snapshot records while preserving the reusable snapshot list.
        /// </summary>
        void ReleaseLayoutCacheItems() {
            if (AppliedLayoutCache != null) { AppliedLayoutCache.Release(); NativeOwnership.Delete(AppliedLayoutCache); AppliedLayoutCache = null; }
            if (LayoutCache != null) { LayoutCache.Release(); NativeOwnership.Delete(LayoutCache); LayoutCache = null; }
        }

        /// <summary>
        /// Deletes all snapshot records and the native list wrapper owned by this fit component.
        /// </summary>
        void ReleaseLayoutCache() {
            ReleaseLayoutCacheItems();
        }

        /// <summary>
        /// Applies the fit scale resolved from the current camera or viewport surface to the captured authored subtree.
        /// </summary>
        /// <summary>Publishes legacy fit bounds without writing participant values.</summary>
        void PublishMetrics() {
            CurrentAnchorSpaceValue.Update(CurrentMetrics.AnchorSize, new float2(0f, 0f));
            CurrentCanvasOriginValue = CurrentMetrics.Origin;
            CurrentPixelAspectValue = CurrentMetrics.PixelAspect;
        }

        /// <summary>Invalidates optional buffers before borrowed participants are detached.</summary>
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
            LayoutStructureDirty = true; PendingApplyValue = true;
        }

        /// <summary>Refreshes hierarchy-dependent borrowed providers at mutation time.</summary>
        internal void HierarchyBindingChanged() {
            RefreshLayoutProviders();
            LayoutStructureDirty = true;
            PendingApplyValue = true;
        }

        /// <summary>Disconnects borrowed layout providers before their native allocations can be destroyed.</summary>
        internal void LayoutProviderRemoved(Component provider) {
            if (ReferenceEquals(LayoutViewport, provider) || ReferenceEquals(LayoutCamera, provider)) {
                DetachFromLayoutProviders();
                PendingApplyValue = true;
            }
        }

        /// <summary>Applies only a new initialized subtree when this legacy owner uses a single-pass policy.</summary>
        internal void PrepareAttachedSubtree(Entity subtree) {
            if (HasAppliedLayout && !IsEditorAuthoring() && LayoutViewport != null && LayoutViewport.LayoutProcessingMode != ViewportLayoutProcessingMode.Dynamic) {
                ViewportLayoutTraversal.ApplyOnce(subtree, Parent, CurrentMetrics);
                ViewportLayoutTraversal.RefreshAnchoring(subtree, true);
                ViewportLayoutTraversal.NotifyAnimations(subtree);
            }
        }

        void ApplyCurrentScale() {
            if (Parent == null || OwnerCore == null || OwnerCore.RenderManager3D == null) { return; }
            CurrentMetrics = ViewportLayoutMath.Resolve(ViewportComponent.ReferenceCanvasScalingMode,
                new int2(ReferenceWidthValue, ReferenceHeightValue), ResolveLayoutSurfaceSize(), ResolveLayoutPixelAspect(), 1d, 0d, 0d);
            PublishMetrics();
            if (LayoutCache != null) { LayoutCache.Apply(CurrentMetrics); }
            else { ViewportLayoutTraversal.ApplyOnce(Parent, Parent, CurrentMetrics); }
            ViewportLayoutTraversal.RefreshAnchoring(Parent, true);
            ViewportLayoutTraversal.NotifyAnimations(Parent);
            RaiseAnchorBoundsChanged();
            if (IsEditorAuthoring() && LayoutCache != null) { RecordAppliedLayout(); }
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

    }
}

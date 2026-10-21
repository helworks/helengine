#if HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Publishes a cooked reference fit without retaining authored values or subscribing to output changes.</summary>
    [RunInEditor]
    public class ReferenceCanvasFitComponent : UpdateComponent, IAnchorBoundsProvider, IAuthoredLayoutOwner {
        /// <summary>Tracks the single metadata publication for this attachment.</summary>
        bool Prepared;
        /// <summary>Shared factors baked by the scene cooker.</summary>
        ViewportLayoutMetrics Metrics;
        /// <summary>Bounds borrowed by anchored descendants.</summary>
        AnchorSpace CanvasSpace = new AnchorSpace(new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight), new float2(0f, 0f));
        /// <summary>Authored reference width used by the public coordinate conversion API.</summary>
        int ReferenceWidthValue = SceneCanvasProfile.DefaultWidth;
        /// <summary>Authored reference height used by the public coordinate conversion API.</summary>
        int ReferenceHeightValue = SceneCanvasProfile.DefaultHeight;
        /// <summary>Publishes the prepared bounds once without a host resize listener.</summary>
        public event Action AnchorBoundsChanged;
        /// <summary>Contains the fixed cook target and factors, without original participant records.</summary>
        [ScenePersistenceAppend(0)]
        [EditorPropertyHidden]
        public ViewportCookedLayout CookedLayout { get; set; }
        /// <summary>Original UI records are never retained by this implementation.</summary>
        public int RetainedLayoutPositionCount => 0;
        /// <summary>Gets the shared factors used by presentation consumers.</summary>
        public ViewportLayoutMetrics PreparedLayoutMetrics => Metrics;
        /// <summary>All fit owners in this player are cooked, including standalone fits.</summary>
        public ViewportLayoutProcessingMode EffectiveLayoutProcessingMode => ViewportLayoutProcessingMode.Cook;
        /// <summary>Gets or sets the reference width before publishing cooked factors.</summary>
        public int ReferenceWidth {
            get { return ReferenceWidthValue; }
            set { RequireReferenceSize(value, ReferenceWidthValue); ReferenceWidthValue = value; }
        }
        /// <summary>Gets or sets the reference height before publishing cooked factors.</summary>
        public int ReferenceHeight {
            get { return ReferenceHeightValue; }
            set { RequireReferenceSize(value, ReferenceHeightValue); ReferenceHeightValue = value; }
        }
        /// <summary>Gets the fixed prepared canvas space.</summary>
        public AnchorSpace AnchorSpace => CanvasSpace;
        /// <summary>Gets the correction from fitted canvas coordinates to full-output coordinates.</summary>
        public float2 ViewportAnchorOrigin => new float2(-Metrics.Origin.X, -Metrics.Origin.Y);
        /// <summary>Converts reference coordinates using the prepared fitted canvas dimensions.</summary>
        public float2 CalculateScale() {
            if (!Prepared && (CookedLayout == null || CookedLayout.AlgorithmVersion != ViewportCookedLayout.CurrentAlgorithmVersion)) {
                throw new InvalidOperationException("Cook reference fit requires compatible prepared layout metadata.");
            }
            int2 size = Prepared ? Metrics.AnchorSize : CookedLayout.AnchorSize;
            return new float2((RealEvaluation)((RealEvaluation)size.X / ReferenceWidthValue),
                (RealEvaluation)((RealEvaluation)size.Y / ReferenceHeightValue));
        }
        /// <summary>Converts an authored position into prepared canvas coordinates.</summary>
        public float3 CalculatePosition(float3 referencePosition) {
            float2 scale = CalculateScale();
            return new float3(referencePosition.X * scale.X, referencePosition.Y * scale.Y, referencePosition.Z);
        }
        /// <summary>Starts a new attachment without creating subscriptions.</summary>
        public override void ComponentAdded(Entity entity) { base.ComponentAdded(entity); Prepared = false; }
        /// <summary>Detaches without restoring participant values that are already baked.</summary>
        public override void ComponentRemoved(Entity entity) { base.ComponentRemoved(entity); Prepared = false; }
        /// <summary>Releases metadata and the shared anchor-space object.</summary>
        public override void Dispose() {
            NativeOwnership.Delete(CookedLayout); CookedLayout = null;
            NativeOwnership.Delete(CanvasSpace); CanvasSpace = null;
            base.Dispose();
        }
        /// <summary>Validates and publishes cooked factors before rendering, leaving participant geometry untouched.</summary>
        internal void PrepareSceneLayout() {
            if (Prepared || Parent == null) { return; }
            ViewportCookedLayout layout = CookedLayout;
            if (layout == null || layout.AlgorithmVersion != ViewportCookedLayout.CurrentAlgorithmVersion) {
                throw new InvalidOperationException("Cook reference fit requires compatible prepared layout metadata.");
            }
            ViewportComponent viewport = CameraWindowLayoutUtils.FindAncestorViewport(Parent);
            CameraComponent camera = viewport == null ? CameraWindowLayoutUtils.FindAncestorCamera(Parent) : viewport.GetBoundCameraComponent();
            int2 size = viewport == null ? CameraWindowLayoutUtils.ResolveCameraSize(OwnerCore.RenderManager3D, camera) : viewport.ResolvedViewportSize;
            RealEvaluation aspect = viewport == null ? CameraWindowLayoutUtils.ResolveCameraPixelAspect(OwnerCore.RenderManager3D, camera) : viewport.ResolvedPixelAspect;
            if (size.X != layout.TargetWidth || size.Y != layout.TargetHeight || RealMath.Abs(aspect - layout.PixelAspect) > 0.000001d) {
                throw new InvalidOperationException("Cooked reference fit presentation target changed; load a scene cooked for this target.");
            }
            Metrics = new ViewportLayoutMetrics { ScaleX = layout.ScaleX, ScaleY = layout.ScaleY, GlyphScale = layout.GlyphScale,
                AnchorSize = layout.AnchorSize, Origin = layout.Origin, PixelAspect = layout.PixelAspect };
            CanvasSpace.Update(Metrics.AnchorSize, new float2(0f, 0f));
            Prepared = true;
            ViewportLayoutTraversal.NotifyAnimations(Parent);
            if (AnchorBoundsChanged != null) { AnchorBoundsChanged(); }
        }
        /// <summary>Supports the common lifecycle without performing another fit pass.</summary>
        public override void Update() { base.Update(); PrepareSceneLayout(); }
        /// <summary>There are no authored originals to restore in a cooked player.</summary>
        public void RestoreAuthoredLayout() { }
        /// <summary>Prepares metadata once without enabling dynamic resizing.</summary>
        public void ReapplyLayout() { PrepareSceneLayout(); }
        /// <summary>There are no retained participant references to invalidate.</summary>
        internal void InvalidateLayout(Component component, Entity subtree) { }
        /// <summary>Prepared target bounds remain unchanged until scene reload.</summary>
        internal void HierarchyBindingChanged() { }
        /// <summary>No layout providers or camera aliases are retained by this fit.</summary>
        internal void LayoutProviderRemoved(Component provider) { }
        /// <summary>Prepares a newly instantiated authored subtree once without rescaling existing siblings.</summary>
        internal void PrepareAttachedSubtree(Entity subtree) {
            if (Prepared) {
                ViewportLayoutTraversal.ApplyOnce(subtree, Parent, Metrics);
                ViewportLayoutTraversal.RefreshAnchoring(subtree, true);
                ViewportLayoutTraversal.NotifyAnimations(subtree);
            }
        }
        /// <summary>Rejects invalid canvas sizes and changes that would reinterpret already baked geometry.</summary>
        void RequireReferenceSize(int value, int current) {
            if (value < 1) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (Prepared && value != current) { throw new InvalidOperationException("Changing a cooked reference fit requires reloading the scene."); }
        }
    }
}
#endif

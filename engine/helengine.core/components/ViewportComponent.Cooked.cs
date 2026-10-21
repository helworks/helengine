#if HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Publishes a layout baked for one console presentation target, without originals or resize subscriptions.</summary>
    [RunInEditor]
    public class ViewportComponent : UpdateComponent, IAnchorBoundsProvider, ICameraBoundViewportOwner, IAuthoredLayoutOwner {
        /// <summary>Binds to the primary output.</summary>
        public const byte ScreenBindingMode = 0;
        /// <summary>Binds to the nearest camera in the entity ancestry.</summary>
        public const byte AncestorCameraBindingMode = 1;
        /// <summary>Uses the explicitly authored dimensions.</summary>
        public const byte FixedBindingMode = 2;
        /// <summary>Binds to a camera assigned before scene preparation.</summary>
        public const byte ExplicitCameraBindingMode = 3;
        /// <summary>Leaves participant dimensions unchanged.</summary>
        public const byte NoScalingMode = 0;
        /// <summary>Fits the reference canvas inside the output.</summary>
        public const byte ReferenceCanvasScalingMode = 1;
        /// <summary>Scales the reference canvas independently on each axis.</summary>
        public const byte ReferenceCanvasStretchScalingMode = 2;
        /// <summary>Uses density factors already resolved by the cooker.</summary>
        public const byte DensityScalingMode = 3;
        /// <summary>Tracks publication of metadata, independently of participant count.</summary>
        bool Prepared;
        /// <summary>Fixed output rectangle captured once before scene publication.</summary>
        float4 PreparedBounds;
        /// <summary>Shared factors consumed by animation and newly attached subtrees.</summary>
        ViewportLayoutMetrics Metrics;
        /// <summary>Prepared reference-canvas bounds shared by descendant anchors.</summary>
        AnchorSpace CanvasSpace = new AnchorSpace(new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight), new float2(0f, 0f));
        /// <summary>Prepared output bounds with the fitted canvas origin correction.</summary>
        AnchorSpace OutputSpace = new AnchorSpace(new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight), new float2(0f, 0f));
        /// <summary>Optional hierarchy-owned camera; never subscribed to resize notifications.</summary>
        CameraComponent ExplicitCamera;
        /// <summary>Stored binding selected by the authored scene.</summary>
        byte BindingModeValue;
        /// <summary>Stored fixed presentation size.</summary>
        int2 FixedSizeValue = new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight);
        /// <summary>Stored scaling policy used to identify subtree boundaries.</summary>
        byte ScalingModeValue;
        /// <summary>Stored authored canvas width, retained for the public component contract.</summary>
        int ReferenceWidthValue = SceneCanvasProfile.DefaultWidth;
        /// <summary>Stored authored canvas height, retained for the public component contract.</summary>
        int ReferenceHeightValue = SceneCanvasProfile.DefaultHeight;
        /// <summary>Stored authored density; no live DPI query is performed.</summary>
        Real PixelsPerCentimeterValue = (RealEvaluation)(160d / 2.54d);
        /// <summary>Stored authored UI multiplier.</summary>
        Real UiScaleValue = 1f;

        /// <summary>Notifies anchors once when prepared bounds are first published.</summary>
        public event Action AnchorBoundsChanged;
        /// <summary>Gets or selects the output binding before preparation.</summary>
        public byte BindingMode { get { return BindingModeValue; } set { RequireUnprepared(BindingModeValue != value); BindingModeValue = value; } }
        /// <summary>Cook-only players refuse content prepared for runtime layout processing.</summary>
        [ScenePersistenceAppend(0)]
        public ViewportLayoutProcessingMode LayoutProcessingMode {
            get { return ViewportLayoutProcessingMode.Cook; }
            set { if (value != ViewportLayoutProcessingMode.Cook) { throw new InvalidOperationException("This player requires cooked viewport layout."); } }
        }
        /// <summary>Contains the cooker's shared target and factors, without participant originals.</summary>
        [ScenePersistenceAppend(1)]
        [EditorPropertyHidden]
        public ViewportCookedLayout CookedLayout { get; set; }
        /// <summary>Cook-only players retain no original UI transform records.</summary>
        public int RetainedLayoutPositionCount => 0;
        /// <summary>Gets or sets a fixed target before preparation.</summary>
        public int2 FixedSize { get { return FixedSizeValue; } set { RequireUnprepared(FixedSizeValue.X != value.X || FixedSizeValue.Y != value.Y); FixedSizeValue = value; } }
        /// <summary>Gets or sets the authored scaling policy before preparation.</summary>
        public byte ScalingMode { get { return ScalingModeValue; } set { RequireUnprepared(ScalingModeValue != value); ScalingModeValue = value; } }
        /// <summary>Gets or sets positive authored pixel density before preparation.</summary>
        public Real PixelsPerCentimeter {
            get { return PixelsPerCentimeterValue; }
            set { if (!(value > 0f) || RealEvaluation.IsInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(value)); } RequireUnprepared(PixelsPerCentimeterValue != value); PixelsPerCentimeterValue = value; }
        }
        /// <summary>Gets or sets a positive authored multiplier before preparation.</summary>
        public Real UiScale {
            get { return UiScaleValue; }
            set { if (!(value > 0f) || RealEvaluation.IsInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(value)); } RequireUnprepared(UiScaleValue != value); UiScaleValue = value; }
        }
        /// <summary>Gets the already baked horizontal density factor.</summary>
        [ScenePersistenceIgnore]
        public Real DensityScale => (RealEvaluation)Metrics.ScaleX;
        /// <summary>Gets or sets the authored canvas width before preparation.</summary>
        public int ReferenceWidth {
            get { return ReferenceWidthValue; }
            set { if (value < 1) { throw new ArgumentOutOfRangeException(nameof(value)); } RequireUnprepared(ReferenceWidthValue != value); ReferenceWidthValue = value; }
        }
        /// <summary>Gets or sets the authored canvas height before preparation.</summary>
        public int ReferenceHeight {
            get { return ReferenceHeightValue; }
            set { if (value < 1) { throw new ArgumentOutOfRangeException(nameof(value)); } RequireUnprepared(ReferenceHeightValue != value); ReferenceHeightValue = value; }
        }
        /// <summary>Borrows an explicit camera assigned before preparation; camera references are not serialized.</summary>
        [ScenePersistenceIgnore]
        public CameraComponent BoundCameraComponent {
            get { return ExplicitCamera; }
            set { RequireUnprepared(!ReferenceEquals(ExplicitCamera, value)); ExplicitCamera = value; }
        }
        /// <summary>Gets the fixed prepared canvas anchor space.</summary>
        public AnchorSpace AnchorSpace => ScalesSubtree ? CanvasSpace : ViewportAnchorSpace;
        /// <summary>Gets the fixed prepared output anchor space.</summary>
        public AnchorSpace ViewportAnchorSpace {
            get {
                if (Prepared) { OutputSpace.Update(new int2(CookedLayout.TargetWidth, CookedLayout.TargetHeight), ResolveOutputOrigin()); }
                return OutputSpace;
            }
        }
        /// <summary>Gets the prepared output rectangle, or the initial binding during scene initialization.</summary>
        public float4 ResolvedViewportBounds => Prepared ? PreparedBounds : ResolveInitialBounds();
        /// <summary>Gets the target's pixel dimensions.</summary>
        public int2 ResolvedViewportSize {
            get {
                float4 bounds = ResolvedViewportBounds;
                return new int2(RealMath.Max(1, (int)RealMath.Round(bounds.Z)), RealMath.Max(1, (int)RealMath.Round(bounds.W)));
            }
        }
        /// <summary>Gets the fixed prepared pixel aspect.</summary>
        public Real ResolvedPixelAspect => Prepared ? (RealEvaluation)Metrics.PixelAspect : ResolveInitialPixelAspect();
        /// <summary>Gets the factors shared by presentation consumers.</summary>
        public ViewportLayoutMetrics PreparedLayoutMetrics => Metrics;
        /// <summary>Identifies independent scaling boundaries without discovering participants.</summary>
        internal bool ScalesSubtree => ScalingModeValue != NoScalingMode;

        /// <summary>Resets preparation for this attachment without subscribing to the host.</summary>
        public override void ComponentAdded(Entity entity) { base.ComponentAdded(entity); Prepared = false; }
        /// <summary>Releases the attachment without restoring already cooked geometry.</summary>
        public override void ComponentRemoved(Entity entity) { base.ComponentRemoved(entity); Prepared = false; }
        /// <summary>Releases owned metadata and anchor spaces; the bound camera remains hierarchy-owned.</summary>
        public override void Dispose() {
            NativeOwnership.Delete(CookedLayout); CookedLayout = null;
            NativeOwnership.Delete(CanvasSpace); CanvasSpace = null;
            NativeOwnership.Delete(OutputSpace); OutputSpace = null;
            ExplicitCamera = null;
            base.Dispose();
        }
        /// <summary>Publishes cooked factors before the scene's first frame; never rewrites participant geometry.</summary>
        internal void PrepareSceneLayout() {
            if (Prepared || Parent == null) { return; }
            ViewportCookedLayout layout = CookedLayout;
            if (layout == null || layout.AlgorithmVersion != ViewportCookedLayout.CurrentAlgorithmVersion) {
                throw new InvalidOperationException("Cook viewport requires compatible prepared layout metadata.");
            }
            float4 bounds = ResolveInitialBounds();
            if (RealMath.Round(bounds.Z) != layout.TargetWidth || RealMath.Round(bounds.W) != layout.TargetHeight
                || RealMath.Abs(ResolveInitialPixelAspect() - layout.PixelAspect) > 0.000001d) {
                throw new InvalidOperationException("Cooked viewport presentation target changed; load a scene cooked for this target.");
            }
            PreparedBounds = bounds;
            Metrics = new ViewportLayoutMetrics { ScaleX = layout.ScaleX, ScaleY = layout.ScaleY, GlyphScale = layout.GlyphScale,
                AnchorSize = layout.AnchorSize, Origin = layout.Origin, PixelAspect = layout.PixelAspect };
            CanvasSpace.Update(Metrics.AnchorSize, new float2(0f, 0f));
            OutputSpace.Update(new int2(layout.TargetWidth, layout.TargetHeight), ResolveOutputOrigin());
            Prepared = true;
            ViewportLayoutTraversal.NotifyAnimations(Parent);
            if (AnchorBoundsChanged != null) { AnchorBoundsChanged(); }
        }
        /// <summary>Supports the shared update lifecycle while preparation remains a single pass.</summary>
        public override void Update() { base.Update(); PrepareSceneLayout(); }
        /// <summary>Cooked players have no authored originals to restore; editor generation selects the dynamic implementation.</summary>
        public void RestoreAuthoredLayout() { }
        /// <summary>Publishes unprepared metadata once, without enabling a resize pass.</summary>
        public void ReapplyLayout() { PrepareSceneLayout(); }
        /// <summary>Authoring-only operation is unavailable in this player implementation.</summary>
        internal void BeginAuthoredViewportRect(int2 authoredCanvasSize) { throw new InvalidOperationException("Cooked players cannot restore authored viewport rectangles."); }
        /// <summary>Authoring-only operation is unavailable in this player implementation.</summary>
        internal void EndAuthoredViewportRect() { throw new InvalidOperationException("Cooked players cannot restore authored viewport rectangles."); }
        /// <summary>There are no borrowed participant records to invalidate in cooked players.</summary>
        internal void InvalidateLayout(Component component, Entity subtree) { }
        /// <summary>Cooked bounds remain fixed across hierarchy edits until the scene is reloaded.</summary>
        internal void HierarchyBindingChanged() { }
        /// <summary>Clears the only borrowed camera alias before its owner releases it.</summary>
        internal void LayoutProviderRemoved(Component provider) { if (ReferenceEquals(provider, ExplicitCamera)) { ExplicitCamera = null; } }
        /// <summary>Scales newly instantiated authored children once without revisiting existing participants.</summary>
        internal void PrepareAttachedSubtree(Entity subtree) {
            if (Prepared && ScalesSubtree) {
                ViewportLayoutTraversal.ApplyOnce(subtree, Parent, Metrics);
                ViewportLayoutTraversal.RefreshAnchoring(subtree, true);
                ViewportLayoutTraversal.NotifyAnimations(subtree);
            }
        }
        /// <summary>Resolves the borrowed camera for rendering; no change notifications are installed.</summary>
        [NativeBorrowedReturn]
        public CameraComponent GetBoundCameraComponent() {
            if (BindingModeValue == ExplicitCameraBindingMode) { return ExplicitCamera; }
            if (BindingModeValue == AncestorCameraBindingMode) { return CameraWindowLayoutUtils.FindAncestorCamera(Parent); }
            return null;
        }
        /// <summary>Resolves the actual first-frame target to reject content cooked for another video mode.</summary>
        float4 ResolveInitialBounds() {
            if (BindingModeValue == FixedBindingMode) { return new float4(0f, 0f, FixedSizeValue.X, FixedSizeValue.Y); }
            if (BindingModeValue == ScreenBindingMode) {
                int2 size = OwnerCore.RenderManager3D.MainWindowSize;
                return new float4(0f, 0f, size.X, size.Y);
            }
            CameraComponent camera = GetBoundCameraComponent();
            if (camera == null) { throw new InvalidOperationException("Cooked viewport requires its bound camera before preparation."); }
            if (camera.RenderTarget != null) { return new float4(0f, 0f, camera.RenderTarget.Width, camera.RenderTarget.Height); }
            int2 output = OwnerCore.RenderManager3D.GetCameraLayoutSize(camera);
            return CameraViewportResolver.ResolveViewport(camera.Viewport, output.X, output.Y);
        }
        /// <summary>Reads the initial surface aspect only for metadata validation.</summary>
        RealEvaluation ResolveInitialPixelAspect() {
            if (BindingModeValue == FixedBindingMode) { return RenderManager3D.SquarePixelAspect; }
            if (BindingModeValue == ScreenBindingMode) { return OwnerCore.RenderManager3D.MainWindowPixelAspect; }
            CameraComponent camera = GetBoundCameraComponent();
            if (camera == null) { throw new InvalidOperationException("Cooked viewport requires its bound camera before preparation."); }
            return OwnerCore.RenderManager3D.GetCameraPixelAspect(camera);
        }
        /// <summary>Offsets full-viewport anchors relative to the independently fitted canvas.</summary>
        float2 ResolveOutputOrigin() {
            if (ScalesSubtree) { return new float2(-Metrics.Origin.X, -Metrics.Origin.Y); }
            foreach (Component component in Parent.Components) {
                if (component is ReferenceCanvasFitComponent fit) { return fit.ViewportAnchorOrigin; }
            }
            return new float2(0f, 0f);
        }
        /// <summary>Prevents reinterpreting baked geometry under another layout contract.</summary>
        void RequireUnprepared(bool changed) {
            if (Prepared && changed) { throw new InvalidOperationException("Changing a cooked viewport requires reloading the scene."); }
        }
    }
}
#endif

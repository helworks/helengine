namespace helengine {
    /// <summary>
    /// Renders a reusable two-line FPS overlay using the core 2D text pipeline.
    /// </summary>
    public class FPSComponent : UpdateComponent {
        /// <summary>
        /// Tracks every active FPS component so the render loop can broadcast frame ticks.
        /// </summary>
        static readonly List<FPSComponent> ActiveComponents = new List<FPSComponent>();

        /// <summary>
        /// Font used by both overlay lines.
        /// </summary>
        FontAsset font;

        /// <summary>
        /// Root entity that positions the overlay in viewport space.
        /// </summary>
        Entity OverlayHost;

        /// <summary>
        /// Child entity that hosts the update-FPS text component.
        /// </summary>
        Entity UpdateRowHost;

        /// <summary>
        /// Child entity that hosts the render-FPS text component.
        /// </summary>
        Entity RenderRowHost;

        /// <summary>
        /// Text component that displays update FPS.
        /// </summary>
        TextComponent UpdateTextComponent;

        /// <summary>
        /// Text component that displays render FPS.
        /// </summary>
        TextComponent RenderTextComponent;

        /// <summary>
        /// Core elapsed-seconds marker used to measure the current sampling window.
        /// </summary>
        double LastSampleElapsedSeconds;

        /// <summary>
        /// Number of update ticks captured during the current sampling window.
        /// </summary>
        int UpdateFrameCount;

        /// <summary>
        /// Number of render ticks captured during the current sampling window.
        /// </summary>
        int RenderFrameCount;

        /// <summary>
        /// Indicates whether the overlay hierarchy has been created.
        /// </summary>
        bool Initialized;

        /// <summary>
        /// Gets or sets the sampling interval used before refreshing the visible FPS values.
        /// </summary>
        public double RefreshIntervalSeconds {
            get { return refreshIntervalSeconds; }
            set {
                if (value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Refresh interval must be zero or greater.");
                }

                refreshIntervalSeconds = value;
            }
        }

        /// <summary>
        /// Gets or sets the overlay padding applied from the top-left viewport edge.
        /// </summary>
        public int2 Padding {
            get { return padding; }
            set {
                padding = value;
                ApplyPadding();
            }
        }

        /// <summary>
        /// Gets or sets the render order used by the overlay text.
        /// </summary>
        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D == value) {
                    return;
                }

                renderOrder2D = value;
                ApplyRenderOrder();
            }
        }

        /// <summary>
        /// Gets or sets the font used by both overlay lines.
        /// </summary>
        public FontAsset Font {
            get { return font; }
            set {
                if (ReferenceEquals(font, value)) {
                    return;
                }

                font = value;
                RefreshOverlayActivation();
            }
        }

        /// <summary>
        /// Gets the last formatted update-FPS line.
        /// </summary>
        public string UpdateFpsText { get; private set; }

        /// <summary>
        /// Gets the last formatted render-FPS line.
        /// </summary>
        public string RenderFpsText { get; private set; }

        /// <summary>
        /// Stores the current refresh interval.
        /// </summary>
        double refreshIntervalSeconds = 0.5d;

        /// <summary>
        /// Stores the current overlay padding.
        /// </summary>
        int2 padding = new int2(8, 6);

        /// <summary>
        /// Stores the current render order used by both overlay text rows.
        /// </summary>
        byte renderOrder2D = 250;

        /// <summary>
        /// Creates a new FPS overlay with no implicit font fallback.
        /// </summary>
        public FPSComponent() {
            ResetSamplingWindow();
        }

        /// <summary>
        /// Builds the overlay entity hierarchy and registers the component for sampling.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            base.ComponentAdded(entity);
            RefreshOverlayActivation();
        }

        /// <summary>
        /// Removes the overlay hierarchy and unregisters the component from the active FPS list.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            TearDownOverlay();
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Resets the sampling window when the hierarchy toggles enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            ResetSamplingWindow();
            ApplyCurrentOverlayText();
        }

        /// <summary>
        /// Refreshes the visible text when the sampling window has elapsed.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                return;
            }

            TryRefreshOverlay();
        }

        /// <summary>
        /// Reconciles the overlay hierarchy with the currently assigned font and attachment state.
        /// </summary>
        void RefreshOverlayActivation() {
            if (Parent == null) {
                return;
            }

            if (Font == null) {
                TearDownOverlay();
                return;
            }

            if (!Initialized) {
                BuildOverlay();
                ResetSamplingWindow();
                ApplyCurrentOverlayText();
                ApplyPadding();
                ApplyRenderOrder();
                return;
            }

            ApplyFont();
            ApplyRenderOrder();
            ApplyPadding();
        }

        /// <summary>
        /// Creates the overlay hierarchy for the currently attached parent entity.
        /// </summary>
        void BuildOverlay() {
            if (Parent == null) {
                throw new InvalidOperationException("FPSComponent must be attached before its overlay can be created.");
            }
            if (Font == null) {
                throw new InvalidOperationException("FPSComponent overlay creation requires a font.");
            }
            if (Initialized) {
                return;
            }
            if (Parent.Children == null) {
                Parent.InitChildren();
            }

            OverlayHost = new Entity();
            OverlayHost.LayerMask = Parent.LayerMask;
            OverlayHost.InitChildren();
            OverlayHost.InitComponents();
            Parent.AddChild(OverlayHost);

            UpdateRowHost = new Entity();
            UpdateRowHost.LayerMask = Parent.LayerMask;
            UpdateRowHost.InitChildren();
            UpdateRowHost.InitComponents();
            OverlayHost.AddChild(UpdateRowHost);

            UpdateTextComponent = new TextComponent();
            UpdateTextComponent.Color = new byte4(255, 255, 255, 255);
            UpdateRowHost.AddComponent(UpdateTextComponent);

            RenderRowHost = new Entity();
            RenderRowHost.LayerMask = Parent.LayerMask;
            RenderRowHost.InitChildren();
            RenderRowHost.InitComponents();
            OverlayHost.AddChild(RenderRowHost);

            RenderTextComponent = new TextComponent();
            RenderTextComponent.Color = new byte4(255, 255, 255, 255);
            RenderRowHost.AddComponent(RenderTextComponent);

            Initialized = true;
            ActiveComponents.Add(this);
            ApplyFont();
        }

        /// <summary>
        /// Removes the overlay hierarchy and unregisters the component from frame sampling.
        /// </summary>
        void TearDownOverlay() {
            ActiveComponents.Remove(this);
            if (OverlayHost != null) {
                OverlayHost.Dispose();
            }

            OverlayHost = null;
            UpdateRowHost = null;
            RenderRowHost = null;
            UpdateTextComponent = null;
            RenderTextComponent = null;
            Initialized = false;
        }

        /// <summary>
        /// Records one update tick for every active FPS overlay.
        /// </summary>
        public static void RecordUpdateFrame() {
            for (int i = ActiveComponents.Count - 1; i >= 0; i--) {
                FPSComponent component = ActiveComponents[i];
                if (component.Initialized && component.Parent != null && component.Parent.IsHierarchyEnabled) {
                    component.UpdateFrameCount++;
                }
            }
        }

        /// <summary>
        /// Records one render tick for every active FPS overlay.
        /// </summary>
        public static void RecordRenderFrame() {
            for (int i = ActiveComponents.Count - 1; i >= 0; i--) {
                FPSComponent component = ActiveComponents[i];
                if (component.Initialized && component.Parent != null && component.Parent.IsHierarchyEnabled) {
                    component.RenderFrameCount++;
                }
            }
        }

        /// <summary>
        /// Applies the configured padding to the overlay root.
        /// </summary>
        void ApplyPadding() {
            if (OverlayHost == null) {
                return;
            }

            OverlayHost.LocalPosition = new float3(padding.X, padding.Y, 0f);
        }

        /// <summary>
        /// Applies the configured render order to the overlay text rows.
        /// </summary>
        void ApplyRenderOrder() {
            if (UpdateTextComponent != null) {
                UpdateTextComponent.RenderOrder2D = RenderOrder2D;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.RenderOrder2D = RenderOrder2D;
            }
        }

        /// <summary>
        /// Applies the configured font to both overlay text rows and repositions the second line.
        /// </summary>
        void ApplyFont() {
            if (UpdateTextComponent != null) {
                UpdateTextComponent.Font = Font;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Font = Font;
            }

            if (RenderRowHost != null) {
                RenderRowHost.LocalPosition = new float3(0f, Font.LineHeight, 0.1f);
            }
        }

        /// <summary>
        /// Resets the current sampling window and restores the placeholder text.
        /// </summary>
        void ResetSamplingWindow() {
            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            Core core = Core.Instance;
            LastSampleElapsedSeconds = core == null ? 0d : core.TotalElapsedSeconds;
            if (Initialized && core != null) {
                UpdateFpsText = ResolveUpdateOverlayText(core, 0d);
                RenderFpsText = ResolveRenderOverlayText(core, 0d, core.LastRenderManager3DDrawMilliseconds);
            } else {
                UpdateFpsText = "Update FPS: --";
                RenderFpsText = "Render FPS: -- (-- ms)";
            }

            if (UpdateTextComponent != null) {
                UpdateTextComponent.Text = UpdateFpsText;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Text = RenderFpsText;
            }
        }

        /// <summary>
        /// Applies the current core-owned or default overlay text immediately, without waiting for the next sampling window.
        /// </summary>
        void ApplyCurrentOverlayText() {
            if (!Initialized) {
                return;
            }

            Core core = Core.Instance;
            if (core == null) {
                return;
            }

            UpdateFpsText = ResolveUpdateOverlayText(core, 0d);
            RenderFpsText = ResolveRenderOverlayText(core, 0d, core.LastRenderManager3DDrawMilliseconds);

            if (UpdateTextComponent != null) {
                UpdateTextComponent.Text = UpdateFpsText;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Text = RenderFpsText;
            }
        }

        /// <summary>
        /// Formats the latest FPS values once the configured refresh interval has elapsed.
        /// </summary>
        void TryRefreshOverlay() {
            Core core = Core.Instance;
            double elapsedSeconds = core.TotalElapsedSeconds - LastSampleElapsedSeconds;
            if (refreshIntervalSeconds > 0d && elapsedSeconds < refreshIntervalSeconds) {
                return;
            }

            double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
            double updateFps = UpdateFrameCount / safeElapsedSeconds;
            double renderFps = RenderFrameCount / safeElapsedSeconds;
            UpdateFpsText = ResolveUpdateOverlayText(core, updateFps);
            RenderFpsText = ResolveRenderOverlayText(core, renderFps, core.LastRenderManager3DDrawMilliseconds);

            if (UpdateTextComponent != null) {
                UpdateTextComponent.Text = UpdateFpsText;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Text = RenderFpsText;
            }

            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            LastSampleElapsedSeconds = core.TotalElapsedSeconds;
        }

        /// <summary>
        /// Formats one FPS value using exactly one decimal place without relying on composite formatting.
        /// </summary>
        /// <param name="fps">FPS value to format.</param>
        /// <returns>One-decimal FPS text such as <c>60.3</c>.</returns>
        string FormatFpsValue(double fps) {
            int tenths = (int)Math.Round(fps * 10d, MidpointRounding.AwayFromZero);
            int whole = tenths / 10;
            int fractional = Math.Abs(tenths % 10);

            return whole + "." + fractional;
        }

        /// <summary>
        /// Formats the render FPS line together with the most recent measured render-manager draw duration.
        /// </summary>
        /// <param name="renderFps">Measured render FPS value.</param>
        /// <param name="drawMilliseconds">Measured render-manager draw duration in milliseconds.</param>
        /// <returns>Render overlay text that includes both FPS and milliseconds.</returns>
        string FormatRenderFpsText(double renderFps, double drawMilliseconds) {
            return "Render FPS: " + FormatFpsValue(renderFps) + " (" + FormatFpsValue(drawMilliseconds) + " ms)";
        }

        /// <summary>
        /// Gets whether the active runtime platform should always use the PS2 performance-overlay row format.
        /// </summary>
        /// <param name="core">Active core instance that owns platform metadata.</param>
        /// <returns><c>true</c> when the runtime platform is PS2; otherwise <c>false</c>.</returns>
        bool ShouldUsePs2PerformanceOverlay(Core core) {
            return core != null
                && core.PlatformInfo != null
                && string.Equals(core.PlatformInfo.Name, "ps2", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets whether the visible FPS rows should use the performance-overlay layout.
        /// </summary>
        /// <param name="core">Active core instance that may publish performance overlay metrics.</param>
        /// <returns><c>true</c> when the active runtime should render performance-overlay rows.</returns>
        bool ShouldUsePerformanceOverlayRows(Core core) {
            return core != null && (core.UsesPerformanceOverlayMetrics || ShouldUsePs2PerformanceOverlay(core));
        }

        /// <summary>
        /// Returns true when the PS2 overlay should temporarily surface managed object counts because the native overlay metrics are all zero.
        /// </summary>
        /// <param name="core">Active core instance that may provide PS2 runtime context.</param>
        /// <returns>True when managed fallback diagnostics should replace the zeroed PS2 metric buckets.</returns>
        bool ShouldUsePs2ManagedFallbackDiagnostics(Core core) {
            if (!ShouldUsePs2PerformanceOverlay(core)) {
                return false;
            }

            return core != null
                && core.PerformanceOverlayTriangleSetupMilliseconds == 0d
                && core.PerformanceOverlayTrianglePrepMilliseconds == 0d
                && core.PerformanceOverlayTriangleEmitMilliseconds == 0d
                && core.PerformanceOverlayPacketEncodeMilliseconds == 0d
                && core.PerformanceOverlaySubmitMilliseconds == 0d
                && core.PerformanceOverlayWaitMilliseconds == 0d
                && core.PerformanceOverlaySubmittedTriangleCount == 0
                && core.PerformanceOverlayDispatchCount == 0;
        }

        /// <summary>
        /// Resolves the visible update row, preferring core-owned metrics when the active runtime publishes them.
        /// </summary>
        /// <param name="core">Active core instance that may provide custom overlay metrics.</param>
        /// <param name="updateFps">Measured update FPS value.</param>
        /// <returns>Update overlay text for the current sample.</returns>
        string ResolveUpdateOverlayText(Core core, double updateFps) {
            if (ShouldUsePerformanceOverlayRows(core)) {
                if (ShouldUsePs2ManagedFallbackDiagnostics(core)) {
                    int drawable3DCount = core.ObjectManager == null ? 0 : core.ObjectManager.Drawables3D.Count;
                    int cameraCount = core.ObjectManager == null ? 0 : core.ObjectManager.Cameras.Count;
                    int entityCount = core.ObjectManager == null ? 0 : core.ObjectManager.Entities.Count;
                    return "Upd " + FormatFpsValue(updateFps)
                        + " Obj3D " + drawable3DCount
                        + " Cam " + cameraCount
                        + " Ent " + entityCount;
                }

                return "Upd " + FormatFpsValue(updateFps)
                    + " Set " + FormatFpsValue(core.PerformanceOverlayTriangleSetupMilliseconds)
                    + " Prep " + FormatFpsValue(core.PerformanceOverlayTrianglePrepMilliseconds)
                    + " Emit " + FormatFpsValue(core.PerformanceOverlayTriangleEmitMilliseconds);
            }

            return "Update FPS: " + FormatFpsValue(updateFps);
        }

        /// <summary>
        /// Resolves the visible render row, preferring core-owned metrics when the active runtime publishes them.
        /// </summary>
        /// <param name="core">Active core instance that may provide custom overlay metrics.</param>
        /// <param name="renderFps">Measured render FPS value.</param>
        /// <param name="drawMilliseconds">Measured draw duration in milliseconds.</param>
        /// <returns>Render overlay text for the current sample.</returns>
        string ResolveRenderOverlayText(Core core, double renderFps, double drawMilliseconds) {
            if (ShouldUsePerformanceOverlayRows(core)) {
                if (ShouldUsePs2ManagedFallbackDiagnostics(core)) {
                    int usesOverlayFlag = core != null && core.UsesPerformanceOverlayMetrics ? 1 : 0;
                    return "Rdr " + FormatFpsValue(renderFps)
                        + " Drw " + FormatFpsValue(drawMilliseconds)
                        + " Ovr " + usesOverlayFlag
                        + " Tri " + core.PerformanceOverlaySubmittedTriangleCount
                        + " Disp " + core.PerformanceOverlayDispatchCount;
                }

                return "Rdr " + FormatFpsValue(renderFps)
                    + " Drw " + FormatFpsValue(drawMilliseconds)
                    + " Enc " + FormatFpsValue(core.PerformanceOverlayPacketEncodeMilliseconds)
                    + " Sub " + FormatFpsValue(core.PerformanceOverlaySubmitMilliseconds)
                    + " Wt " + FormatFpsValue(core.PerformanceOverlayWaitMilliseconds)
                    + " Tri " + core.PerformanceOverlaySubmittedTriangleCount
                    + " Disp " + core.PerformanceOverlayDispatchCount;
            }

            return FormatRenderFpsText(renderFps, drawMilliseconds);
        }

        /// <summary>
    }
}

using System.Globalization;

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
        readonly FontAsset Font;

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
        /// Timestamp used to measure the current sampling window.
        /// </summary>
        DateTime LastSampleUtc;

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
        /// Creates a new FPS overlay that renders with the provided font.
        /// </summary>
        /// <param name="font">Font used for both overlay lines.</param>
        public FPSComponent(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
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

            if (Initialized) {
                return;
            }

            if (entity.Children == null) {
                entity.InitChildren();
            }

            OverlayHost = new Entity();
            OverlayHost.LayerMask = entity.LayerMask;
            OverlayHost.InitChildren();
            OverlayHost.InitComponents();
            entity.AddChild(OverlayHost);

            UpdateRowHost = new Entity();
            UpdateRowHost.LayerMask = entity.LayerMask;
            UpdateRowHost.InitChildren();
            UpdateRowHost.InitComponents();
            OverlayHost.AddChild(UpdateRowHost);

            UpdateTextComponent = new TextComponent();
            UpdateTextComponent.Font = Font;
            UpdateTextComponent.Color = new byte4(255, 255, 255, 255);
            UpdateTextComponent.RenderOrder2D = RenderOrder2D;
            UpdateRowHost.AddComponent(UpdateTextComponent);

            RenderRowHost = new Entity();
            RenderRowHost.LayerMask = entity.LayerMask;
            RenderRowHost.InitChildren();
            RenderRowHost.InitComponents();
            RenderRowHost.LocalPosition = new float3(0f, Font.LineHeight, 0.1f);
            OverlayHost.AddChild(RenderRowHost);

            RenderTextComponent = new TextComponent();
            RenderTextComponent.Font = Font;
            RenderTextComponent.Color = new byte4(255, 255, 255, 255);
            RenderTextComponent.RenderOrder2D = RenderOrder2D;
            RenderRowHost.AddComponent(RenderTextComponent);

            ResetSamplingWindow();
            ApplyPadding();
            Initialized = true;
            ActiveComponents.Add(this);
        }

        /// <summary>
        /// Removes the overlay hierarchy and unregisters the component from the active FPS list.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            bool wasInitialized = Initialized;
            Initialized = false;
            ActiveComponents.Remove(this);

            if (wasInitialized && Parent != null && OverlayHost != null && OverlayHost.Parent == Parent) {
                Parent.RemoveChild(OverlayHost);
            }

            OverlayHost = null;
            UpdateRowHost = null;
            RenderRowHost = null;
            UpdateTextComponent = null;
            RenderTextComponent = null;
            Initialized = false;

            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Resets the sampling window when the hierarchy toggles enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            ResetSamplingWindow();
        }

        /// <summary>
        /// Refreshes the visible text when the sampling window has elapsed.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                throw new InvalidOperationException("FPSComponent must be attached before it can sample frames.");
            }

            TryRefreshOverlay();
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
        /// Resets the current sampling window and restores the placeholder text.
        /// </summary>
        void ResetSamplingWindow() {
            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            LastSampleUtc = DateTime.UtcNow;
            UpdateFpsText = "Update FPS: --";
            RenderFpsText = "Render FPS: --";

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
            double elapsedSeconds = (DateTime.UtcNow - LastSampleUtc).TotalSeconds;
            if (refreshIntervalSeconds > 0d && elapsedSeconds < refreshIntervalSeconds) {
                return;
            }

            double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
            double updateFps = UpdateFrameCount / safeElapsedSeconds;
            double renderFps = RenderFrameCount / safeElapsedSeconds;

            UpdateFpsText = string.Format(CultureInfo.InvariantCulture, "Update FPS: {0:0.0}", updateFps);
            RenderFpsText = string.Format(CultureInfo.InvariantCulture, "Render FPS: {0:0.0}", renderFps);

            if (UpdateTextComponent != null) {
                UpdateTextComponent.Text = UpdateFpsText;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Text = RenderFpsText;
            }

            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            LastSampleUtc = DateTime.UtcNow;
        }
    }
}

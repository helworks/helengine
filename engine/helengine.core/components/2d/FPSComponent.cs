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
            if (Parent != null && OverlayHost != null && OverlayHost.Parent == Parent) {
                Parent.RemoveChild(OverlayHost);
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
            LastSampleElapsedSeconds = Core.Instance == null ? 0d : Core.Instance.TotalElapsedSeconds;
            UpdateFpsText = "Update FPS: --";
            RenderFpsText = "Render FPS: -- (-- ms)";

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
            double elapsedSeconds = Core.Instance.TotalElapsedSeconds - LastSampleElapsedSeconds;
            if (refreshIntervalSeconds > 0d && elapsedSeconds < refreshIntervalSeconds) {
                return;
            }

            double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
            double updateFps = UpdateFrameCount / safeElapsedSeconds;
            double renderFps = RenderFrameCount / safeElapsedSeconds;

            UpdateFpsText = "Update FPS: " + FormatFpsValue(updateFps);
            RenderFpsText = FormatRenderFpsText(renderFps, Core.Instance.LastRenderManager3DDrawMilliseconds);

            if (UpdateTextComponent != null) {
                UpdateTextComponent.Text = UpdateFpsText;
            }

            if (RenderTextComponent != null) {
                RenderTextComponent.Text = FormatOverlaySecondaryLine(RenderFpsText);
            }

            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            LastSampleElapsedSeconds = Core.Instance.TotalElapsedSeconds;
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
        /// Formats the second overlay line, appending DS menu diagnostics when the active runtime is a Nintendo DS menu scene.
        /// </summary>
        /// <param name="baseRenderText">Base render-FPS text.</param>
        /// <returns>Overlay secondary-line text for the current runtime.</returns>
        string FormatOverlaySecondaryLine(string baseRenderText) {
            if (Core.Instance == null || Core.Instance.PlatformInfo == null) {
                return baseRenderText;
            }
            if (!string.Equals(Core.Instance.PlatformInfo.Name, "nintendo-ds", StringComparison.Ordinal)) {
                return baseRenderText;
            }

            MenuComponent menuComponent = FindFirstMenuComponent();
            if (menuComponent == null) {
                return baseRenderText;
            }

            InputSystem inputSystem = Core.Instance.Input;
            InputGamepadState currentGamepadState = inputSystem.GetGamepadState(0);
            InputGamepadState previousGamepadState = inputSystem.GetPreviousGamepadState(0);
            return "D"
                + FormatButtonDiagnostic(currentGamepadState, previousGamepadState, InputGamepadButton.DPadDown)
                + " A"
                + FormatButtonDiagnostic(currentGamepadState, previousGamepadState, InputGamepadButton.South)
                + " "
                + menuComponent.ActivePanelId
                + "/"
                + menuComponent.SelectedItemId;
        }

        /// <summary>
        /// Formats one compact current-and-pressed button diagnostic for the overlay.
        /// </summary>
        /// <param name="currentGamepadState">Current primary gamepad state.</param>
        /// <param name="previousGamepadState">Previous primary gamepad state.</param>
        /// <param name="button">Button to format.</param>
        /// <returns>Compact button diagnostic such as <c>10</c> for down and newly pressed.</returns>
        string FormatButtonDiagnostic(InputGamepadState currentGamepadState, InputGamepadState previousGamepadState, InputGamepadButton button) {
            bool isDown = currentGamepadState.IsButtonDown(button);
            bool wasPressed = currentGamepadState.IsButtonDown(button) && !previousGamepadState.IsButtonDown(button);
            return (isDown ? "1" : "0") + (wasPressed ? "1" : "0");
        }

        /// <summary>
        /// Finds the first active runtime menu component so DS overlay diagnostics can report the current selection state.
        /// </summary>
        /// <returns>First active runtime menu component, or null when no menu is currently loaded.</returns>
        MenuComponent FindFirstMenuComponent() {
            if (Core.Instance == null || Core.Instance.ObjectManager == null || Core.Instance.ObjectManager.Entities == null) {
                return null;
            }

            for (int entityIndex = 0; entityIndex < Core.Instance.ObjectManager.Entities.Count; entityIndex++) {
                MenuComponent menuComponent = FindFirstMenuComponent(Core.Instance.ObjectManager.Entities[entityIndex]);
                if (menuComponent != null) {
                    return menuComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Searches one entity subtree for the first runtime menu component.
        /// </summary>
        /// <param name="entity">Entity subtree to search.</param>
        /// <returns>First runtime menu component in the subtree, or null when none exists.</returns>
        MenuComponent FindFirstMenuComponent(Entity entity) {
            if (entity == null) {
                return null;
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is MenuComponent menuComponent) {
                        return menuComponent;
                    }
                }
            }

            if (entity.Children == null) {
                return null;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                MenuComponent menuComponent = FindFirstMenuComponent(entity.Children[childIndex]);
                if (menuComponent != null) {
                    return menuComponent;
                }
            }

            return null;
        }
    }
}

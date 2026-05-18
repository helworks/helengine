namespace helengine {
    /// <summary>
    /// Renders a fixed five-line runtime diagnostics overlay using the shared 2D text pipeline.
    /// </summary>
    public class DebugComponent : UpdateComponent {
        /// <summary>
        /// Byte count represented by one megabyte in the overlay formatter.
        /// </summary>
        const double BytesPerMegabyte = 1024d * 1024d;

        /// <summary>
        /// Tracks every active debug component so later runtime metrics can broadcast shared render-frame ticks.
        /// </summary>
        static readonly List<DebugComponent> ActiveComponents = new List<DebugComponent>();

        /// <summary>
        /// Font used by every overlay row.
        /// </summary>
        FontAsset FontValue;

        /// <summary>
        /// Root entity that positions the overlay in viewport space.
        /// </summary>
        Entity OverlayHost;

        /// <summary>
        /// Lifetime sentinel attached to the overlay root so stale cached references are cleared before native deletion completes.
        /// </summary>
        DebugComponentOverlayLifetimeComponent OverlayLifetimeComponent;

        /// <summary>
        /// Child entity that hosts the render-FPS text row.
        /// </summary>
        Entity RenderFpsRowHost;

        /// <summary>
        /// Child entity that hosts the resident-memory text row.
        /// </summary>
        Entity ResidentMemoryRowHost;

        /// <summary>
        /// Child entity that hosts the committed-memory text row.
        /// </summary>
        Entity CommittedMemoryRowHost;

        /// <summary>
        /// Child entity that hosts the 2D-drawables text row.
        /// </summary>
        Entity Drawables2DRowHost;

        /// <summary>
        /// Child entity that hosts the 3D-drawables and draw-calls text row.
        /// </summary>
        Entity Drawables3DRowHost;

        /// <summary>
        /// Text component that displays render FPS.
        /// </summary>
        TextComponent RenderFpsTextComponent;

        /// <summary>
        /// Text component that displays resident memory.
        /// </summary>
        TextComponent ResidentMemoryTextComponent;

        /// <summary>
        /// Text component that displays committed memory.
        /// </summary>
        TextComponent CommittedMemoryTextComponent;

        /// <summary>
        /// Text component that displays the 2D drawable count.
        /// </summary>
        TextComponent Drawables2DTextComponent;

        /// <summary>
        /// Text component that displays the 3D drawable count and draw-call count.
        /// </summary>
        TextComponent Drawables3DTextComponent;

        /// <summary>
        /// Stores the elapsed-seconds marker for the current sampling window.
        /// </summary>
        double LastSampleElapsedSeconds;

        /// <summary>
        /// Stores the render-frame count captured during the current sampling window.
        /// </summary>
        int RenderFrameCount;

        /// <summary>
        /// Indicates whether the overlay hierarchy has been created.
        /// </summary>
        bool Initialized;

        /// <summary>
        /// Reusable scalar memory counters captured during steady-state overlay refreshes.
        /// </summary>
        RuntimeMemoryCounters MemoryCountersValue;

        /// <summary>
        /// Stores the current refresh interval.
        /// </summary>
        double RefreshIntervalSecondsValue = 0.5d;

        /// <summary>
        /// Stores the current overlay padding.
        /// </summary>
        int2 PaddingValue = new int2(8, 6);

        /// <summary>
        /// Stores the current render order used by every overlay row.
        /// </summary>
        byte RenderOrder2DValue = 250;

        /// <summary>
        /// Initializes a new debug overlay with no implicit font fallback.
        /// </summary>
        public DebugComponent() {
            MemoryCountersValue = new RuntimeMemoryCounters();
            ResetSamplingWindow();
        }

        /// <summary>
        /// Gets or sets the sampling interval used before refreshing the visible diagnostics.
        /// </summary>
        public double RefreshIntervalSeconds {
            get { return RefreshIntervalSecondsValue; }
            set {
                if (value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Refresh interval must be zero or greater.");
                }

                RefreshIntervalSecondsValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the overlay padding applied from the top-left viewport edge.
        /// </summary>
        public int2 Padding {
            get { return PaddingValue; }
            set {
                PaddingValue = value;
                ApplyPadding();
            }
        }

        /// <summary>
        /// Gets or sets the render order used by the overlay text rows.
        /// </summary>
        public byte RenderOrder2D {
            get { return RenderOrder2DValue; }
            set {
                if (RenderOrder2DValue == value) {
                    return;
                }

                RenderOrder2DValue = value;
                ApplyRenderOrder();
            }
        }

        /// <summary>
        /// Gets or sets the font used by every overlay row.
        /// </summary>
        public FontAsset Font {
            get { return FontValue; }
            set {
                if (ReferenceEquals(FontValue, value)) {
                    return;
                }

                FontValue = value;
                RefreshOverlayActivation();
            }
        }

        /// <summary>
        /// Gets the last formatted render-FPS row.
        /// </summary>
        public string RenderFpsText { get; private set; }

        /// <summary>
        /// Gets the last formatted resident-memory row.
        /// </summary>
        public string ResidentMemoryText { get; private set; }

        /// <summary>
        /// Gets the last formatted committed-memory row.
        /// </summary>
        public string CommittedMemoryText { get; private set; }

        /// <summary>
        /// Gets the last formatted 2D-drawables row.
        /// </summary>
        public string Drawables2DText { get; private set; }

        /// <summary>
        /// Gets the last formatted 3D-drawables row.
        /// </summary>
        public string Drawables3DText { get; private set; }

        /// <summary>
        /// Builds the overlay entity hierarchy when the component is attached with a valid font.
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
        /// Removes the overlay hierarchy and unregisters the component from active diagnostics tracking.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            TearDownOverlay();
            NativeOwnership.Delete(MemoryCountersValue);
            MemoryCountersValue = null;
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Resets the current sampling window when the parent hierarchy changes enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            ResetSamplingWindow();
            ApplyVisibleText();
        }

        /// <summary>
        /// Refreshes the visible diagnostics once the configured sampling window has elapsed.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                return;
            }

            TryRefreshOverlay();
        }

        /// <summary>
        /// Records one render tick for every active debug overlay.
        /// </summary>
        public static void RecordRenderFrame() {
            for (int i = ActiveComponents.Count - 1; i >= 0; i--) {
                DebugComponent component = ActiveComponents[i];
                if (component.Initialized && component.Parent != null && component.Parent.IsHierarchyEnabled) {
                    component.RenderFrameCount++;
                }
            }
        }

        /// <summary>
        /// Reconciles the overlay hierarchy with the current attachment and font state.
        /// </summary>
        void RefreshOverlayActivation() {
            if (Parent == null) {
                return;
            }

            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
            }

            if (Font == null) {
                TearDownOverlay();
                return;
            }

            if (!Initialized) {
                BuildOverlay();
                ResetSamplingWindow();
                ApplyVisibleText();
                ApplyPadding();
                ApplyRenderOrder();
                return;
            }

            ApplyFont();
            ApplyPadding();
            ApplyRenderOrder();
            ApplyVisibleText();
        }

        /// <summary>
        /// Creates the overlay hierarchy for the currently attached parent entity.
        /// </summary>
        void BuildOverlay() {
            if (Parent == null) {
                throw new InvalidOperationException("DebugComponent must be attached before its overlay can be created.");
            }

            if (Font == null) {
                throw new InvalidOperationException("DebugComponent overlay creation requires a font.");
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
            OverlayLifetimeComponent = new DebugComponentOverlayLifetimeComponent(this);
            OverlayHost.AddComponent(OverlayLifetimeComponent);

            RenderFpsRowHost = CreateRowHost();
            RenderFpsTextComponent = CreateRowTextComponent(RenderFpsRowHost);

            ResidentMemoryRowHost = CreateRowHost();
            ResidentMemoryTextComponent = CreateRowTextComponent(ResidentMemoryRowHost);

            CommittedMemoryRowHost = CreateRowHost();
            CommittedMemoryTextComponent = CreateRowTextComponent(CommittedMemoryRowHost);

            Drawables2DRowHost = CreateRowHost();
            Drawables2DTextComponent = CreateRowTextComponent(Drawables2DRowHost);

            Drawables3DRowHost = CreateRowHost();
            Drawables3DTextComponent = CreateRowTextComponent(Drawables3DRowHost);

            Initialized = true;
            ActiveComponents.Add(this);
            ApplyFont();
        }

        /// <summary>
        /// Creates one child entity that hosts a single overlay row.
        /// </summary>
        /// <returns>Initialized row host entity.</returns>
        Entity CreateRowHost() {
            Entity rowHost = new Entity();
            rowHost.LayerMask = Parent.LayerMask;
            rowHost.InitChildren();
            rowHost.InitComponents();
            OverlayHost.AddChild(rowHost);
            return rowHost;
        }

        /// <summary>
        /// Creates one text component configured for a single overlay row.
        /// </summary>
        /// <param name="rowHost">Row host that should receive the text component.</param>
        /// <returns>Created text component.</returns>
        TextComponent CreateRowTextComponent(Entity rowHost) {
            if (rowHost == null) {
                throw new ArgumentNullException(nameof(rowHost));
            }

            TextComponent textComponent = new TextComponent();
            textComponent.Color = new byte4(255, 255, 255, 255);
            textComponent.Font = Font;
            rowHost.AddComponent(textComponent);
            return textComponent;
        }

        /// <summary>
        /// Removes the overlay hierarchy and unregisters the component from shared tracking.
        /// </summary>
        void TearDownOverlay() {
            Entity overlayHost = OverlayHost;
            ReleaseOverlayReferences();
            if (overlayHost != null) {
                overlayHost.Dispose();
            }
        }

        /// <summary>
        /// Clears the cached overlay hierarchy references without attempting to dispose the overlay root.
        /// </summary>
        void ReleaseOverlayReferences() {
            ActiveComponents.Remove(this);
            OverlayHost = null;
            OverlayLifetimeComponent = null;
            RenderFpsRowHost = null;
            ResidentMemoryRowHost = null;
            CommittedMemoryRowHost = null;
            Drawables2DRowHost = null;
            Drawables3DRowHost = null;
            RenderFpsTextComponent = null;
            ResidentMemoryTextComponent = null;
            CommittedMemoryTextComponent = null;
            Drawables2DTextComponent = null;
            Drawables3DTextComponent = null;
            Initialized = false;
        }

        /// <summary>
        /// Applies the configured font to every overlay row and positions each row beneath the previous one.
        /// </summary>
        void ApplyFont() {
            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
                return;
            }

            if (RenderFpsTextComponent != null) {
                RenderFpsTextComponent.Font = Font;
            }
            if (ResidentMemoryTextComponent != null) {
                ResidentMemoryTextComponent.Font = Font;
            }
            if (CommittedMemoryTextComponent != null) {
                CommittedMemoryTextComponent.Font = Font;
            }
            if (Drawables2DTextComponent != null) {
                Drawables2DTextComponent.Font = Font;
            }
            if (Drawables3DTextComponent != null) {
                Drawables3DTextComponent.Font = Font;
            }

            if (ResidentMemoryRowHost != null) {
                ResidentMemoryRowHost.LocalPosition = new float3(0f, Font.LineHeight, 0.1f);
            }
            if (CommittedMemoryRowHost != null) {
                CommittedMemoryRowHost.LocalPosition = new float3(0f, Font.LineHeight * 2f, 0.2f);
            }
            if (Drawables2DRowHost != null) {
                Drawables2DRowHost.LocalPosition = new float3(0f, Font.LineHeight * 3f, 0.3f);
            }
            if (Drawables3DRowHost != null) {
                Drawables3DRowHost.LocalPosition = new float3(0f, Font.LineHeight * 4f, 0.4f);
            }
        }

        /// <summary>
        /// Applies the configured padding to the overlay root.
        /// </summary>
        void ApplyPadding() {
            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
                return;
            }

            if (OverlayHost == null) {
                return;
            }

            OverlayHost.LocalPosition = new float3(Padding.X, Padding.Y, 0f);
        }

        /// <summary>
        /// Applies the configured render order to every overlay row.
        /// </summary>
        void ApplyRenderOrder() {
            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
                return;
            }

            if (RenderFpsTextComponent != null) {
                RenderFpsTextComponent.RenderOrder2D = RenderOrder2D;
            }
            if (ResidentMemoryTextComponent != null) {
                ResidentMemoryTextComponent.RenderOrder2D = RenderOrder2D;
            }
            if (CommittedMemoryTextComponent != null) {
                CommittedMemoryTextComponent.RenderOrder2D = RenderOrder2D;
            }
            if (Drawables2DTextComponent != null) {
                Drawables2DTextComponent.RenderOrder2D = RenderOrder2D;
            }
            if (Drawables3DTextComponent != null) {
                Drawables3DTextComponent.RenderOrder2D = RenderOrder2D;
            }
        }

        /// <summary>
        /// Resets the current sampling window and restores the placeholder row text.
        /// </summary>
        void ResetSamplingWindow() {
            RenderFrameCount = 0;
            Core core = Core.Instance;
            LastSampleElapsedSeconds = core == null ? 0d : core.TotalElapsedSeconds;
            RenderFpsText = "Render FPS: --";
            ResidentMemoryText = "Memory Res: --";
            CommittedMemoryText = "Memory Com: --";
            Drawables2DText = "Drawables 2D: --";
            Drawables3DText = "Drawables 3D: -- DrawCalls: --";
        }

        /// <summary>
        /// Pushes the current row text into the live overlay text components.
        /// </summary>
        void ApplyVisibleText() {
            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
                return;
            }

            if (RenderFpsTextComponent != null) {
                RenderFpsTextComponent.Text = RenderFpsText;
            }
            if (ResidentMemoryTextComponent != null) {
                ResidentMemoryTextComponent.Text = ResidentMemoryText;
            }
            if (CommittedMemoryTextComponent != null) {
                CommittedMemoryTextComponent.Text = CommittedMemoryText;
            }
            if (Drawables2DTextComponent != null) {
                Drawables2DTextComponent.Text = Drawables2DText;
            }
            if (Drawables3DTextComponent != null) {
                Drawables3DTextComponent.Text = Drawables3DText;
            }
        }

        /// <summary>
        /// Formats the five overlay rows from the latest sampled runtime metrics.
        /// </summary>
        void TryRefreshOverlay() {
            if (!EnsureOverlayHierarchyIsLive()) {
                ReleaseOverlayReferences();
                return;
            }

            Core core = Core.Instance ?? throw new InvalidOperationException("DebugComponent requires an active Core instance.");
            double elapsedSeconds = core.TotalElapsedSeconds - LastSampleElapsedSeconds;
            if (RefreshIntervalSeconds > 0d && elapsedSeconds < RefreshIntervalSeconds) {
                return;
            }

            RuntimeMemoryCounters memoryCounters = null;
            if (core.InitializationOptions.RuntimeDiagnosticsProvider != null) {
                memoryCounters = MemoryCountersValue ?? throw new InvalidOperationException("DebugComponent requires reusable memory counters while attached.");
                core.RuntimeDiagnosticsService.CaptureMemoryCounters(memoryCounters);
            }

            double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
            double renderFps = RenderFrameCount / safeElapsedSeconds;

            RenderFpsText = "Render FPS: " + FormatOneDecimal(renderFps);
            ResidentMemoryText = ResolveResidentMemoryText(memoryCounters);
            CommittedMemoryText = ResolveCommittedMemoryText(memoryCounters);
            Drawables2DText = "Drawables 2D: " + core.ObjectManager.Drawables2D.Count;
            Drawables3DText = "Drawables 3D: " + core.ObjectManager.Drawables3D.Count + " DrawCalls: " + core.LastRenderManager3DDrawCallCount;
            ApplyVisibleText();

            RenderFrameCount = 0;
            LastSampleElapsedSeconds = core.TotalElapsedSeconds;
        }

        /// <summary>
        /// Formats one resident-memory row from the latest runtime snapshot.
        /// </summary>
        /// <param name="memoryCounters">Captured counters used by the current overlay refresh.</param>
        /// <returns>Formatted resident-memory row or a placeholder when no provider is active.</returns>
        string ResolveResidentMemoryText(RuntimeMemoryCounters memoryCounters) {
            if (memoryCounters == null) {
                return "Memory Res: --";
            }

            return "Memory Res: " + FormatMegabytes(memoryCounters.ResidentBytes);
        }

        /// <summary>
        /// Formats one committed-memory row from the latest runtime snapshot.
        /// </summary>
        /// <param name="memoryCounters">Captured counters used by the current overlay refresh.</param>
        /// <returns>Formatted committed-memory row or a placeholder when no provider is active.</returns>
        string ResolveCommittedMemoryText(RuntimeMemoryCounters memoryCounters) {
            if (memoryCounters == null) {
                return "Memory Com: --";
            }

            return "Memory Com: " + FormatMegabytes(memoryCounters.CommittedBytes);
        }

        /// <summary>
        /// Formats one byte count as whole megabytes with exactly one decimal place.
        /// </summary>
        /// <param name="bytes">Byte count to format.</param>
        /// <returns>Megabyte text such as <c>128.5 MB</c>.</returns>
        string FormatMegabytes(ulong bytes) {
            double megabytes = bytes / BytesPerMegabyte;
            return FormatOneDecimal(megabytes) + " MB";
        }

        /// <summary>
        /// Formats one numeric value with exactly one decimal place.
        /// </summary>
        /// <param name="value">Value that should be rounded for display.</param>
        /// <returns>Rounded text such as <c>4.0</c>.</returns>
        string FormatOneDecimal(double value) {
            int tenths = (int)Math.Round(value * 10d, MidpointRounding.AwayFromZero);
            int whole = tenths / 10;
            int fractional = Math.Abs(tenths % 10);
            return whole + "." + fractional;
        }

        /// <summary>
        /// Returns whether the cached overlay entity hierarchy is still attached exactly where the component expects it to be.
        /// </summary>
        /// <returns>True when the cached overlay row hosts and text components are still live and parented correctly.</returns>
        bool EnsureOverlayHierarchyIsLive() {
            if (!Initialized) {
                return false;
            }

            return OverlayHost != null
                && OverlayHost.Parent == Parent
                && IsLiveRow(RenderFpsRowHost, OverlayHost, RenderFpsTextComponent)
                && IsLiveRow(ResidentMemoryRowHost, OverlayHost, ResidentMemoryTextComponent)
                && IsLiveRow(CommittedMemoryRowHost, OverlayHost, CommittedMemoryTextComponent)
                && IsLiveRow(Drawables2DRowHost, OverlayHost, Drawables2DTextComponent)
                && IsLiveRow(Drawables3DRowHost, OverlayHost, Drawables3DTextComponent);
        }

        /// <summary>
        /// Returns whether one cached overlay row host and text component pair is still attached to the expected overlay hierarchy.
        /// </summary>
        /// <param name="rowHost">Cached row host entity.</param>
        /// <param name="overlayHost">Cached overlay root entity.</param>
        /// <param name="textComponent">Cached text component attached to the row host.</param>
        /// <returns>True when the row host and text component are still parented to the expected live owners.</returns>
        bool IsLiveRow(Entity rowHost, Entity overlayHost, TextComponent textComponent) {
            return rowHost != null
                && rowHost.Parent == overlayHost
                && textComponent != null
                && textComponent.Parent == rowHost;
        }

        /// <summary>
        /// Clears cached overlay references when the overlay-host subtree is being removed externally during parent-driven disposal.
        /// </summary>
        internal void ReleaseOverlayReferencesFromDisposedHierarchy() {
            ReleaseOverlayReferences();
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Presents per-platform animation clip override editing inside the properties panel.
    /// </summary>
    public class AnimationClipAssetView {
        /// <summary>
        /// Height of the platform tab strip.
        /// </summary>
        const int TabHeight = 24;

        /// <summary>
        /// Width reserved for each platform tab.
        /// </summary>
        const int TabWidth = 88;

        /// <summary>
        /// Width reserved for each platform-tab overflow arrow button.
        /// </summary>
        const int ArrowButtonWidth = 24;

        /// <summary>
        /// Spacing inserted between the tab strip and the active panel.
        /// </summary>
        const int PanelSpacing = 8;

        /// <summary>
        /// Root entity that owns all view visuals.
        /// </summary>
        readonly EditorEntity RootEntity;

        /// <summary>
        /// Platform tab strip used to switch the active platform panel.
        /// </summary>
        readonly PlatformTabStripView PlatformTabStrip;

        /// <summary>
        /// Supported platform identifiers shown in the tab strip.
        /// </summary>
        readonly List<string> SupportedPlatformIds;

        /// <summary>
        /// Per-platform panels keyed by platform id.
        /// </summary>
        readonly Dictionary<string, AnimationClipAssetPlatformPanel> PlatformPanels;

        /// <summary>
        /// Font used by the child controls.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Layer mask applied to child entities.
        /// </summary>
        readonly ushort LayerMask;

        /// <summary>
        /// Asset entry currently being edited.
        /// </summary>
        AssetBrowserEntry CurrentEntry;

        /// <summary>
        /// Animation clip currently being edited.
        /// </summary>
        AnimationClipAsset CurrentClip;

        /// <summary>
        /// Currently selected platform id.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Cached layout height.
        /// </summary>
        int LayoutHeight;

        /// <summary>
        /// Tracks whether the view is visible.
        /// </summary>
        bool IsViewVisible;

        /// <summary>
        /// Initializes one animation clip view.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        public AnimationClipAssetView(FontAsset font, ushort layerMask) {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            LayerMask = layerMask;
            SupportedPlatformIds = new List<string>(4);
            PlatformPanels = new Dictionary<string, AnimationClipAssetPlatformPanel>(StringComparer.OrdinalIgnoreCase);

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;
            RootEntity.Enabled = false;

            PlatformTabStrip = new PlatformTabStripView(font, layerMask, TabWidth, TabHeight, 0, ArrowButtonWidth);
            PlatformTabStrip.SetRenderOrders(RenderOrder2D.PanelSurface, RenderOrder2D.PanelForeground);
            PlatformTabStrip.Root.Enabled = false;
            RootEntity.AddChild(PlatformTabStrip.Root);
        }

        /// <summary>
        /// Gets the root entity that hosts the view visuals.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets the cached layout height.
        /// </summary>
        public int Height => LayoutHeight;

        /// <summary>
        /// Gets whether the view is currently visible.
        /// </summary>
        public bool IsVisible => IsViewVisible;

        /// <summary>
        /// Shows the view for one animation clip and active project platform.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="clipAsset">Animation clip payload to edit.</param>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        /// <param name="activePlatformId">Currently active project platform identifier.</param>
        public void Show(
            AssetBrowserEntry entry,
            AnimationClipAsset clipAsset,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            } else if (clipAsset == null) {
                throw new ArgumentNullException(nameof(clipAsset));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (supportedPlatforms.Count == 0) {
                throw new ArgumentException("At least one supported platform must be provided.", nameof(supportedPlatforms));
            } else if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            CurrentEntry = entry;
            CurrentClip = clipAsset;
            SetSupportedPlatforms(supportedPlatforms);
            CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
            EnsurePlatformPanels();
            PlatformTabStrip.SetPlatforms(SupportedPlatformIds, CurrentPlatformId, HandlePlatformSelectionChanged);
            PlatformTabStrip.SetSelectedPlatform(CurrentPlatformId);
            UpdatePlatformVisibility();
            IsViewVisible = true;
            RootEntity.Enabled = true;
        }

        /// <summary>
        /// Hides the view and clears the active asset state.
        /// </summary>
        public void Hide() {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                if (!PlatformPanels.TryGetValue(platformId, out AnimationClipAssetPlatformPanel panel)) {
                    continue;
                }

                panel.Hide();
            }

            PlatformTabStrip.Root.Enabled = false;
            RootEntity.Enabled = false;
            IsViewVisible = false;
            LayoutHeight = 0;
            CurrentEntry = null;
            CurrentClip = null;
            CurrentPlatformId = string.Empty;
            SupportedPlatformIds.Clear();
        }

        /// <summary>
        /// Updates the view layout inside the properties panel.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width in pixels.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!IsViewVisible) {
                LayoutHeight = 0;
                return;
            }

            PlatformTabStrip.UpdateLayout(left, top, width);

            int currentTop = top + TabHeight + PanelSpacing;
            AnimationClipAssetPlatformPanel activePanel = ResolveActivePanel();
            if (activePanel != null) {
                activePanel.UpdateLayout(left, currentTop, width);
                LayoutHeight = TabHeight + PanelSpacing + activePanel.Height;
                return;
            }

            LayoutHeight = TabHeight;
        }

        /// <summary>
        /// Handles one platform-tab selection change.
        /// </summary>
        /// <param name="platformId">Selected platform identifier.</param>
        void HandlePlatformSelectionChanged(string platformId) {
            CurrentPlatformId = ResolveSelectedPlatformId(platformId);
            PlatformTabStrip.SetSelectedPlatform(CurrentPlatformId);
            UpdatePlatformVisibility();
        }

        /// <summary>
        /// Replaces the supported-platform list with the supplied values while preserving order.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers to display.</param>
        void SetSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            SupportedPlatformIds.Clear();
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId) || SupportedPlatformIds.Contains(platformId, StringComparer.OrdinalIgnoreCase)) {
                    continue;
                }

                SupportedPlatformIds.Add(platformId);
            }
        }

        /// <summary>
        /// Resolves the selected platform id, falling back to the first supported platform when necessary.
        /// </summary>
        /// <param name="activePlatformId">Requested platform identifier.</param>
        /// <returns>Supported platform id used by the view.</returns>
        string ResolveSelectedPlatformId(string activePlatformId) {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                if (string.Equals(platformId, activePlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return platformId;
                }
            }

            return SupportedPlatformIds[0];
        }

        /// <summary>
        /// Ensures one platform panel exists for each supported platform and refreshes its current clip data.
        /// </summary>
        void EnsurePlatformPanels() {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                if (!PlatformPanels.TryGetValue(platformId, out AnimationClipAssetPlatformPanel panel)) {
                    panel = new AnimationClipAssetPlatformPanel(Font, LayerMask);
                    PlatformPanels.Add(platformId, panel);
                    RootEntity.AddChild(panel.Root);
                }

                panel.Show(CurrentEntry, CurrentClip, platformId);
            }
        }

        /// <summary>
        /// Updates per-platform panel visibility so only the active platform remains visible.
        /// </summary>
        void UpdatePlatformVisibility() {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                if (!PlatformPanels.TryGetValue(platformId, out AnimationClipAssetPlatformPanel panel)) {
                    continue;
                }

                if (string.Equals(platformId, CurrentPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    panel.Show(CurrentEntry, CurrentClip, platformId);
                } else {
                    panel.Hide();
                }
            }

            PlatformTabStrip.Root.Enabled = true;
        }

        /// <summary>
        /// Resolves the active platform panel when one exists.
        /// </summary>
        /// <returns>Active platform panel, or null when the selected platform has no panel.</returns>
        AnimationClipAssetPlatformPanel ResolveActivePanel() {
            if (string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                return null;
            }

            PlatformPanels.TryGetValue(CurrentPlatformId, out AnimationClipAssetPlatformPanel panel);
            return panel;
        }
    }
}

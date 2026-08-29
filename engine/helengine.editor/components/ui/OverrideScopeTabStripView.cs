namespace helengine.editor {
    /// <summary>
    /// Hosts platform tabs and the optional environment tabs nested under the selected platform.
    /// </summary>
    public sealed class OverrideScopeTabStripView {
        readonly EditorEntity RootValue;
        readonly PlatformTabStripView PlatformTabsValue;
        readonly PlatformTabStripView EnvironmentTabsValue;
        readonly HashSet<string> EnabledEnvironmentPlatforms;
        readonly int TabHeightValue;
        IReadOnlyList<string> EnvironmentIdsValue;
        string SelectedPlatformIdValue;
        string SelectedEnvironmentIdValue;

        /// <summary>
        /// Initializes one two-level override scope strip.
        /// </summary>
        public OverrideScopeTabStripView(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, ushort layerMask, int tabWidth, int tabHeight, int tabSpacing = 4, int arrowButtonWidth = 16) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            RootValue = new EditorEntity(ownerCore, interactionServices) {
                LayerMask = layerMask,
                InternalEntity = true,
                Enabled = true
            };
            PlatformTabsValue = new PlatformTabStripView(ownerCore, interactionServices, font, layerMask, tabWidth, tabHeight, tabSpacing, arrowButtonWidth);
            EnvironmentTabsValue = new PlatformTabStripView(ownerCore, interactionServices, font, layerMask, tabWidth, tabHeight, tabSpacing, arrowButtonWidth);
            TabHeightValue = tabHeight;
        PlatformTabsValue.SetEnvironmentAddButtonVisible(true);
            PlatformTabsValue.EnvironmentOverrideRequested += HandleEnvironmentOverrideRequested;
            EnvironmentTabsValue.Root.Enabled = false;
            RootValue.AddChild(PlatformTabsValue.Root);
            RootValue.AddChild(EnvironmentTabsValue.Root);
            EnabledEnvironmentPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnvironmentIdsValue = Array.Empty<string>();
            SelectedPlatformIdValue = string.Empty;
            SelectedEnvironmentIdValue = string.Empty;
        }

        /// <summary>Gets the root entity containing both tab levels.</summary>
        public EditorEntity Root => RootValue;
        /// <summary>Gets the platform-level strip.</summary>
        public PlatformTabStripView PlatformTabs => PlatformTabsValue;

        public void SetRendererResources(EditorSessionRendererResources rendererResources) {
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }
            PlatformTabsValue.SetRenderManager2D(rendererResources.RenderManager2D);
            EnvironmentTabsValue.SetRenderManager2D(rendererResources.RenderManager2D);
        }
        /// <summary>Gets the nested environment-level strip.</summary>
        public PlatformTabStripView EnvironmentTabs => EnvironmentTabsValue;
        /// <summary>Gets the selected platform identifier.</summary>
        public string SelectedPlatformId => SelectedPlatformIdValue;
        /// <summary>Gets the selected nested environment identifier.</summary>
        public string SelectedEnvironmentId => SelectedEnvironmentIdValue;
        /// <summary>Gets whether nested environment tabs are visible for the selected platform.</summary>
        public bool EnvironmentTabsVisible => EnvironmentTabsValue.Root.Enabled;

        /// <summary>Raised when a platform is selected.</summary>
        public event Action<string> PlatformSelected;
        /// <summary>Raised when an environment is selected.</summary>
        public event Action<string> EnvironmentSelected;
        /// <summary>Raised when the user presses the platform-side plus affordance.</summary>
        public event Action<string> EnvironmentOverrideRequested;

        /// <summary>
        /// Sets the platform tabs and the registry environments available to every platform.
        /// </summary>
        public void SetPlatforms(IReadOnlyList<string> platformIds, string selectedPlatformId, IReadOnlyList<string> environmentIds, string selectedEnvironmentId) {
            if (platformIds == null) {
                throw new ArgumentNullException(nameof(platformIds));
            }
            EnvironmentIdsValue = environmentIds ?? Array.Empty<string>();
            PlatformTabsValue.SetPlatforms(platformIds, selectedPlatformId, HandlePlatformSelectionChanged);
            SelectedPlatformIdValue = PlatformTabsValue.SelectedPlatformId;
            SelectedEnvironmentIdValue = selectedEnvironmentId ?? string.Empty;
            PlatformTabsValue.SetEnvironmentAddButtonVisible(platformIds.Count > 0);
            RefreshEnvironmentTabs();
        }

        /// <summary>Marks a platform as having opted into environment overrides.</summary>
        public void EnableEnvironmentOverrides(string platformId, string selectedEnvironmentId = null) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EnabledEnvironmentPlatforms.Add(platformId.Trim());
            if (string.Equals(SelectedPlatformIdValue, platformId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(selectedEnvironmentId)) {
                SelectedEnvironmentIdValue = selectedEnvironmentId.Trim();
            }
            RefreshEnvironmentTabs();
        }

        /// <summary>Hides the nested environment layer for one platform without changing authored data.</summary>
        public void DisableEnvironmentOverrides(string platformId) {
            if (!string.IsNullOrWhiteSpace(platformId)) {
                EnabledEnvironmentPlatforms.Remove(platformId.Trim());
            }
            RefreshEnvironmentTabs();
        }

        /// <summary>Updates the strip layout and returns the total height consumed by the visible levels.</summary>
        public int UpdateLayout(int left, int top, int width) {
            PlatformTabsValue.UpdateLayout(left, top, width);
            int height = PlatformTabsValue.Root.Enabled ? TabHeightValue : 0;
            if (EnvironmentTabsValue.Root.Enabled) {
                EnvironmentTabsValue.UpdateLayout(left, top + height, width);
                height += TabHeightValue;
            }
            return height;
        }

        void HandlePlatformSelectionChanged(string platformId) {
            SelectedPlatformIdValue = platformId ?? string.Empty;
            RefreshEnvironmentTabs();
            PlatformSelected?.Invoke(SelectedPlatformIdValue);
        }

        void HandleEnvironmentSelectionChanged(string environmentId) {
            SelectedEnvironmentIdValue = environmentId ?? string.Empty;
            EnvironmentSelected?.Invoke(SelectedEnvironmentIdValue);
        }

        void HandleEnvironmentOverrideRequested(string platformId) {
            EnableEnvironmentOverrides(platformId);
            EnvironmentOverrideRequested?.Invoke(platformId);
        }

        void RefreshEnvironmentTabs() {
            bool enabled = EnabledEnvironmentPlatforms.Contains(SelectedPlatformIdValue)
                && EnvironmentIdsValue.Count > 0;
            EnvironmentTabsValue.Root.Enabled = enabled;
            if (!enabled) {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedEnvironmentIdValue)
                || !EnvironmentIdsValue.Contains(SelectedEnvironmentIdValue, StringComparer.OrdinalIgnoreCase)) {
                SelectedEnvironmentIdValue = EnvironmentIdsValue[0];
            }
            EnvironmentTabsValue.SetPlatforms(EnvironmentIdsValue, SelectedEnvironmentIdValue, HandleEnvironmentSelectionChanged);
        }
    }
}

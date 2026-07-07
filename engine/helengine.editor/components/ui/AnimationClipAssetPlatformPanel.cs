namespace helengine.editor {
    /// <summary>
    /// Presents one platform-specific animation clip override summary and mode selector inside the properties panel.
    /// </summary>
    public class AnimationClipAssetPlatformPanel {
        /// <summary>
        /// Height of each visible row.
        /// </summary>
        const int RowHeight = 24;

        /// <summary>
        /// Spacing between stacked rows.
        /// </summary>
        const int RowSpacing = 6;

        /// <summary>
        /// Width reserved for row labels.
        /// </summary>
        const int LabelWidth = 112;

        /// <summary>
        /// Width reserved for the mode selection combo box.
        /// </summary>
        const int ComboWidth = 148;

        /// <summary>
        /// Mode label displayed beside the combo box.
        /// </summary>
        const string OverrideModeLabel = "Override Mode";

        /// <summary>
        /// User-facing combo-box entries for the supported override modes.
        /// </summary>
        static readonly string[] OverrideModeItems = ["Inherit Base", "Replace Whole", "Override Frames"];

        /// <summary>
        /// Font used for panel labels.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Render order used for panel text.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Root entity that owns all panel controls.
        /// </summary>
        readonly EditorEntity RootEntity;

        /// <summary>
        /// Host entity for the override-mode label.
        /// </summary>
        readonly EditorEntity OverrideModeLabelHost;

        /// <summary>
        /// Text component that labels the override-mode selector.
        /// </summary>
        readonly TextComponent OverrideModeLabelText;

        /// <summary>
        /// Host entity for the override-mode combo box.
        /// </summary>
        readonly EditorEntity OverrideModeComboHost;

        /// <summary>
        /// Combo box used to switch the override mode for the active platform.
        /// </summary>
        readonly ComboBoxComponent OverrideModeComboBox;

        /// <summary>
        /// Host entity for the base-track summary.
        /// </summary>
        readonly EditorEntity BaseSummaryHost;

        /// <summary>
        /// Text component that reports authored base-track counts.
        /// </summary>
        readonly TextComponent BaseSummaryText;

        /// <summary>
        /// Host entity for the override-track summary.
        /// </summary>
        readonly EditorEntity OverrideSummaryHost;

        /// <summary>
        /// Text component that reports platform-override track counts.
        /// </summary>
        readonly TextComponent OverrideSummaryText;

        /// <summary>
        /// Host entity for the editor note shown beneath the summary rows.
        /// </summary>
        readonly EditorEntity NoteHost;

        /// <summary>
        /// Text component that explains the current scope of the animation clip editor.
        /// </summary>
        readonly TextComponent NoteText;

        /// <summary>
        /// Asset entry currently being edited.
        /// </summary>
        AssetBrowserEntry CurrentEntry;

        /// <summary>
        /// Animation clip currently being edited.
        /// </summary>
        AnimationClipAsset CurrentClip;

        /// <summary>
        /// Platform id whose override payload is shown by this panel.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Cached layout height.
        /// </summary>
        int LayoutHeight;

        /// <summary>
        /// Tracks whether the panel is visible.
        /// </summary>
        bool IsVisibleValue;

        /// <summary>
        /// Tracks whether the mode combo is currently being synchronized from data instead of user input.
        /// </summary>
        bool IsSynchronizingModeSelection;

        /// <summary>
        /// Initializes one platform panel for animation clip editing.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the panel entities.</param>
        public AnimationClipAssetPlatformPanel(FontAsset font, ushort layerMask) {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            TextOrder = RenderOrder2D.PanelForeground;

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;
            RootEntity.Enabled = false;

            OverrideModeLabelHost = CreateTextHost(layerMask, out OverrideModeLabelText, OverrideModeLabel);
            RootEntity.AddChild(OverrideModeLabelHost);

            OverrideModeComboHost = new EditorEntity();
            OverrideModeComboHost.LayerMask = layerMask;
            RootEntity.AddChild(OverrideModeComboHost);

            OverrideModeComboBox = new ComboBoxComponent(new int2(ComboWidth, RowHeight), Font, OverrideModeItems, 0);
            OverrideModeComboBox.SelectionChanged += HandleOverrideModeSelectionChanged;
            OverrideModeComboHost.AddComponent(OverrideModeComboBox);

            BaseSummaryHost = CreateTextHost(layerMask, out BaseSummaryText, string.Empty);
            RootEntity.AddChild(BaseSummaryHost);

            OverrideSummaryHost = CreateTextHost(layerMask, out OverrideSummaryText, string.Empty);
            RootEntity.AddChild(OverrideSummaryHost);

            NoteHost = CreateTextHost(layerMask, out NoteText, "Frame authoring stays in the shared clip asset.");
            NoteText.Color = ThemeManager.Colors.InputForegroundSecondary;
            RootEntity.AddChild(NoteHost);
        }

        /// <summary>
        /// Gets the root entity that hosts the panel visuals.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets the cached layout height for the panel.
        /// </summary>
        public int Height => LayoutHeight;

        /// <summary>
        /// Gets whether the panel is currently visible.
        /// </summary>
        public bool IsVisible => IsVisibleValue;

        /// <summary>
        /// Shows the panel for one asset and platform.
        /// </summary>
        /// <param name="entry">Asset entry being edited.</param>
        /// <param name="clipAsset">Animation clip payload being edited.</param>
        /// <param name="platformId">Platform identifier whose override data should be shown.</param>
        public void Show(AssetBrowserEntry entry, AnimationClipAsset clipAsset, string platformId) {
            CurrentEntry = entry ?? throw new ArgumentNullException(nameof(entry));
            CurrentClip = clipAsset ?? throw new ArgumentNullException(nameof(clipAsset));
            CurrentPlatformId = string.IsNullOrWhiteSpace(platformId)
                ? throw new ArgumentException("Platform id must be provided.", nameof(platformId))
                : platformId;

            RefreshContent();
            IsVisibleValue = true;
            RootEntity.Enabled = true;
        }

        /// <summary>
        /// Hides the panel and clears the active asset state.
        /// </summary>
        public void Hide() {
            CurrentEntry = null;
            CurrentClip = null;
            CurrentPlatformId = string.Empty;
            LayoutHeight = 0;
            IsVisibleValue = false;
            RootEntity.Enabled = false;
        }

        /// <summary>
        /// Updates the panel layout within the parent view.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width in pixels.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!IsVisibleValue) {
                LayoutHeight = 0;
                return;
            }

            int labelHeight = (int)Math.Ceiling(Font.LineHeight);
            int labelOffsetY = (int)Math.Round((RowHeight - labelHeight) / 2d);
            int controlLeft = left + LabelWidth + RowSpacing;
            int controlWidth = Math.Max(1, Math.Min(ComboWidth, width - LabelWidth - RowSpacing));
            int currentTop = top;

            OverrideModeLabelHost.Position = new float3(left, currentTop + labelOffsetY, 0.1f);
            OverrideModeLabelText.Size = new int2(LabelWidth, labelHeight);
            OverrideModeComboHost.Position = new float3(controlLeft, currentTop, 0.1f);
            OverrideModeComboBox.Size = new int2(controlWidth, RowHeight);
            currentTop += RowHeight + RowSpacing;

            currentTop = LayoutTextRow(BaseSummaryHost, BaseSummaryText, left, currentTop, width, labelHeight);
            currentTop = LayoutTextRow(OverrideSummaryHost, OverrideSummaryText, left, currentTop, width, labelHeight);
            currentTop = LayoutTextRow(NoteHost, NoteText, left, currentTop, width, labelHeight);

            LayoutHeight = currentTop - top;
        }

        /// <summary>
        /// Applies one newly selected override mode from the combo box.
        /// </summary>
        /// <param name="index">Selected item index.</param>
        /// <param name="value">Selected display text.</param>
        void HandleOverrideModeSelectionChanged(int index, string value) {
            if (IsSynchronizingModeSelection || CurrentClip == null || CurrentEntry == null) {
                return;
            }

            AnimationClipPlatformOverrideAsset platformOverride = FindOrCreatePlatformOverride(CurrentClip, CurrentPlatformId);
            platformOverride.Mode = ResolveOverrideMode(index);
            SaveCurrentClip();
            RefreshContent();
        }

        /// <summary>
        /// Rebuilds the panel text and combo selection from the current clip state.
        /// </summary>
        void RefreshContent() {
            AnimationClipPlatformOverrideAsset platformOverride = FindPlatformOverride(CurrentClip, CurrentPlatformId);
            AnimationClipPlatformOverrideMode mode = platformOverride?.Mode ?? AnimationClipPlatformOverrideMode.InheritBase;
            IsSynchronizingModeSelection = true;
            OverrideModeComboBox.SetItems(OverrideModeItems, ResolveOverrideModeIndex(mode));
            IsSynchronizingModeSelection = false;

            BaseSummaryText.Text = $"Base P:{CurrentClip.PositionTracks.Length} O:{CurrentClip.PositionOffsetTracks.Length} S:{CurrentClip.ScaleTracks.Length} R:{CurrentClip.RotationTracks.Length}";
            OverrideSummaryText.Text = platformOverride == null
                ? "Override P:0 O:0 S:0 R:0"
                : $"Override P:{platformOverride.PositionTracks.Length} O:{platformOverride.PositionOffsetTracks.Length} S:{platformOverride.ScaleTracks.Length} R:{platformOverride.RotationTracks.Length}";
            NoteText.Text = mode == AnimationClipPlatformOverrideMode.InheritBase
                ? "This platform currently inherits the shared clip."
                : "Use the scratch authoring tool to edit platform-specific frames.";
        }

        /// <summary>
        /// Finds one override payload for the supplied platform when it exists.
        /// </summary>
        /// <param name="clipAsset">Animation clip to inspect.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching override payload, or null when the platform currently inherits the base clip.</returns>
        static AnimationClipPlatformOverrideAsset FindPlatformOverride(AnimationClipAsset clipAsset, string platformId) {
            AnimationClipPlatformOverrideAsset[] platformOverrides = clipAsset.PlatformOverrides ?? Array.Empty<AnimationClipPlatformOverrideAsset>();
            for (int index = 0; index < platformOverrides.Length; index++) {
                AnimationClipPlatformOverrideAsset platformOverride = platformOverrides[index];
                if (platformOverride == null || !string.Equals(platformOverride.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return platformOverride;
            }

            return null;
        }

        /// <summary>
        /// Finds one override payload for the supplied platform or creates a new entry when none exists yet.
        /// </summary>
        /// <param name="clipAsset">Animation clip to edit.</param>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <returns>Existing or newly created override payload.</returns>
        static AnimationClipPlatformOverrideAsset FindOrCreatePlatformOverride(AnimationClipAsset clipAsset, string platformId) {
            AnimationClipPlatformOverrideAsset platformOverride = FindPlatformOverride(clipAsset, platformId);
            if (platformOverride != null) {
                return platformOverride;
            }

            List<AnimationClipPlatformOverrideAsset> platformOverrides = new(clipAsset.PlatformOverrides ?? Array.Empty<AnimationClipPlatformOverrideAsset>());
            platformOverride = new AnimationClipPlatformOverrideAsset {
                PlatformId = platformId,
                Mode = AnimationClipPlatformOverrideMode.InheritBase
            };
            platformOverrides.Add(platformOverride);
            clipAsset.PlatformOverrides = platformOverrides.ToArray();
            return platformOverride;
        }

        /// <summary>
        /// Saves the current clip payload back to disk after one editor-side override-mode change.
        /// </summary>
        void SaveCurrentClip() {
            using FileStream stream = new(CurrentEntry.FullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, CurrentClip);
        }

        /// <summary>
        /// Maps one combo-box selection index to its override mode enum value.
        /// </summary>
        /// <param name="index">Selected combo-box index.</param>
        /// <returns>Resolved override mode.</returns>
        static AnimationClipPlatformOverrideMode ResolveOverrideMode(int index) {
            if (index == 1) {
                return AnimationClipPlatformOverrideMode.ReplaceWholeClip;
            }
            if (index == 2) {
                return AnimationClipPlatformOverrideMode.OverrideFrames;
            }

            return AnimationClipPlatformOverrideMode.InheritBase;
        }

        /// <summary>
        /// Maps one override mode enum value back to the combo-box selection index.
        /// </summary>
        /// <param name="mode">Override mode to display.</param>
        /// <returns>Combo-box selection index.</returns>
        static int ResolveOverrideModeIndex(AnimationClipPlatformOverrideMode mode) {
            if (mode == AnimationClipPlatformOverrideMode.ReplaceWholeClip) {
                return 1;
            }
            if (mode == AnimationClipPlatformOverrideMode.OverrideFrames) {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// Creates one text host and label component for the panel.
        /// </summary>
        /// <param name="layerMask">Layer mask applied to the host entity.</param>
        /// <param name="textComponent">Created text component instance.</param>
        /// <param name="text">Initial text value.</param>
        /// <returns>Created host entity.</returns>
        EditorEntity CreateTextHost(ushort layerMask, out TextComponent textComponent, string text) {
            EditorEntity host = new EditorEntity();
            host.LayerMask = layerMask;
            textComponent = new TextComponent();
            textComponent.Font = Font;
            textComponent.Text = text;
            textComponent.Color = ThemeManager.Colors.InputForegroundPrimary;
            textComponent.RenderOrder2D = TextOrder;
            host.AddComponent(textComponent);
            return host;
        }

        /// <summary>
        /// Lays out one stacked text row and returns the next available vertical position.
        /// </summary>
        /// <param name="host">Host entity that owns the row text.</param>
        /// <param name="textComponent">Text component rendered by the row.</param>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available row width in pixels.</param>
        /// <param name="labelHeight">Measured font line height in pixels.</param>
        /// <returns>Next available top offset after the row.</returns>
        static int LayoutTextRow(EditorEntity host, TextComponent textComponent, int left, int top, int width, int labelHeight) {
            host.Position = new float3(left, top, 0.1f);
            textComponent.Size = new int2(Math.Max(1, width), labelHeight);
            return top + labelHeight + RowSpacing;
        }
    }
}

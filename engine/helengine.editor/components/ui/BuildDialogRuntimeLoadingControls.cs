using System.Globalization;

namespace helengine.editor {
    /// <summary>Renders user defaults and per-platform cooperative loading overrides inside the build dialog.</summary>
    sealed class BuildDialogRuntimeLoadingControls {
        /// <summary>Stores four labels and fields, whose entities remain owned by the build dialog hierarchy.</summary>
        readonly EditorEntity[] LabelHosts = new EditorEntity[4];
        /// <summary>Stores text box host entities for the two budget and two loading-screen controls.</summary>
        readonly EditorEntity[] FieldHosts = new EditorEntity[4];
        /// <summary>Stores budget default, budget override, screen default and screen override fields in display order.</summary>
        readonly TextBoxComponent[] Fields = new TextBoxComponent[4];
        /// <summary>Displays the cooperative-budget limitation immediately above the settings fields.</summary>
        readonly EditorEntity HintHost;
        /// <summary>Retains the borrowed composed user configuration currently displayed.</summary>
        EditorBuildConfigDocument Document;
        /// <summary>Retains the borrowed selected platform configuration currently displayed.</summary>
        EditorBuildPlatformConfigDocument Platform;
        /// <summary>Prevents programmatic binding from being interpreted as an edit.</summary>
        bool IsBinding;

        /// <summary>Creates controls under the existing dialog column without owning or duplicating settings documents.</summary>
        public BuildDialogRuntimeLoadingControls(Core core, EditorSessionInteractionServices services, EditorEntity parent, FontAsset font) {
            string[] labels = { "Default loading time (ms/frame)", "Platform time (blank = default)", "Default loading-screen scene id", "Platform loading-screen scene id" };
            for (int index = 0; index < Fields.Length; index++) {
                LabelHosts[index] = new EditorEntity(core, services) { InternalEntity = true, LayerMask = parent.LayerMask };
                LabelHosts[index].AddComponent(new TextComponent { Font = font, Text = labels[index], Color = ThemeManager.Colors.InputForegroundPrimary });
                parent.AddChild(LabelHosts[index]);
                FieldHosts[index] = new EditorEntity(core, services) { InternalEntity = true, LayerMask = parent.LayerMask };
                Fields[index] = new TextBoxComponent(new int2(240, 24), font, index == 3 ? "Default; '-' disables the screen" : string.Empty);
                Fields[index].TextChanged += HandleChanged;
                FieldHosts[index].AddComponent(Fields[index]);
                parent.AddChild(FieldHosts[index]);
            }
            HintHost = new EditorEntity(core, services) { InternalEntity = true, LayerMask = parent.LayerMask };
            HintHost.AddComponent(new TextComponent { Font = font, Text = "Some loading steps may exceed this time budget.", Color = ThemeManager.Colors.InputForegroundPrimary });
            parent.AddChild(HintHost);
        }

        /// <summary>Shows the selected platform's inherited or explicit values without mutating configuration.</summary>
        public void Bind(EditorBuildConfigDocument document, EditorBuildPlatformConfigDocument platform) {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            IsBinding = true;
            try {
                Fields[0].Text = document.SceneLoadingBudgetMilliseconds.ToString("R", CultureInfo.InvariantCulture);
                Fields[1].Text = platform.SceneLoadingBudgetMilliseconds == 0d ? string.Empty : platform.SceneLoadingBudgetMilliseconds.ToString("R", CultureInfo.InvariantCulture);
                Fields[2].Text = document.LoadingScreenSceneId ?? string.Empty;
                Fields[3].Text = !platform.OverridesLoadingScreenSceneId ? string.Empty : string.IsNullOrEmpty(platform.LoadingScreenSceneId) ? "-" : platform.LoadingScreenSceneId;
            } finally { IsBinding = false; }
            ValidateBudgetFields();
        }

        /// <summary>Places two paired rows above the existing copy/output controls, resizing the inputs with UI metrics.</summary>
        public void UpdateLayout(int top, int width, EditorUiMetrics metrics) {
            int gap = metrics.ScalePixels(8);
            int columnWidth = (width - gap) / 2;
            HintHost.Position = new float3(0, top, 0.1f);
            for (int index = 0; index < Fields.Length; index++) {
                int x = (index % 2) * (columnWidth + gap);
                int y = top + metrics.ScalePixels(22 + (index / 2) * 50);
                LabelHosts[index].Position = new float3(x, y, 0.1f);
                FieldHosts[index].Position = new float3(x, y + metrics.ScalePixels(18), 0.1f);
                Fields[index].Size = new int2(columnWidth, metrics.ScalePixels(24));
            }
        }

        /// <summary>Rejects an invalid budget before a platform can be added to the build queue.</summary>
        public bool TryCommit() {
            if (Document == null || Platform == null) { return false; }
            bool valid = EditorRuntimeLoadingSettingsEditService.TryApply(Document, Platform, Fields[0].Text, Fields[1].Text, Fields[2].Text, Fields[3].Text);
            ValidateBudgetFields();
            return valid;
        }

        /// <summary>Updates configuration only when the complete settings edit is valid.</summary>
        void HandleChanged(TextBoxComponent field) {
            if (!IsBinding) { TryCommit(); }
        }

        /// <summary>Marks the specific malformed numeric input rather than silently using its previous value.</summary>
        void ValidateBudgetFields() {
            Fields[0].SetInvalidState(!EditorRuntimeLoadingSettingsEditService.TryParseBudget(Fields[0].Text, false, out _));
            Fields[1].SetInvalidState(!EditorRuntimeLoadingSettingsEditService.TryParseBudget(Fields[1].Text, true, out _));
        }
    }
}

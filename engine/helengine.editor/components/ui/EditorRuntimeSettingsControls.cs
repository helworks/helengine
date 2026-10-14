namespace helengine.editor {
    /// <summary>Edits independent runtime presets within one platform, with a scrollable typed settings form.</summary>
    public sealed class EditorRuntimeSettingsControls {
        /// <summary>Owns the controls under the Runtime tab.</summary>
        readonly EditorEntity RootHost;
        /// <summary>Renders the preset selector.</summary>
        readonly EditorEntity SelectorHost;
        /// <summary>Renders the editable preset id.</summary>
        readonly EditorEntity IdHost;
        /// <summary>Renders the editable preset name.</summary>
        readonly EditorEntity NameHost;
        /// <summary>Owns the category selector above the scrolling form.</summary>
        readonly EditorEntity CategoryHost;
        /// <summary>Selects which setting category is displayed.</summary>
        readonly ComboBoxComponent CategorySelector;
        /// <summary>Retains the selected category while switching presets.</summary>
        string CurrentCategory;
        /// <summary>Owns the create, duplicate, delete and initial-selection buttons.</summary>
        readonly EditorEntity[] ButtonHosts;
        /// <summary>Retains button components for resizing without searching the entity tree.</summary>
        readonly ButtonComponent[] Buttons;
        /// <summary>Hosts the settings viewport and clip region.</summary>
        readonly EditorEntity ViewportHost;
        /// <summary>Scrolls settings while keeping preset management controls fixed.</summary>
        readonly EditorScrollComponent Scroll;
        /// <summary>Renders dynamic settings from the engine and platform definitions.</summary>
        readonly EditorPlatformSettingsSection Section;
        /// <summary>Stores the scaled management row height.</summary>
        readonly int RowHeight;
        /// <summary>Retains the selected platform document edited by the dialog's private copy.</summary>
        EditorPlatformProfileSettingsDocument Platform;
        /// <summary>Retains the selected platform's immutable setting contracts.</summary>
        PlatformRuntimeSettingDefinition[] Definitions;
        /// <summary>Tracks the displayed preset independently of the startup preset.</summary>
        int SelectedIndex;
        /// <summary>Prevents programmatic combo refreshes from committing or selecting twice.</summary>
        bool Refreshing;
        /// <summary>Constructs preset management and the existing typed setting controls.</summary>
        public EditorRuntimeSettingsControls(EditorEntity parent, FontAsset font, int labelWidth, int valueWidth, int rowHeight) {
            RowHeight = rowHeight; RootHost = CreateHost(parent);
            SelectorHost = CreateHost(RootHost); IdHost = CreateHost(RootHost); NameHost = CreateHost(RootHost);
            Selector = new ComboBoxComponent(new int2(240, rowHeight), font, [], -1);
            SelectorHost.AddComponent(Selector); Selector.SelectionChanged += HandleSelectionChanged;
            IdField = new TextBoxComponent(new int2(240, rowHeight), font, string.Empty); IdHost.AddComponent(IdField);
            NameField = new TextBoxComponent(new int2(240, rowHeight), font, string.Empty); NameHost.AddComponent(NameField);
            IdField.Placeholder = "Preset ID"; NameField.Placeholder = "Preset name";
            CategoryHost = CreateHost(RootHost);
            CategorySelector = new ComboBoxComponent(new int2(240, rowHeight), font, [], -1);
            CategoryHost.AddComponent(CategorySelector); CategorySelector.SelectionChanged += HandleCategoryChanged;
            ButtonHosts = new EditorEntity[4];
            Buttons = new ButtonComponent[4];
            string[] labels = ["New", "Duplicate", "Delete", "Use at startup"];
            Action[] actions = [CreatePreset, DuplicatePreset, DeletePreset, SelectInitialPreset];
            for (int index = 0; index < ButtonHosts.Length; index++) {
                ButtonHosts[index] = CreateHost(RootHost);
                Buttons[index] = new ButtonComponent(labels[index], new int2(index == 3 ? 140 : 90, rowHeight), font, actions[index], 0);
                ButtonHosts[index].AddComponent(Buttons[index]);
            }
            ViewportHost = CreateHost(RootHost);
            Section = new EditorPlatformSettingsSection(ViewportHost, parent.LayerMask, font, labelWidth, valueWidth);
            Scroll = new EditorScrollComponent { Orientation = ScrollOrientation.Vertical, ItemExtent = 1, ContentRoot = Section.Root, ClipOriginEntity = ViewportHost };
            ViewportHost.AddComponent(Scroll);
        }
        /// <summary>Gets the tab content root whose visibility the profiles dialog controls.</summary>
        public EditorEntity Root => RootHost;
        /// <summary>Gets the preset selection widget.</summary>
        public ComboBoxComponent Selector { get; }
        /// <summary>Gets the editable stable id used by gameplay code.</summary>
        public TextBoxComponent IdField { get; }
        /// <summary>Gets the editable human-readable preset name.</summary>
        public TextBoxComponent NameField { get; }
        /// <summary>Gets the active setting bindings for editor tests and keyboard traversal.</summary>
        public IReadOnlyList<EditorPlatformSettingBinding> Items => Section.Items;
        /// <summary>Gets the visible height used by the parent dialog's status layout.</summary>
        public float ContentHeight { get; private set; }
        /// <summary>Reports an authoring error through the parent dialog's existing status row.</summary>
        public event Action<string> ErrorReported;
        /// <summary>Loads one platform's complete preset set and selects its initial preset for editing.</summary>
        public void Load(EditorPlatformProfileSettingsDocument platform, PlatformRuntimeSettingDefinition[] definitions) {
            Platform = platform; Definitions = definitions;
            CurrentCategory = definitions.Length == 0 ? "Settings" : definitions[0].Category;
            EditorRuntimeSettingsAuthoringService.Initialize(platform, definitions);
            using PlatformSettingsConfiguration validated = EditorRuntimeSettingsAuthoringService.BuildConfiguration(platform.PlatformId, platform.RuntimeSettings, definitions);
            SelectedIndex = platform.RuntimeSettings.Presets.FindIndex(preset => preset.Id == platform.RuntimeSettings.InitialPresetId);
            Refresh();
        }
        /// <summary>Validates all presets, storing id/name changes without changing any other preset's values.</summary>
        public bool TryStore(out string error) {
            error = string.Empty;
            if (Platform == null) { return true; }
            try {
                EditorRuntimePresetDocument preset = Platform.RuntimeSettings.Presets[SelectedIndex];
                string previousId = preset.Id;
                preset.Id = IdField.Text.Trim(); preset.Name = NameField.Text.Trim();
                if (Platform.RuntimeSettings.InitialPresetId == previousId) { Platform.RuntimeSettings.InitialPresetId = preset.Id; }
                if (!Section.TryValidate(out error)) { return false; }
                using PlatformSettingsConfiguration validated = EditorRuntimeSettingsAuthoringService.BuildConfiguration(Platform.PlatformId, Platform.RuntimeSettings, Definitions);
                return true;
            } catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) {
                error = exception.Message; return false;
            }
        }
        /// <summary>Creates a complete independent preset using the schema's creation defaults.</summary>
        public void CreatePreset() {
            if (!Commit()) { return; }
            string id = CreateUnusedId();
            Platform.RuntimeSettings.Presets.Add(EditorRuntimeSettingsAuthoringService.CreatePreset(Definitions, id, "Preset " + (Platform.RuntimeSettings.Presets.Count + 1)));
            SelectedIndex = Platform.RuntimeSettings.Presets.Count - 1; Refresh();
        }
        /// <summary>Duplicates all values without establishing inheritance or sharing mutable dictionaries.</summary>
        public void DuplicatePreset() {
            if (!Commit()) { return; }
            EditorRuntimePresetDocument source = Platform.RuntimeSettings.Presets[SelectedIndex];
            Platform.RuntimeSettings.Presets.Add(new EditorRuntimePresetDocument { Id = CreateUnusedId(), Name = source.Name + " Copy", Values = new(source.Values) });
            SelectedIndex = Platform.RuntimeSettings.Presets.Count - 1; Refresh();
        }
        /// <summary>Deletes the selected preset, retaining at least one and replacing a deleted initial selection explicitly.</summary>
        public void DeletePreset() {
            if (Platform.RuntimeSettings.Presets.Count == 1) { ErrorReported?.Invoke("Keep at least one runtime preset."); return; }
            string removedId = Platform.RuntimeSettings.Presets[SelectedIndex].Id;
            Platform.RuntimeSettings.Presets.RemoveAt(SelectedIndex); SelectedIndex = Math.Min(SelectedIndex, Platform.RuntimeSettings.Presets.Count - 1);
            if (Platform.RuntimeSettings.InitialPresetId == removedId) { Platform.RuntimeSettings.InitialPresetId = Platform.RuntimeSettings.Presets[SelectedIndex].Id; }
            Refresh();
        }
        /// <summary>Marks the displayed preset as the complete configuration used at game startup.</summary>
        public void SelectInitialPreset() {
            if (!Commit()) { return; }
            Platform.RuntimeSettings.InitialPresetId = Platform.RuntimeSettings.Presets[SelectedIndex].Id; Refresh();
        }
        /// <summary>Positions management controls and clips arbitrary setting counts to the available tab height.</summary>
        public void Layout(int width, int height) {
            int gap = Math.Max(4, RowHeight / 4); int fieldWidth = Math.Max(80, (width - gap) / 2);
            Selector.Size = new int2(width, RowHeight); SelectorHost.Position = float3.Zero;
            IdHost.Position = new float3(0, RowHeight + gap, 0); NameHost.Position = new float3(fieldWidth + gap, RowHeight + gap, 0);
            IdField.Size = new int2(fieldWidth, RowHeight); NameField.Size = new int2(fieldWidth, RowHeight);
            for (int index = 0; index < ButtonHosts.Length; index++) {
                ButtonHosts[index].Position = new float3(index * (width / 4), (RowHeight + gap) * 2, 0);
                Buttons[index].SetSize(new int2(Math.Max(1, width / 4 - gap), RowHeight));
            }
            CategoryHost.Position = new float3(0, (RowHeight + gap) * 3, 0); CategorySelector.Size = new int2(width, RowHeight);
            int top = (RowHeight + gap) * 4;
            ViewportHost.Position = new float3(0, top, 0); Section.Layout();
            Scroll.Size = new int2(width, Math.Max(1, height - top)); Scroll.ItemCount = (int)Math.Ceiling(Section.ContentHeight);
            ContentHeight = height;
        }
        /// <summary>Commits edits before preset management actions and exposes validation errors.</summary>
        bool Commit() { if (TryStore(out string error)) { return true; } ErrorReported?.Invoke(error); return false; }
        /// <summary>Switches the edited preset only after preserving valid values from the current preset.</summary>
        void HandleSelectionChanged(int index, string label) {
            if (Refreshing || Platform == null || index == SelectedIndex) { return; }
            if (!Commit()) { RefreshSelector(); return; }
            SelectedIndex = index; Refresh();
        }
        /// <summary>Rebuilds the active preset's form without copying values from any other preset.</summary>
        void Refresh() {
            RefreshSelector(); EditorRuntimePresetDocument preset = Platform.RuntimeSettings.Presets[SelectedIndex];
            IdField.Text = preset.Id; NameField.Text = preset.Name;
            string[] categories = Definitions.Select(definition => definition.Category).Distinct(StringComparer.Ordinal).ToArray();
            Refreshing = true; CategorySelector.SetItems(categories, Array.IndexOf(categories, CurrentCategory)); Refreshing = false;
            Section.Rebuild(CurrentCategory, EditorRuntimeSettingsAuthoringService.CreateEditorDefinitions(
                Definitions.Where(definition => definition.Category == CurrentCategory).ToArray()), preset.Values);
            Scroll.ResetScrollOffset();
        }
        /// <summary>Commits the current category before showing another category of the same complete preset.</summary>
        void HandleCategoryChanged(int index, string category) {
            if (Refreshing || category == CurrentCategory) { return; }
            if (!Commit()) { Refreshing = true; CategorySelector.SelectedIndex = Array.IndexOf(Definitions.Select(definition => definition.Category).Distinct().ToArray(), CurrentCategory); Refreshing = false; return; }
            CurrentCategory = category; Refresh();
        }
        /// <summary>Marks the initial preset in the selector while suppressing programmatic selection events.</summary>
        void RefreshSelector() {
            Refreshing = true;
            Selector.SetItems(Platform.RuntimeSettings.Presets.Select(preset => preset.Name + (preset.Id == Platform.RuntimeSettings.InitialPresetId ? " (initial)" : string.Empty)).ToArray(), SelectedIndex);
            Refreshing = false;
        }
        /// <summary>Finds a readable stable preset id not already used on this platform.</summary>
        string CreateUnusedId() {
            int number = 1;
            while (Platform.RuntimeSettings.Presets.Any(preset => preset.Id == "preset-" + number)) { number++; }
            return "preset-" + number;
        }
        /// <summary>Creates an editor-owned entity under the given control parent.</summary>
        static EditorEntity CreateHost(EditorEntity parent) {
            EditorEntity host = new(parent.OwnerCore, parent.InteractionServices) { LayerMask = parent.LayerMask, InternalEntity = true };
            parent.AddChild(host); return host;
        }
    }
}

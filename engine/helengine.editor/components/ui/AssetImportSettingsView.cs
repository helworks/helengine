namespace helengine.editor {
    /// <summary>
    /// Presents editable asset import settings inside the properties panel.
    /// </summary>
    public class AssetImportSettingsView {
        /// <summary>
        /// Vertical spacing between stacked rows.
        /// </summary>
        const int RowSpacing = 6;
        /// <summary>
        /// Height of combo box and button controls.
        /// </summary>
        const int ControlHeight = 24;
        /// <summary>
        /// Fixed width for the apply button.
        /// </summary>
        const int ApplyButtonWidth = 96;
        /// <summary>
        /// Label text shown above the importer selection.
        /// </summary>
        const string ImporterLabel = "Importer";
        /// <summary>
        /// Status prefix used for feedback messages.
        /// </summary>
        const string StatusPrefix = "Status:";

        /// <summary>
        /// Font used for text elements.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Render order used for label and status text.
        /// </summary>
        readonly byte textOrder;
        /// <summary>
        /// Root entity that owns view visuals.
        /// </summary>
        readonly EditorEntity root;
        /// <summary>
        /// Host entity for the importer label text.
        /// </summary>
        readonly EditorEntity labelHost;
        /// <summary>
        /// Text component used to render the importer label.
        /// </summary>
        readonly TextComponent labelText;
        /// <summary>
        /// Host entity for the combo box control.
        /// </summary>
        readonly EditorEntity comboHost;
        /// <summary>
        /// Combo box used to pick the importer identifier.
        /// </summary>
        readonly ComboBoxComponent comboBox;
        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity applyHost;
        /// <summary>
        /// Button used to apply the pending importer selection.
        /// </summary>
        readonly ButtonComponent applyButton;
        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity statusHost;
        /// <summary>
        /// Text component used to render status messages.
        /// </summary>
        readonly TextComponent statusText;
        /// <summary>
        /// Registered importer identifiers shown in the combo box.
        /// </summary>
        readonly List<string> importerIds;

        /// <summary>
        /// Current applied importer identifier for the selected asset.
        /// </summary>
        string activeImporterId;
        /// <summary>
        /// Pending importer identifier selected in the combo box.
        /// </summary>
        string pendingImporterId;
        /// <summary>
        /// Cached height of the view in pixels.
        /// </summary>
        int layoutHeight;
        /// <summary>
        /// Tracks whether the view is visible.
        /// </summary>
        bool isVisible;

        /// <summary>
        /// Raised when the user clicks apply with a pending importer change.
        /// </summary>
        public event Action<string> ApplyRequested;

        /// <summary>
        /// Initializes a new view for asset import settings.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        public AssetImportSettingsView(FontAsset font, ushort layerMask) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            importerIds = new List<string>(8);
            textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            root = new EditorEntity();
            root.LayerMask = layerMask;
            root.InternalEntity = true;

            labelHost = new EditorEntity();
            labelHost.LayerMask = layerMask;
            root.AddChild(labelHost);

            labelText = new TextComponent();
            labelText.Font = font;
            labelText.Text = ImporterLabel;
            labelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            labelText.RenderOrder2D = textOrder;
            labelHost.AddComponent(labelText);

            comboHost = new EditorEntity();
            comboHost.LayerMask = layerMask;
            root.AddChild(comboHost);

            comboBox = new ComboBoxComponent(new int2(160, ControlHeight), font, Array.Empty<string>(), -1);
            comboBox.SelectionChanged += HandleComboSelectionChanged;
            comboHost.AddComponent(comboBox);

            applyHost = new EditorEntity();
            applyHost.LayerMask = layerMask;
            root.AddChild(applyHost);

            applyButton = new ButtonComponent("Apply", new int2(ApplyButtonWidth, ControlHeight), font, HandleApplyClicked);
            applyHost.AddComponent(applyButton);

            statusHost = new EditorEntity();
            statusHost.LayerMask = layerMask;
            root.AddChild(statusHost);

            statusText = new TextComponent();
            statusText.Font = font;
            statusText.Text = string.Empty;
            statusText.Color = ThemeManager.Colors.InputForegroundSecondary;
            statusText.RenderOrder2D = textOrder;
            statusHost.AddComponent(statusText);

            Hide();
        }

        /// <summary>
        /// Gets the root entity to attach into the properties panel.
        /// </summary>
        public EditorEntity Root => root;

        /// <summary>
        /// Gets the current height of the view layout.
        /// </summary>
        public int Height => layoutHeight;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Shows the view with the provided importer list and active selection.
        /// </summary>
        /// <param name="importerIds">Registered importer identifiers.</param>
        /// <param name="currentImporterId">Importer identifier currently applied.</param>
        public void Show(IReadOnlyList<string> importerIds, string currentImporterId) {
            if (importerIds == null) {
                throw new ArgumentNullException(nameof(importerIds));
            }
            if (string.IsNullOrWhiteSpace(currentImporterId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(currentImporterId));
            }

            this.importerIds.Clear();
            for (int i = 0; i < importerIds.Count; i++) {
                string importerId = importerIds[i];
                if (string.IsNullOrWhiteSpace(importerId)) {
                    throw new ArgumentException("Importer ids must be provided.", nameof(importerIds));
                }

                this.importerIds.Add(importerId);
            }

            activeImporterId = currentImporterId;
            pendingImporterId = currentImporterId;

            int selectedIndex = FindImporterIndex(currentImporterId);
            comboBox.SetItems(this.importerIds, selectedIndex);
            comboBox.IsOpen = false;

            isVisible = true;
            root.Enabled = true;
            UpdateStatusText();
        }

        /// <summary>
        /// Hides the view and clears its status.
        /// </summary>
        public void Hide() {
            isVisible = false;
            root.Enabled = false;
            comboBox.IsOpen = false;
            statusText.Text = string.Empty;
        }

        /// <summary>
        /// Updates the view layout within the properties panel.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width for controls.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!isVisible) {
                return;
            }

            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Layout width must be positive.");
            }

            root.Position = new float3(left, top, 0.2f);

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            int labelHeight = (int)Math.Ceiling(lineHeight);

            labelHost.Position = new float3(0f, 0f, 0.1f);
            labelText.Size = new int2(width, labelHeight);

            int comboTop = labelHeight + RowSpacing;
            comboHost.Position = new float3(0f, comboTop, 0.1f);
            comboBox.Size = new int2(width, ControlHeight);

            int applyTop = comboTop + ControlHeight + RowSpacing;
            applyHost.Position = new float3(0f, applyTop, 0.1f);

            int statusTop = applyTop + ControlHeight + RowSpacing;
            statusHost.Position = new float3(0f, statusTop, 0.1f);
            statusText.Size = new int2(width, labelHeight);

            layoutHeight = statusTop + labelHeight;
        }

        /// <summary>
        /// Handles combo box selection changes.
        /// </summary>
        /// <param name="index">Selected index.</param>
        /// <param name="value">Selected importer identifier.</param>
        void HandleComboSelectionChanged(int index, string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Importer selection was not provided.");
            }

            pendingImporterId = value;
            UpdateStatusText();
        }

        /// <summary>
        /// Handles apply button clicks by raising the apply event when needed.
        /// </summary>
        void HandleApplyClicked() {
            if (!HasPendingChange()) {
                return;
            }

            if (ApplyRequested != null) {
                ApplyRequested(pendingImporterId);
            }
        }

        /// <summary>
        /// Updates the status message based on current selections.
        /// </summary>
        void UpdateStatusText() {
            if (importerIds.Count == 0) {
                statusText.Text = $"{StatusPrefix} No importers registered.";
                return;
            }

            bool activeValid = IsImporterValid(activeImporterId);
            bool pendingValid = IsImporterValid(pendingImporterId);

            if (!activeValid) {
                statusText.Text = $"{StatusPrefix} Current importer is not registered.";
                return;
            }

            if (!pendingValid) {
                statusText.Text = $"{StatusPrefix} Select an importer.";
                return;
            }

            if (HasPendingChange()) {
                statusText.Text = $"{StatusPrefix} Pending importer '{pendingImporterId}'.";
                return;
            }

            statusText.Text = string.Empty;
        }

        /// <summary>
        /// Checks whether the pending importer differs from the active one.
        /// </summary>
        /// <returns>True when a pending change exists.</returns>
        bool HasPendingChange() {
            return !string.Equals(activeImporterId, pendingImporterId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an importer identifier is available in the list.
        /// </summary>
        /// <param name="importerId">Importer identifier to test.</param>
        /// <returns>True when the identifier exists in the list.</returns>
        bool IsImporterValid(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return FindImporterIndex(importerId) >= 0;
        }

        /// <summary>
        /// Finds the index for a given importer identifier.
        /// </summary>
        /// <param name="importerId">Importer identifier to locate.</param>
        /// <returns>Index of the importer, or -1 when missing.</returns>
        int FindImporterIndex(string importerId) {
            for (int i = 0; i < importerIds.Count; i++) {
                if (string.Equals(importerIds[i], importerId, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }

            return -1;
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to pick one mesh modifier kind for the Modifiers stack.
    /// </summary>
    public sealed class MeshModifierPickerModal : EditorDialogBase {
        /// <summary>
        /// Default panel width for the dialog.
        /// </summary>
        public const int PanelWidth = 320;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Spacing used between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height of each modifier entry button.
        /// </summary>
        public const int EntryButtonHeight = 26;

        /// <summary>
        /// Vertical spacing between modifier entry buttons.
        /// </summary>
        public const int EntryButtonSpacing = 8;

        /// <summary>
        /// Stable modifier kind identifiers offered by the picker, in display order.
        /// </summary>
        static readonly string[] ModifierKinds = {
            MeshComponentModifier.TessellateKind,
            MeshComponentModifier.UvwMapKind
        };

        /// <summary>
        /// Visible display names matching each entry in the kinds list.
        /// </summary>
        static readonly string[] ModifierDisplayNames = {
            "Tessellate",
            "UVW Map"
        };

        /// <summary>
        /// Host entities for the modifier entry buttons.
        /// </summary>
        readonly EditorEntity[] EntryButtonHosts;

        /// <summary>
        /// Modifier entry button components.
        /// </summary>
        readonly ButtonComponent[] EntryButtons;

        /// <summary>
        /// Callback invoked when the user picks a modifier kind.
        /// </summary>
        Action<string> PickedCallback;

        /// <summary>
        /// Tracks whether the modal has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Initializes a new modifier picker modal.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public MeshModifierPickerModal(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new modifier picker modal using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public MeshModifierPickerModal(FontAsset font, EditorUiMetrics metrics)
            : base("MeshModifierPickerModal", "Add Modifier", font, metrics, PanelWidth, ResolvePanelHeight(), HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, ResolvePanelHeight());

            EntryButtonHosts = new EditorEntity[ModifierKinds.Length];
            EntryButtons = new ButtonComponent[ModifierKinds.Length];
            for (int index = 0; index < ModifierKinds.Length; index++) {
                string kind = ModifierKinds[index];
                EditorEntity buttonHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = float3.Zero,
                    InternalEntity = true
                };
                DialogPanelRoot.AddChild(buttonHost);

                ButtonComponent button = new ButtonComponent(ModifierDisplayNames[index], GetEntryButtonSize(), DialogFont, () => HandleEntryClicked(kind), 0f);
                buttonHost.AddComponent(button);
                button.SetRenderOrders(DialogTextOrder, DialogTextOrder);

                EntryButtonHosts[index] = buttonHost;
                EntryButtons[index] = button;
            }

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the modal and registers the callback to receive the picked modifier kind.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected modifier kind identifier.</param>
        public void Show(Action<string> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickedCallback = onPicked;
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the modal and clears any pending pick callback.
        /// </summary>
        public void Hide() {
            PickedCallback = null;
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }
        }

        /// <summary>
        /// Closes the modal when the shared dialog shell requests dismissal.
        /// </summary>
        protected override void OnCloseRequested() {
            Hide();
        }

        /// <summary>
        /// Repositions the entry buttons whenever the shared modal shell position or size changes.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutEntries();
        }

        /// <summary>
        /// Notifies the pending callback that one modifier kind was picked and hides the modal.
        /// </summary>
        /// <param name="kind">Stable modifier kind identifier that was clicked.</param>
        void HandleEntryClicked(string kind) {
            Action<string> callback = PickedCallback;
            PickedCallback = null;
            Hide();
            if (callback != null) {
                callback(kind);
            }
        }

        /// <summary>
        /// Updates entry button placement within the dialog panel.
        /// </summary>
        void LayoutEntries() {
            int buttonWidth = Math.Max(1, DialogWidth - GetPanelPaddingPixels() * 2);
            int entriesTop = GetEntriesTop();
            int entryStride = GetEntryButtonSize().Y + GetEntryButtonSpacingPixels();
            for (int index = 0; index < EntryButtonHosts.Length; index++) {
                EntryButtonHosts[index].LocalPosition = new float3(GetPanelPaddingPixels(), entriesTop + entryStride * index, 0.2f);
                EntryButtons[index].SetSize(new int2(buttonWidth, GetEntryButtonSize().Y));
            }
        }

        /// <summary>
        /// Resolves the fixed panel height covering the header and every modifier entry.
        /// </summary>
        /// <returns>Unscaled panel height in pixels.</returns>
        static int ResolvePanelHeight() {
            return HeaderHeight + PanelPadding + SectionSpacing
                + ModifierKinds.Length * EntryButtonHeight
                + (ModifierKinds.Length - 1) * EntryButtonSpacing
                + PanelPadding;
        }

        /// <summary>
        /// Gets the scaled panel padding used by the dialog.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled top position of the first entry button.
        /// </summary>
        /// <returns>Scaled entries top position in pixels.</returns>
        int GetEntriesTop() {
            return DialogMetrics.ScalePixels(PanelPadding + HeaderHeight + SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled entry button size.
        /// </summary>
        /// <returns>Scaled entry button size.</returns>
        int2 GetEntryButtonSize() {
            return new int2(DialogMetrics.ScalePixels(PanelWidth - PanelPadding * 2), DialogMetrics.ScalePixels(EntryButtonHeight));
        }

        /// <summary>
        /// Gets the scaled vertical spacing between entry buttons.
        /// </summary>
        /// <returns>Scaled entry spacing in pixels.</returns>
        int GetEntryButtonSpacingPixels() {
            return DialogMetrics.ScalePixels(EntryButtonSpacing);
        }
    }
}

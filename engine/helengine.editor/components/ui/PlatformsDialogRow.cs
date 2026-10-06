namespace helengine.editor {
    /// <summary>
    /// Bundles the reusable visuals that render one recycled platform row inside the platforms dialog.
    /// </summary>
    public sealed class PlatformsDialogRow {
        /// <summary>
        /// Initializes a new pooled platform row with the shared dialog styling.
        /// </summary>
        /// <param name="font">Font used to render the row label.</param>
        /// <param name="layerMask">Layer mask applied to the row hierarchy.</param>
        /// <param name="checkBoxSize">Scaled size used for the row checkbox.</param>
        public PlatformsDialogRow(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, ushort layerMask, int2 checkBoxSize) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            CheckBoxHost = new EditorEntity(ownerCore, interactionServices) {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };

            CheckBox = new CheckBoxComponent(checkBoxSize, font, false);
            CheckBoxHost.AddComponent(CheckBox);

            LabelHost = new EditorEntity(ownerCore, interactionServices) {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };

            LabelText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary
            };
            LabelHost.AddComponent(LabelText);

            PlatformIndex = -1;
        }

        /// <summary>
        /// Gets the host entity for the row checkbox.
        /// </summary>
        public EditorEntity CheckBoxHost { get; }

        /// <summary>
        /// Gets the checkbox used to toggle the platform bound to this row.
        /// </summary>
        public CheckBoxComponent CheckBox { get; }

        /// <summary>
        /// Gets the host entity for the row label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the label text rendered for the platform bound to this row.
        /// </summary>
        public TextComponent LabelText { get; }

        /// <summary>
        /// Gets or sets the index into the available-platform list currently bound to this row, or -1 when unbound.
        /// </summary>
        public int PlatformIndex { get; set; }
    }
}

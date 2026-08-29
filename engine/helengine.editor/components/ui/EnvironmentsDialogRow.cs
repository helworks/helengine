namespace helengine.editor {
    /// <summary>
    /// Bundles the reusable visuals that render one environment row inside the environments dialog.
    /// </summary>
    public sealed class EnvironmentsDialogRow {
        /// <summary>
        /// Initializes one environment row.
        /// </summary>
        /// <param name="font">Font used to render the row.</param>
        /// <param name="layerMask">Layer mask applied to row entities.</param>
        /// <param name="buttonSize">Size assigned to the selectable environment button.</param>
        /// <param name="textOrder">Render order used by row text.</param>
        /// <param name="onClicked">Callback invoked when the row is selected.</param>
        public EnvironmentsDialogRow(
            Core ownerCore,
            EditorSessionInteractionServices interactionServices,
            FontAsset font,
            ushort layerMask,
            int2 buttonSize,
            byte textOrder,
            Action<ButtonComponent> onClicked) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (onClicked == null) {
                throw new ArgumentNullException(nameof(onClicked));
            }

            SelectHost = new EditorEntity(ownerCore, interactionServices) {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };

            ButtonComponent selectButton = null;
            selectButton = new ButtonComponent(string.Empty, buttonSize, font, () => onClicked(selectButton), 0f);
            selectButton.SetRenderOrders(textOrder, textOrder);
            selectButton.SetHoverCursor(PointerCursorKind.Hand);
            SelectHost.AddComponent(selectButton);
            SelectButton = selectButton;

            ProtectedHost = new EditorEntity(ownerCore, interactionServices) {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            ProtectedText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                RenderOrder2D = textOrder
            };
            ProtectedHost.AddComponent(ProtectedText);
            EnvironmentIndex = -1;
            EnvironmentId = string.Empty;
        }

        /// <summary>
        /// Gets the host entity for the selectable environment button.
        /// </summary>
        public EditorEntity SelectHost { get; }

        /// <summary>
        /// Gets the button used to select this environment.
        /// </summary>
        public ButtonComponent SelectButton { get; }

        /// <summary>
        /// Gets the host entity for the protected marker.
        /// </summary>
        public EditorEntity ProtectedHost { get; }

        /// <summary>
        /// Gets the protected marker text component.
        /// </summary>
        public TextComponent ProtectedText { get; }

        /// <summary>
        /// Gets or sets the index into the current environment document, or -1 when unbound.
        /// </summary>
        public int EnvironmentIndex { get; set; }

        /// <summary>
        /// Gets or sets the environment identifier currently bound to this row.
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Gets or sets whether the bound environment is protected.
        /// </summary>
        public bool IsProtected { get; set; }
    }
}

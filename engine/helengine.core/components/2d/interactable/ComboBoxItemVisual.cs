namespace helengine {
    /// <summary>
    /// Holds the UI elements needed to render a single combo box item row.
    /// </summary>
    public class ComboBoxItemVisual {
        /// <summary>
        /// Gets the root entity for the item row.
        /// </summary>
        public Entity Root { get; }

        /// <summary>
        /// Gets the rounded rectangle background for the item row.
        /// </summary>
        public RoundedRectComponent Background { get; }

        /// <summary>
        /// Gets the label host entity for the item text.
        /// </summary>
        public Entity LabelHost { get; }

        /// <summary>
        /// Gets the text component used to render the item label.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the interactable region for the item row.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Raised when the item row receives a cursor interaction.
        /// </summary>
        public event Action<ComboBoxItemVisual, int2, int2, PointerInteraction> CursorEvent;

        /// <summary>
        /// Gets or sets the item index represented by this visual.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item is pressed.
        /// </summary>
        public bool IsPressed { get; set; }

        /// <summary>
        /// Initializes a new visual entry for a combo box item.
        /// </summary>
        /// <param name="font">Font used for the item label.</param>
        /// <param name="layerMask">Layer mask applied to the item entities.</param>
        /// <param name="backgroundOrder">Render order used for the background.</param>
        /// <param name="textOrder">Render order used for the label text.</param>
        public ComboBoxItemVisual(FontAsset font, ushort layerMask, byte backgroundOrder, byte textOrder) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Root = new Entity();
            Root.LayerMask = layerMask;
            Root.Enabled = true;
            Root.InitComponents();
            Root.InitChildren();

            Background = new RoundedRectComponent();
            Background.RenderOrder2D = backgroundOrder;
            Background.BorderThickness = 1f;
            Root.AddComponent(Background);

            Interactable = new InteractableComponent();
            Interactable.CursorEvent += HandleCursorEvent;
            Root.AddComponent(Interactable);

            LabelHost = new Entity();
            LabelHost.LayerMask = layerMask;
            LabelHost.Enabled = true;
            LabelHost.InitComponents();
            Root.AddChild(LabelHost);

            Label = new TextComponent();
            Label.Font = font;
            Label.RenderOrder2D = textOrder;
            LabelHost.AddComponent(Label);
        }

        /// <summary>
        /// Forwards cursor interaction from the interactable to listeners.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            if (CursorEvent != null) {
                CursorEvent(this, relPos, delta, state);
            }
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Represents a single tab entry in a docked tab strip.
    /// </summary>
    public sealed class DockTabEntry {
        /// <summary>
        /// Creates a new tab entry for a dockable window.
        /// </summary>
        /// <param name="dockable">Dockable window represented by the tab.</param>
        /// <param name="font">Font used for the tab label.</param>
        /// <param name="layerMask">Layer mask used for rendering and hit testing.</param>
        /// <param name="backgroundOrder">Render order bucket for the tab background.</param>
        /// <param name="textOrder">Render order bucket for the tab text.</param>
        public DockTabEntry(
            DockableEntity dockable,
            FontAsset font,
            ushort layerMask,
            byte backgroundOrder,
            byte textOrder) {
            Dockable = dockable;
            Root = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };

            Background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = backgroundOrder
            };
            Root.AddComponent(Background);

            LabelHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            Root.AddChild(LabelHost);

            Label = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.TextOnAccent,
                Size = new int2(1, 1),
                RenderOrder2D = textOrder
            };
            LabelHost.AddComponent(Label);

            Interactable = new InteractableComponent {
                Size = new int2(1, 1)
            };
            Root.AddComponent(Interactable);
        }

        /// <summary>
        /// Gets or sets the dockable window represented by the tab.
        /// </summary>
        public DockableEntity Dockable { get; set; }

        /// <summary>
        /// Gets the root entity for the tab visuals.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the background sprite for the tab.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the entity hosting the label text.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the text component used for the tab label.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the interactable component used for tab clicks.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Gets or sets the tab index assigned during layout.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the computed tab width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is pressed.
        /// </summary>
        public bool IsPressed { get; set; }
    }
}

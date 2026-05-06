namespace helengine.editor {
    /// <summary>
    /// Bundles the reusable visuals that render one queue item row inside the build dialog.
    /// </summary>
    public sealed class BuildDialogQueueRow {
        /// <summary>
        /// Initializes a new queue row with the shared dialog styling.
        /// </summary>
        /// <param name="font">Font used to render the queue-row text.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the queue row.</param>
        /// <param name="layerMask">Layer mask applied to the row hierarchy.</param>
        /// <param name="panelOrder">Render order used for row backgrounds and separators.</param>
        /// <param name="textOrder">Render order used for row labels and buttons.</param>
        public BuildDialogQueueRow(FontAsset font, EditorUiMetrics metrics, ushort layerMask, byte panelOrder, byte textOrder) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Root = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };

            Background = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.SurfacePrimary,
                BorderThickness = 0f,
                Radius = 0f,
                RenderOrder2D = panelOrder,
                Size = new int2(
                    metrics.ScalePixels(BuildDialog.QueueColumnWidth - 4),
                    metrics.ScalePixels(BuildDialog.QueueRowHeight))
            };
            Root.AddComponent(Background);

            SeparatorHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(SeparatorHost);

            Separator = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentTertiary,
                RenderOrder2D = panelOrder,
                Size = new int2(
                    metrics.ScalePixels(BuildDialog.QueueColumnWidth - 4),
                    metrics.ScalePixels(1))
            };
            SeparatorHost.AddComponent(Separator);

            RemoveButtonHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(RemoveButtonHost);

            RemoveButton = new ButtonComponent(
                "X",
                new int2(
                    metrics.ScalePixels(BuildDialog.QueueCardRemoveButtonWidth),
                    metrics.ScalePixels(28)),
                font,
                HandleRemoveButtonClicked);
            RemoveButton.SetRenderOrders(panelOrder, textOrder);
            RemoveButtonHost.AddComponent(RemoveButton);

            TextHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(TextHost);

            Text = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = textOrder,
                Size = new int2(1, metrics.ScalePixels(BuildDialog.QueueRowHeight))
            };
            TextHost.AddComponent(Text);
        }

        /// <summary>
        /// Raised when the row's remove button is pressed.
        /// </summary>
        public event Action<BuildDialogQueueRow> RemoveRequested;

        /// <summary>
        /// Gets the root entity for the queue row.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the row background surface.
        /// </summary>
        public RoundedRectComponent Background { get; }

        /// <summary>
        /// Gets the host entity for the bottom separator line.
        /// </summary>
        public EditorEntity SeparatorHost { get; }

        /// <summary>
        /// Gets the separator line rendered at the bottom of the row.
        /// </summary>
        public SpriteComponent Separator { get; }

        /// <summary>
        /// Gets the host entity for the row's remove button.
        /// </summary>
        public EditorEntity RemoveButtonHost { get; }

        /// <summary>
        /// Gets the remove button used to delete the queued build item.
        /// </summary>
        public ButtonComponent RemoveButton { get; }

        /// <summary>
        /// Gets the host entity for the row summary text.
        /// </summary>
        public EditorEntity TextHost { get; }

        /// <summary>
        /// Gets the summary text shown for the queued build item.
        /// </summary>
        public TextComponent Text { get; }

        /// <summary>
        /// Gets or sets the queue item id currently rendered by the row.
        /// </summary>
        public string QueueItemId { get; set; }

        /// <summary>
        /// Raises the row remove event when the remove button is clicked.
        /// </summary>
        void HandleRemoveButtonClicked() {
            if (RemoveRequested != null) {
                RemoveRequested(this);
            }
        }
    }
}

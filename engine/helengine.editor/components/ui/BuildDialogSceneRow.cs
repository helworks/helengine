namespace helengine.editor {
    /// <summary>
    /// Bundles the reusable visuals that render one visible scene row inside the build dialog.
    /// </summary>
    public sealed class BuildDialogSceneRow {
        /// <summary>
        /// Initializes one pooled scene row using the shared build-dialog styling.
        /// </summary>
        /// <param name="font">Font used by the scene label and order field.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the row controls.</param>
        /// <param name="layerMask">Layer mask applied to the row hierarchy.</param>
        /// <param name="panelOrder">Render order used for panel-background controls.</param>
        /// <param name="textOrder">Render order used for text and checkbox visuals.</param>
        public BuildDialogSceneRow(FontAsset font, EditorUiMetrics metrics, ushort layerMask, byte panelOrder, byte textOrder) {
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

            OrderHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(OrderHost);

            OrderField = new TextBoxComponent(
                new int2(
                    metrics.ScalePixels(BuildDialog.SceneOrderFieldWidth),
                    metrics.ScalePixels(BuildDialog.SceneOrderFieldHeight)),
                font,
                string.Empty);
            OrderField.SetRenderOrders(panelOrder, textOrder);
            OrderHost.AddComponent(OrderField);

            LabelHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(LabelHost);

            LabelText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = textOrder
            };
            LabelHost.AddComponent(LabelText);

            CheckBoxHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(CheckBoxHost);

            CheckBox = new CheckBoxComponent(
                new int2(
                    metrics.ScalePixels(18),
                    metrics.ScalePixels(18)),
                font,
                false);
            CheckBox.SetRenderOrders(panelOrder, textOrder);
            CheckBoxHost.AddComponent(CheckBox);
        }

        /// <summary>
        /// Gets the root entity for the pooled row.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the host entity for the order textbox.
        /// </summary>
        public EditorEntity OrderHost { get; }

        /// <summary>
        /// Gets the order textbox bound to the current scene id.
        /// </summary>
        public TextBoxComponent OrderField { get; }

        /// <summary>
        /// Gets the host entity for the scene label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the scene label text component.
        /// </summary>
        public TextComponent LabelText { get; }

        /// <summary>
        /// Gets the host entity for the scene selection checkbox.
        /// </summary>
        public EditorEntity CheckBoxHost { get; }

        /// <summary>
        /// Gets the checkbox bound to the current scene id.
        /// </summary>
        public CheckBoxComponent CheckBox { get; }

        /// <summary>
        /// Gets or sets the scene id currently rendered by the row.
        /// </summary>
        public string SceneId { get; set; }
    }
}

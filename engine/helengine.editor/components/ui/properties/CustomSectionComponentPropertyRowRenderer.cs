namespace helengine.editor {
    /// <summary>
    /// Renders the collapsible nested headers that provider-backed custom editors insert between property rows. The
    /// row is drawn as a full-width surface with a hover cursor, and clicking it toggles the section body.
    /// </summary>
    sealed class CustomSectionComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the custom section rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public CustomSectionComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the custom section row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.CustomSection; }
        }

        /// <summary>
        /// Creates the section surface and the interactable region that reports hover and click state.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created chrome.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            SpriteComponent background = new SpriteComponent {
                Texture = View.RendererResources.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, ComponentPropertiesView.RowHeight)
            };
            rowEntity.AddComponent(background);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(1, ComponentPropertiesView.RowHeight),
                HoverCursor = PointerCursorKind.Hand
            };
            rowEntity.AddComponent(interactable);

            row.HeaderBackground = background;
            row.HeaderInteractable = interactable;
            interactable.CursorEvent += (pos, delta, state) => View.HandleCustomSectionCursor(row, state);
        }

        /// <summary>
        /// Returns the section surface to its unhovered appearance for the current expansion state.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            View.UpdateCustomSectionVisual(row, false);
        }

        /// <summary>
        /// Stretches the section surface across the full row width and indents the title like a nested header.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label; unused because the title spans the row.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.HeaderBackground == null || row.HeaderInteractable == null) {
                return;
            }

            int safeWidth = Math.Max(1, width);
            row.HeaderBackground.Size = new int2(safeWidth, height);
            row.HeaderInteractable.Size = new int2(safeWidth, height);
            row.LabelHost.Position = new float3(ComponentPropertiesView.SectionHeaderPadding, row.LabelHost.Position.Y, 0.2f);
            row.Label.Size = new int2(Math.Max(1, safeWidth - ComponentPropertiesView.SectionHeaderPadding * 2), row.Label.Size.Y);
        }
    }
}

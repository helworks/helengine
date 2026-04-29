namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to choose a new parent for one scene entity.
    /// </summary>
    public class ReparentEntityDialog : EditorEntity {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 420;
        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 332;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Height reserved for the embedded hierarchy picker.
        /// </summary>
        public const int HierarchyHeight = 176;
        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Corner radius applied to the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness applied to the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the apply button.
        /// </summary>
        static readonly int2 ApplyButtonSize = new int2(88, 22);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Root entity hosting the panel content.
        /// </summary>
        readonly EditorEntity panelRoot;
        /// <summary>
        /// Dialog background shape.
        /// </summary>
        readonly RoundedRectComponent panelBackground;
        /// <summary>
        /// Host entity for the dialog title.
        /// </summary>
        readonly EditorEntity titleHost;
        /// <summary>
        /// Dialog title text.
        /// </summary>
        readonly TextComponent titleText;
        /// <summary>
        /// Host entity for the target-entity label.
        /// </summary>
        readonly EditorEntity targetHost;
        /// <summary>
        /// Text showing which entity will be reparented.
        /// </summary>
        readonly TextComponent targetText;
        /// <summary>
        /// Embedded hierarchy picker used to choose the destination parent.
        /// </summary>
        readonly SceneHierarchyPickerView parentHierarchyView;
        /// <summary>
        /// Host entity for validation or status text.
        /// </summary>
        readonly EditorEntity statusHost;
        /// <summary>
        /// Validation or status text shown above the footer.
        /// </summary>
        readonly TextComponent statusText;
        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity cancelButtonHost;
        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent cancelButton;
        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity applyButtonHost;
        /// <summary>
        /// Apply button component.
        /// </summary>
        readonly ButtonComponent applyButton;
        /// <summary>
        /// Alternative parent choices exposed to the session and tests.
        /// </summary>
        readonly List<Entity> availableParentEntities;
        /// <summary>
        /// Render order used for panel surfaces.
        /// </summary>
        readonly byte panelOrder;
        /// <summary>
        /// Render order used for foreground text and controls.
        /// </summary>
        readonly byte textOrder;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 panelPosition;
        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Raised when the user confirms one reparent selection.
        /// </summary>
        public event Action<ReparentEntityDialogSelection> ConfirmRequested;
        /// <summary>
        /// Raised when the user cancels the reparent workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new reparent dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public ReparentEntityDialog(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            availableParentEntities = new List<Entity>(8);

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "ReparentEntityDialog";

            panelOrder = RenderOrder2D.ModalBackground;
            textOrder = RenderOrder2D.ModalForeground;

            panelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            AddChild(panelRoot);

            panelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = panelOrder,
                Size = new int2(PanelWidth, PanelHeight)
            };
            panelRoot.AddComponent(panelBackground);

            titleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            panelRoot.AddChild(titleHost);

            titleText = new TextComponent {
                Font = font,
                Text = "Reparent",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = textOrder
            };
            titleHost.AddComponent(titleText);

            targetHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            panelRoot.AddChild(targetHost);

            targetText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = textOrder
            };
            targetHost.AddComponent(targetText);

            parentHierarchyView = new SceneHierarchyPickerView(font, LayerMask, panelOrder, textOrder);
            parentHierarchyView.ParentEntitySelected += HandleParentEntitySelected;
            panelRoot.AddChild(parentHierarchyView.Entity);

            statusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            panelRoot.AddChild(statusHost);

            statusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = textOrder
            };
            statusHost.AddComponent(statusText);

            cancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            panelRoot.AddChild(cancelButtonHost);

            cancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, HandleCancelClicked, 0f);
            cancelButtonHost.AddComponent(cancelButton);
            cancelButton.SetRenderOrders(textOrder, textOrder);

            applyButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            panelRoot.AddChild(applyButtonHost);

            applyButton = new ButtonComponent("Apply", ApplyButtonSize, font, HandleApplyClicked, 0f);
            applyButtonHost.AddComponent(applyButton);
            applyButton.SetRenderOrders(textOrder, textOrder);

            Enabled = false;
            isInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Gets the entity currently being reparented.
        /// </summary>
        public Entity TargetEntity { get; private set; }

        /// <summary>
        /// Gets the visible scene entities available to the hierarchy picker.
        /// </summary>
        public IReadOnlyList<Entity> AvailableParentEntities => availableParentEntities;

        /// <summary>
        /// Gets the parent entity currently selected in the dialog, or null for the scene root.
        /// </summary>
        public Entity SelectedParentEntity { get; private set; }

        /// <summary>
        /// Shows the dialog for the provided entity and visible scene hierarchy.
        /// </summary>
        /// <param name="targetEntity">Entity that should be reparented.</param>
        /// <param name="parentEntities">Visible scene entities that should appear in the picker.</param>
        public void Show(Entity targetEntity, IReadOnlyList<Entity> parentEntities) {
            if (targetEntity == null) {
                throw new ArgumentNullException(nameof(targetEntity));
            }
            if (parentEntities == null) {
                throw new ArgumentNullException(nameof(parentEntities));
            }

            TargetEntity = targetEntity;
            SelectedParentEntity = targetEntity.Parent;
            statusText.Text = string.Empty;
            targetText.Text = GetEntityDisplayName(targetEntity);
            CopyAvailableParentEntities(parentEntities);
            parentHierarchyView.Show(targetEntity, parentEntities, SelectedParentEntity);
            Enabled = true;
        }

        /// <summary>
        /// Hides the dialog and clears its input blocker and transient state.
        /// </summary>
        public void Hide() {
            parentHierarchyView.Hide();
            EditorInputCaptureService.ClearBlocker(this);
            statusText.Text = string.Empty;
            Enabled = false;
            TargetEntity = null;
            SelectedParentEntity = null;
            availableParentEntities.Clear();
        }

        /// <summary>
        /// Shows one validation or reparent error in the dialog.
        /// </summary>
        /// <param name="message">Message to display.</param>
        public void ShowError(string message) {
            statusText.Text = message ?? string.Empty;
        }

        /// <summary>
        /// Updates dialog layout to fit the current host window size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!isInitialized) {
                return;
            }
            if (!Enabled) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            int safeWidth = Math.Max(1, windowWidth);
            int safeHeight = Math.Max(1, windowHeight);
            panelPosition = new int2(
                Math.Max(0, (safeWidth - PanelWidth) / 2),
                Math.Max(0, (safeHeight - PanelHeight) / 2));

            panelRoot.Position = new float3(panelPosition.X, panelPosition.Y, 0.1f);
            EditorInputCaptureService.SetBlocker(this, panelPosition, new int2(PanelWidth, PanelHeight));

            LayoutTitle();
            LayoutTarget();
            LayoutParentHierarchy();
            LayoutStatus();
            LayoutFooter();
        }

        /// <summary>
        /// Copies the visible scene entities into dialog-owned state for inspection and tests.
        /// </summary>
        /// <param name="parentEntities">Visible scene entities provided by the editor session.</param>
        void CopyAvailableParentEntities(IReadOnlyList<Entity> parentEntities) {
            availableParentEntities.Clear();
            for (int entityIndex = 0; entityIndex < parentEntities.Count; entityIndex++) {
                availableParentEntities.Add(parentEntities[entityIndex]);
            }
        }

        /// <summary>
        /// Updates the selected parent entity after one valid hierarchy-row selection.
        /// </summary>
        /// <param name="entity">Selected parent entity, or null for the scene root.</param>
        void HandleParentEntitySelected(Entity entity) {
            SelectedParentEntity = entity;
            statusText.Text = string.Empty;
        }

        /// <summary>
        /// Raises a confirm request for the currently selected reparent target and destination.
        /// </summary>
        void HandleApplyClicked() {
            if (TargetEntity == null) {
                throw new InvalidOperationException("A target entity must be selected before applying reparenting.");
            }

            if (ConfirmRequested != null) {
                ConfirmRequested(new ReparentEntityDialogSelection(TargetEntity, SelectedParentEntity));
            }
        }

        /// <summary>
        /// Raises one cancel request for the active reparent workflow.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Lays out the dialog title.
        /// </summary>
        void LayoutTitle() {
            titleHost.Position = new float3(PanelPadding, PanelPadding, 0.2f);
            FontTightMetrics metrics = font.MeasureTight(titleText.Text);
            titleText.Size = new int2(
                Math.Max(1, (int)Math.Ceiling(metrics.Width)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, font.LineHeight))));
        }

        /// <summary>
        /// Lays out the target-entity label.
        /// </summary>
        void LayoutTarget() {
            int y = PanelPadding + GetLineHeight() + SectionSpacing;
            targetHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = font.MeasureTight(targetText.Text);
            targetText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, font.LineHeight))));
        }

        /// <summary>
        /// Lays out the embedded hierarchy picker.
        /// </summary>
        void LayoutParentHierarchy() {
            int y = PanelPadding + GetLineHeight() + SectionSpacing + GetLineHeight() + SectionSpacing;
            parentHierarchyView.Entity.Position = new float3(PanelPadding, y, 0.2f);
            parentHierarchyView.UpdateLayout(PanelWidth - (PanelPadding * 2), HierarchyHeight);
        }

        /// <summary>
        /// Lays out the validation or status text row.
        /// </summary>
        void LayoutStatus() {
            int y = PanelHeight - PanelPadding - FooterHeight - SectionSpacing - GetLineHeight();
            statusHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = font.MeasureTight(statusText.Text);
            statusText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, font.LineHeight))));
        }

        /// <summary>
        /// Lays out the footer buttons.
        /// </summary>
        void LayoutFooter() {
            int buttonY = PanelHeight - PanelPadding - CancelButtonSize.Y;
            int cancelX = PanelWidth - PanelPadding - CancelButtonSize.X;
            int applyX = cancelX - 8 - ApplyButtonSize.X;

            cancelButtonHost.Position = new float3(cancelX, buttonY, 0.2f);
            applyButtonHost.Position = new float3(applyX, buttonY, 0.2f);
        }

        /// <summary>
        /// Returns the line height used for layout calculations.
        /// </summary>
        /// <returns>Rounded line height for the configured font.</returns>
        int GetLineHeight() {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)));
        }

        /// <summary>
        /// Resolves one display label for an entity or for the scene root.
        /// </summary>
        /// <param name="entity">Entity to label, or null for the scene root.</param>
        /// <returns>Display label used in the dialog.</returns>
        string GetEntityDisplayName(Entity entity) {
            if (entity == null) {
                return "Scene Root";
            }
            if (entity is EditorEntity editorEntity && !string.IsNullOrWhiteSpace(editorEntity.Name)) {
                return editorEntity.Name;
            }

            return entity.GetType().Name;
        }
    }
}

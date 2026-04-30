namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to choose a new parent for one scene entity.
    /// </summary>
    public class ReparentEntityDialog : EditorDialogBase {
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
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

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
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the apply button.
        /// </summary>
        static readonly int2 ApplyButtonSize = new int2(88, 22);

        /// <summary>
        /// Host entity for the target-entity label.
        /// </summary>
        readonly EditorEntity TargetHost;

        /// <summary>
        /// Text showing which entity will be reparented.
        /// </summary>
        readonly TextComponent TargetText;

        /// <summary>
        /// Embedded hierarchy picker used to choose the destination parent.
        /// </summary>
        readonly SceneHierarchyPickerView ParentHierarchyView;

        /// <summary>
        /// Host entity for validation or status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Validation or status text shown above the footer.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity ApplyButtonHost;

        /// <summary>
        /// Apply button component.
        /// </summary>
        readonly ButtonComponent ApplyButton;

        /// <summary>
        /// Alternative parent choices exposed to the session and tests.
        /// </summary>
        readonly List<Entity> AvailableParentEntitiesInternal;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

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
        public ReparentEntityDialog(FontAsset font) : base("ReparentEntityDialog", "Reparent", font, PanelWidth, PanelHeight, HeaderHeight) {
            AvailableParentEntitiesInternal = new List<Entity>(8);

            TargetHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(TargetHost);

            TargetText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            TargetHost.AddComponent(TargetText);

            ParentHierarchyView = new SceneHierarchyPickerView(DialogFont, LayerMask, DialogPanelOrder, DialogTextOrder);
            ParentHierarchyView.Entity.InternalEntity = true;
            ParentHierarchyView.ParentEntitySelected += HandleParentEntitySelected;
            DialogPanelRoot.AddChild(ParentHierarchyView.Entity);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            ApplyButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(ApplyButtonHost);

            ApplyButton = new ButtonComponent("Apply", ApplyButtonSize, DialogFont, HandleApplyClicked, 0f);
            ApplyButtonHost.AddComponent(ApplyButton);
            ApplyButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets the entity currently being reparented.
        /// </summary>
        public Entity TargetEntity { get; private set; }

        /// <summary>
        /// Gets the visible scene entities available to the hierarchy picker.
        /// </summary>
        public IReadOnlyList<Entity> AvailableParentEntities => AvailableParentEntitiesInternal;

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

            ResetDialogPositioning();
            TargetEntity = targetEntity;
            SelectedParentEntity = targetEntity.Parent;
            StatusText.Text = string.Empty;
            TargetText.Text = GetEntityDisplayName(targetEntity);
            CopyAvailableParentEntities(parentEntities);
            ParentHierarchyView.Show(targetEntity, parentEntities, SelectedParentEntity);
            Enabled = true;
        }

        /// <summary>
        /// Hides the dialog and clears its input blocker and transient state.
        /// </summary>
        public void Hide() {
            ParentHierarchyView.Hide();
            ClearDialogBackdrop();
            ResetDialogPositioning();
            StatusText.Text = string.Empty;
            Enabled = false;
            TargetEntity = null;
            SelectedParentEntity = null;
            AvailableParentEntitiesInternal.Clear();
        }

        /// <summary>
        /// Shows one validation or reparent error in the dialog.
        /// </summary>
        /// <param name="message">Message to display.</param>
        public void ShowError(string message) {
            StatusText.Text = message ?? string.Empty;
        }

        /// <summary>
        /// Updates dialog layout to fit the current host window size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }
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
            AvailableParentEntitiesInternal.Clear();
            for (int entityIndex = 0; entityIndex < parentEntities.Count; entityIndex++) {
                AvailableParentEntitiesInternal.Add(parentEntities[entityIndex]);
            }
        }

        /// <summary>
        /// Updates the selected parent entity after one valid hierarchy-row selection.
        /// </summary>
        /// <param name="entity">Selected parent entity, or null for the scene root.</param>
        void HandleParentEntitySelected(Entity entity) {
            SelectedParentEntity = entity;
            StatusText.Text = string.Empty;
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
        /// Lays out the target-entity label.
        /// </summary>
        void LayoutTarget() {
            int y = PanelPadding + HeaderHeight + SectionSpacing;
            TargetHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = DialogFont.MeasureTight(TargetText.Text);
            TargetText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, DialogFont.LineHeight))));
        }

        /// <summary>
        /// Lays out the embedded hierarchy picker.
        /// </summary>
        void LayoutParentHierarchy() {
            int y = PanelPadding + HeaderHeight + SectionSpacing + GetDialogLineHeight() + SectionSpacing;
            ParentHierarchyView.Entity.Position = new float3(PanelPadding, y, 0.2f);
            ParentHierarchyView.UpdateLayout(PanelWidth - (PanelPadding * 2), HierarchyHeight);
        }

        /// <summary>
        /// Lays out the validation or status text row.
        /// </summary>
        void LayoutStatus() {
            int y = PanelHeight - PanelPadding - FooterHeight - SectionSpacing - GetDialogLineHeight();
            StatusHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = DialogFont.MeasureTight(StatusText.Text);
            StatusText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, DialogFont.LineHeight))));
        }

        /// <summary>
        /// Lays out the footer buttons.
        /// </summary>
        void LayoutFooter() {
            int buttonY = PanelHeight - PanelPadding - CancelButtonSize.Y;
            int cancelX = PanelWidth - PanelPadding - CancelButtonSize.X;
            int applyX = cancelX - 8 - ApplyButtonSize.X;

            CancelButtonHost.Position = new float3(cancelX, buttonY, 0.2f);
            ApplyButtonHost.Position = new float3(applyX, buttonY, 0.2f);
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

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}

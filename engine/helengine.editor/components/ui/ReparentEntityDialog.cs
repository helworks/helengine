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
        /// Corner radius applied to the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness applied to the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Padding used inside the title bar for text and buttons.
        /// </summary>
        const int HeaderPadding = 8;
        /// <summary>
        /// Spacing used between the title text and the close button.
        /// </summary>
        const int HeaderButtonSpacing = 8;
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the apply button.
        /// </summary>
        static readonly int2 ApplyButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the header close button.
        /// </summary>
        static readonly int2 CloseButtonSize = new int2(40, HeaderHeight);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the panel content.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Dialog background shape.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Root entity for the draggable title bar.
        /// </summary>
        readonly EditorEntity HeaderRoot;
        /// <summary>
        /// Background sprite rendered behind the title bar.
        /// </summary>
        readonly SpriteComponent HeaderBackground;
        /// <summary>
        /// Interactable region used to drag the dialog from its title bar.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;
        /// <summary>
        /// Host entity for the dialog title text.
        /// </summary>
        readonly EditorEntity TitleHost;
        /// <summary>
        /// Dialog title text shown in the title bar.
        /// </summary>
        readonly TextComponent TitleText;
        /// <summary>
        /// Host entity for the title-bar close button.
        /// </summary>
        readonly EditorEntity CloseButtonHost;
        /// <summary>
        /// Button used to cancel and close the dialog.
        /// </summary>
        readonly ButtonComponent CloseButton;
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
        /// Render order used for panel surfaces.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for foreground text and controls.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached host size used to clamp manual dialog movement.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Tracks whether the user has manually moved the dialog.
        /// </summary>
        bool IsUserPositioned;
        /// <summary>
        /// Tracks whether the title bar is currently being dragged.
        /// </summary>
        bool IsDragging;
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
        public ReparentEntityDialog(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            AvailableParentEntitiesInternal = new List<Entity>(8);

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "ReparentEntityDialog";

            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            PanelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(PanelWidth, PanelHeight)
            };
            PanelRoot.AddComponent(PanelBackground);

            HeaderRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(HeaderRoot);

            HeaderBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = PanelOrder,
                Size = new int2(0, 0)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            HeaderInteractable.CursorEvent += HandleHeaderCursor;
            HeaderRoot.AddComponent(HeaderInteractable);

            TitleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            HeaderRoot.AddChild(TitleHost);

            TitleText = new TextComponent {
                Font = font,
                Text = "Reparent",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            TitleHost.AddComponent(TitleText);

            CloseButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            HeaderRoot.AddChild(CloseButtonHost);

            CloseButton = new ButtonComponent("X", CloseButtonSize, font, HandleCloseClicked, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);
            CloseButton.UseHoverOnlyBackground();
            CloseButton.UseSquareCorners();
            CloseButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);

            TargetHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(TargetHost);

            TargetText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            TargetHost.AddComponent(TargetText);

            ParentHierarchyView = new SceneHierarchyPickerView(font, LayerMask, PanelOrder, TextOrder);
            ParentHierarchyView.Entity.InternalEntity = true;
            ParentHierarchyView.ParentEntitySelected += HandleParentEntitySelected;
            PanelRoot.AddChild(ParentHierarchyView.Entity);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(TextOrder, TextOrder);

            ApplyButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(ApplyButtonHost);

            ApplyButton = new ButtonComponent("Apply", ApplyButtonSize, font, HandleApplyClicked, 0f);
            ApplyButtonHost.AddComponent(ApplyButton);
            ApplyButton.SetRenderOrders(TextOrder, TextOrder);

            Enabled = false;
            IsInitialized = true;
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

            IsDragging = false;
            IsUserPositioned = false;
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
            EditorInputCaptureService.ClearBlocker(this);
            IsDragging = false;
            IsUserPositioned = false;
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
            if (!Enabled) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            int safeWidth = Math.Max(1, windowWidth);
            int safeHeight = Math.Max(1, windowHeight);
            HostSize = new int2(safeWidth, safeHeight);
            if (!IsUserPositioned) {
                PanelPosition = new int2(
                    Math.Max(0, (safeWidth - PanelWidth) / 2),
                    Math.Max(0, (safeHeight - PanelHeight) / 2));
            }

            ClampPanelPosition();
            ApplyPanelPosition();
            EditorInputCaptureService.SetBlocker(this, PanelPosition, new int2(PanelWidth, PanelHeight));

            LayoutHeader();
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
        /// Raises one cancel request from the title-bar close button.
        /// </summary>
        void HandleCloseClicked() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Updates the title-bar placement within the dialog panel.
        /// </summary>
        void LayoutHeader() {
            int headerWidth = PanelWidth;
            HeaderRoot.Position = new float3(0f, 0f, 0.2f);
            HeaderBackground.Size = new int2(headerWidth, HeaderHeight);
            HeaderInteractable.Size = new int2(headerWidth, HeaderHeight);

            int closeButtonX = headerWidth - CloseButtonSize.X;
            CloseButtonHost.Position = new float3(closeButtonX, 0f, 0.2f);

            FontTightMetrics titleMetrics = Font.MeasureTight(TitleText.Text);
            float titleY = GetTextTopOffset(HeaderHeight, titleMetrics);
            TitleHost.Position = new float3(HeaderPadding, titleY, 0.2f);
            int textWidth = Math.Max(1, closeButtonX - HeaderPadding - HeaderButtonSpacing);
            TitleText.Size = new int2(textWidth, Math.Max(1, (int)Math.Ceiling(titleMetrics.Height)));
        }

        /// <summary>
        /// Lays out the target-entity label.
        /// </summary>
        void LayoutTarget() {
            int y = PanelPadding + HeaderHeight + SectionSpacing;
            TargetHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = Font.MeasureTight(TargetText.Text);
            TargetText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, Font.LineHeight))));
        }

        /// <summary>
        /// Lays out the embedded hierarchy picker.
        /// </summary>
        void LayoutParentHierarchy() {
            int y = PanelPadding + HeaderHeight + SectionSpacing + GetLineHeight() + SectionSpacing;
            ParentHierarchyView.Entity.Position = new float3(PanelPadding, y, 0.2f);
            ParentHierarchyView.UpdateLayout(PanelWidth - (PanelPadding * 2), HierarchyHeight);
        }

        /// <summary>
        /// Lays out the validation or status text row.
        /// </summary>
        void LayoutStatus() {
            int y = PanelHeight - PanelPadding - FooterHeight - SectionSpacing - GetLineHeight();
            StatusHost.Position = new float3(PanelPadding, y, 0.2f);

            FontTightMetrics metrics = Font.MeasureTight(StatusText.Text);
            StatusText.Size = new int2(
                Math.Max(1, PanelWidth - (PanelPadding * 2)),
                Math.Max(1, (int)Math.Ceiling(Math.Max(metrics.Height, Font.LineHeight))));
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
        /// Handles pointer interactions on the title bar to allow dragging the dialog window.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleHeaderCursor(int2 pos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Press:
                    if (IsPointerOverCloseButton(pos)) {
                        return;
                    }
                    IsDragging = true;
                    IsUserPositioned = true;
                    break;
                case PointerInteraction.Hover:
                    if (IsDragging) {
                        PanelPosition = new int2(PanelPosition.X + delta.X, PanelPosition.Y + delta.Y);
                        ClampPanelPosition();
                        ApplyPanelPosition();
                    }
                    break;
                case PointerInteraction.Release:
                case PointerInteraction.Leave:
                    IsDragging = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Determines whether the pointer is inside the close-button region.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <returns>True when the pointer overlaps the close button.</returns>
        bool IsPointerOverCloseButton(int2 pos) {
            int closeButtonX = PanelWidth - CloseButtonSize.X;
            return pos.X >= closeButtonX &&
                   pos.X <= closeButtonX + CloseButtonSize.X &&
                   pos.Y >= 0 &&
                   pos.Y <= CloseButtonSize.Y;
        }

        /// <summary>
        /// Applies the cached panel position to the dialog root entity.
        /// </summary>
        void ApplyPanelPosition() {
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
        }

        /// <summary>
        /// Clamps the cached panel position to the visible host area.
        /// </summary>
        void ClampPanelPosition() {
            int maxX = Math.Max(0, HostSize.X - PanelWidth);
            int maxY = Math.Max(0, HostSize.Y - PanelHeight);

            int clampedX = PanelPosition.X;
            if (clampedX < 0) {
                clampedX = 0;
            } else if (clampedX > maxX) {
                clampedX = maxX;
            }

            int clampedY = PanelPosition.Y;
            if (clampedY < 0) {
                clampedY = 0;
            } else if (clampedY > maxY) {
                clampedY = maxY;
            }

            PanelPosition = new int2(clampedX, clampedY);
        }

        /// <summary>
        /// Returns the line height used for layout calculations.
        /// </summary>
        /// <returns>Rounded line height for the configured font.</returns>
        int GetLineHeight() {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f)));
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
        /// Computes the vertical offset needed to center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the title bar.</param>
        /// <param name="metrics">Measured metrics for the title text.</param>
        /// <returns>Top offset that vertically centers the text.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round(containerHeight * 0.5 - metrics.Height * 0.5 - metrics.MinTop);
        }
    }
}

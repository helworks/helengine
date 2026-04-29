namespace helengine {
    /// <summary>
    /// Renders one reusable checkbox control with hover, press, and checked-state visuals.
    /// </summary>
    public class CheckBoxComponent : Component {
        /// <summary>
        /// Glyph rendered when the checkbox is checked.
        /// </summary>
        const string CheckMarkText = "X";

        /// <summary>
        /// Size of the checkbox in pixels.
        /// </summary>
        int2 SizeValue;
        /// <summary>
        /// Font used to render the check mark.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Tracks whether the checkbox is currently checked.
        /// </summary>
        bool IsCheckedValue;
        /// <summary>
        /// Tracks whether the pointer is currently hovering the checkbox.
        /// </summary>
        bool IsHovering;
        /// <summary>
        /// Tracks whether the checkbox is currently pressed.
        /// </summary>
        bool IsPressed;
        /// <summary>
        /// Background rectangle that renders the checkbox border and fill.
        /// </summary>
        RoundedRectComponent Background;
        /// <summary>
        /// Interactable region that receives pointer input for the checkbox.
        /// </summary>
        InteractableComponent Interactable;
        /// <summary>
        /// Child entity that owns the check-mark text.
        /// </summary>
        Entity CheckMarkEntity;
        /// <summary>
        /// Text component used to render the checked mark.
        /// </summary>
        TextComponent CheckMark;

        /// <summary>
        /// Raised when the checked state changes through user interaction.
        /// </summary>
        public event Action<CheckBoxComponent, bool> CheckedChanged;

        /// <summary>
        /// Gets or sets the size of the checkbox in pixels.
        /// </summary>
        public int2 Size {
            get {
                return SizeValue;
            }
            set {
                SizeValue = value;

                if (Background != null) {
                    Background.Size = value;
                }

                if (Interactable != null) {
                    Interactable.Size = value;
                }

                UpdateCheckMarkLayout();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is currently checked.
        /// </summary>
        public bool IsChecked {
            get {
                return IsCheckedValue;
            }
            set {
                SetCheckedState(value, false);
            }
        }

        /// <summary>
        /// Creates one checkbox with a fixed size, check-mark font, and initial checked state.
        /// </summary>
        /// <param name="size">Checkbox dimensions in pixels.</param>
        /// <param name="font">Font used to render the check mark.</param>
        /// <param name="isChecked">Initial checked state.</param>
        public CheckBoxComponent(int2 size, FontAsset font, bool isChecked = false) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            SizeValue = size;
            Font = font;
            IsCheckedValue = isChecked;
        }

        /// <summary>
        /// Creates the checkbox visuals and input region when the component is added to an entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            byte backgroundOrder = RenderOrder2D.PanelSurface;
            byte textOrder = RenderOrder2D.PanelForeground;

            Background = new RoundedRectComponent();
            Background.Size = SizeValue;
            Background.Radius = (float)(Math.Min(SizeValue.X, SizeValue.Y) * 0.15d);
            Background.BorderThickness = 2f;
            Background.RenderOrder2D = backgroundOrder;
            entity.AddComponent(Background);

            Interactable = new InteractableComponent();
            Interactable.Size = SizeValue;
            Interactable.HoverCursor = PointerCursorKind.Hand;
            Interactable.CursorEvent += HandleCursorEvent;
            entity.AddComponent(Interactable);

            CheckMarkEntity = new Entity();
            CheckMarkEntity.LayerMask = entity.LayerMask;
            CheckMarkEntity.Enabled = true;
            CheckMarkEntity.InitComponents();

            if (entity.Children == null) {
                entity.InitChildren();
            }

            entity.AddChild(CheckMarkEntity);

            CheckMark = new TextComponent();
            CheckMark.Font = Font;
            CheckMark.Color = ThemeManager.Colors.InputForegroundPrimary;
            CheckMark.RenderOrder2D = textOrder;
            CheckMarkEntity.AddComponent(CheckMark);

            UpdateCheckMarkLayout();
            UpdateVisualState();
        }

        /// <summary>
        /// Clears transient pointer state and keeps the check-mark child enabled in sync with the parent entity.
        /// </summary>
        /// <param name="newEnabled">New hierarchy-enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                IsHovering = false;
                IsPressed = false;
                UpdateVisualState();
            }

            if (CheckMarkEntity != null) {
                CheckMarkEntity.Enabled = newEnabled;
            }
        }

        /// <summary>
        /// Clears transient pointer state when the checkbox is removed from its owner.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            IsHovering = false;
            IsPressed = false;
        }

        /// <summary>
        /// Handles pointer hover, press, and release to update visuals and toggle the checkbox.
        /// </summary>
        /// <param name="relPos">Pointer position relative to the checkbox.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    if (!IsHovering) {
                        IsHovering = true;
                        UpdateVisualState();
                    }
                    break;

                case PointerInteraction.Press:
                    IsPressed = true;
                    UpdateVisualState();
                    break;

                case PointerInteraction.Release:
                    if (IsPressed && IsHovering) {
                        SetCheckedState(!IsCheckedValue, true);
                    }

                    IsPressed = false;
                    UpdateVisualState();
                    break;

                case PointerInteraction.Leave:
                    if (IsHovering || IsPressed) {
                        IsHovering = false;
                        IsPressed = false;
                        UpdateVisualState();
                    }
                    break;

                case PointerInteraction.None:
                    break;
            }
        }

        /// <summary>
        /// Applies one checked state and optionally raises the change event when the value changes.
        /// </summary>
        /// <param name="isChecked">Checked state to apply.</param>
        /// <param name="raiseEvent">True when a change event should be raised.</param>
        void SetCheckedState(bool isChecked, bool raiseEvent) {
            if (IsCheckedValue == isChecked) {
                UpdateVisualState();
                return;
            }

            IsCheckedValue = isChecked;
            UpdateVisualState();

            if (raiseEvent && CheckedChanged != null) {
                CheckedChanged(this, IsCheckedValue);
            }
        }

        /// <summary>
        /// Updates checkbox colors and check-mark visibility from the current interaction and checked state.
        /// </summary>
        void UpdateVisualState() {
            if (Background == null) {
                return;
            }

            Background.BorderColor = IsCheckedValue || IsHovering
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentTertiary;

            if (IsPressed) {
                Background.FillColor = ThemeManager.Colors.AccentTertiary;
            } else if (IsCheckedValue) {
                Background.FillColor = ThemeManager.Colors.AccentSecondary;
            } else if (IsHovering) {
                Background.FillColor = ThemeManager.Colors.SurfaceInput;
            } else {
                Background.FillColor = ThemeManager.Colors.SurfaceInput;
            }

            if (CheckMark != null) {
                CheckMark.Text = IsCheckedValue ? CheckMarkText : string.Empty;
            }
        }

        /// <summary>
        /// Centers the check-mark text within the checkbox bounds.
        /// </summary>
        void UpdateCheckMarkLayout() {
            if (CheckMarkEntity == null || CheckMark == null) {
                return;
            }

            var tight = Font.MeasureTight(CheckMarkText);
            double lineHeight = Math.Max((double)Font.LineHeight, 1d);
            double positionX = Math.Round((SizeValue.X - tight.Width) / 2d);
            double positionY = Math.Round((SizeValue.Y - lineHeight) / 2d);

            CheckMarkEntity.Position = new float3((float)positionX, (float)positionY, 0.1f);
            CheckMark.Size = new int2((int)Math.Ceiling(tight.Width), (int)Math.Ceiling(lineHeight));
        }
    }
}

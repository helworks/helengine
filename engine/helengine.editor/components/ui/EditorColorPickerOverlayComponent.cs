namespace helengine.editor;

/// <summary>
/// Shared modal-style color picker overlay that combines a hue wheel, a saturation-value triangle, a hex textbox, and a separate alpha slider.
/// </summary>
public sealed class EditorColorPickerOverlayComponent : UpdateComponent {
    /// <summary>
    /// Fixed overlay width in pixels.
    /// </summary>
    const int PanelWidth = 480;

    /// <summary>
    /// Fixed overlay height in pixels.
    /// </summary>
    const int PanelHeight = 336;

    /// <summary>
    /// Horizontal padding inside the overlay panel.
    /// </summary>
    const int PanelPadding = 16;

    /// <summary>
    /// Vertical spacing between stacked controls in the right panel.
    /// </summary>
    const int SectionSpacing = 12;

    /// <summary>
    /// Margin used when clamping the overlay inside the active window bounds.
    /// </summary>
    const int OverlayMargin = 4;

    /// <summary>
    /// Width of the hue wheel control in pixels.
    /// </summary>
    const int WheelSize = 224;

    /// <summary>
    /// Width of the triangle control in pixels.
    /// </summary>
    const int TriangleSize = 160;

    /// <summary>
    /// Width of the color preview square.
    /// </summary>
    const int PreviewSize = 72;

    /// <summary>
    /// Width of the hex textbox.
    /// </summary>
    const int HexTextboxWidth = 184;

    /// <summary>
    /// Width of the alpha slider track.
    /// </summary>
    const int AlphaSliderWidth = 132;

    /// <summary>
    /// Width reserved for the alpha slider value readout.
    /// </summary>
    const int AlphaValueWidth = 36;

    /// <summary>
    /// Width reserved for the alpha slider label.
    /// </summary>
    const int AlphaLabelWidth = 18;

    /// <summary>
    /// Height of each alpha row.
    /// </summary>
    const int AlphaRowHeight = 24;

    /// <summary>
    /// Width of the close button.
    /// </summary>
    const int CloseButtonWidth = 88;

    /// <summary>
    /// Height of the close button.
    /// </summary>
    const int CloseButtonHeight = 24;

    /// <summary>
    /// Height of the hex textbox.
    /// </summary>
    const int HexTextboxHeight = 24;

    /// <summary>
    /// Height of the overlay preview label rows.
    /// </summary>
    const int TextRowHeight = 18;

    /// <summary>
    /// Local X offset used to center the triangle inside the wheel.
    /// </summary>
    const int TriangleInset = (WheelSize - TriangleSize) / 2;

    /// <summary>
    /// The wheel control should align its top-left corner to this local offset.
    /// </summary>
    const int WheelLeft = PanelPadding;

    /// <summary>
    /// The wheel control should align its top-left corner to this local offset.
    /// </summary>
    const int WheelTop = PanelPadding;

    /// <summary>
    /// The triangle control should align its top-left corner to this local offset.
    /// </summary>
    const int TriangleLeft = PanelPadding + TriangleInset;

    /// <summary>
    /// The triangle control should align its top-left corner to this local offset.
    /// </summary>
    const int TriangleTop = PanelPadding + TriangleInset;

    /// <summary>
    /// The right-side content starts at this local X coordinate.
    /// </summary>
    const int SidePanelLeft = PanelPadding + WheelSize + PanelPadding;

    /// <summary>
    /// Height of the wheel and triangle area.
    /// </summary>
    const int PickerAreaHeight = WheelSize;

    /// <summary>
    /// Font used for the overlay text.
    /// </summary>
    readonly FontAsset Font;

    /// <summary>
    /// Layer mask applied to the overlay hierarchy.
    /// </summary>
    readonly ushort OverlayLayerMask;

    /// <summary>
    /// Owner key used when blocking input outside the picker.
    /// </summary>
    readonly object InputBlockerOwner;

    /// <summary>
    /// Host entity that owns the overlay component.
    /// </summary>
    EditorEntity OwnerEntity;

    /// <summary>
    /// Root entity for the overlay panel.
    /// </summary>
    EditorEntity OverlayRoot;

    /// <summary>
    /// Background panel used to keep the picker readable over editor content.
    /// </summary>
    RoundedRectComponent OverlayBackground;

    /// <summary>
    /// Host entity for the preview square.
    /// </summary>
    EditorEntity PreviewHost;

    /// <summary>
    /// Host entity for the hex textbox.
    /// </summary>
    EditorEntity HexTextboxHost;

    /// <summary>
    /// Host entity for the alpha slider label.
    /// </summary>
    EditorEntity AlphaLabelHost;

    /// <summary>
    /// Text component that renders the alpha slider label.
    /// </summary>
    TextComponent AlphaLabelText;

    /// <summary>
    /// Host entity for the alpha slider value text.
    /// </summary>
    EditorEntity AlphaValueHost;

    /// <summary>
    /// Text component that renders the alpha slider value.
    /// </summary>
    TextComponent AlphaValueText;

    /// <summary>
    /// Host entity for the close button.
    /// </summary>
    EditorEntity CloseButtonHost;

    /// <summary>
    /// Button used to dismiss the overlay.
    /// </summary>
    ButtonComponent CloseButton;

    /// <summary>
    /// Current color edited by the overlay.
    /// </summary>
    byte4 CurrentColor;

    /// <summary>
    /// Tracks whether the overlay hierarchy has been created.
    /// </summary>
    bool IsInitialized;

    /// <summary>
    /// Tracks whether the overlay is currently open.
    /// </summary>
    bool IsOpenValue;

    /// <summary>
    /// Prevents control synchronization from recursively re-entering color change handlers.
    /// </summary>
    bool IsSynchronizingState;

    /// <summary>
    /// Local X coordinate used to align the overlay with the swatch button.
    /// </summary>
    float AnchorX;

    /// <summary>
    /// Local Y coordinate used to place the overlay underneath the swatch button.
    /// </summary>
    float AnchorY;

    /// <summary>
    /// Height of the swatch button used to anchor the overlay.
    /// </summary>
    int AnchorHeight;

    /// <summary>
    /// Initializes a new color picker overlay.
    /// </summary>
    /// <param name="font">Font used for overlay labels and button text.</param>
    /// <param name="overlayLayerMask">Layer mask applied to overlay visuals.</param>
    public EditorColorPickerOverlayComponent(FontAsset font, ushort overlayLayerMask) {
        Font = font ?? throw new ArgumentNullException(nameof(font));
        OverlayLayerMask = overlayLayerMask;
        InputBlockerOwner = new object();
        CurrentColor = new byte4(255, 255, 255, 255);
    }

    /// <summary>
    /// Raised whenever the overlay publishes a new RGBA color.
    /// </summary>
    public event Action<byte4> ColorChanged;

    /// <summary>
    /// Raised after the overlay closes.
    /// </summary>
    public event Action Closed;

    /// <summary>
    /// Gets whether the overlay is currently visible.
    /// </summary>
    public bool IsOpen => IsOpenValue;

    /// <summary>
    /// Gets the hue wheel control.
    /// </summary>
    public EditorColorWheelControl HueWheelControl { get; private set; }

    /// <summary>
    /// Gets the saturation-value triangle control.
    /// </summary>
    public EditorColorTriangleControl SaturationValueTriangleControl { get; private set; }

    /// <summary>
    /// Gets the alpha slider control.
    /// </summary>
    public EditorSlider AlphaSliderControl { get; private set; }

    /// <summary>
    /// Gets the hex color textbox.
    /// </summary>
    public TextBoxComponent HexTextBoxControl { get; private set; }

    /// <summary>
    /// Gets the preview swatch.
    /// </summary>
    public RoundedRectComponent PreviewBackground { get; private set; }

    /// <summary>
    /// Gets the overlay root entity.
    /// </summary>
    public EditorEntity OverlayRootEntity => OverlayRoot;

    /// <summary>
    /// Gets or sets the color currently displayed by the overlay.
    /// </summary>
    byte4 CurrentDisplayedColor {
        get { return CurrentColor; }
        set { CurrentColor = value; }
    }

    /// <summary>
    /// Creates the overlay hierarchy when attached to a host entity.
    /// </summary>
    /// <param name="entity">Owning entity.</param>
    public override void ComponentAdded(Entity entity) {
        base.ComponentAdded(entity);

        if (IsInitialized) {
            return;
        }

        OwnerEntity = entity as EditorEntity;
        if (OwnerEntity == null) {
            throw new InvalidOperationException("Color picker overlay must be attached to an EditorEntity.");
        }

        CreateOverlayRoot();
        CreateColorControls();
        CreateRightPanelControls();
        CreateCloseButton();
        LayoutOverlay();
        OverlayRoot.Enabled = false;
        IsInitialized = true;
    }

    /// <summary>
    /// Clears input capture when the overlay component is removed.
    /// </summary>
    /// <param name="entity">Owning entity.</param>
    public override void ComponentRemoved(Entity entity) {
        base.ComponentRemoved(entity);
        ClearInputBlocker();
        IsOpenValue = false;
    }

    /// <summary>
    /// Polls dismissal input while the picker is visible.
    /// </summary>
    public override void Update() {
        if (!IsInitialized) {
            return;
        }

        if (!IsOpenValue) {
            ClearInputBlocker();
            return;
        }

        if (OwnerEntity == null || !OwnerEntity.Enabled) {
            Close();
            return;
        }

        UpdateInputBlocker();

        InputSystem input = Core.Instance.Input;
        if (input == null) {
            return;
        }

        if (input.WasKeyPressed(Keys.Escape)) {
            Close();
            return;
        }

        if (input.WasMouseLeftButtonPressed() || input.WasMouseRightButtonPressed()) {
            HandleOutsidePointerPressed(input.GetMousePosition());
        }
    }

    /// <summary>
    /// Repositions the overlay anchor below the swatch button that opened it.
    /// </summary>
    /// <param name="anchorX">Local X coordinate of the swatch button.</param>
    /// <param name="anchorY">Local Y coordinate of the swatch button.</param>
    /// <param name="anchorHeight">Height of the swatch button.</param>
    public void SetAnchorPosition(float anchorX, float anchorY, int anchorHeight) {
        AnchorX = anchorX;
        AnchorY = anchorY;
        AnchorHeight = anchorHeight;
        LayoutOverlay();
        UpdateInputBlocker();
    }

    /// <summary>
    /// Opens the picker and synchronizes the controls from the supplied color.
    /// </summary>
    /// <param name="color">Color that should be shown when the picker opens.</param>
    public void Open(byte4 color) {
        EnsureInitialized();
        SynchronizeFromColor(color);
        IsOpenValue = true;
        OverlayRoot.Enabled = true;
        LayoutOverlay();
        UpdateInputBlocker();
    }

    /// <summary>
    /// Closes the picker and clears any active input blocker.
    /// </summary>
    public void Close() {
        EnsureInitialized();
        if (!IsOpenValue) {
            return;
        }

        IsOpenValue = false;
        OverlayRoot.Enabled = false;
        ClearInputBlocker();
        if (Closed != null) {
            Closed();
        }
    }

    /// <summary>
    /// Updates the current color without raising a change event.
    /// </summary>
    /// <param name="color">Color to display.</param>
    public void SetColor(byte4 color) {
        SynchronizeFromColor(color);
    }

    /// <summary>
    /// Closes the picker when the pointer press lands outside the overlay bounds.
    /// </summary>
    /// <param name="screenPoint">Pointer position in window coordinates.</param>
    public void HandleOutsidePointerPressed(int2 screenPoint) {
        EnsureInitialized();
        if (!IsOpenValue) {
            return;
        }

        if (EditorInputCaptureService.IsPointerBlocked(screenPoint, owner => ReferenceEquals(owner, InputBlockerOwner))) {
            return;
        }

        if (ContainsOverlayPoint(screenPoint)) {
            return;
        }

        Close();
    }

    /// <summary>
    /// Handles hue changes from the wheel control.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    void HandleHueChanged(double hue) {
        if (IsSynchronizingState) {
            return;
        }

        SaturationValueTriangleControl.SetHue(hue);
        PublishColor(EditorColorUtils.HsvToRgb(
            HueWheelControl.Hue,
            SaturationValueTriangleControl.Saturation,
            SaturationValueTriangleControl.Value,
            (byte)Math.Clamp((int)Math.Round(AlphaSliderControl.Value, MidpointRounding.AwayFromZero), 0, 255)));
    }

    /// <summary>
    /// Handles saturation and value changes from the triangle control.
    /// </summary>
    /// <param name="saturation">Saturation from 0 to 1.</param>
    /// <param name="value">Value from 0 to 1.</param>
    void HandleTriangleSelectionChanged(double saturation, double value) {
        if (IsSynchronizingState) {
            return;
        }

        PublishColor(EditorColorUtils.HsvToRgb(
            HueWheelControl.Hue,
            SaturationValueTriangleControl.Saturation,
            SaturationValueTriangleControl.Value,
            (byte)Math.Clamp((int)Math.Round(AlphaSliderControl.Value, MidpointRounding.AwayFromZero), 0, 255)));
    }

    /// <summary>
    /// Handles alpha slider changes from the separate opacity control.
    /// </summary>
    /// <param name="value">Slider value from 0 to 255.</param>
    void HandleAlphaSliderChanged(double value) {
        if (IsSynchronizingState) {
            return;
        }

        PublishColor(EditorColorUtils.HsvToRgb(
            HueWheelControl.Hue,
            SaturationValueTriangleControl.Saturation,
            SaturationValueTriangleControl.Value,
            (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255)));
    }

    /// <summary>
    /// Handles live text edits from the hex textbox.
    /// </summary>
    /// <param name="textBox">Textbox whose contents changed.</param>
    void HandleHexTextChanged(TextBoxComponent textBox) {
        if (IsSynchronizingState) {
            return;
        }

        if (EditorColorUtils.TryParseHtmlColor(textBox.Text, out byte4 color)) {
            SynchronizeFromColor(color);
            if (ColorChanged != null) {
                ColorChanged(CurrentColor);
            }
        } else {
            textBox.SetInvalidState(true);
        }
    }

    /// <summary>
    /// Commits the textbox color when the user submits the field.
    /// </summary>
    /// <param name="textBox">Textbox whose value was submitted.</param>
    void HandleHexTextSubmitted(TextBoxComponent textBox) {
        if (IsSynchronizingState) {
            return;
        }

        if (!EditorColorUtils.TryParseHtmlColor(textBox.Text, out byte4 color)) {
            SynchronizeFromColor(CurrentColor);
            return;
        }

        SynchronizeFromColor(color);
        if (ColorChanged != null) {
            ColorChanged(CurrentColor);
        }
    }

    /// <summary>
    /// Publishes one new color and updates the visible hex textbox and preview swatch.
    /// </summary>
    /// <param name="color">Color to display and publish.</param>
    void PublishColor(byte4 color) {
        CurrentDisplayedColor = color;
        UpdateHexTextboxAndPreview(color);
        if (ColorChanged != null) {
            ColorChanged(CurrentColor);
        }
    }

    /// <summary>
    /// Synchronizes every control from one authored color value.
    /// </summary>
    /// <param name="color">Color that should be displayed.</param>
    void SynchronizeFromColor(byte4 color) {
        CurrentDisplayedColor = color;
        IsSynchronizingState = true;
        try {
            if (HueWheelControl != null) {
                HueWheelControl.SetColor(color);
            }

            if (SaturationValueTriangleControl != null) {
                SaturationValueTriangleControl.SetColor(color);
            }

            if (AlphaSliderControl != null) {
                AlphaSliderControl.SetValue(color.W);
            }

            if (HexTextBoxControl != null) {
                HexTextBoxControl.SetInvalidState(false);
            }

            UpdateHexTextboxAndPreview(color);
        } finally {
            IsSynchronizingState = false;
        }
    }

    /// <summary>
    /// Updates the hex textbox and preview swatch from the current color.
    /// </summary>
    /// <param name="color">Color to display.</param>
    void UpdateHexTextboxAndPreview(byte4 color) {
        if (HexTextBoxControl != null) {
            HexTextBoxControl.SetInvalidState(false);
            HexTextBoxControl.Text = EditorColorUtils.FormatHtmlColor(color);
        }

        if (PreviewBackground != null) {
            PreviewBackground.FillColor = color;
            PreviewBackground.BorderColor = EditorColorUtils.Mix(color, new byte4(0, 0, 0, color.W), 0.4);
        }

        if (AlphaValueText != null) {
            AlphaValueText.Text = color.W.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Creates the overlay root and its shared background surface.
    /// </summary>
    void CreateOverlayRoot() {
        OverlayRoot = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        OwnerEntity.AddChild(OverlayRoot);

        OverlayBackground = new RoundedRectComponent {
            Size = new int2(PanelWidth, PanelHeight),
            Radius = 8f,
            BorderThickness = 1f,
            FillColor = ThemeManager.Colors.SurfacePrimary,
            BorderColor = ThemeManager.Colors.SurfaceInput,
            RenderOrder2D = RenderOrder2D.ModalOverlayBackground
        };
        OverlayRoot.AddComponent(OverlayBackground);
    }

    /// <summary>
    /// Creates the wheel and triangle controls inside the picker area.
    /// </summary>
    void CreateColorControls() {
        HueWheelControl = new EditorColorWheelControl(OverlayLayerMask);
        HueWheelControl.HueChanged += HandleHueChanged;
        HueWheelControl.Position = new float3(WheelLeft, WheelTop, 0.2f);
        OverlayRoot.AddChild(HueWheelControl);

        SaturationValueTriangleControl = new EditorColorTriangleControl(OverlayLayerMask);
        SaturationValueTriangleControl.SelectionChanged += HandleTriangleSelectionChanged;
        SaturationValueTriangleControl.Position = new float3(TriangleLeft, TriangleTop, 0.25f);
        OverlayRoot.AddChild(SaturationValueTriangleControl);
    }

    /// <summary>
    /// Creates the preview, textbox, and alpha slider controls in the right panel.
    /// </summary>
    void CreateRightPanelControls() {
        CreatePreviewArea();
        CreateHexTextbox();
        CreateAlphaSliderRow();
    }

    /// <summary>
    /// Creates the preview area that mirrors the currently edited color.
    /// </summary>
    void CreatePreviewArea() {
        PreviewHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = new float3(SidePanelLeft, PanelPadding, 0.2f)
        };
        OverlayRoot.AddChild(PreviewHost);

        PreviewBackground = new RoundedRectComponent {
            Size = new int2(PreviewSize, PreviewSize),
            Radius = 6f,
            BorderThickness = 2f,
            FillColor = CurrentColor,
            BorderColor = ThemeManager.Colors.SurfaceInput,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        PreviewHost.AddComponent(PreviewBackground);
    }

    /// <summary>
    /// Creates the HTML hex textbox used for direct color entry.
    /// </summary>
    void CreateHexTextbox() {
        HexTextboxHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = new float3(SidePanelLeft, PanelPadding + PreviewSize + SectionSpacing, 0.2f)
        };
        OverlayRoot.AddChild(HexTextboxHost);

        HexTextBoxControl = new TextBoxComponent(new int2(HexTextboxWidth, HexTextboxHeight), Font);
        HexTextBoxControl.TextChanged += HandleHexTextChanged;
        HexTextBoxControl.Submitted += HandleHexTextSubmitted;
        HexTextBoxControl.SetRenderOrders(RenderOrder2D.ModalOverlayBackground, RenderOrder2D.ModalOverlayForeground);
        HexTextboxHost.AddComponent(HexTextBoxControl);
    }

    /// <summary>
    /// Creates the separate alpha slider row.
    /// </summary>
    void CreateAlphaSliderRow() {
        int rowTop = PanelPadding + PreviewSize + SectionSpacing + HexTextboxHeight + SectionSpacing;

        AlphaLabelHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = new float3(SidePanelLeft, rowTop, 0.2f)
        };
        OverlayRoot.AddChild(AlphaLabelHost);

        AlphaLabelText = new TextComponent {
            Font = Font,
            Text = "A",
            Color = ThemeManager.Colors.InputForegroundPrimary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        AlphaLabelHost.AddComponent(AlphaLabelText);

        AlphaSliderControl = new EditorSlider(0.0, 255.0, 255.0, EditorSliderScaleMode.Linear, AlphaSliderWidth, 16);
        AlphaSliderControl.ApplyLayerMask(OverlayLayerMask);
        AlphaSliderControl.SetRenderOrders(RenderOrder2D.ModalOverlayBackground, RenderOrder2D.ModalOverlayForeground);
        AlphaSliderControl.KeyboardStep = 1.0;
        AlphaSliderControl.ValueChanged += HandleAlphaSliderChanged;
        AlphaSliderControl.Position = new float3(SidePanelLeft + AlphaLabelWidth + 8, rowTop + 4, 0.2f);
        OverlayRoot.AddChild(AlphaSliderControl);

        AlphaValueHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = new float3(SidePanelLeft + AlphaLabelWidth + 8 + AlphaSliderWidth + 8, rowTop, 0.2f)
        };
        OverlayRoot.AddChild(AlphaValueHost);

        AlphaValueText = new TextComponent {
            Font = Font,
            Text = "255",
            Color = ThemeManager.Colors.InputForegroundSecondary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        AlphaValueHost.AddComponent(AlphaValueText);
    }

    /// <summary>
    /// Creates the close button that dismisses the overlay.
    /// </summary>
    void CreateCloseButton() {
        CloseButtonHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        OverlayRoot.AddChild(CloseButtonHost);

        CloseButton = new ButtonComponent("Close", new int2(CloseButtonWidth, CloseButtonHeight), Font, Close);
        CloseButton.UseSquareCorners();
        CloseButton.SetHoverCursor(PointerCursorKind.Hand);
        CloseButton.SetTextColor(ThemeManager.Colors.TextOnAccent);
        CloseButton.SetVisualPalette(
            ThemeManager.Colors.AccentSecondary,
            ThemeManager.Colors.AccentPrimary,
            ThemeManager.Colors.AccentTertiary,
            ThemeManager.Colors.AccentSecondary,
            ThemeManager.Colors.AccentTertiary,
            ThemeManager.Colors.AccentPrimary);
        CloseButton.SetRenderOrders(RenderOrder2D.ModalOverlayBackground, RenderOrder2D.ModalOverlayForeground);
        CloseButtonHost.AddComponent(CloseButton);
    }

    /// <summary>
    /// Positions the overlay panel and its child controls inside the active viewport.
    /// </summary>
    void LayoutOverlay() {
        if (OverlayRoot == null) {
            return;
        }

        int2 windowSize = Core.Instance != null && Core.Instance.RenderManager3D != null
            ? Core.Instance.RenderManager3D.MainWindowSize
            : int2.Zero;

        float panelLeft = AnchorX;
        float panelTop = AnchorY + AnchorHeight + OverlayMargin;

        if (windowSize.X > 0) {
            float minimumLeft = OverlayMargin;
            float maximumLeft = Math.Max(minimumLeft, windowSize.X - PanelWidth - OverlayMargin);
            panelLeft = Math.Clamp(panelLeft, minimumLeft, maximumLeft);
        }

        if (windowSize.Y > 0) {
            float minimumTop = OverlayMargin;
            float maximumTop = Math.Max(minimumTop, windowSize.Y - PanelHeight - OverlayMargin);
            if (panelTop > maximumTop) {
                panelTop = AnchorY - PanelHeight - OverlayMargin;
            }

            panelTop = Math.Clamp(panelTop, minimumTop, maximumTop);
        }

        OverlayRoot.Position = new float3(panelLeft, panelTop, 0.1f);

        if (HueWheelControl != null) {
            HueWheelControl.Position = new float3(WheelLeft, WheelTop, 0.2f);
        }

        if (SaturationValueTriangleControl != null) {
            SaturationValueTriangleControl.Position = new float3(TriangleLeft, TriangleTop, 0.25f);
        }

        if (PreviewHost != null) {
            PreviewHost.Position = new float3(SidePanelLeft, PanelPadding, 0.2f);
        }

        if (HexTextboxHost != null) {
            HexTextboxHost.Position = new float3(SidePanelLeft, PanelPadding + PreviewSize + SectionSpacing, 0.2f);
        }

        int alphaRowTop = PanelPadding + PreviewSize + SectionSpacing + HexTextboxHeight + SectionSpacing;
        if (AlphaLabelHost != null) {
            AlphaLabelHost.Position = new float3(SidePanelLeft, alphaRowTop, 0.2f);
        }

        if (AlphaSliderControl != null) {
            AlphaSliderControl.Position = new float3(SidePanelLeft + AlphaLabelWidth + 8, alphaRowTop + 4, 0.2f);
        }

        if (AlphaValueHost != null) {
            AlphaValueHost.Position = new float3(SidePanelLeft + AlphaLabelWidth + 8 + AlphaSliderWidth + 8, alphaRowTop, 0.2f);
        }

        if (CloseButtonHost != null) {
            CloseButtonHost.Position = new float3(PanelWidth - PanelPadding - CloseButtonWidth, PanelHeight - PanelPadding - CloseButtonHeight, 0.2f);
        }
    }

    /// <summary>
    /// Updates the input blocker to match the overlay panel.
    /// </summary>
    void UpdateInputBlocker() {
        if (!IsOpenValue) {
            EditorInputCaptureService.ClearBlocker(InputBlockerOwner);
            return;
        }

        if (OverlayRoot == null) {
            EditorInputCaptureService.ClearBlocker(InputBlockerOwner);
            return;
        }

        int2 overlayPosition = new int2(
            (int)Math.Round(OverlayRoot.Position.X),
            (int)Math.Round(OverlayRoot.Position.Y));
        EditorInputCaptureService.SetBlocker(InputBlockerOwner, overlayPosition, new int2(PanelWidth, PanelHeight));
    }

    /// <summary>
    /// Removes the active input blocker.
    /// </summary>
    void ClearInputBlocker() {
        EditorInputCaptureService.ClearBlocker(InputBlockerOwner);
    }

    /// <summary>
    /// Returns true when the supplied point lies inside the overlay panel.
    /// </summary>
    /// <param name="point">Screen-space point to evaluate.</param>
    /// <returns>True when the point lies inside the overlay bounds.</returns>
    bool ContainsOverlayPoint(int2 point) {
        if (OverlayRoot == null) {
            return false;
        }

        int left = (int)Math.Round(OverlayRoot.Position.X);
        int top = (int)Math.Round(OverlayRoot.Position.Y);
        return point.X >= left &&
               point.X < left + PanelWidth &&
               point.Y >= top &&
               point.Y < top + PanelHeight;
    }

    /// <summary>
    /// Ensures the overlay has been attached to one editor entity before it is used.
    /// </summary>
    void EnsureInitialized() {
        if (!IsInitialized || OwnerEntity == null || OverlayRoot == null) {
            throw new InvalidOperationException("Color picker overlay must be attached to an editor entity before use.");
        }
    }
}

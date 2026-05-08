namespace helengine.editor;

/// <summary>
/// Non-modal overlay that lets editor users adjust a color with live RGB sliders and preview feedback.
/// </summary>
public sealed class EditorColorPickerOverlayComponent : UpdateComponent {
    /// <summary>
    /// Fixed overlay width in pixels.
    /// </summary>
    const int PanelWidth = 304;

    /// <summary>
    /// Fixed overlay height in pixels.
    /// </summary>
    const int PanelHeight = 184;

    /// <summary>
    /// Horizontal panel padding in pixels.
    /// </summary>
    const int PanelPadding = 12;

    /// <summary>
    /// Vertical spacing between channel rows.
    /// </summary>
    const int RowSpacing = 6;

    /// <summary>
    /// Vertical spacing between the row section and the preview area.
    /// </summary>
    const int SectionSpacing = 10;

    /// <summary>
    /// Height of each color channel row.
    /// </summary>
    const int RowHeight = 24;

    /// <summary>
    /// Margin used when clamping the popup inside the active window bounds.
    /// </summary>
    const int OverlayMargin = 4;

    /// <summary>
    /// Height of each slider control.
    /// </summary>
    const int SliderHeight = 16;

    /// <summary>
    /// Width of each slider control.
    /// </summary>
    const int SliderWidth = 156;

    /// <summary>
    /// Width reserved for the numeric value text next to each slider.
    /// </summary>
    const int ValueWidth = 44;

    /// <summary>
    /// Width reserved for each channel label.
    /// </summary>
    const int LabelWidth = 18;

    /// <summary>
    /// Size of the live preview square.
    /// </summary>
    const int PreviewSize = 56;

    /// <summary>
    /// Height of the swatch button used to anchor the overlay.
    /// </summary>
    int AnchorHeight;

    /// <summary>
    /// Width of the close button.
    /// </summary>
    const int CloseButtonWidth = 88;

    /// <summary>
    /// Height of the close button.
    /// </summary>
    const int CloseButtonHeight = 24;

    /// <summary>
    /// Keyboard step used by the RGB sliders.
    /// </summary>
    const double ChannelKeyboardStep = 1.0;

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
    /// Root entity for the red channel label.
    /// </summary>
    EditorEntity RedLabelHost;

    /// <summary>
    /// Text component for the red channel label.
    /// </summary>
    TextComponent RedLabelText;

    /// <summary>
    /// Root entity for the green channel label.
    /// </summary>
    EditorEntity GreenLabelHost;

    /// <summary>
    /// Text component for the green channel label.
    /// </summary>
    TextComponent GreenLabelText;

    /// <summary>
    /// Root entity for the blue channel label.
    /// </summary>
    EditorEntity BlueLabelHost;

    /// <summary>
    /// Text component for the blue channel label.
    /// </summary>
    TextComponent BlueLabelText;

    /// <summary>
    /// Root entity for the red channel value readout.
    /// </summary>
    EditorEntity RedValueHost;

    /// <summary>
    /// Value text shown for the red channel slider.
    /// </summary>
    TextComponent RedValueText;

    /// <summary>
    /// Root entity for the green channel value readout.
    /// </summary>
    EditorEntity GreenValueHost;

    /// <summary>
    /// Value text shown for the green channel slider.
    /// </summary>
    TextComponent GreenValueText;

    /// <summary>
    /// Root entity for the blue channel value readout.
    /// </summary>
    EditorEntity BlueValueHost;

    /// <summary>
    /// Value text shown for the blue channel slider.
    /// </summary>
    TextComponent BlueValueText;

    /// <summary>
    /// Slider used to edit the red channel.
    /// </summary>
    EditorSlider RedSliderInternal;

    /// <summary>
    /// Slider used to edit the green channel.
    /// </summary>
    EditorSlider GreenSliderInternal;

    /// <summary>
    /// Slider used to edit the blue channel.
    /// </summary>
    EditorSlider BlueSliderInternal;

    /// <summary>
    /// Root entity that renders the color preview swatch.
    /// </summary>
    EditorEntity PreviewHost;

    /// <summary>
    /// Visible preview square that mirrors the current color.
    /// </summary>
    RoundedRectComponent PreviewBackground;

    /// <summary>
    /// Root entity for the close button.
    /// </summary>
    EditorEntity CloseButtonRoot;

    /// <summary>
    /// Close button used to dismiss the overlay.
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
    /// Prevents slider synchronization from recursively re-entering the color change handler.
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
    /// Raised whenever the overlay publishes a new RGB color.
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
    /// Gets the red channel slider.
    /// </summary>
    public EditorSlider RedSlider => RedSliderInternal;

    /// <summary>
    /// Gets the green channel slider.
    /// </summary>
    public EditorSlider GreenSlider => GreenSliderInternal;

    /// <summary>
    /// Gets the blue channel slider.
    /// </summary>
    public EditorSlider BlueSlider => BlueSliderInternal;

    /// <summary>
    /// Gets the overlay root entity.
    /// </summary>
    public EditorEntity OverlayRootEntity => OverlayRoot;

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
        CreateChannelRows();
        CreatePreviewArea();
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
        SetColor(color);
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
        CurrentColor = color;
        SynchronizeFromColor();
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
    /// Creates one RGB slider row for each editable color channel.
    /// </summary>
    void CreateChannelRows() {
        CreateChannelRow(0, "R", out RedLabelHost, out RedLabelText, out RedSliderInternal, out RedValueHost, out RedValueText);
        CreateChannelRow(1, "G", out GreenLabelHost, out GreenLabelText, out GreenSliderInternal, out GreenValueHost, out GreenValueText);
        CreateChannelRow(2, "B", out BlueLabelHost, out BlueLabelText, out BlueSliderInternal, out BlueValueHost, out BlueValueText);
    }

    /// <summary>
    /// Creates the preview area that mirrors the currently edited color.
    /// </summary>
    void CreatePreviewArea() {
        PreviewHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
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
    /// Creates the close button that dismisses the overlay.
    /// </summary>
    void CreateCloseButton() {
        CloseButtonRoot = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        OverlayRoot.AddChild(CloseButtonRoot);

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
        CloseButtonRoot.AddComponent(CloseButton);
    }

    /// <summary>
    /// Creates one channel row and wires its change handlers.
    /// </summary>
    /// <param name="channelIndex">Zero-based channel index.</param>
    /// <param name="channelLabel">Displayed channel label.</param>
    /// <param name="labelHost">Created label host entity.</param>
    /// <param name="labelText">Created label text component.</param>
    /// <param name="slider">Created slider component.</param>
    /// <param name="valueHost">Created value text host entity.</param>
    /// <param name="valueText">Created value text component.</param>
    void CreateChannelRow(
        int channelIndex,
        string channelLabel,
        out EditorEntity labelHost,
        out TextComponent labelText,
        out EditorSlider slider,
        out EditorEntity valueHost,
        out TextComponent valueText) {
        labelHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        OverlayRoot.AddChild(labelHost);

        labelText = new TextComponent {
            Font = Font,
            Text = channelLabel,
            Color = ThemeManager.Colors.InputForegroundPrimary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        labelHost.AddComponent(labelText);

        slider = new EditorSlider(0.0, 255.0, 0.0, EditorSliderScaleMode.Linear, SliderWidth, SliderHeight);
        slider.ApplyLayerMask(OverlayLayerMask);
        slider.SetRenderOrders(RenderOrder2D.ModalOverlayBackground, RenderOrder2D.ModalOverlayForeground);
        slider.KeyboardStep = ChannelKeyboardStep;
        slider.ValueChanged += value => HandleChannelSliderChanged(channelIndex, value);
        OverlayRoot.AddChild(slider);

        valueHost = new EditorEntity {
            LayerMask = OverlayLayerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        OverlayRoot.AddChild(valueHost);

        valueText = new TextComponent {
            Font = Font,
            Text = "0",
            Color = ThemeManager.Colors.InputForegroundSecondary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        valueHost.AddComponent(valueText);
    }

    /// <summary>
    /// Synchronizes all visible UI from the current color value.
    /// </summary>
    void SynchronizeFromColor() {
        if (!IsInitialized) {
            return;
        }

        IsSynchronizingState = true;
        try {
            if (RedSliderInternal != null) {
                RedSliderInternal.SetValue(CurrentColor.X);
            }
            if (GreenSliderInternal != null) {
                GreenSliderInternal.SetValue(CurrentColor.Y);
            }
            if (BlueSliderInternal != null) {
                BlueSliderInternal.SetValue(CurrentColor.Z);
            }

            if (RedValueText != null) {
                RedValueText.Text = CurrentColor.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (GreenValueText != null) {
                GreenValueText.Text = CurrentColor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (BlueValueText != null) {
                BlueValueText.Text = CurrentColor.Z.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (PreviewBackground != null) {
                PreviewBackground.FillColor = CurrentColor;
                PreviewBackground.BorderColor = EditorColorUtils.Mix(CurrentColor, new byte4(0, 0, 0, CurrentColor.W), 0.4);
            }
        } finally {
            IsSynchronizingState = false;
        }
    }

    /// <summary>
    /// Updates the current color from one slider change and publishes the result.
    /// </summary>
    /// <param name="channelIndex">Zero-based RGB channel index.</param>
    /// <param name="value">Slider value that was changed.</param>
    void HandleChannelSliderChanged(int channelIndex, double value) {
        if (IsSynchronizingState) {
            return;
        }

        byte channelValue = (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        if (channelIndex == 0) {
            CurrentColor = new byte4(channelValue, CurrentColor.Y, CurrentColor.Z, CurrentColor.W);
        } else if (channelIndex == 1) {
            CurrentColor = new byte4(CurrentColor.X, channelValue, CurrentColor.Z, CurrentColor.W);
        } else if (channelIndex == 2) {
            CurrentColor = new byte4(CurrentColor.X, CurrentColor.Y, channelValue, CurrentColor.W);
        } else {
            throw new InvalidOperationException("Color channel index is not supported.");
        }

        SynchronizeFromColor();
        if (ColorChanged != null) {
            ColorChanged(CurrentColor);
        }
    }

    /// <summary>
    /// Places the overlay panel, preview square, and control rows.
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

        LayoutChannelRow(RedLabelHost, RedLabelText, RedSliderInternal, RedValueHost, RedValueText, 0);
        LayoutChannelRow(GreenLabelHost, GreenLabelText, GreenSliderInternal, GreenValueHost, GreenValueText, RowHeight + RowSpacing);
        LayoutChannelRow(BlueLabelHost, BlueLabelText, BlueSliderInternal, BlueValueHost, BlueValueText, (RowHeight + RowSpacing) * 2);

        if (PreviewHost != null) {
            PreviewHost.Position = new float3(PanelWidth - PanelPadding - PreviewSize, PanelHeight - PanelPadding - PreviewSize, 0.2f);
        }

        if (CloseButtonRoot != null) {
            CloseButtonRoot.Position = new float3(PanelWidth - PanelPadding - CloseButtonWidth, 92f, 0.2f);
        }
    }

    /// <summary>
    /// Positions one channel row within the overlay panel.
    /// </summary>
    /// <param name="labelHost">Label host entity.</param>
    /// <param name="labelText">Label text component.</param>
    /// <param name="slider">Slider component.</param>
    /// <param name="valueHost">Value host entity.</param>
    /// <param name="valueText">Value text component.</param>
    /// <param name="top">Top offset in pixels relative to the overlay panel.</param>
    void LayoutChannelRow(
        EditorEntity labelHost,
        TextComponent labelText,
        EditorSlider slider,
        EditorEntity valueHost,
        TextComponent valueText,
        int top) {
        if (labelHost == null || labelText == null || slider == null || valueHost == null || valueText == null) {
            return;
        }

        int valueLeft = PanelPadding + LabelWidth + 8 + SliderWidth + 8;
        labelHost.Position = new float3(PanelPadding, top, 0.2f);
        labelText.Size = new int2(LabelWidth, RowHeight);

        slider.Position = new float3(PanelPadding + LabelWidth + 8, top + 4, 0.2f);

        valueHost.Position = new float3(valueLeft, top, 0.2f);
        valueText.Size = new int2(ValueWidth, RowHeight);
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

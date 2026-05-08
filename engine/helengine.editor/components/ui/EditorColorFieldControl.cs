namespace helengine.editor;

/// <summary>
/// Reusable color field that combines an HTML hex textbox with a clickable swatch and picker popup.
/// </summary>
public sealed class EditorColorFieldControl : EditorEntity {
    /// <summary>
    /// Width reserved for the swatch square.
    /// </summary>
    const int SwatchSize = 24;

    /// <summary>
    /// Spacing between the textbox and the swatch.
    /// </summary>
    const int ControlSpacing = 8;

    /// <summary>
    /// Default control height used by the material editor rows.
    /// </summary>
    const int ControlHeight = 24;

    /// <summary>
    /// Root entity that owns the textbox.
    /// </summary>
    readonly EditorEntity TextBoxHost;

    /// <summary>
    /// Root entity that owns the swatch button.
    /// </summary>
    readonly EditorEntity SwatchHost;

    /// <summary>
    /// Tracks whether the control is synchronizing internal widgets from a programmatic color update.
    /// </summary>
    bool IsSynchronizingState;

    /// <summary>
    /// Cached size used for the textbox and swatch layout.
    /// </summary>
    int2 SizeValue;

    /// <summary>
    /// Initializes a new color field control.
    /// </summary>
    /// <param name="font">Font used by the textbox.</param>
    /// <param name="layerMask">Layer mask applied to the control hierarchy.</param>
    public EditorColorFieldControl(FontAsset font, ushort layerMask) {
        if (font == null) {
            throw new ArgumentNullException(nameof(font));
        }

        LayerMask = layerMask;
        InternalEntity = true;
        Name = "Color Field";

        SizeValue = new int2(180, ControlHeight);

        TextBoxHost = new EditorEntity {
            LayerMask = layerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        AddChild(TextBoxHost);

        HexTextBoxControl = new TextBoxComponent(new int2(1, ControlHeight), font);
        HexTextBoxControl.TextChanged += HandleTextChanged;
        HexTextBoxControl.Submitted += HandleTextSubmitted;
        TextBoxHost.AddComponent(HexTextBoxControl);

        SwatchHost = new EditorEntity {
            LayerMask = layerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
        AddChild(SwatchHost);

        SwatchButtonControl = new ButtonComponent(string.Empty, new int2(SwatchSize, ControlHeight), font, HandleSwatchClicked);
        SwatchButtonControl.UseSquareCorners();
        SwatchButtonControl.SetHoverCursor(PointerCursorKind.Hand);
        SwatchButtonControl.SetTextColor(new byte4(255, 255, 255, 0));
        SwatchHost.AddComponent(SwatchButtonControl);

        Value = new byte4(255, 255, 255, 255);
        UpdateLayout();
        SetValue(Value);
    }

    /// <summary>
    /// Raised whenever the control publishes a new color value.
    /// </summary>
    public event Action<byte4> ColorChanged;

    /// <summary>
    /// Raised when the control commits the current color from the textbox.
    /// </summary>
    public event Action<byte4> Submitted;

    /// <summary>
    /// Gets the textbox used to edit the HTML color string.
    /// </summary>
    public TextBoxComponent HexTextBoxControl { get; }

    /// <summary>
    /// Gets the swatch button used to open the RGB picker.
    /// </summary>
    public ButtonComponent SwatchButtonControl { get; }

    /// <summary>
    /// Gets the current color value.
    /// </summary>
    public byte4 Value { get; private set; }

    /// <summary>
    /// Raised when the user clicks the swatch and requests the shared RGB picker.
    /// </summary>
    public event Action PickerRequested;

    /// <summary>
    /// Gets or sets the size of the control.
    /// </summary>
    public int2 Size {
        get { return SizeValue; }
        set {
            SizeValue = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// Updates the color shown by the control without raising a change event.
    /// </summary>
    /// <param name="value">Color that should be displayed.</param>
    public void SetValue(byte4 value) {
        ApplyColor(value, false);
    }

    /// <summary>
    /// Keeps the textbox and swatch laid out as one inline color field.
    /// </summary>
    void UpdateLayout() {
        int textWidth = Math.Max(0, SizeValue.X - SwatchSize - ControlSpacing);
        TextBoxHost.Position = float3.Zero;
        HexTextBoxControl.Size = new int2(textWidth, SizeValue.Y);

        SwatchHost.Position = new float3(textWidth + ControlSpacing, 0f, 0.1f);
        SwatchButtonControl.SetSize(new int2(SwatchSize, SizeValue.Y));
    }

    /// <summary>
    /// Applies one color value to the widget state and optionally publishes the change.
    /// </summary>
    /// <param name="value">Color value to apply.</param>
    /// <param name="raiseEvent">True when the update should be sent to listeners.</param>
    void ApplyColor(byte4 value, bool raiseEvent) {
        Value = value;

        IsSynchronizingState = true;
        try {
            if (HexTextBoxControl != null) {
                HexTextBoxControl.SetInvalidState(false);
                HexTextBoxControl.Text = EditorColorUtils.FormatHtmlColor(value);
            }

            UpdateSwatchVisuals();
        } finally {
            IsSynchronizingState = false;
        }

        if (raiseEvent && ColorChanged != null) {
            ColorChanged(Value);
        }
    }

    /// <summary>
    /// Updates the swatch colors to match the current value.
    /// </summary>
    void UpdateSwatchVisuals() {
        if (SwatchButtonControl == null) {
            return;
        }

        byte4 baseColor = Value;
        byte4 hoverColor = EditorColorUtils.Mix(baseColor, new byte4(255, 255, 255, baseColor.W), 0.12);
        byte4 pressedColor = EditorColorUtils.Mix(baseColor, new byte4(0, 0, 0, baseColor.W), 0.18);
        byte4 borderColor = EditorColorUtils.Mix(baseColor, new byte4(0, 0, 0, baseColor.W), 0.42);

        SwatchButtonControl.SetVisualPalette(
            baseColor,
            hoverColor,
            pressedColor,
            baseColor,
            borderColor,
            borderColor);
    }

    /// <summary>
    /// Handles textbox edits and publishes valid color changes.
    /// </summary>
    /// <param name="textBox">Textbox whose contents changed.</param>
    void HandleTextChanged(TextBoxComponent textBox) {
        if (IsSynchronizingState) {
            return;
        }

        if (EditorColorUtils.TryParseHtmlColor(textBox.Text, out byte4 color)) {
            ApplyColor(color, false);
            if (ColorChanged != null) {
                ColorChanged(Value);
            }
        } else {
            textBox.SetInvalidState(true);
        }
    }

    /// <summary>
    /// Commits the textbox color when the user submits the field.
    /// </summary>
    /// <param name="textBox">Textbox whose value was submitted.</param>
    void HandleTextSubmitted(TextBoxComponent textBox) {
        if (IsSynchronizingState) {
            return;
        }

        if (!EditorColorUtils.TryParseHtmlColor(textBox.Text, out byte4 color)) {
            ApplyColor(Value, false);
            return;
        }

        ApplyColor(color, false);
        if (Submitted != null) {
            Submitted(Value);
        }
    }

    /// <summary>
    /// Raises the picker request when the swatch button is clicked.
    /// </summary>
    void HandleSwatchClicked() {
        if (PickerRequested != null) {
            PickerRequested();
        }
    }
}

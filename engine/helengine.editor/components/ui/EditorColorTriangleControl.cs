namespace helengine.editor;

/// <summary>
/// Reusable saturation and value triangle control used by the shared editor color picker overlay.
/// </summary>
public sealed class EditorColorTriangleControl : EditorEntity {
    /// <summary>
    /// Square marker size used to show the selected point inside the triangle.
    /// </summary>
    const int MarkerSize = 12;

    /// <summary>
    /// Root entity that hosts the triangle sprite.
    /// </summary>
    readonly EditorEntity TriangleHost;

    /// <summary>
    /// Root entity that hosts the selection marker.
    /// </summary>
    readonly EditorEntity MarkerHost;

    /// <summary>
    /// Visible triangle sprite rendered from a procedurally generated texture.
    /// </summary>
    readonly SpriteComponent TriangleSprite;

    /// <summary>
    /// Marker used to visualize the selected saturation/value point.
    /// </summary>
    readonly RoundedRectComponent SelectionMarker;

    /// <summary>
    /// Pointer hit region that receives triangle dragging input.
    /// </summary>
    readonly InteractableComponent TriangleInteractable;

    /// <summary>
    /// Tracks whether the pointer is actively dragging inside the triangle.
    /// </summary>
    bool IsDragging;

    /// <summary>
    /// Cached hue angle in degrees used to tint the triangle.
    /// </summary>
    double HueValue;

    /// <summary>
    /// Cached saturation value from 0 to 1.
    /// </summary>
    double SaturationValue;

    /// <summary>
    /// Cached value value from 0 to 1.
    /// </summary>
    double ValueValue;

    /// <summary>
    /// Square control size used by the triangle texture and hit area.
    /// </summary>
    readonly int TriangleSizeValue;

    /// <summary>
    /// Initializes a new saturation/value triangle control.
    /// </summary>
    /// <param name="layerMask">Layer mask applied to the control hierarchy.</param>
    public EditorColorTriangleControl(ushort layerMask) : this(layerMask, 124) {
    }

    /// <summary>
    /// Initializes a new saturation/value triangle control with an explicit square size.
    /// </summary>
    /// <param name="layerMask">Layer mask applied to the control hierarchy.</param>
    /// <param name="triangleSize">Square control size used by the triangle texture and hit area.</param>
    public EditorColorTriangleControl(ushort layerMask, int triangleSize) {
        if (triangleSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(triangleSize), "Triangle size must be greater than zero.");
        }

        LayerMask = layerMask;
        InternalEntity = true;
        Name = "Color Triangle";
        TriangleSizeValue = triangleSize;

        TriangleHost = CreateChildHost(layerMask);
        AddChild(TriangleHost);

        TriangleSprite = new SpriteComponent {
            Size = new int2(TriangleSizeValue, TriangleSizeValue),
            Texture = EditorColorUtils.BuildTriangleTexture(TriangleSizeValue, HueValue),
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        TriangleHost.AddComponent(TriangleSprite);

        TriangleInteractable = new InteractableComponent {
            Size = new int2(TriangleSizeValue, TriangleSizeValue),
            HoverCursor = PointerCursorKind.Hand
        };
        TriangleInteractable.CursorEvent += HandleTriangleCursor;
        AddComponent(TriangleInteractable);

        MarkerHost = CreateChildHost(layerMask);
        AddChild(MarkerHost);

        SelectionMarker = new RoundedRectComponent {
            Size = new int2(MarkerSize, MarkerSize),
            Radius = MarkerSize * 0.5f,
            BorderThickness = 2f,
            FillColor = ThemeManager.Colors.TextOnAccent,
            BorderColor = ThemeManager.Colors.SurfacePrimary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        MarkerHost.AddComponent(SelectionMarker);

        UpdateMarker();
    }

    /// <summary>
    /// Raised whenever the selected saturation or value changes.
    /// </summary>
    public event Action<double, double> SelectionChanged;

    /// <summary>
    /// Gets the active interactable region used by the triangle.
    /// </summary>
    public InteractableComponent Interactable => TriangleInteractable;

    /// <summary>
    /// Gets the current hue in degrees.
    /// </summary>
    public double Hue => HueValue;

    /// <summary>
    /// Gets the current saturation from 0 to 1.
    /// </summary>
    public double Saturation => SaturationValue;

    /// <summary>
    /// Gets the current value from 0 to 1.
    /// </summary>
    public double Value => ValueValue;

    /// <summary>
    /// Gets the triangle size in pixels.
    /// </summary>
    public int2 Size => new int2(TriangleSizeValue, TriangleSizeValue);

    /// <summary>
    /// Updates the triangle hue and redraws the procedural texture.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    public void SetHue(double hue) {
        ApplyHue(hue);
    }

    /// <summary>
    /// Updates the selected saturation and value values.
    /// </summary>
    /// <param name="saturation">Saturation from 0 to 1.</param>
    /// <param name="value">Value from 0 to 1.</param>
    public void SetSelection(double saturation, double value) {
        ApplySelection(saturation, value, true);
    }

    /// <summary>
    /// Updates the triangle from one authored color value.
    /// </summary>
    /// <param name="color">Color whose hue, saturation, and value should be displayed.</param>
    public void SetColor(byte4 color) {
        EditorColorUtils.RgbToHsv(color, out double hue, out double saturation, out double value);
        ApplyHue(hue);
        ApplySelection(saturation, value, true);
    }

    /// <summary>
    /// Creates one internal child host entity with the supplied layer mask.
    /// </summary>
    /// <param name="layerMask">Layer mask applied to the child host.</param>
    /// <returns>Created internal child host.</returns>
    static EditorEntity CreateChildHost(ushort layerMask) {
        return new EditorEntity {
            LayerMask = layerMask,
            InternalEntity = true,
            Position = float3.Zero
        };
    }

    /// <summary>
    /// Applies one hue update and refreshes the triangle texture.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    void ApplyHue(double hue) {
        double normalizedHue = EditorColorUtils.NormalizeHue(hue);
        if (Math.Abs(normalizedHue - HueValue) <= 0.000001) {
            return;
        }

        HueValue = normalizedHue;
        TriangleSprite.Texture = EditorColorUtils.BuildTriangleTexture(TriangleSizeValue, HueValue);
        UpdateMarker();
    }

    /// <summary>
    /// Applies one selection update and optionally raises the public change event.
    /// </summary>
    /// <param name="saturation">Saturation from 0 to 1.</param>
    /// <param name="value">Value from 0 to 1.</param>
    /// <param name="raiseEvent">True when listeners should be notified.</param>
    void ApplySelection(double saturation, double value, bool raiseEvent) {
        double clampedSaturation = Math.Clamp(saturation, 0.0, 1.0);
        double clampedValue = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(clampedSaturation - SaturationValue) <= 0.000001 && Math.Abs(clampedValue - ValueValue) <= 0.000001) {
            UpdateMarker();
            return;
        }

        SaturationValue = clampedSaturation;
        ValueValue = clampedValue;
        UpdateMarker();

        if (raiseEvent && SelectionChanged != null) {
            SelectionChanged(SaturationValue, ValueValue);
        }
    }

    /// <summary>
    /// Updates the marker position so the selection remains visible inside the triangle.
    /// </summary>
    void UpdateMarker() {
        int2 selectionPoint = EditorColorUtils.ResolveTrianglePoint(SaturationValue, ValueValue, TriangleSizeValue);
        MarkerHost.Position = new float3(
            selectionPoint.X - (MarkerSize * 0.5f),
            selectionPoint.Y - (MarkerSize * 0.5f),
            0.3f);
    }

    /// <summary>
    /// Handles cursor interaction routed from the triangle's hit area.
    /// </summary>
    /// <param name="relPos">Pointer position relative to the triangle bounds.</param>
    /// <param name="delta">Pointer movement delta.</param>
    /// <param name="state">Pointer interaction state.</param>
    void HandleTriangleCursor(int2 relPos, int2 delta, PointerInteraction state) {
        if (state == PointerInteraction.Leave) {
            IsDragging = false;
            return;
        }

        if (state == PointerInteraction.Press) {
            if (!EditorColorUtils.TryResolveTriangleSelection(relPos, TriangleSizeValue, out double saturation, out double value)) {
                return;
            }

            IsDragging = true;
            ApplySelection(saturation, value, true);
            return;
        }

        if (state == PointerInteraction.Hover && IsDragging) {
            if (!EditorColorUtils.TryResolveTriangleSelection(relPos, TriangleSizeValue, out double saturation, out double value)) {
                return;
            }

            ApplySelection(saturation, value, true);
            return;
        }

        if (state == PointerInteraction.Release) {
            if (IsDragging && EditorColorUtils.TryResolveTriangleSelection(relPos, TriangleSizeValue, out double saturation, out double value)) {
                ApplySelection(saturation, value, true);
            }

            IsDragging = false;
        }
    }
}

namespace helengine.editor;

/// <summary>
/// Reusable hue wheel control used by the shared editor color picker overlay.
/// </summary>
public sealed class EditorColorWheelControl : EditorEntity {
    /// <summary>
    /// Square control size used by the wheel texture and hit area.
    /// </summary>
    const int WheelSize = 224;

    /// <summary>
    /// Square marker size used to show the selected hue on the ring.
    /// </summary>
    const int MarkerSize = 12;

    /// <summary>
    /// Root entity that hosts the wheel sprite.
    /// </summary>
    readonly EditorEntity WheelHost;

    /// <summary>
    /// Root entity that hosts the hue selection marker.
    /// </summary>
    readonly EditorEntity MarkerHost;

    /// <summary>
    /// Visible wheel sprite rendered from a procedurally generated texture.
    /// </summary>
    readonly SpriteComponent WheelSprite;

    /// <summary>
    /// Marker used to visualize the selected hue on the wheel ring.
    /// </summary>
    readonly RoundedRectComponent HueMarker;

    /// <summary>
    /// Pointer hit region that receives wheel dragging input.
    /// </summary>
    readonly InteractableComponent WheelInteractable;

    /// <summary>
    /// Tracks whether the pointer is actively dragging the wheel.
    /// </summary>
    bool IsDragging;

    /// <summary>
    /// Cached hue angle in degrees.
    /// </summary>
    double HueValue;

    /// <summary>
    /// Initializes a new hue wheel control.
    /// </summary>
    /// <param name="layerMask">Layer mask applied to the control hierarchy.</param>
    public EditorColorWheelControl(ushort layerMask) {
        LayerMask = layerMask;
        InternalEntity = true;
        Name = "Color Wheel";

        WheelHost = CreateChildHost(layerMask);
        AddChild(WheelHost);

        WheelSprite = new SpriteComponent {
            Size = new int2(WheelSize, WheelSize),
            Texture = EditorColorUtils.BuildHueWheelTexture(WheelSize),
            RenderOrder2D = RenderOrder2D.ModalOverlayBackground
        };
        WheelHost.AddComponent(WheelSprite);

        WheelInteractable = new InteractableComponent {
            Size = new int2(WheelSize, WheelSize),
            HoverCursor = PointerCursorKind.Hand
        };
        WheelInteractable.CursorEvent += HandleWheelCursor;
        AddComponent(WheelInteractable);

        MarkerHost = CreateChildHost(layerMask);
        AddChild(MarkerHost);

        HueMarker = new RoundedRectComponent {
            Size = new int2(MarkerSize, MarkerSize),
            Radius = MarkerSize * 0.5f,
            BorderThickness = 2f,
            FillColor = ThemeManager.Colors.TextOnAccent,
            BorderColor = ThemeManager.Colors.SurfacePrimary,
            RenderOrder2D = RenderOrder2D.ModalOverlayForeground
        };
        MarkerHost.AddComponent(HueMarker);

        UpdateMarker();
    }

    /// <summary>
    /// Raised whenever the selected hue changes.
    /// </summary>
    public event Action<double> HueChanged;

    /// <summary>
    /// Gets the active interactable region used by the wheel.
    /// </summary>
    public InteractableComponent Interactable => WheelInteractable;

    /// <summary>
    /// Gets the current hue in degrees.
    /// </summary>
    public double Hue => HueValue;

    /// <summary>
    /// Gets the wheel size in pixels.
    /// </summary>
    public int2 Size => new int2(WheelSize, WheelSize);

    /// <summary>
    /// Updates the visible hue without changing the drag state.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    public void SetHue(double hue) {
        ApplyHue(hue, true);
    }

    /// <summary>
    /// Updates the hue from an authored color value without changing the drag state.
    /// </summary>
    /// <param name="color">Color whose hue should be displayed.</param>
    public void SetColor(byte4 color) {
        EditorColorUtils.RgbToHsv(color, out double hue, out _, out _);
        ApplyHue(hue, true);
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
    /// Applies one hue change and optionally raises the public change event.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    /// <param name="raiseEvent">True when listeners should be notified.</param>
    void ApplyHue(double hue, bool raiseEvent) {
        double normalizedHue = EditorColorUtils.NormalizeHue(hue);
        if (Math.Abs(normalizedHue - HueValue) <= 0.000001) {
            UpdateMarker();
            return;
        }

        HueValue = normalizedHue;
        UpdateMarker();

        if (raiseEvent && HueChanged != null) {
            HueChanged(HueValue);
        }
    }

    /// <summary>
    /// Updates the hue marker position so the selection remains visible on the ring.
    /// </summary>
    void UpdateMarker() {
        double radius = (WheelSize - 1) / 2.0;
        double markerRadius = radius * 0.81;
        double angleRadians = HueValue * (Math.PI / 180.0);
        double center = radius;
        double markerX = center + (Math.Cos(angleRadians) * markerRadius);
        double markerY = center + (Math.Sin(angleRadians) * markerRadius);

        MarkerHost.Position = new float3(
            (float)Math.Round(markerX - (MarkerSize * 0.5), MidpointRounding.AwayFromZero),
            (float)Math.Round(markerY - (MarkerSize * 0.5), MidpointRounding.AwayFromZero),
            0.3f);
    }

    /// <summary>
    /// Handles cursor interaction routed from the wheel's hit area.
    /// </summary>
    /// <param name="relPos">Pointer position relative to the wheel bounds.</param>
    /// <param name="delta">Pointer movement delta.</param>
    /// <param name="state">Pointer interaction state.</param>
    void HandleWheelCursor(int2 relPos, int2 delta, PointerInteraction state) {
        if (state == PointerInteraction.Leave) {
            IsDragging = false;
            return;
        }

        if (state == PointerInteraction.Press) {
            if (!EditorColorUtils.IsPointInsideHueWheelRing(relPos, WheelSize)) {
                return;
            }

            IsDragging = true;
            ApplyHue(EditorColorUtils.ResolveHueFromWheelPoint(relPos, WheelSize), true);
            return;
        }

        if (state == PointerInteraction.Hover && IsDragging) {
            if (!EditorColorUtils.IsPointInsideHueWheelRing(relPos, WheelSize)) {
                return;
            }

            ApplyHue(EditorColorUtils.ResolveHueFromWheelPoint(relPos, WheelSize), true);
            return;
        }

        if (state == PointerInteraction.Release) {
            if (IsDragging && EditorColorUtils.IsPointInsideHueWheelRing(relPos, WheelSize)) {
                ApplyHue(EditorColorUtils.ResolveHueFromWheelPoint(relPos, WheelSize), true);
            }

            IsDragging = false;
        }
    }
}

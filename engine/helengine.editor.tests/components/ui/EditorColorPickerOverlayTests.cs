using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies the reusable color picker overlay exposes the GIMP-style picker controls and keeps them synchronized.
/// </summary>
public sealed class EditorColorPickerOverlayTests {
    /// <summary>
    /// Ensures opening the overlay creates the wheel, triangle, alpha slider, preview, and hex textbox.
    /// </summary>
    [Fact]
    public void Open_WhenPickerIsCreated_ShowsWheelTriangleAlphaAndHexFields() {
        InitializeCore();

        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddChild(overlay);

        overlay.Open(new byte4(32, 64, 96, 255));

        Assert.True(overlay.IsOpen);
        Assert.NotNull(overlay.HueWheelControl);
        Assert.NotNull(overlay.SaturationValueTriangleControl);
        Assert.NotNull(overlay.AlphaSliderControl);
        Assert.NotNull(overlay.HexTextBoxControl);
        Assert.NotNull(overlay.PreviewBackground);
        Assert.Equal("#204060", overlay.HexTextBoxControl.Text);
    }

    /// <summary>
    /// Ensures the overlay publishes hue changes back into the live HTML textbox.
    /// </summary>
    [Fact]
    public void HueWheel_WhenHueChanges_UpdatesTheHexTextbox() {
        InitializeCore();

        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddChild(overlay);
        overlay.Open(new byte4(255, 0, 0, 255));

        overlay.HueWheelControl.SetHue(120.0);

        Assert.Equal("#00ff00", overlay.HexTextBoxControl.Text);
    }

    /// <summary>
    /// Ensures the triangle control updates the visible preview and textbox when saturation or value changes.
    /// </summary>
    [Fact]
    public void Triangle_WhenSelectionChanges_UpdatesThePreviewAndTextbox() {
        InitializeCore();

        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddChild(overlay);
        overlay.Open(new byte4(255, 255, 255, 255));

        overlay.SaturationValueTriangleControl.SetSelection(1.0, 0.5);

        Assert.Equal("#800000", overlay.HexTextBoxControl.Text);
        Assert.Equal(new byte4(128, 0, 0, 255), overlay.PreviewBackground.FillColor);
    }

    /// <summary>
    /// Ensures the separate alpha slider updates only the alpha channel in the live textbox.
    /// </summary>
    [Fact]
    public void AlphaSlider_WhenChanged_UpdatesOnlyTheAlphaChannel() {
        InitializeCore();

        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddChild(overlay);
        overlay.Open(new byte4(32, 64, 96, 255));

        overlay.AlphaSliderControl.SetValue(128.0);

        Assert.Equal("#20406080", overlay.HexTextBoxControl.Text);
    }

    /// <summary>
    /// Ensures the saturation/value triangle fits fully inside the hue wheel center instead of overlapping the outer ring.
    /// </summary>
    [Fact]
    public void Open_WhenPickerIsCreated_KeepsTheTriangleInsideTheWheelInnerRadius() {
        InitializeCore();

        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddChild(overlay);
        overlay.Open(new byte4(255, 255, 255, 255));

        double wheelRadius = (overlay.HueWheelControl.Size.X - 1) / 2.0;
        double wheelInnerRadius = wheelRadius * 0.62;
        double wheelCenterX = overlay.HueWheelControl.Position.X + wheelRadius;
        double wheelCenterY = overlay.HueWheelControl.Position.Y + wheelRadius;

        double triangleLeft = overlay.SaturationValueTriangleControl.Position.X;
        double triangleTop = overlay.SaturationValueTriangleControl.Position.Y;
        double triangleSize = overlay.SaturationValueTriangleControl.Size.X;

        double[,] vertices = new double[,] {
            { triangleLeft + (triangleSize * 0.5), triangleTop + (triangleSize * 0.10) },
            { triangleLeft + (triangleSize * 0.12), triangleTop + (triangleSize * 0.88) },
            { triangleLeft + (triangleSize * 0.88), triangleTop + (triangleSize * 0.88) }
        };

        for (int index = 0; index < vertices.GetLength(0); index++) {
            double dx = vertices[index, 0] - wheelCenterX;
            double dy = vertices[index, 1] - wheelCenterY;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            Assert.True(
                distance <= wheelInnerRadius,
                $"Triangle vertex {index} should fit inside inner radius {wheelInnerRadius:F2}, but was {distance:F2}.");
        }
    }

    /// <summary>
    /// Initializes the core services required by color picker overlay tests.
    /// </summary>
    void InitializeCore() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
    }

    /// <summary>
    /// Creates a compact test font with the glyphs needed by the color picker textbox.
    /// </summary>
    /// <returns>Font asset with basic glyph coverage.</returns>
    FontAsset CreateFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
            ['#'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['c'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['f'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
        };

        return new FontAsset(
            new FontInfo("Test", 16, 4f),
            new TestRuntimeTexture {
                Width = 64,
                Height = 64
            },
            characters,
            16f,
            64,
            64);
    }
}

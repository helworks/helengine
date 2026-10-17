using helengine.baseplatform.Player;

namespace helengine.player.tests.input;

/// <summary>Verifies mouse hit coordinates follow the framebuffer magnified into the Play window.</summary>
public sealed class PlayerMouseCoordinateInputBackendTests {
    /// <summary>Tests integer scaling, non-square display pixels, and positions outside the window.</summary>
    [Theory]
    [InlineData(640, 480, 320, 240, 239, 119, 119, 59)]
    [InlineData(640, 480, 320, 240, -1, -1, -1, -1)]
    [InlineData(640, 480, 320, 240, 640, 480, 320, 240)]
    [InlineData(640, 448, 256, 224, 320, 224, 128, 112)]
    [InlineData(1280, 720, 1280, 720, 120, 80, 120, 80)]
    public void CaptureFrame_MapsToRenderPixelsAndPreservesButtonsAndScroll(
        int clientWidth, int clientHeight, int renderWidth, int renderHeight,
        int x, int y, int expectedX, int expectedY) {
        FakeWrappedInputBackend wrapped = new() {
            Frame = new InputFrameState {
                Mouse = new MouseState { X = x, Y = y, LeftButton = ButtonState.Pressed, ScrollWheelValue = 120 }
            }
        };
        PlayerMouseCoordinateInputBackend backend = new(wrapped, new PlayerRenderSize(renderWidth, renderHeight, 1d),
            () => new System.Drawing.Size(clientWidth, clientHeight));

        MouseState result = backend.CaptureFrame().Mouse;

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(ButtonState.Pressed, result.LeftButton);
        Assert.Equal(120, result.ScrollWheelValue);
    }

    /// <summary>Each frame uses the current client size so a DPI change does not leave stale hit coordinates.</summary>
    [Fact]
    public void CaptureFrame_UsesCurrentClientSizeAndForwardsBackgroundSetting() {
        FakeWrappedInputBackend wrapped = new() { Frame = new InputFrameState { Mouse = new MouseState { X = 200, Y = 100 } } };
        System.Drawing.Size size = new(640, 480);
        PlayerMouseCoordinateInputBackend backend = new(wrapped, new PlayerRenderSize(320, 240, 1d), () => size);
        backend.ReceiveInputInBackground = true;
        Assert.True(wrapped.ReceiveInputInBackground);
        Assert.Equal(100, backend.CaptureFrame().Mouse.X);

        size = new System.Drawing.Size(1280, 960);

        Assert.Equal(50, backend.CaptureFrame().Mouse.X);
        Assert.Equal(25, backend.CaptureFrame().Mouse.Y);
    }
}

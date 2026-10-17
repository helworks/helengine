using helengine.baseplatform.Player;

namespace helengine.player;

/// <summary>Converts window-client mouse positions to the render pixels used by Play scene hit testing.</summary>
public sealed class PlayerMouseCoordinateInputBackend : IInputBackend {
    /// <summary>Desktop input source; all input other than mouse coordinates passes through unchanged.</summary>
    readonly IInputBackend Wrapped;

    /// <summary>Fixed framebuffer dimensions selected by the platform adapter.</summary>
    readonly PlayerRenderSize RenderSize;

    /// <summary>Reads the current window-client size, including any DPI-induced size change.</summary>
    readonly Func<Size> ClientSizeProvider;

    /// <summary>Creates a mouse coordinate converter for one magnified Play framebuffer.</summary>
    /// <param name="wrapped">Desktop backend that reports client-pixel mouse positions.</param>
    /// <param name="renderSize">Framebuffer whose pixels the core uses for hit testing.</param>
    /// <param name="clientSizeProvider">Current size of the client area containing the whole framebuffer.</param>
    public PlayerMouseCoordinateInputBackend(IInputBackend wrapped, PlayerRenderSize renderSize, Func<Size> clientSizeProvider) {
        Wrapped = wrapped ?? throw new ArgumentNullException(nameof(wrapped));
        RenderSize = renderSize ?? throw new ArgumentNullException(nameof(renderSize));
        ClientSizeProvider = clientSizeProvider ?? throw new ArgumentNullException(nameof(clientSizeProvider));
    }

    /// <summary>Forwards the player's background-input policy to the desktop backend.</summary>
    public bool ReceiveInputInBackground {
        get => Wrapped.ReceiveInputInBackground;
        set => Wrapped.ReceiveInputInBackground = value;
    }

    /// <summary>
    /// Maps mouse positions independently on each axis, preserving non-square display scaling and positions outside
    /// the client area. Outside positions are not clamped onto edge controls. Buttons and scroll are unchanged.
    /// </summary>
    /// <returns>The input frame with mouse coordinates in framebuffer pixels.</returns>
    public InputFrameState CaptureFrame() {
        InputFrameState frame = Wrapped.CaptureFrame();
        Size clientSize = ClientSizeProvider();
        if (clientSize.Width > 0 && clientSize.Height > 0) {
            MouseState mouse = frame.Mouse;
            mouse.X = (int)Math.Floor(mouse.X * (double)RenderSize.Width / clientSize.Width);
            mouse.Y = (int)Math.Floor(mouse.Y * (double)RenderSize.Height / clientSize.Height);
            frame.Mouse = mouse;
        }
        return frame;
    }
}

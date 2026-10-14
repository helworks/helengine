namespace helengine.core.tests {
    /// <summary>
    /// Minimal render target that lets a camera render off-window, so pixel-aspect tests can check that render-target
    /// cameras always project with square pixels.
    /// </summary>
    public sealed class PixelAspectTestRenderTarget : RenderTarget {
    }
}

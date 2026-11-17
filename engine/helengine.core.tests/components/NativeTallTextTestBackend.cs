using helengine;
namespace helengine.core.tests.components {
    /// <summary>Represents a complete 80px line and an exactly clipped raster so tests can inspect the real control-to-command transform.</summary>
    public sealed class NativeTallTextTestBackend : ISystemTextBackend, ISystemTextLayout {
        /// <summary>Creates metrics backed by an engine-owned test texture.</summary>
        public NativeTallTextTestBackend(RuntimeTexture texture) { Texture = texture; }
        /// <summary>Gets the most recently requested complete-layout raster window.</summary>
        public float4 LastWindow { get; private set; }
        /// <summary>Gets the bounded complete input length.</summary>
        public int MaximumTextLength => 16384;
        /// <summary>Gets the engine-owned test texture.</summary>
        public RuntimeTexture Texture { get; }
        /// <summary>Gets exactly the requested visible raster intersection for the representative full line.</summary>
        public float4 RasterBounds => new float4(Math.Max(0f, (float)LastWindow.X), Math.Max(0f, (float)LastWindow.Y), Math.Min(40f, (float)LastWindow.Z), Math.Min(80f - Math.Max(0f, (float)LastWindow.Y), (float)LastWindow.W));
        /// <summary>Gets full typographic metrics, independent of the clipped raster.</summary>
        public float2 Size => new float2(40f, 80f);
        /// <summary>Gets the single complete line count.</summary>
        public int LineCount => 1;
        /// <summary>Gets the complete two-code-unit text length.</summary>
        public int TextLength => 2;
        /// <summary>Gets representative ascent.</summary>
        public float Ascent => 60f;
        /// <summary>Gets representative descent.</summary>
        public float Descent => 20f;
        /// <summary>Returns stable full-line caret metrics before any vertical viewport transform.</summary>
        public float4 GetCaret(int index) { return new float4(index * 20f, 0f, 80f, index * 20f); }
        /// <summary>Accepts the two ASCII code-unit boundaries in this deterministic layout.</summary>
        public bool IsCaretBoundary(int index) { return index >= 0 && index <= 2; }
        /// <summary>Maps a local hit to its nearest complete boundary.</summary>
        public int HitTest(float x, float y) { return Math.Clamp((int)Math.Round(x / 20f), 0, 2); }
        /// <summary>Returns complete metrics for the old unbounded request contract.</summary>
        public ISystemTextLayout Layout(string text, SystemTextStyle style, float scale, int width, bool wrap, TextAlignment alignment) { LastWindow = new float4(0f, 0f, 40f, 80f); return this; }
        /// <summary>Records the visible window without changing full caret or typographic metrics.</summary>
        public ISystemTextLayout LayoutClipped(string text, SystemTextStyle style, float scale, int width, bool wrap, TextAlignment alignment, float4 window) { LastWindow = window; return this; }
        /// <summary>Exposes the explicit invalidation seam without allocating another test layout.</summary>
        public void Invalidate() { }
    }
}

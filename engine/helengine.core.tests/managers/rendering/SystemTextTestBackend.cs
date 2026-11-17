using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Records generic shaping requests while returning stable, already cached test texture metrics.</summary>
    public sealed class SystemTextTestBackend : ISystemTextBackend {
        /// <summary>Stores the borrowed test layout.</summary>
        readonly ISystemTextLayout Result;
        /// <summary>Creates a test service bound to deterministic metrics.</summary>
        public SystemTextTestBackend(ISystemTextLayout result) { Result = result; }
        /// <summary>Gets the supported text bound used by the platform contract.</summary>
        public int MaximumTextLength => 16384;
        /// <summary>Gets how many command builds requested fresh borrowed metrics.</summary>
        public int Calls { get; private set; }
        /// <summary>Gets the last resolved drawable scale.</summary>
        public float LastScale { get; private set; }
        /// <summary>Gets the last wrap width.</summary>
        public int LastWidth { get; private set; }
        /// <summary>Gets whether the last request opted into bounded raster pixels.</summary>
        public bool LastWasClipped { get; private set; }
        /// <summary>Gets the exact complete-layout-local window supplied by the caller.</summary>
        public float4 LastWindow { get; private set; }
        /// <summary>Records one request without allocating another texture.</summary>
        public ISystemTextLayout Layout(string text, SystemTextStyle style, float pixelScale, int width, bool wrap, TextAlignment alignment) {
            Calls++;
            LastScale = pixelScale;
            LastWidth = width;
            LastWasClipped = false;
            return Result;
        }
        /// <summary>Records a windowed request through the same metrics path without changing the borrowed test result.</summary>
        public ISystemTextLayout LayoutClipped(string text, SystemTextStyle style, float pixelScale, int width, bool wrap, TextAlignment alignment, float4 rasterWindow) {
            ISystemTextLayout result = Layout(text, style, pixelScale, width, wrap, alignment);
            LastWasClipped = true;
            LastWindow = rasterWindow;
            return result;
        }
        /// <summary>Provides the explicit invalidation entry point used by the production host.</summary>
        public void Invalidate() { }
    }
}

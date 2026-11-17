using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Deterministic borrowed raster metrics for testing the existing generic command path independently of a platform shaper.</summary>
    public sealed class SystemTextTestLayout : ISystemTextLayout {
        /// <summary>Creates test metrics backed by one renderer-owned texture.</summary>
        public SystemTextTestLayout(RuntimeTexture texture) { Texture = texture; }
        /// <summary>Gets the texture shared by repeated test layout requests.</summary>
        public RuntimeTexture Texture { get; }
        /// <summary>Gets representative overhanging raster bounds.</summary>
        public float4 RasterBounds => new float4(-2f, -3f, 40f, 24f);
        /// <summary>Gets representative typographic metrics.</summary>
        public float2 Size => new float2(36f, 22f);
        /// <summary>Gets the single test line count.</summary>
        public int LineCount => 1;
        /// <summary>Gets the small test UTF-16 length.</summary>
        public int TextLength => 2;
        /// <summary>Gets the test ascent.</summary>
        public float Ascent => 17f;
        /// <summary>Gets the test descent.</summary>
        public float Descent => 5f;
        /// <summary>Returns simple valid caret metrics for bridge testing.</summary>
        public float4 GetCaret(int utf16Index) { return new float4(utf16Index * 18f, 0f, 22f, utf16Index * 18f); }
        /// <summary>Accepts the bounded test indices.</summary>
        public bool IsCaretBoundary(int utf16Index) { return utf16Index >= 0 && utf16Index <= 2; }
        /// <summary>Resolves the nearest test caret.</summary>
        public int HitTest(float x, float y) { return Math.Clamp((int)Math.Round(x / 18f), 0, 2); }
    }
}

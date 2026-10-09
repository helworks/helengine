namespace helengine {
    /// <summary>Distinguishes native GX2 sampling arithmetic from raw integer, depth and planar shader data.</summary>
    public enum WiiUNativeTextureNumericKind {
        /// <summary>Unsigned normalized components map their native range to zero through one.</summary>
        UnsignedNormalized,
        /// <summary>Signed normalized components retain negative native samples.</summary>
        SignedNormalized,
        /// <summary>RGB uses the sRGB transfer function while alpha remains linear.</summary>
        Srgb,
        /// <summary>Components use native binary floating-point representations.</summary>
        Float,
        /// <summary>Shader integer components require an explicit native shader contract.</summary>
        Integer,
        /// <summary>Depth and stencil data requires an explicit native shader contract.</summary>
        DepthStencil,
        /// <summary>NV12 planes require native shader interpretation rather than ordinary RGBA sampling.</summary>
        Planar
    }
}

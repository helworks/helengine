namespace helengine {
    /// <summary>Identifies supported two-dimensional GXM storage using actual SDK texture-type words.</summary>
    public enum VitaNativeTextureLayoutType : uint {
        /// <summary>Uses power-of-two dimensions and Morton addressing with Y in the low interleaved bit.</summary>
        Swizzled = 0,
        /// <summary>Uses scanlines aligned to eight texels.</summary>
        Linear = 0x60000000,
        /// <summary>Uses row-major thirty-two-by-thirty-two tiles.</summary>
        Tiled = 0x80000000,
        /// <summary>Retains arbitrary logical dimensions over power-of-two Morton storage.</summary>
        SwizzledArbitrary = 0xA0000000
    }
}

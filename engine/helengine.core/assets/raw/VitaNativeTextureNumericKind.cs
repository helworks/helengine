namespace helengine {
    /// <summary>Distinguishes native numeric interpretation from layout and image-cooking capability.</summary>
    public enum VitaNativeTextureNumericKind {
        /// <summary>Samples unsigned normalized components.</summary>
        UnsignedNormalized,
        /// <summary>Samples signed normalized components; previews clamp negative values.</summary>
        SignedNormalized,
        /// <summary>Samples floating-point components; previews clamp to zero through one.</summary>
        Float,
        /// <summary>Preserves shader integers without inventing normalized image conversion.</summary>
        Integer,
        /// <summary>Preserves native depth and stencil data for specialized shader use.</summary>
        DepthStencil,
        /// <summary>Contains native compressed color or normalized components.</summary>
        Compressed,
        /// <summary>Contains color-space converted YUV planes or pairs.</summary>
        Yuv,
        /// <summary>Preserves mixed channels requiring an explicit native shader contract.</summary>
        Mixed
    }
}

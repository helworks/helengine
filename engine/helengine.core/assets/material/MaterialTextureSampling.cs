namespace helengine {
    /// <summary>Sampling policies for authored material textures; separate from UI and shadow samplers.</summary>
    public enum MaterialTextureSampling {
        /// <summary>Preserves the backend's existing material sampling policy.</summary>
        Default = 0,
        /// <summary>Repeats in both axes with trilinear mip filtering and sixteen anisotropic samples.</summary>
        RepeatAnisotropic16 = 1,
        /// <summary>Clamps to the texture edges with trilinear mip filtering and sixteen anisotropic samples.</summary>
        ClampAnisotropic16 = 2
    }
}

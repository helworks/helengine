namespace helengine {
    /// <summary>
    /// Stores one generic platform-owned cooked material payload selected by a fixed-pipeline or otherwise platform-owned builder.
    /// </summary>
    public class PlatformMaterialAsset : Asset {
        /// <summary>
        /// Gets or sets the builder-owned renderer family identifier that should interpret this cooked payload.
        /// </summary>
        public string RendererFamilyId;

        /// <summary>
        /// Gets or sets the cooked runtime-relative texture path consumed by the active platform renderer.
        /// </summary>
        public string TextureRelativePath;

        /// <summary>
        /// Gets or sets whether the cooked material should render both winding directions.
        /// </summary>
        public bool DoubleSided;

        /// <summary>
        /// Gets or sets whether vertex color should modulate the final material color.
        /// </summary>
        public bool UseVertexColor;

        /// <summary>
        /// Gets or sets whether the material should use lighting during runtime shading.
        /// </summary>
        public bool Lit;

        /// <summary>
        /// Gets or sets the cooked base-color red channel.
        /// </summary>
        public byte BaseColorR;

        /// <summary>
        /// Gets or sets the cooked base-color green channel.
        /// </summary>
        public byte BaseColorG;

        /// <summary>
        /// Gets or sets the cooked base-color blue channel.
        /// </summary>
        public byte BaseColorB;

        /// <summary>
        /// Gets or sets the cooked base-color alpha channel.
        /// </summary>
        public byte BaseColorA;
    }
}

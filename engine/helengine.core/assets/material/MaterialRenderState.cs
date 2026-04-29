namespace helengine {
    /// <summary>
    /// Stores the fixed-function render-state choices that accompany a material.
    /// </summary>
    public class MaterialRenderState {
        /// <summary>
        /// Initializes a new material render-state instance using the engine defaults for opaque 3D drawing.
        /// </summary>
        public MaterialRenderState() {
            BlendMode = MaterialBlendMode.Opaque;
            CullMode = MaterialCullMode.Back;
            DepthTestEnabled = true;
            DepthWriteEnabled = true;
        }

        /// <summary>
        /// Gets or sets how the material output blends with the existing render target contents.
        /// </summary>
        public MaterialBlendMode BlendMode { get; set; }

        /// <summary>
        /// Gets or sets which triangle winding should be culled while rendering.
        /// </summary>
        public MaterialCullMode CullMode { get; set; }

        /// <summary>
        /// Gets or sets whether depth testing should be enabled for the material.
        /// </summary>
        public bool DepthTestEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether depth writes should update the depth buffer for the material.
        /// </summary>
        public bool DepthWriteEnabled { get; set; }

        /// <summary>
        /// Creates a copy of the render-state values so runtime materials do not share mutable asset instances.
        /// </summary>
        /// <returns>Copied render-state instance.</returns>
        public MaterialRenderState Clone() {
            return new MaterialRenderState {
                BlendMode = BlendMode,
                CullMode = CullMode,
                DepthTestEnabled = DepthTestEnabled,
                DepthWriteEnabled = DepthWriteEnabled
            };
        }
    }
}

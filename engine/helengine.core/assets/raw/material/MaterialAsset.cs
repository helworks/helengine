namespace helengine {
    /// <summary>
    /// Represents one generic authored material shell that stores cross-platform render-state values only.
    /// </summary>
    public class MaterialAsset : Asset {
        /// <summary>
        /// Initializes a new material asset with default render state and generic shadow flags.
        /// </summary>
        public MaterialAsset() {
            RenderState = new MaterialRenderState();
            CastsShadows = true;
            ReceivesShadows = true;
        }

        /// <summary>
        /// Gets or sets the fixed-function render state used while drawing the material.
        /// </summary>
        public MaterialRenderState RenderState;

        /// <summary>
        /// Gets or sets whether the material contributes geometry to shadow-map passes.
        /// </summary>
        public bool CastsShadows;

        /// <summary>
        /// Gets or sets whether the material receives shadow attenuation during lighting.
        /// </summary>
        public bool ReceivesShadows;
    }
}

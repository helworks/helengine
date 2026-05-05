namespace helengine {
    /// <summary>
    /// Identifies the lighting path selected by one cooked PS2 material payload.
    /// </summary>
    public enum Ps2MaterialLightingMode {
        /// <summary>
        /// Draws the material without per-vertex lighting.
        /// </summary>
        Unlit,

        /// <summary>
        /// Draws the material with the baseline PS2 vertex-lighting path.
        /// </summary>
        SimpleLit,

        /// <summary>
        /// Draws the material with the expensive showcase-only lighting path.
        /// </summary>
        ShowcaseLit
    }
}

namespace helengine {
    /// <summary>
    /// Identifies the coarse render bucket selected for one cooked PS2 material payload.
    /// </summary>
    public enum Ps2RenderClass {
        /// <summary>
        /// Routes the material through the opaque draw path.
        /// </summary>
        Opaque,

        /// <summary>
        /// Routes the material through the alpha-test draw path.
        /// </summary>
        AlphaTest,

        /// <summary>
        /// Routes the material through a transparent draw path.
        /// </summary>
        Transparent
    }
}

namespace helengine.baseplatform.Player {

    /// <summary>
    /// Describes how the desktop player must blend one material decoded from platform-cooked content.
    /// </summary>
    public enum PlayerMaterialRenderClass {
        /// <summary>
        /// The material writes depth and fully covers whatever is behind it.
        /// </summary>
        Opaque = 0,

        /// <summary>
        /// The material discards pixels below an alpha threshold but otherwise renders like an opaque surface.
        /// </summary>
        AlphaTest = 1,

        /// <summary>
        /// The material blends with what is behind it and is drawn after the opaque and alpha-tested passes.
        /// </summary>
        Transparent = 2
    }
}

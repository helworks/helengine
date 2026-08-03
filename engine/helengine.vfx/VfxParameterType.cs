namespace helengine.vfx {
    /// <summary>
    /// Value shape an effect parameter accepts, used to describe the parameter in CLI help output.
    /// </summary>
    public enum VfxParameterType {
        /// <summary>
        /// A single scalar number.
        /// </summary>
        Float,

        /// <summary>
        /// A discrete choice, written either as an integer or as the name of an enumeration member.
        /// </summary>
        Int,

        /// <summary>
        /// A color written as three comma-separated components, R,G,B.
        /// </summary>
        Color
    }
}

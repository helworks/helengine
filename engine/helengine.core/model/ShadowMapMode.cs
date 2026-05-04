namespace helengine {
    /// <summary>
    /// Describes how one authored light or camera should participate in shadow-map planning.
    /// </summary>
    public enum ShadowMapMode : byte {
        /// <summary>
        /// Disables shadow-map participation for the authored object.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Lets the active backend decide whether the authored object should receive shadow resources.
        /// </summary>
        Auto = 1,

        /// <summary>
        /// Requests that the authored object stays shadow-enabled whenever the backend can support it.
        /// </summary>
        Forced = 2
    }
}

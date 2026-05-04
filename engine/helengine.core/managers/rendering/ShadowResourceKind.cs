namespace helengine {
    /// <summary>
    /// Describes the shadow-resource family selected for one planned light shadow.
    /// </summary>
    public enum ShadowResourceKind {
        /// <summary>
        /// Stores shadow depth in one shared atlas allocation.
        /// </summary>
        Atlas,
        /// <summary>
        /// Stores shadow depth in one cube-style point-light shadow resource.
        /// </summary>
        Cube
    }
}

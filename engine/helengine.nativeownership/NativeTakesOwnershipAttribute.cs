namespace helengine {
    /// <summary>
    /// Declares that a generated native method assumes cleanup responsibility for one argument when the call begins, including exceptional exits.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NativeTakesOwnershipAttribute : Attribute {
        /// <summary>
        /// Initializes compile-time ownership metadata for one ownership-transferring parameter.
        /// </summary>
        public NativeTakesOwnershipAttribute() {
        }
    }
}

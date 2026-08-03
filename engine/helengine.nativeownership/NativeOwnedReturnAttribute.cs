namespace helengine {
    /// <summary>
    /// Declares that generated native callers assume cleanup responsibility for each non-null returned reference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NativeOwnedReturnAttribute : Attribute {
        /// <summary>
        /// Initializes the compile-time ownership-transfer marker used by native code generation.
        /// </summary>
        public NativeOwnedReturnAttribute() {
        }
    }
}

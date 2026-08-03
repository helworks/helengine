namespace helengine {
    /// <summary>
    /// Declares that generated native callers borrow a returned reference whose lifetime remains owned by shared engine state.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NativeBorrowedReturnAttribute : Attribute {
        /// <summary>
        /// Initializes the compile-time ownership marker used by native code generation.
        /// </summary>
        public NativeBorrowedReturnAttribute() {
        }
    }
}

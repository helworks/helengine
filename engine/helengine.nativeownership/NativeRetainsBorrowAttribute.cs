namespace helengine {
    /// <summary>
    /// Declares that a generated native callee retains a non-owning reference while cleanup responsibility remains with the caller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class NativeRetainsBorrowAttribute : Attribute {
        /// <summary>
        /// Initializes the compile-time retained-borrow contract.
        /// </summary>
        public NativeRetainsBorrowAttribute() {
        }
    }
}

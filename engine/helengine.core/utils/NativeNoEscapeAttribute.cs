namespace helengine {
    /// <summary>
    /// Marks a generated-native method parameter as a borrowed reference that is not retained beyond the call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NativeNoEscapeAttribute : Attribute {
        /// <summary>
        /// Initializes the marker used by native code generation to preserve caller-owned cleanup for a parameter.
        /// </summary>
        public NativeNoEscapeAttribute() {
        }
    }
}

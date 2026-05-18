namespace helengine {
    /// <summary>
    /// Declares that one source-level method should lower to one native free-function call during generated C++ emission.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class NativeFreeFunctionAttribute : Attribute {
        /// <summary>
        /// Gets the native free-function name that generated C++ should call.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Gets the native include path that declares the free function.
        /// </summary>
        public string IncludePath { get; }

        /// <summary>
        /// Initializes one native free-function lowering contract.
        /// </summary>
        /// <param name="functionName">Native free-function name that generated C++ should call.</param>
        /// <param name="includePath">Native include path that declares the free function.</param>
        public NativeFreeFunctionAttribute(string functionName, string includePath) {
            if (string.IsNullOrWhiteSpace(functionName)) {
                throw new ArgumentException("Native free-function name must be provided.", nameof(functionName));
            }
            if (string.IsNullOrWhiteSpace(includePath)) {
                throw new ArgumentException("Native include path must be provided.", nameof(includePath));
            }

            FunctionName = functionName;
            IncludePath = includePath;
        }
    }
}

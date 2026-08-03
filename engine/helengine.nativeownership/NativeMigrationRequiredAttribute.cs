namespace helengine {
    /// <summary>
    /// Marks one managed implementation whose behavior must be mirrored into a separate native implementation when the managed source changes.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Method |
        AttributeTargets.Constructor |
        AttributeTargets.Property,
        AllowMultiple = true,
        Inherited = false)]
    public sealed class NativeMigrationRequiredAttribute : Attribute {
        /// <summary>
        /// Initializes one native-migration marker.
        /// </summary>
        /// <param name="targetId">Stable native implementation target identifier that should stay synchronized with the annotated managed code.</param>
        /// <param name="reason">Human-readable explanation of the native migration requirement.</param>
        public NativeMigrationRequiredAttribute(string targetId, string reason) {
            if (string.IsNullOrWhiteSpace(targetId)) {
                throw new ArgumentException("Native migration target id must be provided.", nameof(targetId));
            } else if (string.IsNullOrWhiteSpace(reason)) {
                throw new ArgumentException("Native migration reason must be provided.", nameof(reason));
            }

            TargetId = targetId;
            Reason = reason;
        }

        /// <summary>
        /// Gets the stable native implementation target identifier that should stay synchronized with the annotated managed code.
        /// </summary>
        public string TargetId { get; }

        /// <summary>
        /// Gets the human-readable explanation of the native migration requirement.
        /// </summary>
        public string Reason { get; }
    }
}

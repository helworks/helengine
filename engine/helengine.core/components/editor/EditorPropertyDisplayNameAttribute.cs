namespace helengine {
    /// <summary>
    /// Provides the display label used by the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyDisplayNameAttribute : Attribute {
        /// <summary>
        /// Initializes a new display-name attribute.
        /// </summary>
        /// <param name="displayName">Display label shown in the editor.</param>
        public EditorPropertyDisplayNameAttribute(string displayName) {
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }

            DisplayName = displayName;
        }

        /// <summary>
        /// Gets the display label shown in the editor.
        /// </summary>
        public string DisplayName { get; }
    }
}

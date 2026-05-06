namespace helengine {
    /// <summary>
    /// Controls the display order used by the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyOrderAttribute : Attribute {
        /// <summary>
        /// Initializes a new order attribute.
        /// </summary>
        /// <param name="order">Order value used when sorting editor rows.</param>
        public EditorPropertyOrderAttribute(int order) {
            Order = order;
        }

        /// <summary>
        /// Gets the sort order used when rendering the property.
        /// </summary>
        public int Order { get; }
    }
}

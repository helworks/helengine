namespace helengine {
    /// <summary>
    /// Hides one component property from the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyHiddenAttribute : Attribute {
    }
}

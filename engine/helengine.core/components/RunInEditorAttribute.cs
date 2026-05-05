namespace helengine {
    /// <summary>
    /// Marks one behavior component as responsible for running its full lifecycle while authored inside the editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class RunInEditorAttribute : Attribute {
    }
}

#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
namespace helengine {
    /// <summary>
    /// Explicitly permits a concrete component to run its lifecycle and updates in the editor.
    /// Derived classes must opt in themselves so extending a renderer cannot implicitly enable gameplay.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class RunInEditorAttribute : Attribute {
    }
}
#endif

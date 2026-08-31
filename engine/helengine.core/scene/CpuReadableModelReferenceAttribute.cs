#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
namespace helengine {
    /// <summary>
    /// Marks a public scene-component member whose model reference should receive a CPU-readable companion during packaging.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class CpuReadableModelReferenceAttribute : Attribute {
    }
}
#endif

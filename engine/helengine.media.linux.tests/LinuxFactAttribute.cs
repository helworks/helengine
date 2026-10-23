namespace helengine.media.linux.tests;
/// <summary>Marks process-lifecycle fixtures requiring a native Linux executable script.</summary>
public sealed class LinuxFactAttribute : FactAttribute {
    /// <summary>Reports a visible skip on other platforms instead of silently bypassing lifecycle assertions.</summary>
    public LinuxFactAttribute() {if(!OperatingSystem.IsLinux()) {Skip="Requires native Linux process and executable-file semantics.";}}
}

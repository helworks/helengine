namespace helengine;

/// <summary>
/// Declares one optional generated runtime module bootstrap contract that can be emitted into native generated core when its activation types are used by cooked scenes.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedRuntimeModuleManifestAttribute : Attribute {
    /// <summary>
    /// Initializes one generated runtime module manifest declaration.
    /// </summary>
    /// <param name="moduleId">Stable module id used for deterministic emission order.</param>
    /// <param name="registrationType">Static registration type that exposes the runtime bootstrap entrypoint.</param>
    /// <param name="registrationMethodName">Static registration method that should be emitted into generated bootstrap code.</param>
    /// <param name="activationTypes">Runtime-owned types that activate the module when used by cooked content.</param>
    public GeneratedRuntimeModuleManifestAttribute(
        string moduleId,
        Type registrationType,
        string registrationMethodName,
        params Type[] activationTypes) {
        if (string.IsNullOrWhiteSpace(moduleId)) {
            throw new ArgumentException("Runtime module id is required.", nameof(moduleId));
        } else if (registrationType == null) {
            throw new ArgumentNullException(nameof(registrationType));
        } else if (string.IsNullOrWhiteSpace(registrationMethodName)) {
            throw new ArgumentException("Runtime module registration method is required.", nameof(registrationMethodName));
        } else if (activationTypes == null) {
            throw new ArgumentNullException(nameof(activationTypes));
        } else if (activationTypes.Length == 0) {
            throw new ArgumentException("At least one activation type is required.", nameof(activationTypes));
        } else if (Array.Exists(activationTypes, activationType => activationType == null)) {
            throw new ArgumentException("Activation types cannot contain null entries.", nameof(activationTypes));
        }

        ModuleId = moduleId;
        RegistrationType = registrationType;
        RegistrationMethodName = registrationMethodName;
        ActivationTypes = [.. activationTypes];
    }

    /// <summary>
    /// Gets the stable runtime module id used for deterministic emission order.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the static registration type that should be emitted into the generated bootstrap source.
    /// </summary>
    public Type RegistrationType { get; }

    /// <summary>
    /// Gets the static registration method that should be invoked by generated core.
    /// </summary>
    public string RegistrationMethodName { get; }

    /// <summary>
    /// Gets the runtime-owned activation types that enable this module when they are used by cooked content.
    /// </summary>
    public Type[] ActivationTypes { get; }
}

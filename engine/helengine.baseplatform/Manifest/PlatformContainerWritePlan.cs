namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes the container layout plan used to write cooked placements.
/// </summary>
public sealed class PlatformContainerWritePlan {
    /// <summary>
    /// Initializes one container write plan.
    /// </summary>
    public PlatformContainerWritePlan(string runtimeSpecializationId, PlatformContainerArtifact[] containerArtifacts) {
        if (runtimeSpecializationId == null) {
            throw new ArgumentNullException(nameof(runtimeSpecializationId), "Runtime specialization id is required.");
        } else if (containerArtifacts == null) {
            throw new ArgumentNullException(nameof(containerArtifacts), "Container artifacts are required.");
        } else if (Array.Exists(containerArtifacts, containerArtifact => containerArtifact == null)) {
            throw new ArgumentException("Container artifacts cannot contain null entries.", nameof(containerArtifacts));
        }

        RuntimeSpecializationId = runtimeSpecializationId;
        ContainerArtifacts = [.. containerArtifacts];
    }

    /// <summary>
    /// Gets the runtime specialization id that selected this plan.
    /// </summary>
    public string RuntimeSpecializationId { get; }

    /// <summary>
    /// Gets the planned containers for the layout.
    /// </summary>
    public PlatformContainerArtifact[] ContainerArtifacts { get; }
}

using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.baseplatform.Builders;

/// <summary>
/// Defines the execution boundary a platform asset builder must expose to cook resolved project content.
/// </summary>
public interface IPlatformAssetBuilder {
    /// <summary>
    /// Gets the explicit builder descriptor that identifies the implementation and its compatibility.
    /// </summary>
    PlatformBuilderDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the typed platform definition that describes the build profiles, graphics profiles, and asset requirements exposed to the editor.
    /// </summary>
    PlatformDefinition Definition { get; }

    /// <summary>
    /// Translates one editor-authored material schema payload into the cooked material bytes the target platform runtime consumes.
    /// </summary>
    /// <param name="request">Material translation request that carries the schema id and serialized field values.</param>
    /// <returns>Cooked material payload plus referenced shader dependencies.</returns>
    PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request);

    /// <summary>
    /// Executes the platform content build for one fully resolved request.
    /// </summary>
    /// <param name="request">The build request to process.</param>
    /// <param name="progressReporter">The reporter that receives streamed progress updates.</param>
    /// <param name="diagnosticReporter">The reporter that receives streamed diagnostics.</param>
    /// <param name="cancellationToken">The cancellation token used to stop the build cooperatively.</param>
    /// <returns>The final build report for the completed execution.</returns>
    Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken);
}

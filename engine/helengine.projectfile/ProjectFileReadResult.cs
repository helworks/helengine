namespace helengine.projectfile;

/// <summary>
/// Represents the outcome of reading a canonical `.heproj` file, including either a parsed document or structured errors.
/// </summary>
public sealed class ProjectFileReadResult {
    /// <summary>
    /// Initializes a successful project-file read result.
    /// </summary>
    /// <param name="document">The parsed canonical project document.</param>
    public ProjectFileReadResult(ProjectFileDocument document) {
        Document = document;
        Errors = [];
        Succeeded = true;
    }

    /// <summary>
    /// Initializes a failed project-file read result.
    /// </summary>
    /// <param name="errors">The structured failures encountered while reading the project file.</param>
    public ProjectFileReadResult(IReadOnlyList<ProjectFileReadError> errors) {
        Errors = errors;
        Succeeded = false;
    }

    /// <summary>
    /// Gets the parsed canonical project document when reading succeeded.
    /// </summary>
    public ProjectFileDocument Document { get; }

    /// <summary>
    /// Gets the structured read errors when reading failed.
    /// </summary>
    public IReadOnlyList<ProjectFileReadError> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the read operation produced a valid canonical project document.
    /// </summary>
    public bool Succeeded { get; }
}

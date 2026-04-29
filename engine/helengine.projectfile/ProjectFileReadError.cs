namespace helengine.projectfile;

/// <summary>
/// Describes one structured validation or parse failure encountered while reading a canonical `.heproj` file.
/// </summary>
public sealed class ProjectFileReadError {
    /// <summary>
    /// Initializes one structured project-file read error.
    /// </summary>
    /// <param name="code">Categorizes the type of read failure that occurred.</param>
    /// <param name="message">Provides the human-readable explanation for the failure.</param>
    /// <param name="fieldName">Identifies the field associated with the failure when one specific field is known.</param>
    public ProjectFileReadError(ProjectFileReadErrorCode code, string message, string fieldName) {
        Code = code;
        Message = message;
        FieldName = fieldName;
    }

    /// <summary>
    /// Gets the structured failure category.
    /// </summary>
    public ProjectFileReadErrorCode Code { get; }

    /// <summary>
    /// Gets the human-readable explanation for the failure.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the canonical field name associated with the failure when applicable.
    /// </summary>
    public string FieldName { get; }
}

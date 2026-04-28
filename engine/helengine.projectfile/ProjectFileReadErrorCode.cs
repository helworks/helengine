namespace helengine.projectfile;

/// <summary>
/// Enumerates the structured failure categories that can occur while reading a canonical `.heproj` file.
/// </summary>
public enum ProjectFileReadErrorCode {
    /// <summary>
    /// Indicates the project file could not be parsed as valid JSON.
    /// </summary>
    InvalidJson,

    /// <summary>
    /// Indicates a required canonical project field was absent from the file.
    /// </summary>
    MissingRequiredField,

    /// <summary>
    /// Indicates the file declares a project-format version that this library does not support.
    /// </summary>
    UnsupportedFormatVersion,

    /// <summary>
    /// Indicates one field was present but contained a value that failed canonical validation.
    /// </summary>
    InvalidFieldValue
}

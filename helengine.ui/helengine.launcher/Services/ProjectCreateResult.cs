namespace helengine.editor.launcher.Services;

/// <summary>
/// Describes the outcome of one launcher project-creation request.
/// </summary>
/// <param name="Success">Whether the project was created successfully.</param>
/// <param name="Message">Human-readable status text for the launcher UI.</param>
/// <param name="ProjectPath">Created project root directory path when creation succeeded.</param>
public sealed record ProjectCreateResult(bool Success, string Message, string ProjectPath);

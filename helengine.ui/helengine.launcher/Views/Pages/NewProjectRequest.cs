using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Views.Pages;

/// <summary>
/// Describes the validated inputs required to scaffold a new launcher project.
/// </summary>
/// <param name="ProjectName">Project name entered by the user.</param>
/// <param name="ProjectLocation">Directory where the project should be created.</param>
/// <param name="Engine">Engine install selected for the new project.</param>
public sealed record NewProjectRequest(string ProjectName, string ProjectLocation, EngineInstall Engine);

using helengine.projectfile;

namespace helengine.editor;

/// <summary>
/// Resolves editor startup arguments to one validated canonical project file path.
/// </summary>
public sealed class EditorStartupProjectPathResolver {
    /// <summary>
    /// Shared project-file path resolver used to validate incoming project arguments.
    /// </summary>
    ProjectFilePathResolver ProjectFilePathResolver { get; }

    /// <summary>
    /// Creates one startup argument resolver backed by the shared canonical project-file resolver.
    /// </summary>
    public EditorStartupProjectPathResolver() {
        ProjectFilePathResolver = new ProjectFilePathResolver();
    }

    /// <summary>
    /// Resolves one startup argument array to the canonical `.heproj` file path required by the editor.
    /// </summary>
    /// <param name="args">Command-line arguments that may contain a project path.</param>
    /// <returns>Validated canonical `.heproj` file path.</returns>
    public string Resolve(string[] args) {
        string projectPathArgument = FindProjectPathArgument(args);
        return ProjectFilePathResolver.Resolve(projectPathArgument);
    }

    /// <summary>
    /// Finds the first non-switch command-line argument that should be interpreted as a project path.
    /// </summary>
    /// <param name="args">Command-line arguments supplied by the host process.</param>
    /// <returns>Project path argument selected for canonical resolution.</returns>
    static string FindProjectPathArgument(string[] args) {
        if (args == null || args.Length == 0) {
            throw new InvalidOperationException("Project path argument is required.");
        }

        foreach (string argument in args) {
            if (string.IsNullOrWhiteSpace(argument)) {
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal)) {
                continue;
            }

            return argument;
        }

        throw new InvalidOperationException("Project path argument is required.");
    }
}

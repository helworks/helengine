namespace helengine.tools;

using helengine.editor;

/// <summary>
/// Executes the editor Windows build pipeline for one explicit project, scene, and output folder.
/// </summary>
public static class Program {
    /// <summary>
    /// Validates arguments, runs the Windows build executor, and returns a process exit code.
    /// </summary>
    /// <param name="args">Command-line arguments: project root, scene id, and output directory.</param>
    /// <returns>Zero when the build succeeds; otherwise one.</returns>
    public static int Main(string[] args) {
        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.Length != 3) {
            Console.Error.WriteLine("Usage: <project-root> <scene-id> <output-directory>");
            return 1;
        }

        string projectRootPath = Path.GetFullPath(args[0]);
        string sceneId = args[1];
        string outputDirectoryPath = Path.GetFullPath(args[2]);

        var executor = new EditorWindowsBuildExecutor(projectRootPath);
        var queueItem = new EditorBuildQueueItemDocument {
            QueueItemId = "manual-runner",
            PlatformId = "windows",
            OutputDirectoryPath = outputDirectoryPath,
            SelectedSceneIds = [sceneId]
        };

        EditorBuildExecutionResult result = executor.Execute(queueItem);
        Console.WriteLine(result.Message);
        return result.Succeeded ? 0 : 1;
    }
}

using helengine.editor;

/// <summary>
/// Writes the exportable physics validation scenes into one Helengine project.
/// </summary>
public static class Program {
    /// <summary>
    /// Entry point that writes all physics validation scenes into the provided project root.
    /// </summary>
    /// <param name="args">Command-line arguments where the first argument is the project root path.</param>
    /// <returns>Zero when scene generation succeeds.</returns>
    public static int Main(string[] args) {
        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }
        if (args.Length != 1) {
            Console.Error.WriteLine("Usage: physics-scene-writer <project-root>");
            return 1;
        }

        string projectRootPath = Path.GetFullPath(args[0]);
        PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();
        factory.WriteScenes(projectRootPath);
        Console.WriteLine("Wrote physics validation scenes to " + projectRootPath);
        return 0;
    }
}

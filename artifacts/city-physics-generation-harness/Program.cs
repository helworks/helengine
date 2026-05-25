using helengine.editor;

namespace helengine.debugtools {
    /// <summary>
    /// Runs the city physics scene generator through an initialized editor core.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Stable city project root used by the local generator harness.
        /// </summary>
        const string ProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Runs the physics generator and writes the result to stdout.
        /// </summary>
        /// <param name="args">Unused command-line arguments.</param>
        /// <returns>Zero when generation succeeds; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                PhysicsValidationSceneFactory generator = new PhysicsValidationSceneFactory();
                generator.WriteScenes(ProjectRootPath);
                Console.WriteLine("Generated city physics scenes.");
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}

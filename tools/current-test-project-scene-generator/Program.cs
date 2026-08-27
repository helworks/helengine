namespace helengine.current_test_project_scene_generator {
    /// <summary>
    /// Runs the current public writer for the engine test-project rendering scene catalog.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Generates the rendering scene catalog under the supplied test-project root.
        /// </summary>
        /// <param name="args">Optional test-project root path.</param>
        /// <returns>Zero when generation succeeds; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                if (args != null && args.Length == 1 && string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase)) {
                    Console.WriteLine("Usage: helengine.current-test-project-scene-generator [--project-root <test-project-root>]");
                    return 0;
                }

                string projectRootPath = ResolveProjectRoot(args);
                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
                });
                new RenderingSceneFixtureGenerator().Generate(projectRootPath);
                Console.WriteLine("Current test-project rendering scenes were generated.");
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// Resolves the optional project-root command argument or the maintained repository default.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Absolute test-project root path.</returns>
        static string ResolveProjectRoot(string[] args) {
            if (args == null || args.Length == 0) {
                return TestProjectPathResolver.ResolveDefaultProjectRoot(AppContext.BaseDirectory);
            }

            if (args.Length == 1 && !string.Equals(args[0], "--project-root", StringComparison.OrdinalIgnoreCase)) {
                return Path.GetFullPath(args[0]);
            }

            if (args.Length == 2 && string.Equals(args[0], "--project-root", StringComparison.OrdinalIgnoreCase)) {
                return Path.GetFullPath(args[1]);
            }

            throw new ArgumentException("Expected no arguments or --project-root <test-project-root>.");
        }
    }
}

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Entry point for the demo-disc scene writer tool.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Default city project root used when no explicit path is supplied.
        /// </summary>
        static readonly string DefaultProjectRootPath = @"C:\dev\helprojs\city";
        /// <summary>
        /// Default test-project root used when writing committed rendering smoke scenes.
        /// </summary>
        static readonly string DefaultRenderingProjectRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "test-project"));

        /// <summary>
        /// Runs the demo-disc scene writer tool.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Zero when generation succeeds; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                if (ShouldWriteRenderingScenes(args)) {
                    string renderingProjectRootPath = ResolveRenderingProjectRootPath(args);
                    RenderingSceneWriter renderingSceneWriter = new RenderingSceneWriter();
                    renderingSceneWriter.WriteAll(renderingProjectRootPath);
                    ExistingRenderingSceneTaggedMigrator migrator = new ExistingRenderingSceneTaggedMigrator();
                    migrator.MigrateAll(renderingProjectRootPath);
                    Console.WriteLine("Rendering smoke scenes were written successfully.");
                    return 0;
                }

                string projectRootPath = ResolveProjectRootPath(args);
                DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());
                writer.WriteAll(projectRootPath);
                Console.WriteLine("Demo disc menu assets were written successfully.");
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// Resolves the target city project root from command-line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Project root path that should receive the generated files.</returns>
        static string ResolveProjectRootPath(string[] args) {
            if (args == null || args.Length == 0) {
                return DefaultProjectRootPath;
            }

            return args[0];
        }

        /// <summary>
        /// Gets whether the current invocation requested rendering smoke-scene generation.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>True when the invocation should write rendering smoke scenes.</returns>
        static bool ShouldWriteRenderingScenes(string[] args) {
            return args != null &&
                args.Length > 0 &&
                string.Equals(args[0], "--rendering-scenes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the project root used when writing committed rendering smoke scenes.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Project root path that should receive the committed rendering scenes.</returns>
        static string ResolveRenderingProjectRootPath(string[] args) {
            if (args == null || args.Length < 2) {
                return DefaultRenderingProjectRootPath;
            }

            return args[1];
        }
    }
}

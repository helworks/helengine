namespace helshader {
    /// <summary>
    /// Entry point for the helshader command line tool.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Runs the helshader tool with the provided arguments.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code indicating success or failure.</returns>
        public static int Main(string[] args) {
            var app = new HelShaderApp();
            return app.Run(args);
        }
    }
}

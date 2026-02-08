namespace helengine.render.validation {
    /// <summary>
    /// Entry point for the render validation executable.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Executes render validation for the selected backend targets.
        /// </summary>
        /// <param name="args">Command-line arguments controlling backend selection and output.</param>
        /// <returns>Zero when all validations pass; otherwise one.</returns>
        [STAThread]
        public static int Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            RenderValidationOptions options = RenderValidationOptions.Parse(args);
            var runner = new RenderValidationRunner(options);
            IReadOnlyList<RenderValidationResult> results = runner.Run();

            bool allPassed = true;
            for (int i = 0; i < results.Count; i++) {
                RenderValidationResult result = results[i];
                string status = result.Passed ? "PASS" : "FAIL";
                Console.WriteLine($"{result.Backend}: {status} - {result.Message}");
                Console.WriteLine($"  Output: {result.OutputPath}");
                if (!result.Passed) {
                    allPassed = false;
                }
            }

            return allPassed ? 0 : 1;
        }
    }
}

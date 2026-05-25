namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Runs the BEPU and helengine stacked-box comparison harness.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Executes both physics worlds and writes comparison traces to disk.
        /// </summary>
        /// <param name="args">Command-line arguments are currently ignored.</param>
        public static void Main(string[] args) {
            PhysicsComparisonRunner runner = new PhysicsComparisonRunner();
            runner.Run();
        }
    }
}

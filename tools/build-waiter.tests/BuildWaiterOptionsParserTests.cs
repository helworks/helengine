using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the build-waiter command line accepts complete invocations and rejects unsafe artifact paths.
    /// </summary>
    public sealed class BuildWaiterOptionsParserTests {
        /// <summary>
        /// Ensures a complete waiter command preserves the output root, required artifacts, child executable, and child arguments.
        /// </summary>
        [Fact]
        public void Parse_WhenOutputArtifactsAndCommandAreProvided_ReturnsValidatedOptions() {
            BuildWaiterOptions options = BuildWaiterOptionsParser.Parse([
                "--output", "C:\\build-output",
                "--require", "game.iso",
                "--require", "disc/SYSTEM.CNF",
                "--",
                "dotnet", "build", "project.csproj"
            ]);

            Assert.Equal(Path.GetFullPath("C:\\build-output"), options.OutputRootPath);
            Assert.Equal(["game.iso", "disc/SYSTEM.CNF"], options.RequiredArtifactRelativePaths);
            Assert.Equal("dotnet", options.CommandFileName);
            Assert.Equal(["build", "project.csproj"], options.CommandArguments);
        }

        /// <summary>
        /// Ensures required artifact paths cannot escape the final output directory through parent traversal.
        /// </summary>
        [Fact]
        public void Parse_WhenARequiredPathEscapesOutputRoot_ThrowsArgumentException() {
            Assert.Throws<ArgumentException>(() => BuildWaiterOptionsParser.Parse([
                "--output", "C:\\build-output", "--require", "../game.iso", "--", "dotnet", "build"
            ]));
        }
    }
}

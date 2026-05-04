using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor CLI build argument parser.
    /// </summary>
    public sealed class EditorCliArgumentParserTests {
        /// <summary>
        /// Ensures build mode is not reported when no build flag is supplied.
        /// </summary>
        [Fact]
        public void IsBuildModeRequested_WhenBuildFlagMissing_ReturnsFalse() {
            Assert.False(EditorCliArgumentParser.IsBuildModeRequested(Array.Empty<string>()));
            Assert.False(EditorCliArgumentParser.IsBuildModeRequested(["--project", "project.heproj"]));
        }

        /// <summary>
        /// Ensures inline argument syntax parses into one build request.
        /// </summary>
        [Fact]
        public void TryParseBuildOptions_WhenInlineArgumentsProvided_ReturnsParsedOptions() {
            bool parsed = EditorCliArgumentParser.TryParseBuildOptions(
                [
                    "--project=C:/dev/helengine/sample.heproj",
                    "--build=windows",
                    "--output=C:/dev/out",
                    "--full-graph"
                ],
                out EditorCliBuildOptions options,
                out string errorMessage);

            Assert.True(parsed);
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal("C:/dev/helengine/sample.heproj", options.ProjectPath);
            Assert.Equal("windows", options.PlatformId);
            Assert.Equal("C:/dev/out", options.OutputDirectoryPath);
            Assert.True(options.UseCommonOutputDirectory);
        }

        /// <summary>
        /// Ensures the long form common-output flag also enables full-graph mode.
        /// </summary>
        [Fact]
        public void TryParseBuildOptions_WhenCommonOutputFlagProvided_ReturnsFullGraphEnabled() {
            bool parsed = EditorCliArgumentParser.TryParseBuildOptions(
                [
                    "--project", "project.heproj",
                    "--build", "windows",
                    "--output", "out",
                    "--common-output"
                ],
                out EditorCliBuildOptions options,
                out string errorMessage);

            Assert.True(parsed);
            Assert.Equal(string.Empty, errorMessage);
            Assert.True(options.UseCommonOutputDirectory);
        }

        /// <summary>
        /// Ensures missing required build arguments fail explicitly.
        /// </summary>
        [Fact]
        public void TryParseBuildOptions_WhenRequiredArgumentsAreMissing_ReturnsFalse() {
            bool parsed = EditorCliArgumentParser.TryParseBuildOptions(
                ["--project", "project.heproj", "--output", "out"],
                out EditorCliBuildOptions options,
                out string errorMessage);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal("Build mode requires a target platform supplied through `--build`.", errorMessage);
        }
    }
}

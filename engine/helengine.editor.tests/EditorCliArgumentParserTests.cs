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
        /// Ensures editor-command mode is not reported when no editor-command flag is supplied.
        /// </summary>
        [Fact]
        public void IsEditorCommandModeRequested_WhenCommandFlagMissing_ReturnsFalse() {
            Assert.False(EditorCliArgumentParser.IsEditorCommandModeRequested(Array.Empty<string>()));
            Assert.False(EditorCliArgumentParser.IsEditorCommandModeRequested(["--project", "project.heproj"]));
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
                    "--build-profile=release",
                    "--output=C:/dev/out",
                    "--full-graph"
                ],
                out EditorCliBuildOptions options,
                out string errorMessage);

            Assert.True(parsed);
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal("C:/dev/helengine/sample.heproj", options.ProjectPath);
            Assert.Equal("windows", options.PlatformId);
            Assert.Equal("release", options.BuildProfileId);
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

        /// <summary>
        /// Ensures inline argument syntax parses into one editor-command request.
        /// </summary>
        [Fact]
        public void TryParseEditorCommandOptions_WhenInlineArgumentsProvided_ReturnsParsedOptions() {
            bool parsed = EditorCliArgumentParser.TryParseEditorCommandOptions(
                [
                    "--project=C:/dev/helengine/sample.heproj",
                    "--editor-command=menu.regenerate-demo-disc-main-menu"
                ],
                out EditorCliCommandOptions options,
                out string errorMessage);

            Assert.True(parsed);
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal("C:/dev/helengine/sample.heproj", options.ProjectPath);
            Assert.Equal("menu.regenerate-demo-disc-main-menu", options.CommandId);
        }

        /// <summary>
        /// Ensures missing required editor-command arguments fail explicitly.
        /// </summary>
        [Fact]
        public void TryParseEditorCommandOptions_WhenRequiredArgumentsAreMissing_ReturnsFalse() {
            bool parsed = EditorCliArgumentParser.TryParseEditorCommandOptions(
                ["--project", "project.heproj"],
                out EditorCliCommandOptions options,
                out string errorMessage);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal("Editor command mode requires a command id supplied through `--editor-command`.", errorMessage);
        }
    }
}

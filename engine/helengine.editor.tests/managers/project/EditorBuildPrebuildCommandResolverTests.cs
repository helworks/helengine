namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies project-authored editor prebuild command selection for a platform build profile.
    /// </summary>
    public sealed class EditorBuildPrebuildCommandResolverTests {
        /// <summary>
        /// Ensures commands declared for one build profile preserve their authored execution order.
        /// </summary>
        [Fact]
        public void Resolve_WhenProfileDeclaresCommands_ReturnsCommandsInAuthoredOrder() {
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "release",
                EditorPrebuildCommandIdsByBuildProfileId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
                    ["release"] = ["scene.generate", "menu.regenerate"]
                }
            };

            IReadOnlyList<string> commandIds = new EditorBuildPrebuildCommandResolver().Resolve(platformConfig, "release");

            Assert.Equal(["scene.generate", "menu.regenerate"], commandIds);
        }

        /// <summary>
        /// Ensures profiles without an authored prebuild declaration require no editor-command execution.
        /// </summary>
        [Fact]
        public void Resolve_WhenProfileHasNoDeclaration_ReturnsNoCommands() {
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "colored-cube-grid"
            };

            IReadOnlyList<string> commandIds = new EditorBuildPrebuildCommandResolver().Resolve(platformConfig, "colored-cube-grid");

            Assert.Empty(commandIds);
        }

        /// <summary>
        /// Ensures malformed prebuild command entries name the owning build profile rather than being silently skipped.
        /// </summary>
        [Fact]
        public void Resolve_WhenProfileContainsBlankCommand_ThrowsWithProfileId() {
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                EditorPrebuildCommandIdsByBuildProfileId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
                    ["release"] = ["scene.generate", " "]
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorBuildPrebuildCommandResolver().Resolve(platformConfig, "release"));

            Assert.Contains("release", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}

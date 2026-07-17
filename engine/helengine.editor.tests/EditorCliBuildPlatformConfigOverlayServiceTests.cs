using System.Reflection;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the headless CLI build path overlays shared platform profile settings onto local build execution state before packaging begins.
    /// </summary>
    public sealed class EditorCliBuildPlatformConfigOverlayServiceTests {
        /// <summary>
        /// Ensures shared platform build settings replace stale local build selections while preserving local scene, graphics, and codegen execution state.
        /// </summary>
        [Fact]
        public void ApplySharedProfileSettings_whenSharedSettingsExist_overlaysOnlyLocalBuildSelectionsAndPreservesRemainingExecutionState() {
            EditorBuildPlatformConfigDocument localPlatformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "ds",
                SelectedSceneIds = ["DemoDiscMainMenuHandheld"],
                OutputDirectoryPath = @"C:\temp\ds-build",
                DebugBuild = true,
                SelectedBuildProfileId = "debug",
                SelectedGraphicsProfileId = "ds-main-2d",
                SelectedBuildOptionValues = new Dictionary<string, string>(StringComparer.Ordinal) {
                    ["enable-native-runtime-diagnostics"] = "true",
                    ["enable-native-fatal-error-console"] = "true"
                },
                SelectedGraphicsOptionValues = new Dictionary<string, string>(StringComparer.Ordinal) {
                    ["default-width"] = "256"
                },
                SelectedCodegenProfileId = "default",
                SelectedStorageProfileId = "nitrofs-package",
                SelectedMediaProfileId = "ds-cartridge",
                SelectedCodegenOptionValues = new Dictionary<string, string>(StringComparer.Ordinal) {
                    ["load-native-runtime-metadata"] = "true"
                }
            };
            EditorPlatformProfileSettingsDocument sharedPlatformSettings = new EditorPlatformProfileSettingsDocument {
                PlatformId = "ds",
                Build = new EditorBuildProfileSettingsDocument {
                    SelectedBuildProfileId = "release",
                    SelectedOptionValues = new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["enable-native-runtime-diagnostics"] = "false",
                        ["enable-native-fatal-error-console"] = "false"
                    }
                }
            };

            InvokeApplySharedProfileSettings(localPlatformConfig, sharedPlatformSettings);

            Assert.Equal("release", localPlatformConfig.SelectedBuildProfileId);
            Assert.Equal("false", localPlatformConfig.SelectedBuildOptionValues["enable-native-runtime-diagnostics"]);
            Assert.Equal("false", localPlatformConfig.SelectedBuildOptionValues["enable-native-fatal-error-console"]);
            Assert.Equal("ds-main-2d", localPlatformConfig.SelectedGraphicsProfileId);
            Assert.Equal("256", localPlatformConfig.SelectedGraphicsOptionValues["default-width"]);
            Assert.Equal("default", localPlatformConfig.SelectedCodegenProfileId);
            Assert.Equal("true", localPlatformConfig.SelectedCodegenOptionValues["load-native-runtime-metadata"]);
            Assert.Equal(["DemoDiscMainMenuHandheld"], localPlatformConfig.SelectedSceneIds);
            Assert.Equal(@"C:\temp\ds-build", localPlatformConfig.OutputDirectoryPath);
            Assert.True(localPlatformConfig.DebugBuild);
            Assert.Equal("nitrofs-package", localPlatformConfig.SelectedStorageProfileId);
            Assert.Equal("ds-cartridge", localPlatformConfig.SelectedMediaProfileId);
        }

        /// <summary>
        /// Resolves the overlay helper through the editor assembly and invokes the shared-profile application method.
        /// </summary>
        /// <param name="platformConfig">Local platform configuration that should receive the shared settings overlay.</param>
        /// <param name="sharedPlatformSettings">Shared profile settings that should replace local build selections.</param>
        static void InvokeApplySharedProfileSettings(
            EditorBuildPlatformConfigDocument platformConfig,
            EditorPlatformProfileSettingsDocument sharedPlatformSettings) {
            Type overlayServiceType = typeof(EditorCliBuildRunner).Assembly.GetType("helengine.editor.EditorCliBuildPlatformConfigOverlayService");
            Assert.NotNull(overlayServiceType);

            MethodInfo applyMethod = overlayServiceType.GetMethod(
                "ApplySharedProfileSettings",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(applyMethod);

            applyMethod.Invoke(null, [platformConfig, sharedPlatformSettings]);
        }
    }
}

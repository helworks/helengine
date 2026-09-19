using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the shared override scope path helpers used by the runtime reader, the file format and the editor.
    /// </summary>
    public sealed class SceneOverrideScopePathTests {
        [Fact]
        public void Platform_BuildsOnePlatformStep() {
            SceneOverrideScopeStepAsset[] steps = SceneOverrideScopePath.Platform("ps1");

            SceneOverrideScopeStepAsset step = Assert.Single(steps);
            Assert.Equal(SceneOverrideScopeStepKind.Platform, step.Kind);
            Assert.Equal("ps1", step.Id);
        }

        [Fact]
        public void PlatformBuildConfig_WithBlankEnvironment_OmitsTheBuildConfigStep() {
            Assert.Single(SceneOverrideScopePath.PlatformBuildConfig("ps1", " "));
            Assert.Equal(2, SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug").Length);
        }

        [Fact]
        public void Format_WritesKindAndIdPerStepAndCommonForTheEmptyPath() {
            Assert.Equal("common", SceneOverrideScopePath.Format(SceneOverrideScopePath.Common()));
            Assert.Equal("common", SceneOverrideScopePath.Format(null));
            Assert.Equal("platform:ps1/buildconfig:debug", SceneOverrideScopePath.Format(SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug")));
            Assert.Equal("group:handheld/platform:ds", SceneOverrideScopePath.Format(new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" },
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = "ds" }
            }));
        }

        [Fact]
        public void AreEqual_IgnoresIdCaseAndSurroundingWhitespace() {
            SceneOverrideScopeStepAsset[] left = SceneOverrideScopePath.PlatformBuildConfig("PS1", "Debug");
            SceneOverrideScopeStepAsset[] right = new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = " ps1 " },
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.BuildConfig, Id = "debug" }
            };

            Assert.True(SceneOverrideScopePath.AreEqual(left, right));
            Assert.False(SceneOverrideScopePath.AreEqual(left, SceneOverrideScopePath.Platform("ps1")));
            Assert.True(SceneOverrideScopePath.AreEqual(null, SceneOverrideScopePath.Common()));
        }

        [Fact]
        public void Normalize_TrimsIdsDropsNullStepsAndRejectsBlankIds() {
            SceneOverrideScopeStepAsset[] normalized = SceneOverrideScopePath.Normalize(new[] {
                null,
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = " ps1 " }
            });

            Assert.Equal("ps1", Assert.Single(normalized).Id);
            Assert.Empty(SceneOverrideScopePath.Normalize(null));
            Assert.Throws<InvalidOperationException>(() => SceneOverrideScopePath.Normalize(new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "" }
            }));
        }
    }
}

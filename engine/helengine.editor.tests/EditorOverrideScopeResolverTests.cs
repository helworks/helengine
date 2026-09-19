using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies target-path construction and deepest-prefix selection against a platform group tree.
    /// </summary>
    public sealed class EditorOverrideScopeResolverTests {
        static readonly string[] Platforms = { "windows", "ps1", "ds", "psp" };

        static EditorProjectPlatformGroupsDocument CreateTree() {
            return new EditorProjectPlatformGroupsDocument {
                Groups = [
                    new EditorProjectPlatformGroupDefinition {
                        Id = "consoles",
                        PlatformIds = ["ps1"],
                        Children = [ new EditorProjectPlatformGroupDefinition { Id = "handheld", PlatformIds = ["ds", "psp"] } ]
                    }
                ],
                DefaultLevelOrder = [SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig]
            };
        }

        [Fact]
        public void BuildTargetPath_UnderDefaultOrder_MatchesThePairConstructor() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);

            Assert.Equal(new EditorOverrideScope("ps1", "debug"), resolver.BuildTargetPath(resolver.ResolveLevelOrder(null), "ps1", "debug"));
            Assert.Equal(new EditorOverrideScope("ps1"), resolver.BuildTargetPath(resolver.ResolveLevelOrder(null), "ps1", ""));
        }

        [Fact]
        public void BuildTargetPath_WithGroupLevel_ExpandsTheChainOutermostFirst() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig };

            EditorOverrideScope path = resolver.BuildTargetPath(order, "psp", "release");

            Assert.Equal("group:consoles/group:handheld/platform:psp/buildconfig:release", path.ToString());
            Assert.Equal("platform:windows/buildconfig:release", resolver.BuildTargetPath(order, "windows", "release").ToString());
            Assert.Equal("buildconfig:release/platform:psp", resolver.BuildTargetPath(new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform }, "psp", "release").ToString());
            Assert.True(resolver.BuildTargetPath(Array.Empty<SceneOverrideScopeStepKind>(), "psp", "release").IsCommon);
        }

        [Fact]
        public void TrySelectDeepest_PicksTheLongestPrefixAndFallsBackToCommon() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform };
            SceneEntityPlatformExistenceOverrideAsset[] overrides = {
                new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Common(), Exists = false },
                new SceneEntityPlatformExistenceOverrideAsset { Scope = new[] {
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "consoles" },
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" } }, Exists = true }
            };

            Assert.True(EditorOverrideScopeResolver.TrySelectDeepest(overrides, item => EditorOverrideScope.FromSteps(item.Scope), resolver.BuildTargetPath(order, "ds", ""), out SceneEntityPlatformExistenceOverrideAsset selected));
            Assert.True(selected.Exists);
            Assert.True(EditorOverrideScopeResolver.TrySelectDeepest(overrides, item => EditorOverrideScope.FromSteps(item.Scope), resolver.BuildTargetPath(order, "ps1", ""), out selected));
            Assert.False(selected.Exists);
            Assert.False(EditorOverrideScopeResolver.TrySelectDeepest(Array.Empty<SceneEntityPlatformExistenceOverrideAsset>(), item => EditorOverrideScope.FromSteps(item.Scope), EditorOverrideScope.Common, out _));
        }

        [Fact]
        public void Constructor_RejectsInconsistentGroupSettingsNamingBothIds() {
            EditorProjectPlatformGroupsDocument tree = CreateTree();
            tree.Groups.Add(new EditorProjectPlatformGroupDefinition { Id = "other", PlatformIds = ["ds"] });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new EditorOverrideScopeResolver(tree, Platforms));

            Assert.Contains("'handheld'", error.Message);
            Assert.Contains("'other'", error.Message);
        }
    }
}

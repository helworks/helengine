using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the path-based override scope and its prefix-aware map.
    /// </summary>
    public sealed class EditorOverrideScopeTests {
        [Fact]
        public void PairConstructor_BuildsTheDefaultOrderPath() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "debug");

            Assert.Equal(2, scope.Depth);
            Assert.Equal(SceneOverrideScopeStepKind.Platform, scope.Steps[0].Kind);
            Assert.Equal(SceneOverrideScopeStepKind.BuildConfig, scope.Steps[1].Kind);
            Assert.Equal("platform:ps1/buildconfig:debug", scope.ToString());
            Assert.Equal(new EditorOverrideScope("ps1"), scope.Parent);
            Assert.True(scope.Parent.Parent.IsCommon);
        }

        [Fact]
        public void PairConstructor_WithCommonPlatformId_YieldsTheCommonScope() {
            Assert.True(new EditorOverrideScope("common").IsCommon);
            Assert.True(new EditorOverrideScope("Common", "debug").IsCommon);
            Assert.Equal(EditorOverrideScope.Common, new EditorOverrideScope("common"));
            Assert.Equal("common", EditorOverrideScope.Common.ToString());
        }

        [Fact]
        public void Equality_IgnoresIdCase() {
            Assert.Equal(new EditorOverrideScope("PS1", "DEBUG"), new EditorOverrideScope("ps1", "debug"));
            Assert.Equal(new EditorOverrideScope("PS1").GetHashCode(), new EditorOverrideScope("ps1").GetHashCode());
            Assert.NotEqual(EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("ps1")), new EditorOverrideScope("ps1"));
        }

        [Fact]
        public void IsPrefixOf_AcceptsCommonAndProperPrefixesOnly() {
            EditorOverrideScope target = EditorOverrideScope.Common
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "handheld"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            Assert.True(EditorOverrideScope.Common.IsPrefixOf(target));
            Assert.True(EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")).IsPrefixOf(target));
            Assert.True(target.IsPrefixOf(target));
            Assert.False(new EditorOverrideScope("ds").IsPrefixOf(target));
            Assert.False(target.IsPrefixOf(target.Parent));
        }

        [Fact]
        public void ToSteps_RoundTripsThroughFromSteps() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "release");

            Assert.Equal(scope, EditorOverrideScope.FromSteps(scope.ToSteps()));
            Assert.Empty(EditorOverrideScope.Common.ToSteps());
        }

        [Fact]
        public void TryGetStepId_FindsTheRequestedKind() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "release");

            Assert.True(scope.TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environmentId));
            Assert.Equal("release", environmentId);
            Assert.False(scope.TryGetStepId(SceneOverrideScopeStepKind.Group, out _));
        }

        [Fact]
        public void ScopeMap_TryGetDeepestPrefix_PrefersTheLongestAuthoredPrefix() {
            EditorOverrideScopeMap<string> map = new EditorOverrideScopeMap<string>();
            EditorOverrideScope platform = new EditorOverrideScope("ps1");
            EditorOverrideScope config = new EditorOverrideScope("ps1", "debug");
            map.Set(EditorOverrideScope.Common, "common");
            map.Set(platform, "platform");

            Assert.True(map.TryGetDeepestPrefix(config, out string value, out EditorOverrideScope matched));
            Assert.Equal("platform", value);
            Assert.Equal(platform, matched);

            map.Set(config, "config");
            Assert.True(map.TryGetDeepestPrefix(config, out value, out _));
            Assert.Equal("config", value);

            Assert.True(map.TryGetDeepestPrefix(new EditorOverrideScope("n64"), out value, out matched));
            Assert.Equal("common", value);
            Assert.True(matched.IsCommon);

            map.Remove(EditorOverrideScope.Common);
            Assert.False(map.TryGetDeepestPrefix(new EditorOverrideScope("n64"), out _, out _));
        }
    }
}

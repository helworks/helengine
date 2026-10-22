namespace helengine.editor.tests {
    /// <summary>Protects explicit CLI build profiles from being reversed by persisted debug toggles.</summary>
    public sealed class EditorCliRequestedBuildProfileTests {
        /// <summary>Canonical profiles align the legacy toggle; custom profiles preserve its independent overlay setting.</summary>
        [Theory]
        [InlineData("debug", false, true)]
        [InlineData("release", true, false)]
        [InlineData("DEBUG", false, true)]
        [InlineData("profiler", true, true)]
        [InlineData("profiler", false, false)]
        public void ExplicitProfileSurvivesPersistedBuildMode(string profileId, bool previousDebug, bool expectedDebug) {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "release",
                DebugBuild = previousDebug
            };
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedBuildProfile(config, profileId);
            Assert.Equal(profileId, config.SelectedBuildProfileId);
            Assert.Equal(expectedDebug, config.DebugBuild);
        }
    }
}

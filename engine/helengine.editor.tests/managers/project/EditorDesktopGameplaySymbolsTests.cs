namespace helengine.editor.tests.managers.project {
    /// <summary>Verifies desktop gameplay compilation receives the same input contract on each native desktop target.</summary>
    public sealed class EditorDesktopGameplaySymbolsTests {
        /// <summary>Desktop source branches must be available independently of the authoring host operating system.</summary>
        [Theory]
        [InlineData("windows")]
        [InlineData("linux")]
        [InlineData("macos")]
        public void DesktopTargetsIncludeDesktopInput(string platformId) {
            Assert.Contains("DESKTOP_PLATFORM", EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId));
        }

        /// <summary>Mobile and console targets retain their portable input branches.</summary>
        [Theory]
        [InlineData("ios")]
        [InlineData("android")]
        [InlineData("ps1")]
        public void OtherTargetsKeepPortableInput(string platformId) {
            Assert.DoesNotContain("DESKTOP_PLATFORM", EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId));
        }
    }
}

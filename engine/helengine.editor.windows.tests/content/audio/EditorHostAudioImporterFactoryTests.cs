using helengine.editor;

namespace helengine.editor.windows.tests.content.audio {
    /// <summary>
    /// Verifies the editor host audio importer registrations used at startup.
    /// </summary>
    public sealed class EditorHostAudioImporterFactoryTests {
        /// <summary>
        /// Ensures the default audio importer list includes the lazy Windows-backed source importer.
        /// </summary>
        [Fact]
        public void CreateDefault_WhenCalled_RegistersWindowsAudioImporterForWavAndMp3() {
            IReadOnlyList<IAssetImporterRegistration> registrations = EditorHostAudioImporterFactory.CreateDefault();

            AudioImporterRegistration registration = Assert.Single(registrations.OfType<AudioImporterRegistration>());

            Assert.Equal("naudio-source", registration.ImporterId);
            Assert.Equal(
                new[] { ".wav", ".wave", ".mp3" },
                registration.Extensions,
                StringComparer.OrdinalIgnoreCase);
            Assert.IsType<LazyAudioImporter>(registration.Importer);
        }
    }
}

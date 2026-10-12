namespace helengine.editor.tests.managers.project {
    /// <summary>Provides deterministic decoded audio without involving an external audio tool in scene dependency tests.</summary>
    internal sealed class EditorSceneDependencyAudioImporter : IAudioImporter {
        /// <summary>Imports a stereo PCM payload whose cook must remain selected by every referencing scene.</summary>
        /// <param name="stream">Source audio fixture opened by the production importer manager.</param>
        /// <returns>Small valid decoded stereo source.</returns>
        public ImportedAudioSource ImportAudio(Stream stream) {
            ArgumentNullException.ThrowIfNull(stream);
            return new ImportedAudioSource {
                Channels = 2,
                SampleRate = 44100,
                DurationSeconds = 1f,
                Pcm16Bytes = [1, 2, 3, 4]
            };
        }
    }
}

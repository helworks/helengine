using helengine;

namespace helengine.editor.windows.tests.audio {
    /// <summary>
    /// Verifies the Windows runtime audio backend can start and track simple PCM playback.
    /// </summary>
    public sealed class WindowsAudioBackendTests {
        /// <summary>
        /// Ensures buffered PCM assets produce one voice identifier that stays observable through the backend tracking surface.
        /// </summary>
        [Fact]
        public void Play_WhenBufferedAssetSubmitted_ReturnsVoiceIdAndTracksPlayback() {
            using WindowsAudioBackend backend = new WindowsAudioBackend();
            AudioAsset asset = CreateBufferedPcmAudioAsset();

            int voiceId = backend.Play(asset, new AudioPlaybackRequest {
                BusId = "sfx",
                Loop = false,
                Gain = 1f
            });

            Assert.True(voiceId >= 0);
            Assert.True(backend.IsPlaying(voiceId));
        }

        static AudioAsset CreateBufferedPcmAudioAsset() {
            byte[] pcmBytes = new byte[44100];
            return new AudioAsset {
                Id = "test-buffered-audio",
                PlaybackMode = AudioPlaybackMode.Predecoded,
                DefaultLoop = false,
                DefaultBusId = "sfx",
                Channels = 1,
                SampleRate = 22050,
                DurationSeconds = 1f,
                EncodingFamilyId = "pcm-buffered",
                EncodedBytes = pcmBytes,
                Chunks = [
                    new AudioChunkDescriptor {
                        ByteOffset = 0,
                        ByteLength = pcmBytes.Length
                    }
                ]
            };
        }
    }
}

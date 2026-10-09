namespace helengine.core.tests.audio {
    /// <summary>
    /// Verifies the optional playback start offset: it defaults to zero, reaches the backend unchanged, and the manager
    /// reports whether the backend honours it.
    /// </summary>
    public sealed class AudioStartOffsetTests {
        /// <summary>
        /// A new request starts at the beginning of the asset.
        /// </summary>
        [Fact]
        public void NewRequest_startsAtZero() {
            Assert.Equal(0f, new AudioPlaybackRequest().StartOffsetSeconds);
        }

        /// <summary>
        /// The manager forwards the offset to a seekable backend and says it is supported.
        /// </summary>
        [Fact]
        public void SeekableBackend_receivesOffsetAndIsReportedAsSupported() {
            SeekableRecordingAudioBackend backend = new SeekableRecordingAudioBackend();
            AudioManager manager = new AudioManager(backend);

            manager.Play(new AudioAsset(), new AudioPlaybackRequest { StartOffsetSeconds = 1.25f });

            Assert.True(manager.SupportsStartOffset);
            Assert.Equal(1.25f, Assert.Single(backend.Requests).StartOffsetSeconds);
        }

        /// <summary>
        /// A backend that cannot seek is reported as such, so callers can skip mid-clip starts.
        /// </summary>
        [Fact]
        public void PlainBackend_isReportedAsNotSupportingOffsets() {
            Assert.False(new AudioManager(new RecordingAudioBackend()).SupportsStartOffset);
        }
    }
}

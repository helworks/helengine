namespace helengine.core.tests.audio {
    /// <summary>
    /// Recording backend double that declares it honours start offsets.
    /// </summary>
    sealed class SeekableRecordingAudioBackend : RecordingAudioBackend, ISeekableAudioBackend {
    }
}

namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Recording audio backend that declares it honours start offsets.
    /// </summary>
    public sealed class SeekableRecordingAudioBackend : RecordingAudioBackend, ISeekableAudioBackend {
    }
}

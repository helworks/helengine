namespace helengine {
    /// <summary>
    /// Marks an audio backend that honours <see cref="AudioPlaybackRequest.StartOffsetSeconds"/> by starting playback that
    /// many seconds into the asset. Backends that cannot seek (fixed hardware voices, streamed banks) implement only
    /// <see cref="IAudioBackend"/> and ignore the offset; <see cref="AudioManager.SupportsStartOffset"/> reports which kind
    /// is active so callers such as sequence players can skip a mid-clip start instead of playing it from the beginning.
    /// </summary>
    public interface ISeekableAudioBackend : IAudioBackend {
    }
}

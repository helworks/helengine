namespace helengine.baseplatform.Builders;

/// <summary>Optionally supplies platform-owned audio payload encoders to the editor cook boundary.</summary>
public interface IPlatformAudioPayloadEncoderProvider {
    /// <summary>Gets the encoders published by the loaded platform builder.</summary>
    IReadOnlyList<IPlatformAudioPayloadEncoder> GetAudioPayloadEncoders();
}
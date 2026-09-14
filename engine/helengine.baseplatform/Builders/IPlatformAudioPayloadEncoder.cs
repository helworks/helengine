namespace helengine.baseplatform.Builders;

/// <summary>Encodes processed PCM16 samples into a platform-owned runtime audio payload family.</summary>
public interface IPlatformAudioPayloadEncoder {
    /// <summary>Gets the stable encoding-family identifier.</summary>
    string EncodingFamilyId { get; }
    /// <summary>Encodes a complete signed PCM16 sample buffer.</summary>
    byte[] Encode(short[] samples);
}
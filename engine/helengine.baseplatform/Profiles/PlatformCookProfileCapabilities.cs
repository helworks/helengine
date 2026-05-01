namespace helengine.baseplatform.Profiles;

/// <summary>
/// Describes the cooked-output capabilities for one shared platform cook profile.
/// </summary>
public class PlatformCookProfileCapabilities {
    /// <summary>
    /// Initializes one cook-profile capability set with explicit output families.
    /// </summary>
    /// <param name="graphicsBackendFamily">The graphics backend family produced by the profile.</param>
    /// <param name="textureCompressionFamily">The texture compression family produced by the profile.</param>
    /// <param name="audioEncodingFamily">The audio encoding family produced by the profile.</param>
    /// <param name="sceneSerializationFamily">The scene and runtime serialization family produced by the profile.</param>
    /// <param name="serializationEndianness">The byte order used when serializing cooked output.</param>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing.</exception>
    public PlatformCookProfileCapabilities(
        string graphicsBackendFamily,
        string textureCompressionFamily,
        string audioEncodingFamily,
        string sceneSerializationFamily,
        PlatformSerializationEndianness serializationEndianness) {
        if (string.IsNullOrWhiteSpace(graphicsBackendFamily)) {
            throw new ArgumentException("Graphics backend family is required.", nameof(graphicsBackendFamily));
        } else if (string.IsNullOrWhiteSpace(textureCompressionFamily)) {
            throw new ArgumentException("Texture compression family is required.", nameof(textureCompressionFamily));
        } else if (string.IsNullOrWhiteSpace(audioEncodingFamily)) {
            throw new ArgumentException("Audio encoding family is required.", nameof(audioEncodingFamily));
        } else if (string.IsNullOrWhiteSpace(sceneSerializationFamily)) {
            throw new ArgumentException("Scene serialization family is required.", nameof(sceneSerializationFamily));
        }

        GraphicsBackendFamily = graphicsBackendFamily;
        TextureCompressionFamily = textureCompressionFamily;
        AudioEncodingFamily = audioEncodingFamily;
        SceneSerializationFamily = sceneSerializationFamily;
        SerializationEndianness = serializationEndianness;
    }

    /// <summary>
    /// Gets the graphics backend family produced by the profile.
    /// </summary>
    public string GraphicsBackendFamily { get; }

    /// <summary>
    /// Gets the texture compression family produced by the profile.
    /// </summary>
    public string TextureCompressionFamily { get; }

    /// <summary>
    /// Gets the audio encoding family produced by the profile.
    /// </summary>
    public string AudioEncodingFamily { get; }

    /// <summary>
    /// Gets the scene and runtime serialization family produced by the profile.
    /// </summary>
    public string SceneSerializationFamily { get; }

    /// <summary>
    /// Gets the byte order used when serializing cooked output for the profile.
    /// </summary>
    public PlatformSerializationEndianness SerializationEndianness { get; }
}

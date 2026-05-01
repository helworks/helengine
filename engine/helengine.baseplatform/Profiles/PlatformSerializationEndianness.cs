namespace helengine.baseplatform.Profiles;

/// <summary>
/// Identifies the byte order used by a cooked platform profile.
/// </summary>
public enum PlatformSerializationEndianness {
    /// <summary>
    /// Cooked payloads are serialized with least-significant bytes first.
    /// </summary>
    LittleEndian = 0,

    /// <summary>
    /// Cooked payloads are serialized with most-significant bytes first.
    /// </summary>
    BigEndian = 1
}

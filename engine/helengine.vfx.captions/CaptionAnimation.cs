using System.Text.Json.Serialization;

namespace helengine.vfx.captions;

/// <summary>Deterministic caption motion computed from transcription time.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaptionAnimation>))]
public enum CaptionAnimation {
    /// <summary>Displays captions at their final size and position.</summary>
    None,
    /// <summary>Scales up at the beginning of a phrase or aligned word.</summary>
    Pop,
    /// <summary>Adds a short settling vertical bounce at each aligned word or phrase.</summary>
    Bounce,
    /// <summary>Alternates two fixed offsets, producing a two-frame hand-drawn wobble.</summary>
    TwoFrame
}

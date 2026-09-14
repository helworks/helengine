using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of an animation clip asset for the editor asset format, including its keyframe tracks and its platform-authored track overrides.
    /// </summary>
    public sealed class AnimationClipAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for animation clip payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.AnimationClipAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is an animation clip asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is an animation clip asset.</returns>
        public bool Handles(Asset asset) {
            return asset is AnimationClipAsset;
        }

        /// <summary>
        /// Performs no validation because an animation clip payload sorts its platform overrides while writing and has no invariant that must be rejected before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes an animation clip asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Animation clip asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            AnimationClipAsset animationClipAsset = (AnimationClipAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(animationClipAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, animationClipAsset);
            writer.WriteSingle(animationClipAsset.Duration);
            writer.WriteArray(animationClipAsset.PositionTracks, WritePositionKeyframeTrackAsset);
            writer.WriteArray(animationClipAsset.PositionOffsetTracks, WritePositionOffsetKeyframeTrackAsset);
            writer.WriteArray(animationClipAsset.ScaleTracks, WriteScaleKeyframeTrackAsset);
            writer.WriteArray(animationClipAsset.RotationTracks, WriteRotationKeyframeTrackAsset);
            writer.WriteArray(animationClipAsset.PlatformOverrides?
                .OrderBy(platformOverride => platformOverride?.PlatformId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(platformOverride => platformOverride?.EnvironmentId ?? string.Empty, StringComparer.Ordinal)
                .ToArray(), WriteAnimationClipPlatformOverrideAsset);
        }

        /// <summary>
        /// Reads an animation clip asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized animation clip asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            AnimationClipAsset asset = new AnimationClipAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.Duration = reader.ReadSingle();
            asset.PositionTracks = reader.ReadArray(ReadPositionKeyframeTrackAsset) ?? Array.Empty<PositionKeyframeTrackAsset>();
            asset.PositionOffsetTracks = reader.ReadArray(ReadPositionOffsetKeyframeTrackAsset) ?? Array.Empty<PositionOffsetKeyframeTrackAsset>();
            asset.ScaleTracks = reader.ReadArray(ReadScaleKeyframeTrackAsset) ?? Array.Empty<ScaleKeyframeTrackAsset>();
            asset.RotationTracks = reader.ReadArray(ReadRotationKeyframeTrackAsset) ?? Array.Empty<RotationKeyframeTrackAsset>();
            asset.PlatformOverrides = reader.ReadArray(ReadAnimationClipPlatformOverrideAsset) ?? Array.Empty<AnimationClipPlatformOverrideAsset>();
            return asset;
        }

        /// <summary>
        /// Writes one absolute-position keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WritePositionKeyframeTrackAsset(EngineBinaryWriter writer, PositionKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WritePositionKeyframeAsset);
        }

        /// <summary>
        /// Reads one absolute-position keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static PositionKeyframeTrackAsset ReadPositionKeyframeTrackAsset(EngineBinaryReader reader) {
            return new PositionKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadPositionKeyframeAsset) ?? Array.Empty<PositionKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one additive-position keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WritePositionOffsetKeyframeTrackAsset(EngineBinaryWriter writer, PositionOffsetKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WritePositionKeyframeAsset);
        }

        /// <summary>
        /// Reads one additive-position keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static PositionOffsetKeyframeTrackAsset ReadPositionOffsetKeyframeTrackAsset(EngineBinaryReader reader) {
            return new PositionOffsetKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadPositionKeyframeAsset) ?? Array.Empty<PositionKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one scale keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WriteScaleKeyframeTrackAsset(EngineBinaryWriter writer, ScaleKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WritePositionKeyframeAsset);
        }

        /// <summary>
        /// Reads one scale keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static ScaleKeyframeTrackAsset ReadScaleKeyframeTrackAsset(EngineBinaryReader reader) {
            return new ScaleKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadPositionKeyframeAsset) ?? Array.Empty<PositionKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one rotation keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WriteRotationKeyframeTrackAsset(EngineBinaryWriter writer, RotationKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WriteRotationKeyframeAsset);
        }

        /// <summary>
        /// Reads one rotation keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static RotationKeyframeTrackAsset ReadRotationKeyframeTrackAsset(EngineBinaryReader reader) {
            return new RotationKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadRotationKeyframeAsset) ?? Array.Empty<RotationKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one platform-authored animation clip override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Override asset to serialize.</param>
        static void WriteAnimationClipPlatformOverrideAsset(EngineBinaryWriter writer, AnimationClipPlatformOverrideAsset asset) {
            writer.WriteString(asset.PlatformId ?? string.Empty);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteByte((byte)asset.Mode);
            writer.WriteArray(asset.PositionTracks, WritePlatformPositionKeyframeTrackAsset);
            writer.WriteArray(asset.PositionOffsetTracks, WritePlatformPositionKeyframeTrackAsset);
            writer.WriteArray(asset.ScaleTracks, WritePlatformPositionKeyframeTrackAsset);
            writer.WriteArray(asset.RotationTracks, WritePlatformRotationKeyframeTrackAsset);
        }

        /// <summary>
        /// Reads one platform-authored animation clip override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform override asset.</returns>
        static AnimationClipPlatformOverrideAsset ReadAnimationClipPlatformOverrideAsset(EngineBinaryReader reader) {
            return new AnimationClipPlatformOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                Mode = (AnimationClipPlatformOverrideMode)reader.ReadByte(),
                PositionTracks = reader.ReadArray(ReadPlatformPositionKeyframeTrackAsset) ?? Array.Empty<PlatformPositionKeyframeTrackAsset>(),
                PositionOffsetTracks = reader.ReadArray(ReadPlatformPositionKeyframeTrackAsset) ?? Array.Empty<PlatformPositionKeyframeTrackAsset>(),
                ScaleTracks = reader.ReadArray(ReadPlatformPositionKeyframeTrackAsset) ?? Array.Empty<PlatformPositionKeyframeTrackAsset>(),
                RotationTracks = reader.ReadArray(ReadPlatformRotationKeyframeTrackAsset) ?? Array.Empty<PlatformRotationKeyframeTrackAsset>()
            };
        }

        /// <summary>
        /// Writes one platform-authored position-style keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WritePlatformPositionKeyframeTrackAsset(EngineBinaryWriter writer, PlatformPositionKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WritePositionKeyframeAsset);
        }

        /// <summary>
        /// Reads one platform-authored position-style keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static PlatformPositionKeyframeTrackAsset ReadPlatformPositionKeyframeTrackAsset(EngineBinaryReader reader) {
            return new PlatformPositionKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadPositionKeyframeAsset) ?? Array.Empty<PositionKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one platform-authored rotation keyframe track payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Track asset to serialize.</param>
        static void WritePlatformRotationKeyframeTrackAsset(EngineBinaryWriter writer, PlatformRotationKeyframeTrackAsset asset) {
            writer.WriteArray(asset.Keyframes, WriteRotationKeyframeAsset);
        }

        /// <summary>
        /// Reads one platform-authored rotation keyframe track payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized track asset.</returns>
        static PlatformRotationKeyframeTrackAsset ReadPlatformRotationKeyframeTrackAsset(EngineBinaryReader reader) {
            return new PlatformRotationKeyframeTrackAsset {
                Keyframes = reader.ReadArray(ReadRotationKeyframeAsset) ?? Array.Empty<RotationKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one position-style keyframe payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Keyframe asset to serialize.</param>
        static void WritePositionKeyframeAsset(EngineBinaryWriter writer, PositionKeyframeAsset asset) {
            writer.WriteString(asset.FrameId ?? string.Empty);
            writer.WriteSingle(asset.Time);
            EditorAssetPayloadPrimitives.WriteFloat3Value(writer, asset.Value);
            WriteAnimationInterpolationMode(writer, asset.InterpolationMode);
        }

        /// <summary>
        /// Reads one position-style keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static PositionKeyframeAsset ReadPositionKeyframeAsset(EngineBinaryReader reader) {
            PositionKeyframeAsset asset = new PositionKeyframeAsset {
                FrameId = reader.ReadString(),
                Time = reader.ReadSingle(),
            };
            asset.Value = EditorAssetPayloadPrimitives.ReadFloat3Value(reader);
            asset.InterpolationMode = ReadAnimationInterpolationMode(reader);
            return asset;
        }

        /// <summary>
        /// Writes one rotation keyframe payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Keyframe asset to serialize.</param>
        static void WriteRotationKeyframeAsset(EngineBinaryWriter writer, RotationKeyframeAsset asset) {
            writer.WriteString(asset.FrameId ?? string.Empty);
            writer.WriteSingle(asset.Time);
            EditorAssetPayloadPrimitives.WriteFloat4Value(writer, asset.Value);
            WriteAnimationInterpolationMode(writer, asset.InterpolationMode);
        }

        /// <summary>
        /// Reads one rotation keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static RotationKeyframeAsset ReadRotationKeyframeAsset(EngineBinaryReader reader) {
            RotationKeyframeAsset asset = new RotationKeyframeAsset {
                FrameId = reader.ReadString(),
                Time = reader.ReadSingle(),
            };
            asset.Value = EditorAssetPayloadPrimitives.ReadFloat4Value(reader);
            asset.InterpolationMode = ReadAnimationInterpolationMode(reader);
            return asset;
        }

        /// <summary>
        /// Writes one animation interpolation mode value.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">Interpolation mode to serialize.</param>
        static void WriteAnimationInterpolationMode(EngineBinaryWriter writer, AnimationInterpolationMode value) {
            writer.WriteByte((byte)value);
        }

        /// <summary>
        /// Reads one animation interpolation mode value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized interpolation mode.</returns>
        static AnimationInterpolationMode ReadAnimationInterpolationMode(EngineBinaryReader reader) {
            return (AnimationInterpolationMode)reader.ReadByte();
        }
    }
}

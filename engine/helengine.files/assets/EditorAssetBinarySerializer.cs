using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes and deserializes editor asset payloads using the engine's minimal HELE binary format.
    /// </summary>
    public static class EditorAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for editor-authored binary files.
        /// </summary>
        public const ushort FormatId = 1;

        /// <summary>
        /// Record kind used for serialized asset payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.Asset;

        /// <summary>
        /// Serializer version for the current editor asset payload layout.
        /// </summary>
        public const byte CurrentVersion = 24;

        /// <summary>
        /// Version marker written into current scene entity payloads.
        /// </summary>
        const byte SceneEntityPayloadVersion = 8;

        /// <summary>
        /// Payload description used when a stored header carries a foreign format id.
        /// </summary>
        const string FormatIdMismatchSubject = "asset binary";

        /// <summary>
        /// Payload description used when a stored header carries an unexpected record kind.
        /// </summary>
        const string RecordMismatchSubject = "asset";

        /// <summary>
        /// Payload description used when a stored header carries an unsupported serializer version.
        /// </summary>
        const string VersionMismatchSubject = "Editor asset";

        /// <summary>
        /// Recovery guidance appended to the version mismatch message for stale authored assets.
        /// </summary>
        const string RegenerateInstruction = "Regenerate the authored asset.";

        /// <summary>
        /// Payload endianness used by the current editor asset format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes an asset to the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Destination stream for the asset payload.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        public static void Serialize(Stream stream, Asset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ValidateDeterministicSceneOverrides(asset);
            EditorAssetBinaryValueKind valueKind = GetValueKind(asset);
            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                FormatId,
                (ushort)RecordKind,
                (ushort)valueKind);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            WriteAssetPayload(writer, asset);
        }

        /// <summary>
        /// Deserializes an asset from the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Source stream containing the asset payload.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream) {
            EngineBinaryHeader header;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeaderWithDispatchedValueKind(
                stream,
                FormatId,
                (ushort)RecordKind,
                CurrentVersion,
                FormatIdMismatchSubject,
                RecordMismatchSubject,
                VersionMismatchSubject,
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                RegenerateInstruction,
                out header);

            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind);
        }

        /// <summary>
        /// Deserializes an asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream, EngineBinaryHeader header) {
            using EngineBinaryReader reader = VersionedBinaryPayload.ValidateHeaderWithDispatchedValueKind(
                stream,
                header,
                FormatId,
                (ushort)RecordKind,
                CurrentVersion,
                FormatIdMismatchSubject,
                RecordMismatchSubject,
                VersionMismatchSubject,
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                RegenerateInstruction);

            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind);
        }

        /// <summary>
        /// Resolves the value kind identifier for a runtime asset instance.
        /// </summary>
        /// <param name="asset">Asset instance to classify.</param>
        /// <returns>Format-specific value kind identifier.</returns>
        static EditorAssetBinaryValueKind GetValueKind(Asset asset) {
            IEditorAssetPayloadSerializer serializer = EditorAssetPayloadSerializerRegistry.FindByAsset(asset);
            if (serializer != null) {
                return serializer.ValueKind;
            }

            if (asset is AnimationClipAsset) {
                return EditorAssetBinaryValueKind.AnimationClipAsset;
            } else if (asset is SceneAsset) {
                return EditorAssetBinaryValueKind.SceneAsset;
            } else if (asset is BlueprintAsset) {
                return EditorAssetBinaryValueKind.BlueprintAsset;
            }

            throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not supported by the editor binary serializer.");
        }

        /// <summary>
        /// Writes the payload for a specific runtime asset instance.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        static void WriteAssetPayload(EngineBinaryWriter writer, Asset asset) {
            IEditorAssetPayloadSerializer serializer = EditorAssetPayloadSerializerRegistry.FindByAsset(asset);
            if (serializer != null) {
                serializer.Write(writer, asset);
                return;
            }

            if (asset is AnimationClipAsset animationClipAsset) {
                WriteAnimationClipAsset(writer, animationClipAsset);
                return;
            } else if (asset is SceneAsset sceneAsset) {
                WriteSceneAsset(writer, sceneAsset);
                return;
            } else if (asset is BlueprintAsset blueprintAsset) {
                WriteBlueprintAsset(writer, blueprintAsset);
                return;
            }

            throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not supported by the editor binary serializer.");
        }

        /// <summary>
        /// Validates scene and blueprint override scopes before emitting any bytes.
        /// </summary>
        static void ValidateDeterministicSceneOverrides(Asset asset) {
            SceneAsset sceneAsset = asset as SceneAsset;
            if (sceneAsset != null) {
                for (int index = 0; index < (sceneAsset.RootEntities?.Length ?? 0); index++) {
                    ValidateDeterministicSceneEntityOverrides(sceneAsset.RootEntities[index]);
                }
                return;
            }

            BlueprintAsset blueprintAsset = asset as BlueprintAsset;
            if (blueprintAsset != null) {
                ValidateDeterministicSceneEntityOverrides(blueprintAsset.RootEntity);
            }
        }

        /// <summary>Validates one scene entity and all nested entities.</summary>
        static void ValidateDeterministicSceneEntityOverrides(SceneEntityAsset entity) {
            if (entity == null) {
                return;
            }

            EnsureUniquePlatformOverrideScopes(entity.PlatformExistenceOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            EnsureUniquePlatformOverrideScopes(entity.PlatformTransformOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            EnsureUniquePlatformOverrideScopes(entity.PlatformComponentOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            for (int index = 0; index < (entity.Children?.Length ?? 0); index++) {
                ValidateDeterministicSceneEntityOverrides(entity.Children[index]);
            }
        }

        /// <summary>
        /// Reads an asset payload using the supplied value kind.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="valueKind">Format-specific value kind identifier.</param>
        /// <returns>Deserialized asset instance.</returns>
        static Asset ReadAssetPayload(EngineBinaryReader reader, EditorAssetBinaryValueKind valueKind) {
            IEditorAssetPayloadSerializer serializer = EditorAssetPayloadSerializerRegistry.FindByValueKind(valueKind);
            if (serializer != null) {
                return serializer.Read(reader);
            }

            switch (valueKind) {
                case EditorAssetBinaryValueKind.AnimationClipAsset:
                    return ReadAnimationClipAsset(reader);
                case EditorAssetBinaryValueKind.SceneAsset:
                    return ReadSceneAsset(reader);
                case EditorAssetBinaryValueKind.BlueprintAsset:
                    return ReadBlueprintAsset(reader);
                default:
                    throw new InvalidOperationException($"Unsupported asset value kind '{(ushort)valueKind}'.");
            }
        }

        /// <summary>
        /// Writes an animation clip asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Animation clip asset to serialize.</param>
        static void WriteAnimationClipAsset(EngineBinaryWriter writer, AnimationClipAsset asset) {
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(asset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, asset);
            writer.WriteSingle(asset.Duration);
            writer.WriteArray(asset.PositionTracks, WritePositionKeyframeTrackAsset);
            writer.WriteArray(asset.PositionOffsetTracks, WritePositionOffsetKeyframeTrackAsset);
            writer.WriteArray(asset.ScaleTracks, WriteScaleKeyframeTrackAsset);
            writer.WriteArray(asset.RotationTracks, WriteRotationKeyframeTrackAsset);
            writer.WriteArray(asset.PlatformOverrides?
                .OrderBy(platformOverride => platformOverride?.PlatformId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(platformOverride => platformOverride?.EnvironmentId ?? string.Empty, StringComparer.Ordinal)
                .ToArray(), WriteAnimationClipPlatformOverrideAsset);
        }

        /// <summary>
        /// Reads an animation clip asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized animation clip asset.</returns>
        static AnimationClipAsset ReadAnimationClipAsset(EngineBinaryReader reader) {
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

        /// <summary>
        /// Writes a scene asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene asset to serialize.</param>
        static void WriteSceneAsset(EngineBinaryWriter writer, SceneAsset asset) {
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(asset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, asset);
            writer.WriteArray(asset.RootEntities, WriteSceneEntityAsset);
            writer.WriteArray(SortSceneAssetReferences(asset.AssetReferences), WriteSceneAssetReference);
            writer.WriteUInt32(asset.Physics3DSceneFeatureFlags);
            WriteSceneSettingsAsset(writer, asset.SceneSettings);
        }

        /// <summary>
        /// Reads a scene asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset.</returns>
        static SceneAsset ReadSceneAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            SceneAsset asset = new SceneAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.RootEntities = ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>();
            asset.AssetReferences = ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>();
            asset.Physics3DSceneFeatureFlags = reader.ReadUInt32();
            asset.SceneSettings = ReadSceneSettingsAsset(reader);
            return asset;
        }

        /// <summary>
        /// Writes a blueprint asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Blueprint asset to serialize.</param>
        static void WriteBlueprintAsset(EngineBinaryWriter writer, BlueprintAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint assets must define exactly one root entity.");
            }

            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(asset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, asset);
            WriteSceneEntityAsset(writer, asset.RootEntity);
            writer.WriteArray(SortSceneAssetReferences(asset.AssetReferences), WriteSceneAssetReference);
        }

        /// <summary>
        /// Reads a blueprint asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized blueprint asset.</returns>
        static BlueprintAsset ReadBlueprintAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            BlueprintAsset asset = new BlueprintAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.RootEntity = ReadSceneEntityAsset(reader);
            if (asset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint assets must define exactly one root entity.");
            }

            asset.AssetReferences = ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>();
            return asset;
        }

        /// <summary>
        /// Writes scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="sceneSettings">Scene settings to serialize.</param>
        static void WriteSceneSettingsAsset(EngineBinaryWriter writer, SceneSettingsAsset sceneSettings) {
            WriteSceneCanvasProfile(writer, sceneSettings.CanvasProfile);
            writer.WriteByte(sceneSettings.DontUnload ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="reader">Source reader positioned at the scene settings payload.</param>
        /// <returns>Deserialized scene settings.</returns>
        static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader) {
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = ReadSceneCanvasProfile(reader)
            };
            sceneSettings.DontUnload = ReadBooleanByte(reader, "scene settings");
            return sceneSettings;
        }

        /// <summary>
        /// Writes one authored scene canvas profile.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="canvasProfile">Canvas profile to serialize.</param>
        static void WriteSceneCanvasProfile(EngineBinaryWriter writer, SceneCanvasProfile canvasProfile) {
            writer.WriteInt32(canvasProfile.Width);
            writer.WriteInt32(canvasProfile.Height);
        }

        /// <summary>
        /// Reads one authored scene canvas profile.
        /// </summary>
        /// <param name="reader">Source reader positioned at the canvas profile payload.</param>
        /// <returns>Deserialized scene canvas profile.</returns>
        static SceneCanvasProfile ReadSceneCanvasProfile(EngineBinaryReader reader) {
            return new SceneCanvasProfile {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Reads a boolean encoded as one byte where zero means false and one means true.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded boolean value.</param>
        /// <param name="context">Description of the payload being decoded.</param>
        /// <returns>Decoded boolean value.</returns>
        static bool ReadBooleanByte(EngineBinaryReader reader, string context) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (string.IsNullOrWhiteSpace(context)) {
                throw new ArgumentException("Boolean read context is required.", nameof(context));
            }

            byte value = reader.ReadByte();
            if (value == 0) {
                return false;
            }
            if (value == 1) {
                return true;
            }

            throw new InvalidOperationException($"Unsupported {context} boolean value '{value}'.");
        }

        /// <summary>
        /// Writes one serialized scene entity payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity asset to serialize.</param>
        static void WriteSceneEntityAsset(EngineBinaryWriter writer, SceneEntityAsset asset) {
            writer.WriteByte(SceneEntityPayloadVersion);
            writer.WriteUInt32(asset.Id);
            writer.WriteString(asset.Name);
            writer.WriteByte(asset.IsStatic ? (byte)1 : (byte)0);
            writer.WriteByte(asset.Enabled ? (byte)1 : (byte)0);
            writer.WriteUInt16(asset.LayerMask);
            writer.WriteFloat3(asset.LocalPosition);
            writer.WriteFloat3(asset.LocalScale);
            writer.WriteFloat4(asset.LocalOrientation);
            writer.WriteArray(asset.Components, WriteSceneComponentAssetRecordValue);
            writer.WriteArray(SortSceneEntityPlatformExistenceOverrides(asset.PlatformExistenceOverrides), WriteSceneEntityPlatformExistenceOverrideAsset);
            writer.WriteArray(SortSceneEntityPlatformTransformOverrides(asset.PlatformTransformOverrides), WriteSceneEntityPlatformTransformOverrideAsset);
            writer.WriteArray(SortSceneEntityPlatformComponentOverrides(asset.PlatformComponentOverrides), WriteSceneEntityPlatformComponentOverrideAsset);
            writer.WriteArray(asset.Children, WriteSceneEntityAsset);
        }

        /// <summary>
        /// Reads one serialized scene entity payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity asset.</returns>
        static SceneEntityAsset ReadSceneEntityAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte payloadVersion = reader.ReadByte();
            if (payloadVersion != SceneEntityPayloadVersion) {
                throw new InvalidOperationException($"Unsupported scene entity payload version '{payloadVersion}'.");
            }

            uint id = reader.ReadUInt32();
            string name = reader.ReadString();
            bool isStatic = reader.ReadByte() != 0;
            bool enabled = reader.ReadByte() != 0;
            ushort layerMask = reader.ReadUInt16();
            float3 localPosition = reader.ReadFloat3();
            float3 localScale = reader.ReadFloat3();
            float4 localOrientation = reader.ReadFloat4();
            SceneComponentAssetRecord[] components = ReadSceneComponentAssetRecordArray(reader) ?? Array.Empty<SceneComponentAssetRecord>();
            SceneEntityPlatformExistenceOverrideAsset[] platformExistenceOverrides = reader.ReadArray(ReadSceneEntityPlatformExistenceOverrideAsset) ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = reader.ReadArray(ReadSceneEntityPlatformTransformOverrideAsset) ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = reader.ReadArray(ReadSceneEntityPlatformComponentOverrideAsset) ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

            return new SceneEntityAsset {
                Id = id,
                Name = name,
                IsStatic = isStatic,
                Enabled = enabled,
                LayerMask = layerMask,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = localOrientation,
                Components = components,
                PlatformExistenceOverrides = platformExistenceOverrides,
                PlatformTransformOverrides = platformTransformOverrides,
                PlatformComponentOverrides = platformComponentOverrides,
                Children = ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Orders entity existence overrides by scope and then by their serialized value.
        /// </summary>
        static SceneEntityPlatformExistenceOverrideAsset[] SortSceneEntityPlatformExistenceOverrides(SceneEntityPlatformExistenceOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => item?.Exists == true ? 1 : 0)
                .ToArray();
        }

        /// <summary>
        /// Orders entity transform overrides by scope and then by their serialized value.
        /// </summary>
        static SceneEntityPlatformTransformOverrideAsset[] SortSceneEntityPlatformTransformOverrides(SceneEntityPlatformTransformOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => item?.HasLocalPositionOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalPosition.X ?? 0f)
                .ThenBy(item => item?.LocalPosition.Y ?? 0f)
                .ThenBy(item => item?.LocalPosition.Z ?? 0f)
                .ThenBy(item => item?.HasLocalScaleOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalScale.X ?? 0f)
                .ThenBy(item => item?.LocalScale.Y ?? 0f)
                .ThenBy(item => item?.LocalScale.Z ?? 0f)
                .ThenBy(item => item?.HasLocalOrientationOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalOrientation.X ?? 0f)
                .ThenBy(item => item?.LocalOrientation.Y ?? 0f)
                .ThenBy(item => item?.LocalOrientation.Z ?? 0f)
                .ThenBy(item => item?.LocalOrientation.W ?? 0f)
                .ToArray();
        }

        /// <summary>
        /// Orders entity component overrides by scope and every serialized nested field.
        /// </summary>
        static SceneEntityPlatformComponentOverrideAsset[] SortSceneEntityPlatformComponentOverrides(SceneEntityPlatformComponentOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => string.Join("\u001f", (item?.RemovedComponentKeys ?? Array.Empty<string>())
                    .OrderBy(key => key ?? string.Empty, StringComparer.Ordinal)), StringComparer.Ordinal)
                .ThenBy(item => string.Join("\u001f", SortSceneEntityPlatformAddedComponents(item?.AddedComponents ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>())
                    .Select(SerializeSceneComponentSortKey)), StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Rejects multiple override records for one exact platform/environment scope before bytes are emitted.
        /// </summary>
        static void EnsureUniquePlatformOverrideScopes<T>(
            T[] overrides,
            Func<T, string> platformSelector,
            Func<T, string> environmentSelector) {
            if (overrides == null) {
                return;
            }

            Dictionary<string, HashSet<string>> environmentsByPlatform = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < overrides.Length; index++) {
                string platformId = NormalizeOverrideScopeIdentifier(platformSelector(overrides[index]));
                string environmentId = NormalizeOverrideScopeIdentifier(environmentSelector(overrides[index]));
                if (!environmentsByPlatform.TryGetValue(platformId, out HashSet<string> environments)) {
                    environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    environmentsByPlatform.Add(platformId, environments);
                }

                if (!environments.Add(environmentId)) {
                    throw new InvalidOperationException($"Duplicate platform override scope '{platformId}/{environmentId}'.");
                }
            }
        }

        /// <summary>
        /// Normalizes one serialized override scope identifier using the public scope identity rules.
        /// </summary>
        static string NormalizeOverrideScopeIdentifier(string value) {
            return (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Orders platform-added component records by stable key and complete payload identity.
        /// </summary>
        static SceneEntityPlatformAddedComponentAsset[] SortSceneEntityPlatformAddedComponents(SceneEntityPlatformAddedComponentAsset[] components) {
            return components?.OrderBy(item => item?.Component?.ComponentKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item?.Component?.ComponentTypeId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item?.Component?.ComponentIndex ?? 0)
                .ThenBy(item => SerializeSceneComponentSortKey(item), StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Creates a total ordinal sort key for a platform-added component payload.
        /// </summary>
        static string SerializeSceneComponentSortKey(SceneEntityPlatformAddedComponentAsset item) {
            SceneComponentAssetRecord component = item?.Component;
            if (component == null) {
                return string.Empty;
            }

            return (component.ComponentKey ?? string.Empty) + "\u001f"
                + (component.ComponentTypeId ?? string.Empty) + "\u001f"
                + component.ComponentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u001f"
                + Convert.ToBase64String(component.Payload ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Writes one serialized scene entity existence override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity existence override to serialize.</param>
        static void WriteSceneEntityPlatformExistenceOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformExistenceOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteByte(asset.Exists ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads one serialized scene entity existence override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity existence override.</returns>
        static SceneEntityPlatformExistenceOverrideAsset ReadSceneEntityPlatformExistenceOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformExistenceOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                Exists = reader.ReadByte() != 0
            };
        }

        /// <summary>
        /// Writes one serialized scene entity transform override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity transform override to serialize.</param>
        static void WriteSceneEntityPlatformTransformOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformTransformOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteByte(asset.HasLocalPositionOverride ? (byte)1 : (byte)0);
            writer.WriteFloat3(asset.LocalPosition);
            writer.WriteByte(asset.HasLocalScaleOverride ? (byte)1 : (byte)0);
            writer.WriteFloat3(asset.LocalScale);
            writer.WriteByte(asset.HasLocalOrientationOverride ? (byte)1 : (byte)0);
            writer.WriteFloat4(asset.LocalOrientation);
        }

        /// <summary>
        /// Reads one serialized scene entity transform override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity transform override.</returns>
        static SceneEntityPlatformTransformOverrideAsset ReadSceneEntityPlatformTransformOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                HasLocalPositionOverride = reader.ReadByte() != 0,
                LocalPosition = reader.ReadFloat3(),
                HasLocalScaleOverride = reader.ReadByte() != 0,
                LocalScale = reader.ReadFloat3(),
                HasLocalOrientationOverride = reader.ReadByte() != 0,
                LocalOrientation = reader.ReadFloat4()
            };
        }

        /// <summary>
        /// Writes one serialized scene entity component existence override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity component existence override to serialize.</param>
        static void WriteSceneEntityPlatformComponentOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformComponentOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteArray(asset.RemovedComponentKeys?.OrderBy(key => key ?? string.Empty, StringComparer.Ordinal).ToArray(), EditorAssetPayloadPrimitives.WriteStringValue);
            writer.WriteArray(SortSceneEntityPlatformAddedComponents(asset.AddedComponents), WriteSceneEntityPlatformAddedComponentAsset);
        }

        /// <summary>
        /// Writes one serialized platform-only component payload attached to a scene entity.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Platform-only component payload to serialize.</param>
        static void WriteSceneEntityPlatformAddedComponentAsset(EngineBinaryWriter writer, SceneEntityPlatformAddedComponentAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.Component == null) {
                throw new InvalidOperationException("Platform-added component assets must define a serialized component record.");
            }

            WriteSceneComponentAssetRecord(writer, asset.Component);
        }

        /// <summary>
        /// Reads one serialized scene entity component existence override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity component existence override.</returns>
        static SceneEntityPlatformComponentOverrideAsset ReadSceneEntityPlatformComponentOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                RemovedComponentKeys = reader.ReadArray(EditorAssetPayloadPrimitives.ReadStringValue) ?? Array.Empty<string>(),
                AddedComponents = reader.ReadArray(ReadSceneEntityPlatformAddedComponentAsset) ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>()
            };
        }

        /// <summary>
        /// Reads one serialized platform-only component payload attached to a scene entity.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-only component payload.</returns>
        static SceneEntityPlatformAddedComponentAsset ReadSceneEntityPlatformAddedComponentAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformAddedComponentAsset {
                Component = ReadSceneComponentAssetRecord(reader)
            };
        }

        /// <summary>
        /// Writes one serialized scene asset reference payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="reference">Scene asset reference to serialize.</param>
        static void WriteSceneAssetReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
            writer.WriteString(reference.ContentHash);
        }

        /// <summary>
        /// Reads one serialized scene asset reference payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset reference.</returns>
        static SceneAssetReference ReadSceneAssetReference(EngineBinaryReader reader) {
            return SceneAssetReferenceFactory.ReadRequiredCurrentReference(reader);
        }

        /// <summary>
        /// Reads an array of scene asset references from the payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset references.</returns>
        static SceneAssetReference[] ReadSceneAssetReferenceArray(EngineBinaryReader reader) {
            return reader.ReadArray(SceneAssetReferenceFactory.ReadRequiredCurrentReference);
        }

        /// <summary>
        /// Orders scene and blueprint references by every serialized identity field.
        /// </summary>
        /// <param name="references">References to order.</param>
        /// <returns>Ordinally ordered references.</returns>
        static SceneAssetReference[] SortSceneAssetReferences(SceneAssetReference[] references) {
            return references?.OrderBy(reference => (int)(reference?.SourceKind ?? default(SceneAssetReferenceSourceKind)))
                .ThenBy(reference => reference?.RelativePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.ProviderId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.AssetId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.ContentHash ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Writes one serialized scene component record.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="record">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecord(EngineBinaryWriter writer, SceneComponentAssetRecord record) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            writer.WriteString(record.ComponentKey);
            writer.WriteString(record.ComponentTypeId);
            writer.WriteInt32(record.ComponentIndex);
            writer.WriteByteArray(record.Payload);
        }

        /// <summary>
        /// Reads one serialized scene component record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene component record.</returns>
        static SceneComponentAssetRecord ReadSceneComponentAssetRecord(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneComponentAssetRecord {
                ComponentKey = reader.ReadString(),
                ComponentTypeId = reader.ReadString(),
                ComponentIndex = reader.ReadInt32(),
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
            };
        }

        /// <summary>
        /// Reads one array of serialized scene component records.
        /// </summary>
        /// <param name="reader">Source reader positioned at the component array payload.</param>
        /// <returns>Decoded component records or null when the source payload was null.</returns>
        static SceneComponentAssetRecord[] ReadSceneComponentAssetRecordArray(EngineBinaryReader reader) {
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<SceneComponentAssetRecord>();
            }

            SceneComponentAssetRecord[] values = new SceneComponentAssetRecord[length];
            for (int index = 0; index < values.Length; index++) {
                values[index] = ReadSceneComponentAssetRecord(reader);
            }

            return values;
        }

        /// <summary>
        /// Reads a scene entity array using the current scene-entity layout.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <returns>Decoded scene entity array or null when the source payload was null.</returns>
        static SceneEntityAsset[] ReadSceneEntityAssetArray(EngineBinaryReader reader) {
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<SceneEntityAsset>();
            }

            SceneEntityAsset[] values = new SceneEntityAsset[length];
            for (int index = 0; index < values.Length; index++) {
                values[index] = ReadSceneEntityAsset(reader);
            }

            return values;
        }

        /// <summary>
        /// Writes one scene-component record into a scene entity using the current scene payload version.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecordValue(EngineBinaryWriter writer, SceneComponentAssetRecord asset) {
            WriteSceneComponentAssetRecord(writer, asset);
        }

    }
}

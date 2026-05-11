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
        public const byte CurrentVersion = 10;

        /// <summary>
        /// Version marker written into scene entity payloads that include stable ids.
        /// </summary>
        const byte SceneEntityPayloadVersion = 3;

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
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            return Deserialize(stream, header);
        }

        /// <summary>
        /// Deserializes an asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream, EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind, header.Version);
        }

        /// <summary>
        /// Validates that the provided header matches the editor asset format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header.FormatId != FormatId) {
                throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected asset record kind '{header.RecordKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{header.Version}'.");
            }
        }

        /// <summary>
        /// Resolves the value kind identifier for a runtime asset instance.
        /// </summary>
        /// <param name="asset">Asset instance to classify.</param>
        /// <returns>Format-specific value kind identifier.</returns>
        static EditorAssetBinaryValueKind GetValueKind(Asset asset) {
            if (asset is TextureAsset) {
                return EditorAssetBinaryValueKind.TextureAsset;
            } else if (asset is ModelAsset) {
                return EditorAssetBinaryValueKind.ModelAsset;
            } else if (asset is ShaderAsset) {
                return EditorAssetBinaryValueKind.ShaderAsset;
            } else if (asset is TextAsset) {
                return EditorAssetBinaryValueKind.TextAsset;
            } else if (asset is MaterialAsset) {
                return EditorAssetBinaryValueKind.MaterialAsset;
            } else if (asset is Ps2MaterialAsset) {
                return EditorAssetBinaryValueKind.Ps2MaterialAsset;
            } else if (asset is AnimationClipAsset) {
                return EditorAssetBinaryValueKind.AnimationClipAsset;
            } else if (asset is SceneAsset) {
                return EditorAssetBinaryValueKind.SceneAsset;
            }

            throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not supported by the editor binary serializer.");
        }

        /// <summary>
        /// Writes the payload for a specific runtime asset instance.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        static void WriteAssetPayload(EngineBinaryWriter writer, Asset asset) {
            if (asset is TextureAsset textureAsset) {
                WriteTextureAsset(writer, textureAsset);
                return;
            } else if (asset is ModelAsset modelAsset) {
                WriteModelAsset(writer, modelAsset);
                return;
            } else if (asset is ShaderAsset shaderAsset) {
                WriteShaderAsset(writer, shaderAsset);
                return;
            } else if (asset is TextAsset textAsset) {
                WriteTextAsset(writer, textAsset);
                return;
            } else if (asset is MaterialAsset materialAsset) {
                WriteMaterialAsset(writer, materialAsset);
                return;
            } else if (asset is Ps2MaterialAsset ps2MaterialAsset) {
                WritePs2MaterialAsset(writer, ps2MaterialAsset);
                return;
            } else if (asset is AnimationClipAsset animationClipAsset) {
                WriteAnimationClipAsset(writer, animationClipAsset);
                return;
            } else if (asset is SceneAsset sceneAsset) {
                WriteSceneAsset(writer, sceneAsset);
                return;
            }

            throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not supported by the editor binary serializer.");
        }

        /// <summary>
        /// Reads an asset payload using the supplied value kind.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="valueKind">Format-specific value kind identifier.</param>
        /// <returns>Deserialized asset instance.</returns>
        static Asset ReadAssetPayload(EngineBinaryReader reader, EditorAssetBinaryValueKind valueKind, byte version) {
            switch (valueKind) {
                case EditorAssetBinaryValueKind.TextureAsset:
                    return ReadTextureAsset(reader);
                case EditorAssetBinaryValueKind.ModelAsset:
                    return ReadModelAsset(reader, version);
                case EditorAssetBinaryValueKind.ShaderAsset:
                    return ReadShaderAsset(reader);
                case EditorAssetBinaryValueKind.TextAsset:
                    return ReadTextAsset(reader);
                case EditorAssetBinaryValueKind.MaterialAsset:
                    return ReadMaterialAsset(reader, version);
                case EditorAssetBinaryValueKind.Ps2MaterialAsset:
                    return ReadPs2MaterialAsset(reader);
                case EditorAssetBinaryValueKind.AnimationClipAsset:
                    return ReadAnimationClipAsset(reader);
                case EditorAssetBinaryValueKind.SceneAsset:
                    return ReadSceneAsset(reader, version);
                default:
                    throw new InvalidOperationException($"Unsupported asset value kind '{(ushort)valueKind}'.");
            }
        }

        /// <summary>
        /// Writes a texture asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Texture asset to serialize.</param>
        static void WriteTextureAsset(EngineBinaryWriter writer, TextureAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteUInt16(asset.Width);
            writer.WriteUInt16(asset.Height);
            writer.WriteByteArray(asset.Colors);
        }

        /// <summary>
        /// Reads a texture asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized texture asset.</returns>
        static TextureAsset ReadTextureAsset(EngineBinaryReader reader) {
            return new TextureAsset {
                Id = reader.ReadString(),
                Width = reader.ReadUInt16(),
                Height = reader.ReadUInt16(),
                Colors = reader.ReadByteArray()
            };
        }

        /// <summary>
        /// Writes a model asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Model asset to serialize.</param>
        static void WriteModelAsset(EngineBinaryWriter writer, ModelAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteArray(asset.Positions, WriteFloat3);
            writer.WriteArray(asset.Normals, WriteFloat3);
            writer.WriteArray(asset.TexCoords, WriteFloat2);
            writer.WriteArray(asset.Indices16, WriteUInt16Value);
            writer.WriteArray(asset.Indices32, WriteUInt32Value);
            writer.WriteArray(asset.Submeshes, WriteModelSubmeshAsset);
            writer.WriteByteArray(asset.Ps2PackedMeshBytes);
        }

        /// <summary>
        /// Reads a model asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model asset.</returns>
        static ModelAsset ReadModelAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            return new ModelAsset {
                Id = reader.ReadString(),
                Positions = reader.ReadArray(ReadFloat3),
                Normals = reader.ReadArray(ReadFloat3),
                TexCoords = reader.ReadArray(ReadFloat2),
                Indices16 = reader.ReadArray(ReadUInt16Value),
                Indices32 = reader.ReadArray(ReadUInt32Value),
                Submeshes = reader.ReadArray(ReadModelSubmeshAsset),
                Ps2PackedMeshBytes = reader.ReadByteArray()
            };
        }

        /// <summary>
        /// Writes one model submesh payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="submesh">Model submesh to serialize.</param>
        static void WriteModelSubmeshAsset(EngineBinaryWriter writer, ModelSubmeshAsset submesh) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (submesh == null) {
                throw new ArgumentNullException(nameof(submesh));
            }

            writer.WriteString(submesh.MaterialSlotName ?? string.Empty);
            writer.WriteInt32(submesh.IndexStart);
            writer.WriteInt32(submesh.IndexCount);
        }

        /// <summary>
        /// Reads one model submesh payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model submesh.</returns>
        static ModelSubmeshAsset ReadModelSubmeshAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new ModelSubmeshAsset {
                MaterialSlotName = reader.ReadString(),
                IndexStart = reader.ReadInt32(),
                IndexCount = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Writes a text asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Text asset to serialize.</param>
        static void WriteTextAsset(EngineBinaryWriter writer, TextAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteString(asset.Text);
        }

        /// <summary>
        /// Reads a text asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized text asset.</returns>
        static TextAsset ReadTextAsset(EngineBinaryReader reader) {
            return new TextAsset {
                Id = reader.ReadString(),
                Text = reader.ReadString()
            };
        }

        /// <summary>
        /// Writes a material asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Material asset to serialize.</param>
        static void WriteMaterialAsset(EngineBinaryWriter writer, MaterialAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteString(asset.ShaderAssetId);
            writer.WriteString(asset.VertexProgram);
            writer.WriteString(asset.PixelProgram);
            writer.WriteString(asset.Variant);
            writer.WriteString(asset.DiffuseTextureAssetId);
            writer.WriteByte(asset.CastsShadows ? (byte)1 : (byte)0);
            writer.WriteByte(asset.ReceivesShadows ? (byte)1 : (byte)0);
            WriteMaterialRenderState(writer, asset.RenderState);
            writer.WriteArray(asset.ConstantBuffers, WriteMaterialConstantBufferAsset);
        }

        /// <summary>
        /// Reads a material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material asset.</returns>
        static MaterialAsset ReadMaterialAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            var materialAsset = new MaterialAsset {
                Id = reader.ReadString(),
                ShaderAssetId = reader.ReadString(),
                VertexProgram = reader.ReadString(),
                PixelProgram = reader.ReadString(),
                Variant = reader.ReadString(),
                DiffuseTextureAssetId = reader.ReadString(),
                CastsShadows = reader.ReadByte() != 0,
                ReceivesShadows = reader.ReadByte() != 0,
                RenderState = ReadMaterialRenderState(reader),
                ConstantBuffers = reader.ReadArray(ReadMaterialConstantBufferAsset) ?? Array.Empty<MaterialConstantBufferAsset>()
            };

            return materialAsset;
        }

        /// <summary>
        /// Writes a PS2 material asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">PS2 material asset to serialize.</param>
        static void WritePs2MaterialAsset(EngineBinaryWriter writer, Ps2MaterialAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteString(asset.RendererFamilyId);
            writer.WriteInt32((int)asset.LightingMode);
            writer.WriteInt32((int)asset.AlphaMode);
            writer.WriteInt32((int)asset.RenderClass);
            writer.WriteByte(asset.BaseColorR);
            writer.WriteByte(asset.BaseColorG);
            writer.WriteByte(asset.BaseColorB);
            writer.WriteByte(asset.BaseColorA);
            writer.WriteString(asset.TextureRelativePath);
            writer.WriteByte(asset.DoubleSided ? (byte)1 : (byte)0);
            writer.WriteByte(asset.CastShadows ? (byte)1 : (byte)0);
            writer.WriteByte(asset.UseVertexColor ? (byte)1 : (byte)0);
            writer.WriteByte(asset.ExpensiveModeAllowed ? (byte)1 : (byte)0);
            writer.WriteSingle(asset.Roughness);
            writer.WriteSingle(asset.SpecularStrength);
            writer.WriteSingle(asset.EmissiveStrength);
        }

        /// <summary>
        /// Reads a PS2 material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized PS2 material asset.</returns>
        static Ps2MaterialAsset ReadPs2MaterialAsset(EngineBinaryReader reader) {
            return new Ps2MaterialAsset {
                Id = reader.ReadString(),
                RendererFamilyId = reader.ReadString(),
                LightingMode = (Ps2MaterialLightingMode)reader.ReadInt32(),
                AlphaMode = (Ps2MaterialAlphaMode)reader.ReadInt32(),
                RenderClass = (Ps2RenderClass)reader.ReadInt32(),
                BaseColorR = reader.ReadByte(),
                BaseColorG = reader.ReadByte(),
                BaseColorB = reader.ReadByte(),
                BaseColorA = reader.ReadByte(),
                TextureRelativePath = reader.ReadString(),
                DoubleSided = reader.ReadByte() != 0,
                CastShadows = reader.ReadByte() != 0,
                UseVertexColor = reader.ReadByte() != 0,
                ExpensiveModeAllowed = reader.ReadByte() != 0,
                Roughness = reader.ReadSingle(),
                SpecularStrength = reader.ReadSingle(),
                EmissiveStrength = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes an animation clip asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Animation clip asset to serialize.</param>
        static void WriteAnimationClipAsset(EngineBinaryWriter writer, AnimationClipAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteSingle(asset.Duration);
            writer.WriteArray(asset.PositionTracks, WritePositionKeyframeTrackAsset);
            writer.WriteArray(asset.PositionOffsetTracks, WritePositionOffsetKeyframeTrackAsset);
            writer.WriteArray(asset.ScaleTracks, WriteScaleKeyframeTrackAsset);
            writer.WriteArray(asset.RotationTracks, WriteRotationKeyframeTrackAsset);
        }

        /// <summary>
        /// Reads an animation clip asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized animation clip asset.</returns>
        static AnimationClipAsset ReadAnimationClipAsset(EngineBinaryReader reader) {
            return new AnimationClipAsset {
                Id = reader.ReadString(),
                Duration = reader.ReadSingle(),
                PositionTracks = reader.ReadArray(ReadPositionKeyframeTrackAsset) ?? Array.Empty<PositionKeyframeTrackAsset>(),
                PositionOffsetTracks = reader.ReadArray(ReadPositionOffsetKeyframeTrackAsset) ?? Array.Empty<PositionOffsetKeyframeTrackAsset>(),
                ScaleTracks = reader.ReadArray(ReadScaleKeyframeTrackAsset) ?? Array.Empty<ScaleKeyframeTrackAsset>(),
                RotationTracks = reader.ReadArray(ReadRotationKeyframeTrackAsset) ?? Array.Empty<RotationKeyframeTrackAsset>()
            };
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
        /// Writes one position-style keyframe payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Keyframe asset to serialize.</param>
        static void WritePositionKeyframeAsset(EngineBinaryWriter writer, PositionKeyframeAsset asset) {
            writer.WriteSingle(asset.Time);
            WriteFloat3(writer, asset.Value);
            WriteAnimationInterpolationMode(writer, asset.InterpolationMode);
        }

        /// <summary>
        /// Reads one position-style keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static PositionKeyframeAsset ReadPositionKeyframeAsset(EngineBinaryReader reader) {
            return new PositionKeyframeAsset(
                reader.ReadSingle(),
                ReadFloat3(reader),
                ReadAnimationInterpolationMode(reader));
        }

        /// <summary>
        /// Writes one rotation keyframe payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Keyframe asset to serialize.</param>
        static void WriteRotationKeyframeAsset(EngineBinaryWriter writer, RotationKeyframeAsset asset) {
            writer.WriteSingle(asset.Time);
            WriteFloat4(writer, asset.Value);
            WriteAnimationInterpolationMode(writer, asset.InterpolationMode);
        }

        /// <summary>
        /// Reads one rotation keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static RotationKeyframeAsset ReadRotationKeyframeAsset(EngineBinaryReader reader) {
            return new RotationKeyframeAsset(
                reader.ReadSingle(),
                ReadFloat4(reader),
                ReadAnimationInterpolationMode(reader));
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
            writer.WriteString(asset.Id);
            writer.WriteArray(asset.RootEntities, WriteSceneEntityAsset);
            writer.WriteArray(asset.AssetReferences, WriteSceneAssetReference);
            writer.WriteUInt32(asset.Physics3DSceneFeatureFlags);
            WriteSceneSettingsAsset(writer, asset.SceneSettings);
        }

        /// <summary>
        /// Reads a scene asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset.</returns>
        static SceneAsset ReadSceneAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            return new SceneAsset {
                Id = reader.ReadString(),
                RootEntities = ReadSceneEntityAssetArray(reader, version) ?? Array.Empty<SceneEntityAsset>(),
                AssetReferences = ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>(),
                Physics3DSceneFeatureFlags = reader.ReadUInt32(),
                SceneSettings = ReadSceneSettingsAsset(reader)
            };
        }

        /// <summary>
        /// Writes scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="sceneSettings">Scene settings to serialize.</param>
        static void WriteSceneSettingsAsset(EngineBinaryWriter writer, SceneSettingsAsset sceneSettings) {
            WriteSceneCanvasProfile(writer, sceneSettings.CanvasProfile);
        }

        /// <summary>
        /// Reads scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="reader">Source reader positioned at the scene settings payload.</param>
        /// <returns>Deserialized scene settings.</returns>
        static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader) {
            return new SceneSettingsAsset {
                CanvasProfile = ReadSceneCanvasProfile(reader)
            };
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
        /// Writes one serialized scene entity payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity asset to serialize.</param>
        static void WriteSceneEntityAsset(EngineBinaryWriter writer, SceneEntityAsset asset) {
            writer.WriteByte(SceneEntityPayloadVersion);
            writer.WriteString(asset.Id);
            writer.WriteString(asset.Name);
            writer.WriteFloat3(asset.LocalPosition);
            writer.WriteFloat3(asset.LocalScale);
            writer.WriteFloat4(asset.LocalOrientation);
            writer.WriteArray(asset.Components, WriteSceneComponentAssetRecordValue);
            writer.WriteArray(asset.PlatformTransformOverrides, WriteSceneEntityPlatformTransformOverrideAsset);
            writer.WriteArray(asset.PlatformComponentOverrides, WriteSceneEntityPlatformComponentOverrideAsset);
            writer.WriteArray(asset.Children, WriteSceneEntityAsset);
        }

        /// <summary>
        /// Reads one serialized scene entity payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity asset.</returns>
        static SceneEntityAsset ReadSceneEntityAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            byte payloadVersion = reader.ReadByte();
            if (payloadVersion != SceneEntityPayloadVersion) {
                throw new InvalidOperationException($"Unsupported scene entity payload version '{payloadVersion}'.");
            }

            string id = reader.ReadString();
            string name = reader.ReadString();
            float3 localPosition = reader.ReadFloat3();
            float3 localScale = reader.ReadFloat3();
            float4 localOrientation = reader.ReadFloat4();
            SceneComponentAssetRecord[] components = ReadSceneComponentAssetRecordArray(reader, payloadVersion) ?? Array.Empty<SceneComponentAssetRecord>();
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = reader.ReadArray(ReadSceneEntityPlatformTransformOverrideAsset) ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = reader.ReadArray(ReadSceneEntityPlatformComponentOverrideAsset) ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

            return new SceneEntityAsset {
                Id = id,
                Name = name,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = localOrientation,
                Components = components,
                PlatformTransformOverrides = platformTransformOverrides,
                PlatformComponentOverrides = platformComponentOverrides,
                Children = ReadSceneEntityAssetArray(reader, version) ?? Array.Empty<SceneEntityAsset>()
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
            writer.WriteArray(asset.RemovedComponentKeys, WriteStringValue);
            writer.WriteArray(asset.AddedComponents, WriteSceneEntityPlatformAddedComponentAsset);
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

            WriteSceneComponentAssetRecord(writer, asset.Component, SceneEntityPayloadVersion);
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
                RemovedComponentKeys = reader.ReadArray(ReadStringValue) ?? Array.Empty<string>(),
                AddedComponents = reader.ReadArray(ReadSceneEntityPlatformAddedComponentAssetValue) ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>()
            };
        }

        /// <summary>
        /// Reads one serialized platform-only component payload attached to a scene entity.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="sceneEntityPayloadVersion">Owning scene entity payload version.</param>
        /// <returns>Deserialized platform-only component payload.</returns>
        static SceneEntityPlatformAddedComponentAsset ReadSceneEntityPlatformAddedComponentAsset(EngineBinaryReader reader, byte sceneEntityPayloadVersion) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformAddedComponentAsset {
                Component = ReadSceneComponentAssetRecord(reader, sceneEntityPayloadVersion)
            };
        }

        /// <summary>
        /// Reads one platform-only component payload attached to a scene entity using the current scene payload version.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-only component payload.</returns>
        static SceneEntityPlatformAddedComponentAsset ReadSceneEntityPlatformAddedComponentAssetValue(EngineBinaryReader reader) {
            return ReadSceneEntityPlatformAddedComponentAsset(reader, SceneEntityPayloadVersion);
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
        }

        /// <summary>
        /// Reads one serialized scene asset reference payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset reference.</returns>
        static SceneAssetReference ReadSceneAssetReference(EngineBinaryReader reader) {
            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Reads an array of scene asset references from the payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset references.</returns>
        static SceneAssetReference[] ReadSceneAssetReferenceArray(EngineBinaryReader reader) {
            return reader.ReadArray(ReadSceneAssetReference);
        }

        /// <summary>
        /// Writes one serialized scene component record.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="record">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecord(EngineBinaryWriter writer, SceneComponentAssetRecord record, byte sceneEntityPayloadVersion) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (sceneEntityPayloadVersion >= 3) {
                writer.WriteString(record.ComponentKey);
            }

            writer.WriteString(record.ComponentTypeId);
            writer.WriteInt32(record.ComponentIndex);
            writer.WriteByteArray(record.Payload);
        }

        /// <summary>
        /// Reads one serialized scene component record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene component record.</returns>
        static SceneComponentAssetRecord ReadSceneComponentAssetRecord(EngineBinaryReader reader, byte sceneEntityPayloadVersion) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneComponentAssetRecord {
                ComponentKey = sceneEntityPayloadVersion >= 3 ? reader.ReadString() : string.Empty,
                ComponentTypeId = reader.ReadString(),
                ComponentIndex = reader.ReadInt32(),
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
            };
        }

        /// <summary>
        /// Reads one array of serialized scene component records using the owning scene entity payload version.
        /// </summary>
        /// <param name="reader">Source reader positioned at the component array payload.</param>
        /// <param name="sceneEntityPayloadVersion">Owning scene entity payload version.</param>
        /// <returns>Decoded component records or null when the source payload was null.</returns>
        static SceneComponentAssetRecord[] ReadSceneComponentAssetRecordArray(EngineBinaryReader reader, byte sceneEntityPayloadVersion) {
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
                values[index] = ReadSceneComponentAssetRecord(reader, sceneEntityPayloadVersion);
            }

            return values;
        }

        /// <summary>
        /// Reads a scene entity array using the active scene-entity version.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <param name="version">Scene entity payload version.</param>
        /// <returns>Decoded scene entity array or null when the source payload was null.</returns>
        static SceneEntityAsset[] ReadSceneEntityAssetArray(EngineBinaryReader reader, byte version) {
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
                values[index] = ReadSceneEntityAsset(reader, version);
            }

            return values;
        }

        /// <summary>
        /// Writes one material render-state payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="renderState">Render state to serialize.</param>
        static void WriteMaterialRenderState(EngineBinaryWriter writer, MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            }

            writer.WriteInt32((int)renderState.BlendMode);
            writer.WriteInt32((int)renderState.CullMode);
            writer.WriteByte(renderState.DepthTestEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(renderState.DepthWriteEnabled ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads one material render-state payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material render-state.</returns>
        static MaterialRenderState ReadMaterialRenderState(EngineBinaryReader reader) {
            return new MaterialRenderState {
                BlendMode = (MaterialBlendMode)reader.ReadInt32(),
                CullMode = (MaterialCullMode)reader.ReadInt32(),
                DepthTestEnabled = reader.ReadByte() != 0,
                DepthWriteEnabled = reader.ReadByte() != 0
            };
        }

        /// <summary>
        /// Writes one material constant-buffer payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Material constant-buffer asset to serialize.</param>
        static void WriteMaterialConstantBufferAsset(EngineBinaryWriter writer, MaterialConstantBufferAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (string.IsNullOrWhiteSpace(asset.Name)) {
                throw new InvalidOperationException("Material constant-buffer assets must define a binding name.");
            } else if (asset.Data == null) {
                throw new InvalidOperationException("Material constant-buffer assets must define a byte payload.");
            }

            writer.WriteString(asset.Name);
            writer.WriteByteArray(asset.Data);
        }

        /// <summary>
        /// Reads one material constant-buffer payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material constant-buffer asset.</returns>
        static MaterialConstantBufferAsset ReadMaterialConstantBufferAsset(EngineBinaryReader reader) {
            return new MaterialConstantBufferAsset {
                Name = reader.ReadString(),
                Data = reader.ReadByteArray()
            };
        }

        /// <summary>
        /// Writes a shader asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader asset to serialize.</param>
        static void WriteShaderAsset(EngineBinaryWriter writer, ShaderAsset asset) {
            writer.WriteString(asset.Id);
            writer.WriteString(asset.Name);
            writer.WriteString(asset.TargetName);
            writer.WriteArray(asset.Programs, WriteShaderProgramAsset);
            writer.WriteArray(asset.Binaries, WriteShaderBinaryAsset);
        }

        /// <summary>
        /// Reads a shader asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        static ShaderAsset ReadShaderAsset(EngineBinaryReader reader) {
            return new ShaderAsset {
                Id = reader.ReadString(),
                Name = reader.ReadString(),
                TargetName = reader.ReadString(),
                Programs = reader.ReadArray(ReadShaderProgramAsset),
                Binaries = reader.ReadArray(ReadShaderBinaryAsset)
            };
        }

        /// <summary>
        /// Writes a shader program asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader program asset to serialize.</param>
        static void WriteShaderProgramAsset(EngineBinaryWriter writer, ShaderProgramAsset asset) {
            writer.WriteString(asset.Name);
            writer.WriteInt32((int)asset.Stage);
            writer.WriteString(asset.EntryPoint);
            writer.WriteArray(asset.Bindings, WriteShaderBindingAsset);
            writer.WriteArray(asset.Inputs, WriteShaderVertexElementAsset);
            writer.WriteArray(asset.Outputs, WriteShaderVertexElementAsset);
            writer.WriteArray(asset.Variants, WriteShaderVariantAsset);
        }

        /// <summary>
        /// Reads a shader program asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader program asset.</returns>
        static ShaderProgramAsset ReadShaderProgramAsset(EngineBinaryReader reader) {
            return new ShaderProgramAsset {
                Name = reader.ReadString(),
                Stage = (ShaderStage)reader.ReadInt32(),
                EntryPoint = reader.ReadString(),
                Bindings = reader.ReadArray(ReadShaderBindingAsset),
                Inputs = reader.ReadArray(ReadShaderVertexElementAsset),
                Outputs = reader.ReadArray(ReadShaderVertexElementAsset),
                Variants = reader.ReadArray(ReadShaderVariantAsset)
            };
        }

        /// <summary>
        /// Writes a shader binary asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader binary asset to serialize.</param>
        static void WriteShaderBinaryAsset(EngineBinaryWriter writer, ShaderBinaryAsset asset) {
            writer.WriteString(asset.ProgramName);
            writer.WriteInt32((int)asset.Stage);
            writer.WriteString(asset.TargetName);
            writer.WriteString(asset.Variant);
            writer.WriteByteArray(asset.Bytecode);
        }

        /// <summary>
        /// Reads a shader binary asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader binary asset.</returns>
        static ShaderBinaryAsset ReadShaderBinaryAsset(EngineBinaryReader reader) {
            return new ShaderBinaryAsset {
                ProgramName = reader.ReadString(),
                Stage = (ShaderStage)reader.ReadInt32(),
                TargetName = reader.ReadString(),
                Variant = reader.ReadString(),
                Bytecode = reader.ReadByteArray()
            };
        }

        /// <summary>
        /// Writes a shader binding asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader binding asset to serialize.</param>
        static void WriteShaderBindingAsset(EngineBinaryWriter writer, ShaderBindingAsset asset) {
            writer.WriteString(asset.Name);
            writer.WriteInt32((int)asset.Type);
            writer.WriteInt32(asset.Set);
            writer.WriteInt32(asset.Slot);
            writer.WriteInt32(asset.Size);
            writer.WriteArray(asset.Members, WriteShaderConstantMemberAsset);
        }

        /// <summary>
        /// Reads a shader binding asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader binding asset.</returns>
        static ShaderBindingAsset ReadShaderBindingAsset(EngineBinaryReader reader) {
            return new ShaderBindingAsset {
                Name = reader.ReadString(),
                Type = (ShaderResourceType)reader.ReadInt32(),
                Set = reader.ReadInt32(),
                Slot = reader.ReadInt32(),
                Size = reader.ReadInt32(),
                Members = reader.ReadArray(ReadShaderConstantMemberAsset)
            };
        }

        /// <summary>
        /// Writes a shader constant member payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader constant member asset to serialize.</param>
        static void WriteShaderConstantMemberAsset(EngineBinaryWriter writer, ShaderConstantMemberAsset asset) {
            writer.WriteString(asset.Name);
            writer.WriteString(asset.Type);
            writer.WriteInt32(asset.Offset);
            writer.WriteInt32(asset.Size);
        }

        /// <summary>
        /// Reads a shader constant member payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader constant member asset.</returns>
        static ShaderConstantMemberAsset ReadShaderConstantMemberAsset(EngineBinaryReader reader) {
            return new ShaderConstantMemberAsset {
                Name = reader.ReadString(),
                Type = reader.ReadString(),
                Offset = reader.ReadInt32(),
                Size = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Writes a shader variant payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader variant asset to serialize.</param>
        static void WriteShaderVariantAsset(EngineBinaryWriter writer, ShaderVariantAsset asset) {
            writer.WriteString(asset.Name);
            writer.WriteArray(asset.Defines, WriteStringValue);
        }

        /// <summary>
        /// Reads a shader variant payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader variant asset.</returns>
        static ShaderVariantAsset ReadShaderVariantAsset(EngineBinaryReader reader) {
            return new ShaderVariantAsset {
                Name = reader.ReadString(),
                Defines = reader.ReadArray(ReadStringValue)
            };
        }

        /// <summary>
        /// Writes a shader vertex element payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader vertex element asset to serialize.</param>
        static void WriteShaderVertexElementAsset(EngineBinaryWriter writer, ShaderVertexElementAsset asset) {
            writer.WriteString(asset.Semantic);
            writer.WriteInt32(asset.Index);
            writer.WriteString(asset.Format);
        }

        /// <summary>
        /// Reads a shader vertex element payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader vertex element asset.</returns>
        static ShaderVertexElementAsset ReadShaderVertexElementAsset(EngineBinaryReader reader) {
            return new ShaderVertexElementAsset {
                Semantic = reader.ReadString(),
                Index = reader.ReadInt32(),
                Format = reader.ReadString()
            };
        }

        /// <summary>
        /// Writes one string value inside an array payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">String value to serialize.</param>
        static void WriteStringValue(EngineBinaryWriter writer, string value) {
            writer.WriteString(value);
        }

        /// <summary>
        /// Reads one string value from an array payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Deserialized string value.</returns>
        static string ReadStringValue(EngineBinaryReader reader) {
            return reader.ReadString();
        }

        /// <summary>
        /// Writes one scene-component record into a scene entity using the current scene payload version.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecordValue(EngineBinaryWriter writer, SceneComponentAssetRecord asset) {
            WriteSceneComponentAssetRecord(writer, asset, SceneEntityPayloadVersion);
        }

        /// <summary>
        /// Writes one unsigned integer value inside an array payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">Unsigned integer to serialize.</param>
        static void WriteUInt16Value(EngineBinaryWriter writer, ushort value) {
            writer.WriteUInt16(value);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer array element.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="value">Value to serialize.</param>
        static void WriteUInt32Value(EngineBinaryWriter writer, uint value) {
            writer.WriteUInt32(value);
        }

        /// <summary>
        /// Reads one unsigned integer value from an array payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Deserialized unsigned integer.</returns>
        static ushort ReadUInt16Value(EngineBinaryReader reader) {
            return reader.ReadUInt16();
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer array element.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized unsigned integer.</returns>
        static uint ReadUInt32Value(EngineBinaryReader reader) {
            return reader.ReadUInt32();
        }

        /// <summary>
        /// Writes a float2 value.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">Vector value to serialize.</param>
        static void WriteFloat2(EngineBinaryWriter writer, float2 value) {
            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
        }

        /// <summary>
        /// Reads a float2 value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized vector value.</returns>
        static float2 ReadFloat2(EngineBinaryReader reader) {
            return new float2(
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Writes a float3 value.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">Vector value to serialize.</param>
        static void WriteFloat3(EngineBinaryWriter writer, float3 value) {
            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
        }

        /// <summary>
        /// Reads a float3 value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized vector value.</returns>
        static float3 ReadFloat3(EngineBinaryReader reader) {
            return new float3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Writes a float4 value.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">Vector value to serialize.</param>
        static void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads a float4 value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized vector value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }
    }
}

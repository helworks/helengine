namespace helengine {
    /// <summary>
    /// Deserializes packaged runtime asset payloads using the engine's minimal HELE binary format.
    /// </summary>
    public static class PackagedAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for packaged runtime binary files.
        /// </summary>
        public const ushort FormatId = 1;

        /// <summary>
        /// Record kind used for serialized asset payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.Asset;

        /// <summary>
        /// Serializer version for the current packaged runtime asset payload layout.
        /// </summary>
        public const byte CurrentVersion = 24;

        /// <summary>
        /// Version marker written into scene entity payloads that include stable ids, static state, layer masks, and enabled state.
        /// </summary>
        const byte SceneEntityPayloadVersion = 8;

        /// <summary>
        /// Deserializes an asset from the supplied stream using the packaged runtime asset format.
        /// </summary>
        /// <param name="stream">Source stream containing the asset payload.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            try {
                return Deserialize(stream, header);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Deserializes one scene asset directly from the supplied stream using the packaged runtime asset format.
        /// </summary>
        /// <param name="stream">Source stream containing the scene-asset payload.</param>
        /// <returns>Deserialized scene asset.</returns>
        public static SceneAsset DeserializeSceneAsset(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            try {
                return DeserializeSceneAsset(stream, header);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Deserializes an asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize([NativeNoEscape] Stream stream, [NativeNoEscape] EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            EngineBinaryReadContext.CurrentReadStage = "PackagedAssetBinarySerializer:ValidateHeader";
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            EngineBinaryReadContext.CurrentReadStage = "PackagedAssetBinarySerializer:ReadAssetPayload";
            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind);
        }

        /// <summary>
        /// Deserializes one scene asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized scene asset.</returns>
        public static SceneAsset DeserializeSceneAsset(Stream stream, [NativeNoEscape] EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            EngineBinaryReadContext.CurrentReadStage = "PackagedAssetBinarySerializer:ValidateHeader";
            ValidateHeader(header);
            if ((EditorAssetBinaryValueKind)header.ValueKind != EditorAssetBinaryValueKind.SceneAsset) {
                throw new InvalidOperationException($"Serialized payload value kind '{header.ValueKind}' is not supported for scene-asset deserialization.");
            }

            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            EngineBinaryReadContext.CurrentReadStage = "PackagedAssetBinarySerializer:ReadAssetPayload";
            return ReadSceneAsset(reader);
        }

        /// <summary>
        /// Validates that the provided header matches the packaged runtime asset format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader([NativeNoEscape] EngineBinaryHeader header) {
            if (header.FormatId != FormatId) {
                throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected asset record kind '{header.RecordKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException(
                    $"Packaged asset version '{header.Version}' is unsupported; version '{CurrentVersion}' is required. Regenerate the packaged asset.");
            }
        }

        /// <summary>
        /// Reads an asset payload using the supplied value kind.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="valueKind">Format-specific value kind identifier.</param>
        /// <returns>Deserialized asset instance.</returns>
        static Asset ReadAssetPayload([NativeNoEscape] EngineBinaryReader reader, EditorAssetBinaryValueKind valueKind) {
            switch (valueKind) {
                case EditorAssetBinaryValueKind.TextureAsset:
                    return ReadTextureAsset(reader);
                case EditorAssetBinaryValueKind.ModelAsset:
                    return ReadModelAsset(reader);
                case EditorAssetBinaryValueKind.TextAsset:
                    return ReadTextAsset(reader);
                case EditorAssetBinaryValueKind.MaterialAsset:
                    return ReadMaterialAsset(reader);
                case EditorAssetBinaryValueKind.AnimationClipAsset:
                    return ReadAnimationClipAsset(reader);
                case EditorAssetBinaryValueKind.AudioAsset:
                    return ReadAudioAsset(reader);
                case EditorAssetBinaryValueKind.PlatformMaterialAsset:
                    return ReadPlatformMaterialAsset(reader);
                case EditorAssetBinaryValueKind.SceneAsset:
                    return ReadSceneAsset(reader);
                default:
                    throw new InvalidOperationException($"Unsupported asset value kind '{(ushort)valueKind}'.");
            }
        }

        /// <summary>
        /// Reads a texture asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized texture asset.</returns>
        static TextureAsset ReadTextureAsset(EngineBinaryReader reader) {
            TextureAsset asset = new TextureAsset();
            ReadAssetIdentity(reader, asset);
            ushort width = reader.ReadUInt16();
            ushort height = reader.ReadUInt16();
            TextureAssetColorFormat colorFormat = ReadTextureAssetColorFormat(reader);
            TextureAssetAlphaPrecision alphaPrecision = ReadTextureAssetAlphaPrecision(reader);
            byte[] paletteColors = reader.ReadByteArray();
            byte[] colors = reader.ReadByteArray();
            asset.Width = width;
            asset.Height = height;
            asset.ColorFormat = colorFormat;
            asset.AlphaPrecision = alphaPrecision;
            asset.PaletteColors = paletteColors;
            asset.Colors = colors;
            return asset;
        }

        /// <summary>
        /// Reads one serialized texture color-format value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the format byte.</param>
        /// <returns>Decoded texture color format.</returns>
        static TextureAssetColorFormat ReadTextureAssetColorFormat(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetColorFormat.Rgba32) {
                return TextureAssetColorFormat.Rgba32;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Rgba4444) {
                return TextureAssetColorFormat.Rgba4444;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed4) {
                return TextureAssetColorFormat.Indexed4;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed8) {
                return TextureAssetColorFormat.Indexed8;
            } else if (serializedValue == (byte)TextureAssetColorFormat.GxRgb5A3) {
                return TextureAssetColorFormat.GxRgb5A3;
            }

            throw new InvalidOperationException($"Unsupported texture color format '{serializedValue}'.");
        }

        /// <summary>
        /// Reads one serialized texture alpha-precision value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the alpha-precision byte.</param>
        /// <returns>Decoded texture alpha precision.</returns>
        static TextureAssetAlphaPrecision ReadTextureAssetAlphaPrecision(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetAlphaPrecision.Opaque) {
                return TextureAssetAlphaPrecision.Opaque;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.Binary) {
                return TextureAssetAlphaPrecision.Binary;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A4) {
                return TextureAssetAlphaPrecision.A4;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A8) {
                return TextureAssetAlphaPrecision.A8;
            }

            throw new InvalidOperationException($"Unsupported texture alpha precision '{serializedValue}'.");
        }

        /// <summary>
        /// Reads a model asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model asset.</returns>
        static ModelAsset ReadModelAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            ModelAsset asset = new ModelAsset();
            ReadAssetIdentity(reader, asset);
            float3[] positions = reader.ReadArray(ReadFloat3);
            float3[] normals = reader.ReadArray(ReadFloat3);
            float2[] texCoords = reader.ReadArray(ReadFloat2);
            ushort[] indices16 = reader.ReadArray(ReadUInt16Value);
            uint[] indices32 = reader.ReadArray(ReadUInt32Value);
            ModelSubmeshAsset[] submeshes = reader.ReadArray(ReadModelSubmeshAsset);

            asset.Positions = positions;
            asset.Normals = normals;
            asset.TexCoords = texCoords;
            asset.Indices16 = indices16;
            asset.Indices32 = indices32;
            asset.Submeshes = submeshes;
            return asset;
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
        /// Reads a text asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized text asset.</returns>
        static TextAsset ReadTextAsset(EngineBinaryReader reader) {
            TextAsset asset = new TextAsset();
            ReadAssetIdentity(reader, asset);
            asset.Text = reader.ReadString();
            return asset;
        }

        /// <summary>
        /// Reads a material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material asset.</returns>
        static MaterialAsset ReadMaterialAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            MaterialAsset materialAsset = new MaterialAsset();
            ReadAssetIdentity(reader, materialAsset);
            materialAsset.CastsShadows = reader.ReadByte() != 0;
            materialAsset.ReceivesShadows = reader.ReadByte() != 0;
            NativeOwnership.Delete(materialAsset.RenderState);
            materialAsset.RenderState = ReadMaterialRenderState(reader);
            return materialAsset;
        }

        /// <summary>
        /// Reads a generic platform-owned cooked material payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-owned cooked material asset.</returns>
        static PlatformMaterialAsset ReadPlatformMaterialAsset(EngineBinaryReader reader) {
            PlatformMaterialAsset asset = new PlatformMaterialAsset();
            ReadAssetIdentity(reader, asset);
            asset.RendererFamilyId = reader.ReadString();
            asset.TextureRelativePath = reader.ReadString();
            asset.DoubleSided = reader.ReadByte() != 0;
            asset.UseVertexColor = reader.ReadByte() != 0;
            asset.Lit = reader.ReadByte() != 0;
            asset.BaseColorR = reader.ReadByte();
            asset.BaseColorG = reader.ReadByte();
            asset.BaseColorB = reader.ReadByte();
            asset.BaseColorA = reader.ReadByte();
            return asset;
        }

        /// <summary>
        /// Reads an animation clip asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized animation clip asset.</returns>
        static AnimationClipAsset ReadAnimationClipAsset(EngineBinaryReader reader) {
            AnimationClipAsset asset = new AnimationClipAsset();
            ReadAssetIdentity(reader, asset);
            asset.Duration = reader.ReadSingle();
            asset.PositionTracks = reader.ReadArray(ReadPositionKeyframeTrackAsset) ?? Array.Empty<PositionKeyframeTrackAsset>();
            asset.PositionOffsetTracks = reader.ReadArray(ReadPositionOffsetKeyframeTrackAsset) ?? Array.Empty<PositionOffsetKeyframeTrackAsset>();
            asset.ScaleTracks = reader.ReadArray(ReadScaleKeyframeTrackAsset) ?? Array.Empty<ScaleKeyframeTrackAsset>();
            asset.RotationTracks = reader.ReadArray(ReadRotationKeyframeTrackAsset) ?? Array.Empty<RotationKeyframeTrackAsset>();
            asset.PlatformOverrides = reader.ReadArray(ReadAnimationClipPlatformOverrideAsset) ?? Array.Empty<AnimationClipPlatformOverrideAsset>();
            return asset;
        }

        /// <summary>
        /// Reads an audio asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized audio asset.</returns>
        static AudioAsset ReadAudioAsset(EngineBinaryReader reader) {
            AudioAsset asset = new AudioAsset();
            ReadAssetIdentity(reader, asset);
            asset.PlaybackMode = (AudioPlaybackMode)reader.ReadByte();
            asset.DefaultLoop = reader.ReadByte() != 0;
            asset.DefaultBusId = reader.ReadString();
            asset.Channels = reader.ReadInt32();
            asset.SampleRate = reader.ReadInt32();
            asset.DurationSeconds = reader.ReadSingle();
            asset.EncodingFamilyId = reader.ReadString();
            asset.EncodedBytes = reader.ReadByteArray() ?? Array.Empty<byte>();
            asset.Chunks = reader.ReadArray(ReadAudioChunkDescriptor) ?? Array.Empty<AudioChunkDescriptor>();
            asset.PlatformOverrides = reader.ReadArray(ReadAudioAssetPlatformOverrideAsset) ?? Array.Empty<AudioAssetPlatformOverrideAsset>();
            return asset;
        }

        /// <summary>
        /// Reads one audio chunk descriptor payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized audio chunk descriptor.</returns>
        static AudioChunkDescriptor ReadAudioChunkDescriptor(EngineBinaryReader reader) {
            return new AudioChunkDescriptor {
                ByteOffset = reader.ReadInt32(),
                ByteLength = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Reads one platform-authored audio override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-authored audio override.</returns>
        static AudioAssetPlatformOverrideAsset ReadAudioAssetPlatformOverrideAsset(EngineBinaryReader reader) {
            return new AudioAssetPlatformOverrideAsset {
                PlatformId = reader.ReadString(),
                PlaybackMode = (AudioPlaybackMode)reader.ReadByte(),
                DefaultLoop = reader.ReadByte() != 0,
                DefaultBusId = reader.ReadString(),
                Channels = reader.ReadInt32(),
                SampleRate = reader.ReadInt32(),
                DurationSeconds = reader.ReadSingle(),
                EncodingFamilyId = reader.ReadString(),
                EncodedBytes = reader.ReadByteArray() ?? Array.Empty<byte>(),
                Chunks = reader.ReadArray(ReadAudioChunkDescriptor) ?? Array.Empty<AudioChunkDescriptor>()
            };
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
        /// Reads one position-style keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static PositionKeyframeAsset ReadPositionKeyframeAsset(EngineBinaryReader reader) {
            PositionKeyframeAsset asset = new PositionKeyframeAsset();
            asset.FrameId = reader.ReadString();
            asset.Time = reader.ReadSingle();
            asset.Value = ReadFloat3(reader);
            asset.InterpolationMode = ReadAnimationInterpolationMode(reader);
            return asset;
        }

        /// <summary>
        /// Reads one rotation keyframe payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized keyframe asset.</returns>
        static RotationKeyframeAsset ReadRotationKeyframeAsset(EngineBinaryReader reader) {
            RotationKeyframeAsset asset = new RotationKeyframeAsset();
            asset.FrameId = reader.ReadString();
            asset.Time = reader.ReadSingle();
            asset.Value = ReadFloat4(reader);
            asset.InterpolationMode = ReadAnimationInterpolationMode(reader);
            return asset;
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
        /// Reads a scene asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset.</returns>
        static SceneAsset ReadSceneAsset([NativeNoEscape] EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            SceneAsset asset = new SceneAsset();
            EngineBinaryReadContext.CurrentReadStage = "SceneAsset:Identity";
            ReadAssetIdentity(reader, asset);
            EngineBinaryReadContext.CurrentReadStage = "SceneAsset:RootEntities";
            asset.RootEntities = ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>();
            EngineBinaryReadContext.CurrentReadStage = "SceneAsset:AssetReferences";
            asset.AssetReferences = ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>();
            EngineBinaryReadContext.CurrentReadStage = "SceneAsset:Physics3DSceneFeatureFlags";
            asset.Physics3DSceneFeatureFlags = reader.ReadUInt32();
            EngineBinaryReadContext.CurrentReadStage = "SceneAsset:SceneSettings";
            asset.SceneSettings = ReadSceneSettingsAsset(reader);
            return asset;
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
        /// Reads one serialized scene entity payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity asset.</returns>
        static SceneEntityAsset ReadSceneEntityAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:PayloadVersion";
            byte payloadVersion = reader.ReadByte();
            if (payloadVersion != SceneEntityPayloadVersion) {
                throw new InvalidOperationException($"Unsupported scene entity payload version '{payloadVersion}'.");
            }

            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:Identity";
            uint id = reader.ReadUInt32();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:Name";
            string name = reader.ReadString();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:Transform";
            bool isStatic = reader.ReadByte() != 0;
            bool enabled = reader.ReadByte() != 0;
            ushort layerMask = reader.ReadUInt16();
            float3 localPosition = reader.ReadFloat3();
            float3 localScale = reader.ReadFloat3();
            float4 localOrientation = reader.ReadFloat4();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:Components";
            SceneComponentAssetRecord[] components = ReadSceneComponentAssetRecordArray(reader) ?? Array.Empty<SceneComponentAssetRecord>();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:PlatformExistenceOverrides";
            SceneEntityPlatformExistenceOverrideAsset[] platformExistenceOverrides = reader.ReadArray(ReadSceneEntityPlatformExistenceOverrideAsset) ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:PlatformTransformOverrides";
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = reader.ReadArray(ReadSceneEntityPlatformTransformOverrideAsset) ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:PlatformComponentOverrides";
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = reader.ReadArray(ReadSceneEntityPlatformComponentOverrideAsset) ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
            EngineBinaryReadContext.CurrentReadStage = "SceneEntity:Children";
            SceneEntityAsset[] children = ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>();
            EngineBinaryReadContext.LastCheckpoint = $"SceneEntityEnd:{name}@{reader.GetStreamPosition()}";

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
                Children = children
            };
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
                RemovedComponentKeys = reader.ReadArray(ReadStringValue) ?? Array.Empty<string>(),
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
        /// Reads one serialized scene asset reference payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset reference.</returns>
        static SceneAssetReference ReadSceneAssetReference(EngineBinaryReader reader) {
            return SceneAssetReferenceFactory.ReadRequiredReference(reader);
        }

        /// <summary>
        /// Reads an array of scene asset references from the payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset references.</returns>
        static SceneAssetReference[] ReadSceneAssetReferenceArray(EngineBinaryReader reader) {
            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReferenceArray:Length";
            return reader.ReadArray(ReadSceneAssetReference);
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

            EngineBinaryReadContext.CurrentReadStage = "SceneComponentRecord:ComponentKey";
            string componentKey = reader.ReadString();
            EngineBinaryReadContext.CurrentReadStage = "SceneComponentRecord:ComponentTypeId";
            string componentTypeId = reader.ReadString();
            EngineBinaryReadContext.CurrentReadStage = "SceneComponentRecord:ComponentIndex";
            int componentIndex = reader.ReadInt32();
            EngineBinaryReadContext.CurrentReadStage = $"SceneComponentRecord:Payload:{componentTypeId}";
            return new SceneComponentAssetRecord {
                ComponentKey = componentKey,
                ComponentTypeId = componentTypeId,
                ComponentIndex = componentIndex,
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
            };
        }

        /// <summary>
        /// Reads one array of serialized scene component records using the owning scene entity payload version.
        /// </summary>
        /// <param name="reader">Source reader positioned at the component array payload.</param>
        /// <returns>Decoded component records or null when the source payload was null.</returns>
        static SceneComponentAssetRecord[] ReadSceneComponentAssetRecordArray(EngineBinaryReader reader) {
            EngineBinaryReadContext.CurrentReadStage = "SceneComponentRecordArray:Length";
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return new SceneComponentAssetRecord[0];
            }

            SceneComponentAssetRecord[] values = new SceneComponentAssetRecord[length];
            for (int index = 0; index < values.Length; index++) {
                EngineBinaryReadContext.CurrentReadStage = $"SceneComponentRecordArray:Element:{index}";
                values[index] = ReadSceneComponentAssetRecord(reader);
            }

            return values;
        }

        /// <summary>
        /// Reads a scene entity array using the active scene-entity version.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <returns>Decoded scene entity array or null when the source payload was null.</returns>
        static SceneEntityAsset[] ReadSceneEntityAssetArray(EngineBinaryReader reader) {
            EngineBinaryReadContext.CurrentReadStage = "SceneEntityArray:Length";
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return new SceneEntityAsset[0];
            }

            SceneEntityAsset[] values = new SceneEntityAsset[length];
            for (int index = 0; index < values.Length; index++) {
                EngineBinaryReadContext.CurrentReadStage = $"SceneEntityArray:Element:{index}";
                values[index] = ReadSceneEntityAsset(reader);
            }

            return values;
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
        /// Reads the shared editor-facing and runtime-facing identity for one top-level asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the asset identity payload.</param>
        /// <param name="asset">Asset instance receiving the deserialized identity.</param>
        static void ReadAssetIdentity(EngineBinaryReader reader, Asset asset) {
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = (ulong)reader.ReadInt64();
            asset.AuthoringAssetId = reader.ReadString();
            asset.FormerAuthoringAssetIds = reader.ReadArray(ReadStringValue) ?? Array.Empty<string>();
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

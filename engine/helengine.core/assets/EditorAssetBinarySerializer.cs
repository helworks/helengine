namespace helengine {
    /// <summary>
    /// Deserializes editor asset payloads using the engine's minimal HELE binary format.
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
        public const byte CurrentVersion = 15;

        /// <summary>
        /// Last asset version that used the legacy scene entity layout without stable entity ids.
        /// </summary>
        const byte LegacyVersion = 2;

        /// <summary>
        /// Last asset binary version that omitted runtime asset ids.
        /// </summary>
        const byte PreviousVersionWithoutRuntimeAssetId = 10;

        /// <summary>
        /// First asset version that stored explicit texture color formats.
        /// </summary>
        const byte TextureColorFormatVersion = 13;

        /// <summary>
        /// First asset version that stored texture alpha precision and palette payloads.
        /// </summary>
        const byte TexturePaletteMetadataVersion = 14;

        /// <summary>
        /// Version marker written into scene entity payloads that include stable ids and the static flag.
        /// </summary>
        const byte SceneEntityPayloadVersion = 4;

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
            try {
                return Deserialize(stream, header);
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
            } else if (header.Version < LegacyVersion || header.Version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{header.Version}'.");
            }
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
                    return ReadTextureAsset(reader, version);
                case EditorAssetBinaryValueKind.ModelAsset:
                    return ReadModelAsset(reader, version);
                case EditorAssetBinaryValueKind.ShaderAsset:
                    return ReadShaderAsset(reader, version);
                case EditorAssetBinaryValueKind.TextAsset:
                    return ReadTextAsset(reader, version);
                case EditorAssetBinaryValueKind.MaterialAsset:
                    return ReadMaterialAsset(reader, version);
                case EditorAssetBinaryValueKind.Ps2MaterialAsset:
                    return ReadPs2MaterialAsset(reader, version);
                case EditorAssetBinaryValueKind.Ps2TextureAsset:
                    return ReadPs2TextureAsset(reader, version);
                case EditorAssetBinaryValueKind.AnimationClipAsset:
                    return ReadAnimationClipAsset(reader, version);
                case EditorAssetBinaryValueKind.PlatformMaterialAsset:
                    return ReadPlatformMaterialAsset(reader, version);
                case EditorAssetBinaryValueKind.SceneAsset:
                    return ReadSceneAsset(reader, version);
                default:
                    throw new InvalidOperationException($"Unsupported asset value kind '{(ushort)valueKind}'.");
            }
        }

        /// <summary>
        /// Reads a texture asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized texture asset.</returns>
        static TextureAsset ReadTextureAsset(EngineBinaryReader reader, byte version) {
            TextureAsset asset = new TextureAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Width = reader.ReadUInt16();
            asset.Height = reader.ReadUInt16();
            asset.ColorFormat = version >= TextureColorFormatVersion
                ? ReadTextureAssetColorFormat(reader)
                : TextureAssetColorFormat.Rgba32;
            asset.AlphaPrecision = version >= TexturePaletteMetadataVersion
                ? ReadTextureAssetAlphaPrecision(reader)
                : GetDefaultTextureAssetAlphaPrecision(asset.ColorFormat);
            asset.PaletteColors = version >= TexturePaletteMetadataVersion
                ? reader.ReadByteArray()
                : Array.Empty<byte>();
            asset.Colors = reader.ReadByteArray();
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
        /// Resolves the alpha precision assumed by legacy texture payloads that predate explicit metadata.
        /// </summary>
        /// <param name="colorFormat">Cooked texture color format read from the legacy payload.</param>
        /// <returns>Best-effort alpha precision for the legacy payload.</returns>
        static TextureAssetAlphaPrecision GetDefaultTextureAssetAlphaPrecision(TextureAssetColorFormat colorFormat) {
            if (colorFormat == TextureAssetColorFormat.Rgba4444) {
                return TextureAssetAlphaPrecision.A4;
            }

            return TextureAssetAlphaPrecision.A8;
        }

        /// <summary>
        /// Reads a model asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model asset.</returns>
        static ModelAsset ReadModelAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version < PreviousVersionWithoutRuntimeAssetId || version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            ModelAsset asset = new ModelAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Positions = reader.ReadArray(ReadFloat3);
            asset.Normals = reader.ReadArray(ReadFloat3);
            asset.TexCoords = reader.ReadArray(ReadFloat2);
            asset.Indices16 = reader.ReadArray(ReadUInt16Value);
            asset.Indices32 = reader.ReadArray(ReadUInt32Value);
            asset.Submeshes = reader.ReadArray(ReadModelSubmeshAsset);
            asset.Ps2PackedMeshBytes = reader.ReadByteArray();
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
        static TextAsset ReadTextAsset(EngineBinaryReader reader, byte version) {
            TextAsset asset = new TextAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Text = reader.ReadString();
            return asset;
        }

        /// <summary>
        /// Reads a material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material asset.</returns>
        static MaterialAsset ReadMaterialAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version < PreviousVersionWithoutRuntimeAssetId || version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            MaterialAsset materialAsset = new MaterialAsset();
            ReadAssetIdentity(reader, materialAsset, version);
            materialAsset.ShaderAssetId = reader.ReadString();
            materialAsset.VertexProgram = reader.ReadString();
            materialAsset.PixelProgram = reader.ReadString();
            materialAsset.Variant = reader.ReadString();
            materialAsset.DiffuseTextureAssetId = reader.ReadString();
            materialAsset.CastsShadows = reader.ReadByte() != 0;
            materialAsset.ReceivesShadows = reader.ReadByte() != 0;
            MaterialRenderState defaultRenderState = materialAsset.RenderState;
            materialAsset.RenderState = ReadMaterialRenderState(reader);
            NativeOwnership.Delete(defaultRenderState);
            materialAsset.ConstantBuffers = reader.ReadArray(ReadMaterialConstantBufferAsset) ?? Array.Empty<MaterialConstantBufferAsset>();
            return materialAsset;
        }

        /// <summary>
        /// Reads a PS2 material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized PS2 material asset.</returns>
        static Ps2MaterialAsset ReadPs2MaterialAsset(EngineBinaryReader reader, byte version) {
            Ps2MaterialAsset asset = new Ps2MaterialAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.RendererFamilyId = reader.ReadString();
            asset.LightingMode = (Ps2MaterialLightingMode)reader.ReadInt32();
            asset.AlphaMode = (Ps2MaterialAlphaMode)reader.ReadInt32();
            asset.RenderClass = (Ps2RenderClass)reader.ReadInt32();
            asset.BaseColorR = reader.ReadByte();
            asset.BaseColorG = reader.ReadByte();
            asset.BaseColorB = reader.ReadByte();
            asset.BaseColorA = reader.ReadByte();
            asset.TextureRelativePath = reader.ReadString();
            asset.DoubleSided = reader.ReadByte() != 0;
            asset.CastShadows = reader.ReadByte() != 0;
            asset.UseVertexColor = reader.ReadByte() != 0;
            asset.ExpensiveModeAllowed = reader.ReadByte() != 0;
            asset.Roughness = reader.ReadSingle();
            asset.SpecularStrength = reader.ReadSingle();
            asset.EmissiveStrength = reader.ReadSingle();
            return asset;
        }

        /// <summary>
        /// Reads a PS2-native runtime texture asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="version">Serialized asset format version.</param>
        /// <returns>Deserialized PS2-native runtime texture asset.</returns>
        static Ps2TextureAsset ReadPs2TextureAsset(EngineBinaryReader reader, byte version) {
            Ps2TextureAsset asset = new Ps2TextureAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Width = reader.ReadUInt16();
            asset.Height = reader.ReadUInt16();
            asset.Format = (Ps2TextureFormat)reader.ReadByte();
            asset.AlphaMode = (Ps2TextureAlphaMode)reader.ReadByte();
            asset.PaletteData = reader.ReadByteArray();
            asset.PixelData = reader.ReadByteArray();
            return asset;
        }

        /// <summary>
        /// Reads a generic platform-owned cooked material payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-owned cooked material asset.</returns>
        static PlatformMaterialAsset ReadPlatformMaterialAsset(EngineBinaryReader reader, byte version) {
            PlatformMaterialAsset asset = new PlatformMaterialAsset();
            ReadAssetIdentity(reader, asset, version);
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
        static AnimationClipAsset ReadAnimationClipAsset(EngineBinaryReader reader, byte version) {
            AnimationClipAsset asset = new AnimationClipAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Duration = reader.ReadSingle();
            asset.PositionTracks = reader.ReadArray(ReadPositionKeyframeTrackAsset) ?? Array.Empty<PositionKeyframeTrackAsset>();
            asset.PositionOffsetTracks = reader.ReadArray(ReadPositionOffsetKeyframeTrackAsset) ?? Array.Empty<PositionOffsetKeyframeTrackAsset>();
            asset.ScaleTracks = reader.ReadArray(ReadScaleKeyframeTrackAsset) ?? Array.Empty<ScaleKeyframeTrackAsset>();
            asset.RotationTracks = reader.ReadArray(ReadRotationKeyframeTrackAsset) ?? Array.Empty<RotationKeyframeTrackAsset>();
            return asset;
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
        static SceneAsset ReadSceneAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version < LegacyVersion || version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            SceneAsset asset = new SceneAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.RootEntities = ReadSceneEntityAssetArray(reader, version) ?? Array.Empty<SceneEntityAsset>();
            asset.AssetReferences = version >= 4
                ? ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>()
                : Array.Empty<SceneAssetReference>();
            asset.Physics3DSceneFeatureFlags = version >= 5
                ? reader.ReadUInt32()
                : 0u;
            asset.SceneSettings = version >= 6
                ? ReadSceneSettingsAsset(reader, version)
                : new SceneSettingsAsset();
            return asset;
        }

        /// <summary>
        /// Reads scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="reader">Source reader positioned at the scene settings payload.</param>
        /// <param name="version">Scene asset binary version being read.</param>
        /// <returns>Deserialized scene settings.</returns>
        static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader, byte version) {
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = ReadSceneCanvasProfile(reader)
            };

            if (version >= 15) {
                sceneSettings.DontUnload = ReadBooleanByte(reader, "scene settings");
            }

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
        static SceneEntityAsset ReadSceneEntityAsset(EngineBinaryReader reader, byte version) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (version < LegacyVersion || version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset binary version '{version}'.");
            }

            if (version == LegacyVersion) {
                return ReadLegacySceneEntityAsset(reader);
            }

            byte payloadVersion = reader.ReadByte();
            if (payloadVersion != 1 && payloadVersion != 2 && payloadVersion != 3 && payloadVersion != SceneEntityPayloadVersion) {
                throw new InvalidOperationException($"Unsupported scene entity payload version '{payloadVersion}'.");
            }

            uint id = 0u;
            if (payloadVersion >= 4) {
                id = reader.ReadUInt32();
            } else {
                reader.ReadString();
            }
            string name = reader.ReadString();
            bool isStatic = payloadVersion >= 4 && reader.ReadByte() != 0;
            float3 localPosition = reader.ReadFloat3();
            float3 localScale = reader.ReadFloat3();
            float4 localOrientation = reader.ReadFloat4();
            SceneComponentAssetRecord[] components = ReadSceneComponentAssetRecordArray(reader, payloadVersion) ?? Array.Empty<SceneComponentAssetRecord>();
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = payloadVersion >= 2
                ? reader.ReadArray(ReadSceneEntityPlatformTransformOverrideAsset) ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>()
                : Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = payloadVersion >= 3
                ? reader.ReadArray(ReadSceneEntityPlatformComponentOverrideAsset) ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>()
                : Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

            return new SceneEntityAsset {
                Id = id,
                Name = name,
                IsStatic = isStatic,
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
        /// Reads one serialized legacy scene entity payload written before stable entity ids were introduced.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized legacy scene entity asset.</returns>
        static SceneEntityAsset ReadLegacySceneEntityAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityAsset {
                Id = 0u,
                Name = reader.ReadString(),
                LocalPosition = reader.ReadFloat3(),
                LocalScale = reader.ReadFloat3(),
                LocalOrientation = reader.ReadFloat4(),
                Components = reader.ReadArray(ReadLegacySceneComponentAssetRecord) ?? Array.Empty<SceneComponentAssetRecord>(),
                PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                Children = ReadLegacySceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Reads a legacy scene entity array written before stable entity ids were introduced.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <returns>Decoded legacy scene entity array or null when the source payload was null.</returns>
        static SceneEntityAsset[] ReadLegacySceneEntityAssetArray(EngineBinaryReader reader) {
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
                values[index] = ReadLegacySceneEntityAsset(reader);
            }

            return values;
        }

        /// <summary>
        /// Reads one serialized legacy scene component payload that predates component keys and stable ids.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized legacy scene component payload.</returns>
        static SceneComponentAssetRecord ReadLegacySceneComponentAssetRecord(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneComponentAssetRecord {
                ComponentKey = string.Empty,
                ComponentTypeId = reader.ReadString(),
                ComponentIndex = reader.ReadInt32(),
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
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
        /// Reads the shared editor-facing and runtime-facing identity for one top-level asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the asset identity payload.</param>
        /// <param name="asset">Asset instance receiving the deserialized identity.</param>
        /// <param name="version">Serialized asset format version.</param>
        static void ReadAssetIdentity(EngineBinaryReader reader, Asset asset, byte version) {
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = version > PreviousVersionWithoutRuntimeAssetId
                ? (ulong)reader.ReadInt64()
                : 0ul;
        }

        /// <summary>
        /// Reads a shader asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        static ShaderAsset ReadShaderAsset(EngineBinaryReader reader, byte version) {
            ShaderAsset asset = new ShaderAsset();
            ReadAssetIdentity(reader, asset, version);
            asset.Name = reader.ReadString();
            asset.TargetName = reader.ReadString();
            asset.Programs = reader.ReadArray(ReadShaderProgramAsset);
            asset.Binaries = reader.ReadArray(ReadShaderBinaryAsset);
            return asset;
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

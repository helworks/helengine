namespace helengine {
    /// <summary>
    /// Serializes shader-owned raw material payloads without teaching the generic core serializer about shader-authored material structure.
    /// </summary>
    public static class ShaderMaterialAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for shader-owned raw material payloads.
        /// </summary>
        public const ushort FormatId = 2;

        /// <summary>
        /// Record kind used for serialized shader-owned raw material payloads.
        /// </summary>
        public const ushort RecordKind = 1;

        /// <summary>
        /// Value kind used for serialized shader-owned raw material payloads.
        /// </summary>
        public const ushort ValueKind = 1;

        /// <summary>
        /// Current serializer version for shader-owned raw material payloads.
        /// </summary>
        public const byte CurrentVersion = 2;

        /// <summary>
        /// Serializes one shader-owned raw material asset to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream receiving the serialized payload.</param>
        /// <param name="asset">Shader-owned raw material payload to serialize.</param>
        public static void Serialize(Stream stream, ShaderMaterialAsset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.RenderState == null) {
                throw new InvalidOperationException("Shader material assets must include a render state.");
            }
            if (asset.ConstantBuffers == null) {
                throw new InvalidOperationException("Shader material assets must include a constant-buffer collection.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                CurrentVersion,
                FormatId,
                RecordKind,
                ValueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteString(asset.Id ?? string.Empty);
            WriteRuntimeAssetId(writer, asset.RuntimeAssetId);
            writer.WriteString(asset.ShaderAssetId ?? string.Empty);
            writer.WriteString(asset.VertexProgram ?? string.Empty);
            writer.WriteString(asset.PixelProgram ?? string.Empty);
            writer.WriteString(asset.Variant ?? string.Empty);
            writer.WriteString(asset.DiffuseTextureAssetId ?? string.Empty);
            writer.WriteString(asset.NormalTextureAssetId ?? string.Empty);
            writer.WriteString(asset.EmissiveTextureAssetId ?? string.Empty);
            WriteMaterialRenderState(writer, asset.RenderState);
            writer.WriteArray(asset.ConstantBuffers, WriteMaterialConstantBufferAsset);
            writer.WriteByte(asset.CastsShadows ? (byte)1 : (byte)0);
            writer.WriteByte(asset.ReceivesShadows ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Deserializes one shader-owned raw material asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned raw material asset.</returns>
        public static ShaderMaterialAsset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            return Deserialize(stream, header);
        }

        /// <summary>
        /// Deserializes one shader-owned raw material asset from the supplied stream after the standardized HELE header was already read.
        /// </summary>
        /// <param name="stream">Stream positioned at the serialized payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized shader-owned raw material asset.</returns>
        public static ShaderMaterialAsset Deserialize(Stream stream, EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            try {
                if (header.FormatId != FormatId) {
                    throw new InvalidOperationException($"Unsupported shader material binary format id '{header.FormatId}'.");
                }
                if (header.RecordKind != RecordKind) {
                    throw new InvalidOperationException($"Unsupported shader material record kind '{header.RecordKind}'.");
                }
                if (header.ValueKind != ValueKind) {
                    throw new InvalidOperationException($"Unsupported shader material value kind '{header.ValueKind}'.");
                }
                if (header.Version > CurrentVersion) {
                    throw new InvalidOperationException($"Unsupported shader material binary version '{header.Version}'.");
                }

                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
                return ReadShaderMaterialAsset(reader, header.Version);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Creates a serialized byte array for the supplied shader-owned raw material asset.
        /// </summary>
        /// <param name="asset">Shader-owned raw material payload to serialize.</param>
        /// <returns>Serialized byte array.</returns>
        public static byte[] SerializeToBytes(ShaderMaterialAsset asset) {
            using MemoryStream stream = new MemoryStream();
            Serialize(stream, asset);
            return stream.ToArray();
        }

        /// <summary>
        /// Reads one shader-owned raw material asset payload from the supplied binary reader.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
        /// <param name="version">Serialized shader material payload version.</param>
        /// <returns>Deserialized shader-owned raw material asset.</returns>
        static ShaderMaterialAsset ReadShaderMaterialAsset(EngineBinaryReader reader, byte version) {
            ShaderMaterialAsset asset = new ShaderMaterialAsset();
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = ReadRuntimeAssetId(reader, version);
            asset.ShaderAssetId = reader.ReadString();
            asset.VertexProgram = reader.ReadString();
            asset.PixelProgram = reader.ReadString();
            asset.Variant = reader.ReadString();
            asset.DiffuseTextureAssetId = reader.ReadString();
            asset.NormalTextureAssetId = reader.ReadString();
            asset.EmissiveTextureAssetId = reader.ReadString();
            asset.RenderState = ReadMaterialRenderState(reader);
            asset.ConstantBuffers = reader.ReadArray(ReadMaterialConstantBufferAsset) ?? Array.Empty<MaterialConstantBufferAsset>();
            asset.CastsShadows = reader.ReadByte() != 0;
            asset.ReceivesShadows = reader.ReadByte() != 0;
            return asset;
        }

        /// <summary>
        /// Reads one serialized runtime asset identifier while preserving all 64 bits across serializer versions.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the runtime asset identifier payload.</param>
        /// <param name="version">Serialized shader material payload version.</param>
        /// <returns>Decoded runtime asset identifier.</returns>
        static ulong ReadRuntimeAssetId(EngineBinaryReader reader, byte version) {
            if (version >= 2) {
                ulong low = reader.ReadUInt32();
                ulong high = reader.ReadUInt32();
                return (high << 32) | low;
            }

            return ReadLegacyRuntimeAssetId(reader);
        }

        /// <summary>
        /// Reads one legacy version-1 runtime asset identifier that was stored as a signed 64-bit payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the legacy runtime asset identifier payload.</param>
        /// <returns>Decoded runtime asset identifier.</returns>
        static ulong ReadLegacyRuntimeAssetId(EngineBinaryReader reader) {
            long serializedRuntimeAssetId = reader.ReadInt64();
            if (serializedRuntimeAssetId >= 0) {
                return (ulong)serializedRuntimeAssetId;
            }

            ulong lowerBits = (ulong)(serializedRuntimeAssetId & long.MaxValue);
            return lowerBits | 0x8000000000000000UL;
        }

        /// <summary>
        /// Writes one runtime asset identifier using two unsigned 32-bit words so transpiled native code does not rely on signed overflow casts.
        /// </summary>
        /// <param name="writer">Binary writer receiving the runtime asset identifier payload.</param>
        /// <param name="runtimeAssetId">Runtime asset identifier to serialize.</param>
        static void WriteRuntimeAssetId(EngineBinaryWriter writer, ulong runtimeAssetId) {
            writer.WriteUInt32((uint)(runtimeAssetId & uint.MaxValue));
            writer.WriteUInt32((uint)(runtimeAssetId >> 32));
        }

        /// <summary>
        /// Reads one serialized render-state payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the render-state payload.</param>
        /// <returns>Deserialized material render state.</returns>
        static MaterialRenderState ReadMaterialRenderState(EngineBinaryReader reader) {
            return new MaterialRenderState {
                BlendMode = (MaterialBlendMode)reader.ReadInt32(),
                CullMode = (MaterialCullMode)reader.ReadInt32(),
                DepthTestEnabled = reader.ReadByte() != 0,
                DepthWriteEnabled = reader.ReadByte() != 0
            };
        }

        /// <summary>
        /// Writes one serialized render-state payload.
        /// </summary>
        /// <param name="writer">Binary writer receiving the render-state payload.</param>
        /// <param name="renderState">Render state to serialize.</param>
        static void WriteMaterialRenderState(EngineBinaryWriter writer, MaterialRenderState renderState) {
            writer.WriteInt32((int)renderState.BlendMode);
            writer.WriteInt32((int)renderState.CullMode);
            writer.WriteByte(renderState.DepthTestEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(renderState.DepthWriteEnabled ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Writes one serialized material constant-buffer payload.
        /// </summary>
        /// <param name="writer">Binary writer receiving the constant-buffer payload.</param>
        /// <param name="asset">Constant-buffer payload to serialize.</param>
        static void WriteMaterialConstantBufferAsset(EngineBinaryWriter writer, MaterialConstantBufferAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.Name ?? string.Empty);
            writer.WriteByteArray(asset.Data ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Reads one serialized material constant-buffer payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the constant-buffer payload.</param>
        /// <returns>Deserialized constant-buffer payload.</returns>
        static MaterialConstantBufferAsset ReadMaterialConstantBufferAsset(EngineBinaryReader reader) {
            return new MaterialConstantBufferAsset {
                Name = reader.ReadString(),
                Data = reader.ReadByteArray()
            };
        }
    }
}

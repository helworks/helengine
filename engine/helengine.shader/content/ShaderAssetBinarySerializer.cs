namespace helengine {
    /// <summary>
    /// Deserializes shader asset payloads without routing through the generic core asset serializer.
    /// </summary>
    public static class ShaderAssetBinarySerializer {
        /// <summary>
        /// Earliest editor asset format version that wrote runtime asset ids.
        /// </summary>
        const byte PreviousVersionWithoutRuntimeAssetId = 2;

        /// <summary>
        /// Deserializes one shader asset from the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing one serialized shader asset payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        public static ShaderAsset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            try {
                if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                    throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
                }
                if ((EditorAssetBinaryValueKind)header.ValueKind != EditorAssetBinaryValueKind.ShaderAsset) {
                    throw new InvalidOperationException($"Serialized asset value kind '{header.ValueKind}' is not a shader asset.");
                }
                if (header.Version > EditorAssetBinarySerializer.CurrentVersion) {
                    throw new InvalidOperationException($"Unsupported shader asset binary version '{header.Version}'.");
                }

                using EngineBinaryReader reader = new BinaryReaderLE(stream, true);
                return ReadShaderAsset(reader, header.Version);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Reads one shader asset payload from the supplied binary reader.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
        /// <param name="version">Serialized asset format version.</param>
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
        /// Reads one shader program asset payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
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
        /// Reads one shader binary asset payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
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
        /// Reads one shader binding asset payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
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
        /// Reads one shader constant-member payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
        /// <returns>Deserialized shader constant-member asset.</returns>
        static ShaderConstantMemberAsset ReadShaderConstantMemberAsset(EngineBinaryReader reader) {
            return new ShaderConstantMemberAsset {
                Name = reader.ReadString(),
                Type = reader.ReadString(),
                Offset = reader.ReadInt32(),
                Size = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Reads one shader variant payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
        /// <returns>Deserialized shader variant asset.</returns>
        static ShaderVariantAsset ReadShaderVariantAsset(EngineBinaryReader reader) {
            return new ShaderVariantAsset {
                Name = reader.ReadString(),
                Defines = reader.ReadArray(ReadStringValue)
            };
        }

        /// <summary>
        /// Reads one shader vertex-element payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the payload.</param>
        /// <returns>Deserialized shader vertex-element asset.</returns>
        static ShaderVertexElementAsset ReadShaderVertexElementAsset(EngineBinaryReader reader) {
            return new ShaderVertexElementAsset {
                Semantic = reader.ReadString(),
                Index = reader.ReadInt32(),
                Format = reader.ReadString()
            };
        }

        /// <summary>
        /// Reads one top-level asset identity payload shared by editor-authored assets.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the asset identity payload.</param>
        /// <param name="asset">Asset instance receiving the deserialized identity.</param>
        /// <param name="version">Serialized asset format version.</param>
        static void ReadAssetIdentity(EngineBinaryReader reader, Asset asset, byte version) {
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = version > PreviousVersionWithoutRuntimeAssetId
                ? (ulong)reader.ReadInt64()
                : 0ul;
        }

        /// <summary>
        /// Reads one string array element.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the value.</param>
        /// <returns>Deserialized string value.</returns>
        static string ReadStringValue(EngineBinaryReader reader) {
            return reader.ReadString();
        }
    }
}

using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a shader asset for the editor asset format, including its programs, compiled binaries, bindings, variants and vertex elements.
    /// </summary>
    public sealed class ShaderAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for shader asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.ShaderAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a shader asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a shader asset.</returns>
        public bool Handles(Asset asset) {
            return asset is ShaderAsset;
        }

        /// <summary>
        /// Performs no validation because a shader payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a shader asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Shader asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            ShaderAsset shaderAsset = (ShaderAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(shaderAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, shaderAsset);
            writer.WriteString(shaderAsset.Name);
            writer.WriteString(shaderAsset.TargetName);
            writer.WriteArray(shaderAsset.Programs, WriteShaderProgramAsset);
            writer.WriteArray(shaderAsset.Binaries, WriteShaderBinaryAsset);
        }

        /// <summary>
        /// Reads a shader asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            ShaderAsset asset = new ShaderAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.Name = reader.ReadString();
            asset.TargetName = reader.ReadString();
            asset.Programs = reader.ReadArray(ReadShaderProgramAsset);
            asset.Binaries = reader.ReadArray(ReadShaderBinaryAsset);
            return asset;
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
            writer.WriteArray(asset.Defines, EditorAssetPayloadPrimitives.WriteStringValue);
        }

        /// <summary>
        /// Reads a shader variant payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized shader variant asset.</returns>
        static ShaderVariantAsset ReadShaderVariantAsset(EngineBinaryReader reader) {
            return new ShaderVariantAsset {
                Name = reader.ReadString(),
                Defines = reader.ReadArray(EditorAssetPayloadPrimitives.ReadStringValue)
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
    }
}

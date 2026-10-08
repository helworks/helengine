using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes <see cref="EffectAsset"/> payloads: the effect identity, its inputs, intermediate targets, passes and
    /// typed parameters, in declaration order so pass and slot ordering survive a round trip byte for byte.
    /// </summary>
    public sealed class EffectAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the HELE header of effect payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.EffectAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is an effect definition.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True for <see cref="EffectAsset"/> instances.</returns>
        public bool Handles(Asset asset) {
            return asset is EffectAsset;
        }

        /// <summary>
        /// Rejects effects whose identity or nested entries are missing before any byte is written.
        /// </summary>
        /// <param name="asset">Effect about to be serialized.</param>
        public void Validate(Asset asset) {
            EffectAsset effect = (EffectAsset)asset;
            if (string.IsNullOrWhiteSpace(effect.EffectId)) {
                throw new InvalidOperationException("Effect assets require an effect id.");
            }
            if (effect.Inputs == null || effect.Targets == null || effect.Passes == null || effect.Parameters == null) {
                throw new InvalidOperationException($"Effect '{effect.EffectId}' has a missing input, target, pass or parameter list.");
            }
        }

        /// <summary>
        /// Writes the effect payload after the shared HELE header.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header.</param>
        /// <param name="asset">Effect to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            EffectAsset effect = (EffectAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(effect);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, effect);
            writer.WriteString(effect.EffectId);
            writer.WriteString(effect.DisplayName ?? string.Empty);
            writer.WriteInt32(effect.EffectVersion);
            writer.WriteInt32((int)effect.DowngradeMode);
            writer.WriteInt32((int)effect.Category);
            writer.WriteArray(effect.Inputs, WriteInput);
            writer.WriteArray(effect.Targets, WriteTarget);
            writer.WriteArray(effect.Passes, WritePass);
            writer.WriteArray(effect.Parameters, WriteParameter);
        }

        /// <summary>
        /// Reads one effect payload produced by <see cref="Write"/>.
        /// </summary>
        /// <param name="reader">Source reader positioned after the header.</param>
        /// <returns>Deserialized effect definition.</returns>
        public Asset Read(EngineBinaryReader reader) {
            EffectAsset effect = new EffectAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, effect);
            effect.EffectId = reader.ReadString();
            effect.DisplayName = reader.ReadString();
            effect.EffectVersion = reader.ReadInt32();
            effect.DowngradeMode = (RendererFeatureDowngradeMode)reader.ReadInt32();
            effect.Category = (EffectCategory)reader.ReadInt32();
            effect.Inputs = reader.ReadArray(ReadInput) ?? Array.Empty<EffectInputAsset>();
            effect.Targets = reader.ReadArray(ReadTarget) ?? Array.Empty<EffectTargetAsset>();
            effect.Passes = reader.ReadArray(ReadPass) ?? Array.Empty<EffectPassAsset>();
            effect.Parameters = reader.ReadArray(ReadParameter) ?? Array.Empty<EffectParameterAsset>();
            return effect;
        }

        /// <summary>
        /// Writes one named effect input.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="input">Input to write.</param>
        static void WriteInput(EngineBinaryWriter writer, EffectInputAsset input) {
            writer.WriteString(input.Name);
            writer.WriteByte(input.RequiresAlpha ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads one named effect input.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized input.</returns>
        static EffectInputAsset ReadInput(EngineBinaryReader reader) {
            string name = reader.ReadString();
            bool requiresAlpha = reader.ReadByte() != 0;
            return new EffectInputAsset(name, requiresAlpha);
        }

        /// <summary>
        /// Writes one intermediate target declaration.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="target">Target to write.</param>
        static void WriteTarget(EngineBinaryWriter writer, EffectTargetAsset target) {
            writer.WriteString(target.Name);
            writer.WriteSingle(target.Scale);
            writer.WriteInt32((int)target.Format);
        }

        /// <summary>
        /// Reads one intermediate target declaration.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized target.</returns>
        static EffectTargetAsset ReadTarget(EngineBinaryReader reader) {
            string name = reader.ReadString();
            float scale = reader.ReadSingle();
            EffectTargetFormat format = (EffectTargetFormat)reader.ReadInt32();
            return new EffectTargetAsset(name, scale, format);
        }

        /// <summary>
        /// Writes one fullscreen pass.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="pass">Pass to write.</param>
        static void WritePass(EngineBinaryWriter writer, EffectPassAsset pass) {
            writer.WriteString(pass.ShaderPath);
            writer.WriteString(pass.PixelEntryPoint);
            writer.WriteArray(pass.Reads, WriteName);
            writer.WriteString(pass.Writes);
            writer.WriteFloat4(pass.PassConstants);
        }

        /// <summary>
        /// Reads one fullscreen pass.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized pass.</returns>
        static EffectPassAsset ReadPass(EngineBinaryReader reader) {
            return new EffectPassAsset {
                ShaderPath = reader.ReadString(),
                PixelEntryPoint = reader.ReadString(),
                Reads = reader.ReadArray(ReadName) ?? Array.Empty<string>(),
                Writes = reader.ReadString(),
                PassConstants = reader.ReadFloat4()
            };
        }

        /// <summary>
        /// Writes one typed parameter declaration; graphic templates reuse it for their parameters.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="parameter">Parameter to write.</param>
        internal static void WriteParameter(EngineBinaryWriter writer, EffectParameterAsset parameter) {
            writer.WriteString(parameter.Name);
            writer.WriteString(parameter.Description ?? string.Empty);
            writer.WriteInt32((int)parameter.Type);
            writer.WriteFloat4(parameter.DefaultValue);
            writer.WriteSingle(parameter.Minimum);
            writer.WriteSingle(parameter.Maximum);
            writer.WriteInt32(parameter.Slot);
            writer.WriteArray(parameter.AllowedValues, WriteName);
        }

        /// <summary>
        /// Reads one typed parameter declaration; graphic templates reuse it for their parameters.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized parameter.</returns>
        internal static EffectParameterAsset ReadParameter(EngineBinaryReader reader) {
            return new EffectParameterAsset {
                Name = reader.ReadString(),
                Description = reader.ReadString(),
                Type = (EffectParameterType)reader.ReadInt32(),
                DefaultValue = reader.ReadFloat4(),
                Minimum = reader.ReadSingle(),
                Maximum = reader.ReadSingle(),
                Slot = reader.ReadInt32(),
                AllowedValues = reader.ReadArray(ReadName) ?? Array.Empty<string>()
            };
        }

        /// <summary>
        /// Writes one name of a string list.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="name">Name to write.</param>
        internal static void WriteName(EngineBinaryWriter writer, string name) {
            writer.WriteString(name);
        }

        /// <summary>
        /// Reads one name of a string list.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized name.</returns>
        internal static string ReadName(EngineBinaryReader reader) {
            return reader.ReadString();
        }
    }
}

using helengine.animation.runtime;
using helengine.files;

namespace helengine.animation {
    /// <summary>
    /// Stores hierarchy animation clips (value kind <see cref="EditorAssetBinaryValueKind.HierarchyAnimationClipAsset"/>) in
    /// the HELE asset format. The payload layout lives only in <see cref="HierarchyAnimationClipReader"/>: writing delegates to
    /// its <see cref="HierarchyAnimationClipReader.Write"/> and reading to its <see cref="HierarchyAnimationClipReader.ReadPayload"/>,
    /// so an editor-written <c>.hanimclip</c> is exactly what the runtime loads. Registered by <see cref="AnimationEditorRegistration"/>.
    /// </summary>
    public sealed class HierarchyAnimationClipPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind reserved in core for hierarchy animation clips.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.HierarchyAnimationClipAsset;
            }
        }

        /// <summary>
        /// Returns whether the asset is a hierarchy animation clip.
        /// </summary>
        /// <param name="asset">Asset to test.</param>
        /// <returns>True for <see cref="HierarchyAnimationClipAsset"/>.</returns>
        public bool Handles(Asset asset) {
            return asset is HierarchyAnimationClipAsset;
        }

        /// <summary>
        /// Rejects a clip the runtime could not play before the header is written, by running the runtime writer (which validates
        /// every track: key counts, finite strictly increasing times inside the duration) into a scratch buffer.
        /// </summary>
        /// <param name="asset">Clip to check.</param>
        public void Validate(Asset asset) {
            WritePayload((HierarchyAnimationClipAsset)asset, EngineBinaryEndianness.LittleEndian, RealRepresentation.Policy);
        }

        /// <summary>
        /// Writes the payload through the runtime writer. The runtime writer takes the core writer type, so the payload is first
        /// written to memory with the destination's endianness and numeric policy and then copied byte for byte.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header and numeric policy marker.</param>
        /// <param name="asset">Clip to write.</param>
        public void Write(helengine.files.EngineBinaryWriter writer, Asset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            byte[] payload = WritePayload((HierarchyAnimationClipAsset)asset, writer.Endianness, writer.NumericPolicy);
            for (int index = 0; index < payload.Length; index++) {
                writer.WriteByte(payload[index]);
            }
        }

        /// <summary>
        /// Reads a clip through the runtime reader.
        /// </summary>
        /// <param name="reader">Reader positioned after the header and numeric policy marker.</param>
        /// <returns>The clip.</returns>
        public Asset Read(global::helengine.EngineBinaryReader reader) {
            return HierarchyAnimationClipReader.ReadPayload(reader);
        }

        /// <summary>
        /// Writes the clip payload through the runtime writer into memory.
        /// </summary>
        /// <param name="clip">Clip to write.</param>
        /// <param name="endianness">Byte order of the destination.</param>
        /// <param name="numericPolicy">Numeric policy of the destination.</param>
        /// <returns>Payload bytes.</returns>
        static byte[] WritePayload(HierarchyAnimationClipAsset clip, EngineBinaryEndianness endianness, NumericPrecisionPolicy numericPolicy) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }

            using MemoryStream stream = new MemoryStream();
            using (global::helengine.EngineBinaryWriter payloadWriter = global::helengine.EngineBinaryWriter.Create(stream, endianness)) {
                payloadWriter.NumericPolicy = numericPolicy;
                HierarchyAnimationClipReader.Write(payloadWriter, clip);
            }

            return stream.ToArray();
        }
    }
}

namespace helengine {
    /// <summary>
    /// Reads and writes packaged runtime mesh-component payloads with backward compatibility for legacy single-material records.
    /// </summary>
    public static class MeshComponentScenePayloadSerializer {
        /// <summary>
        /// Current payload version for serialized runtime mesh-component records.
        /// </summary>
        public const byte CurrentVersion = 2;

        /// <summary>
        /// Writes one packaged runtime mesh-component payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="modelReference">Optional packaged model reference.</param>
        /// <param name="materialReferences">Ordered packaged material references by submesh slot.</param>
        /// <param name="renderOrder3D">Persisted 3D render order.</param>
        public static void Write(
            EngineBinaryWriter writer,
            SceneAssetReference modelReference,
            SceneAssetReference[] materialReferences,
            byte renderOrder3D) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (materialReferences == null) {
                throw new ArgumentNullException(nameof(materialReferences));
            }

            writer.WriteByte(CurrentVersion);
            WriteOptionalReference(writer, modelReference);
            writer.WriteInt32(materialReferences.Length);
            for (int materialIndex = 0; materialIndex < materialReferences.Length; materialIndex++) {
                WriteOptionalReference(writer, materialReferences[materialIndex]);
            }

            writer.WriteByte(renderOrder3D);
        }

        /// <summary>
        /// Reads one packaged runtime mesh-component payload and resolves legacy payload versions when required.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload start.</param>
        /// <param name="modelReference">Decoded packaged model reference.</param>
        /// <param name="materialReferences">Decoded packaged material references ordered by submesh slot.</param>
        /// <param name="renderOrder3D">Decoded 3D render order.</param>
        public static void Read(
            EngineBinaryReader reader,
            out SceneAssetReference modelReference,
            out SceneAssetReference[] materialReferences,
            out byte renderOrder3D) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte version = reader.ReadByte();
            if (version == 1) {
                ReadVersion1(reader, out modelReference, out materialReferences, out renderOrder3D);
                return;
            }
            if (version == CurrentVersion) {
                ReadVersion2(reader, out modelReference, out materialReferences, out renderOrder3D);
                return;
            }

            throw new InvalidOperationException($"Unsupported mesh component payload version '{version}'.");
        }

        /// <summary>
        /// Reads one legacy version-1 mesh-component payload that stores a single material reference.
        /// </summary>
        /// <param name="reader">Source reader positioned after the version byte.</param>
        /// <param name="modelReference">Decoded packaged model reference.</param>
        /// <param name="materialReferences">Decoded packaged material references ordered by submesh slot.</param>
        /// <param name="renderOrder3D">Decoded 3D render order.</param>
        static void ReadVersion1(
            EngineBinaryReader reader,
            out SceneAssetReference modelReference,
            out SceneAssetReference[] materialReferences,
            out byte renderOrder3D) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            modelReference = ReadOptionalReference(reader);
            SceneAssetReference materialReference = ReadOptionalReference(reader);
            renderOrder3D = reader.ReadByte();
            materialReferences = materialReference == null
                ? Array.Empty<SceneAssetReference>()
                : new[] { materialReference };
        }

        /// <summary>
        /// Reads one version-2 mesh-component payload that stores one material-reference array.
        /// </summary>
        /// <param name="reader">Source reader positioned after the version byte.</param>
        /// <param name="modelReference">Decoded packaged model reference.</param>
        /// <param name="materialReferences">Decoded packaged material references ordered by submesh slot.</param>
        /// <param name="renderOrder3D">Decoded 3D render order.</param>
        static void ReadVersion2(
            EngineBinaryReader reader,
            out SceneAssetReference modelReference,
            out SceneAssetReference[] materialReferences,
            out byte renderOrder3D) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            modelReference = ReadOptionalReference(reader);
            int materialReferenceCount = reader.ReadInt32();
            if (materialReferenceCount < 0) {
                throw new InvalidOperationException("Mesh component payload material reference count must be non-negative.");
            }

            materialReferences = new SceneAssetReference[materialReferenceCount];
            for (int materialIndex = 0; materialIndex < materialReferenceCount; materialIndex++) {
                materialReferences[materialIndex] = ReadOptionalReference(reader);
            }

            renderOrder3D = reader.ReadByte();
        }

        /// <summary>
        /// Reads one optional scene asset reference from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the optional-reference payload.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Writes one optional scene asset reference to the current payload position.
        /// </summary>
        /// <param name="writer">Writer receiving the optional-reference payload.</param>
        /// <param name="reference">Optional scene asset reference to encode.</param>
        static void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }
    }
}

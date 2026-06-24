namespace helengine.editor {
    /// <summary>
    /// Provides shared binary encoders for complex editor scene component field payloads.
    /// </summary>
    public static class SceneComponentBinaryFieldEncoding {
        /// <summary>
        /// Writes one optional scene asset reference to the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the encoded reference.</param>
        /// <param name="reference">Reference to encode.</param>
        public static void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
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

        /// <summary>
        /// Reads one optional scene asset reference from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the encoded reference.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        public static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            return global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
        }

        /// <summary>
        /// Writes one ordered optional-reference array into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the encoded references.</param>
        /// <param name="references">Ordered references to encode.</param>
        public static void WriteOptionalReferenceArray(EngineBinaryWriter writer, SceneAssetReference[] references) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (references == null) {
                throw new ArgumentNullException(nameof(references));
            }

            writer.WriteInt32(references.Length);
            for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++) {
                WriteOptionalReference(writer, references[referenceIndex]);
            }
        }

        /// <summary>
        /// Reads one ordered optional-reference array from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the encoded reference array.</param>
        /// <returns>Decoded reference array.</returns>
        public static SceneAssetReference[] ReadOptionalReferenceArray(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int referenceCount = reader.ReadInt32();
            if (referenceCount < 0) {
                throw new InvalidOperationException("Scene asset reference array counts must be non-negative.");
            }

            SceneAssetReference[] references = new SceneAssetReference[referenceCount];
            for (int referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++) {
                references[referenceIndex] = ReadOptionalReference(reader);
            }

            return references;
        }

        /// <summary>
        /// Writes one packed byte4 color into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the color payload.</param>
        /// <param name="value">Color value to encode.</param>
        public static void WriteByte4(EngineBinaryWriter writer, byte4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(value.X);
            writer.WriteByte(value.Y);
            writer.WriteByte(value.Z);
            writer.WriteByte(value.W);
        }

        /// <summary>
        /// Reads one packed byte4 color from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the color payload.</param>
        /// <returns>Decoded color value.</returns>
        public static byte4 ReadByte4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new byte4(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte());
        }

        /// <summary>
        /// Writes one camera clear-settings payload into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the clear-settings payload.</param>
        /// <param name="settings">Clear settings to encode.</param>
        public static void WriteCameraClearSettings(EngineBinaryWriter writer, CameraClearSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(settings.ClearColorEnabled ? (byte)1 : (byte)0);
            writer.WriteFloat4(settings.ClearColor);
            writer.WriteByte(settings.ClearDepthEnabled ? (byte)1 : (byte)0);
            writer.WriteSingle(settings.ClearDepth);
            writer.WriteByte(settings.ClearStencilEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(settings.ClearStencil);
        }

        /// <summary>
        /// Reads one camera clear-settings payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the clear-settings payload.</param>
        /// <returns>Decoded camera clear settings.</returns>
        public static CameraClearSettings ReadCameraClearSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraClearSettings(
                reader.ReadByte() != 0,
                reader.ReadFloat4(),
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadByte() != 0,
                reader.ReadByte());
        }

        /// <summary>
        /// Writes one camera render-settings payload into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the render-settings payload.</param>
        /// <param name="settings">Render settings to encode.</param>
        public static void WriteCameraRenderSettings(EngineBinaryWriter writer, CameraRenderSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            writer.WriteByte((byte)settings.DepthPrepassMode);
            writer.WriteSingle(settings.ShadowDistance);
            writer.WriteByte((byte)settings.PostProcessTier);
        }

        /// <summary>
        /// Reads one camera render-settings payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the render-settings payload.</param>
        /// <returns>Decoded camera render settings.</returns>
        public static CameraRenderSettings ReadCameraRenderSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraRenderSettings {
                DepthPrepassMode = (DepthPrepassMode)reader.ReadByte(),
                ShadowDistance = reader.ReadSingle(),
                PostProcessTier = (PostProcessTier)reader.ReadByte()
            };
        }
    }
}

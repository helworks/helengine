namespace helengine {
    /// <summary>
    /// Deserializes packaged sprite components for player builds.
    /// </summary>
    public sealed class RuntimeSpriteComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized sprite component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for sprite components.
        /// </summary>
        const string ComponentType = "helengine.SpriteComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Sprite component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported sprite component payload version '{version}'.");
            }

            SceneAssetReference textureReference = ReadOptionalReference(reader);
            try {
                SpriteComponent component = new SpriteComponent {
                    SourceRect = ReadFloat4(reader),
                    Size = reader.ReadInt2(),
                    Color = ReadByte4(reader),
                    Rotation = reader.ReadSingle(),
                    RenderOrder2D = reader.ReadByte(),
                    LayerMask = reader.ReadByte()
                };

                if (textureReference != null) {
                    if (referenceResolver == null) {
                        throw new InvalidOperationException("Sprite component deserialization requires a runtime scene asset reference resolver when a texture reference is present.");
                    }

                    component.Texture = referenceResolver.ResolveTexture(textureReference);
                }

                return component;
            } finally {
                NativeOwnership.Delete(textureReference);
            }
        }

        /// <summary>
        /// Reads one optional scene asset reference from the packaged sprite payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the optional reference flag.</param>
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
        /// Reads one packed `byte4` color value from the sprite payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the color payload.</param>
        /// <returns>Decoded `byte4` color value.</returns>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }

        /// <summary>
        /// Reads one `float4` value from the sprite payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the rectangle payload.</param>
        /// <returns>Decoded `float4` value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}

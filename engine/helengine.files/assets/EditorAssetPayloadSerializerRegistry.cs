using helengine;

namespace helengine.files {
    /// <summary>
    /// Holds the one registration per editor asset type that used to be spread across the three parallel dispatch chains of <see cref="EditorAssetBinarySerializer"/>: value-kind selection when writing, payload writing, and payload reading.
    /// </summary>
    public static class EditorAssetPayloadSerializerRegistry {
        /// <summary>
        /// Registered payload serializers in the order the hand-written dispatch chain tested them, so an asset that matches more than one registration still resolves to the same serializer it always did.
        /// </summary>
        static readonly IEditorAssetPayloadSerializer[] Serializers = new IEditorAssetPayloadSerializer[] {
            new TextureAssetPayloadSerializer(),
            new ModelAssetPayloadSerializer(),
            new ShaderAssetPayloadSerializer(),
            new TextAssetPayloadSerializer(),
            new MaterialAssetPayloadSerializer(),
            new PlatformMaterialAssetPayloadSerializer(),
            new AnimationClipAssetPayloadSerializer(),
            new AudioAssetPayloadSerializer()
        };

        /// <summary>
        /// Registered payload serializers indexed by the value kind stored in the payload header.
        /// </summary>
        static readonly Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> SerializersByValueKind = BuildSerializersByValueKind();

        /// <summary>
        /// Resolves the serializer that owns the supplied asset instance.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>Owning serializer, or null when no registered serializer handles the asset.</returns>
        public static IEditorAssetPayloadSerializer FindByAsset(Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            for (int index = 0; index < Serializers.Length; index++) {
                if (Serializers[index].Handles(asset)) {
                    return Serializers[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the serializer that reads payloads stored under the supplied value kind.
        /// </summary>
        /// <param name="valueKind">Value kind decoded from the payload header.</param>
        /// <returns>Owning serializer, or null when no registered serializer reads that value kind.</returns>
        public static IEditorAssetPayloadSerializer FindByValueKind(EditorAssetBinaryValueKind valueKind) {
            IEditorAssetPayloadSerializer serializer;
            if (SerializersByValueKind.TryGetValue(valueKind, out serializer)) {
                return serializer;
            }

            return null;
        }

        /// <summary>
        /// Builds the value-kind lookup and rejects two serializers claiming the same value kind, which would make stored payloads ambiguous.
        /// </summary>
        /// <returns>Lookup from stored value kind to the serializer that reads it.</returns>
        static Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> BuildSerializersByValueKind() {
            Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> serializersByValueKind =
                new Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer>(Serializers.Length);
            for (int index = 0; index < Serializers.Length; index++) {
                IEditorAssetPayloadSerializer serializer = Serializers[index];
                if (serializersByValueKind.ContainsKey(serializer.ValueKind)) {
                    throw new InvalidOperationException(
                        $"Editor asset value kind '{(ushort)serializer.ValueKind}' is registered by more than one payload serializer.");
                }

                serializersByValueKind.Add(serializer.ValueKind, serializer);
            }

            return serializersByValueKind;
        }
    }
}

using helengine;

namespace helengine.files {
    /// <summary>
    /// Holds the one registration per editor asset type that used to be spread across the three parallel dispatch chains of <see cref="EditorAssetBinarySerializer"/>: value-kind selection when writing, payload writing, and payload reading.
    /// Optional modules (such as the timeline module) add their own serializers through <see cref="Register"/>, so neither core nor this assembly references them.
    /// </summary>
    public static class EditorAssetPayloadSerializerRegistry {
        /// <summary>
        /// Guards the registration lists, since an optional module may register while another thread serializes assets.
        /// </summary>
        static readonly object Gate = new object();

        /// <summary>
        /// Registered payload serializers: the built-in ones in the order the hand-written dispatch chain tested them, so an asset that matches more than one registration still resolves to the same serializer it always did, followed by module registrations in registration order.
        /// </summary>
        static readonly List<IEditorAssetPayloadSerializer> Serializers = new List<IEditorAssetPayloadSerializer> {
            new TextureAssetPayloadSerializer(),
            new ModelAssetPayloadSerializer(),
            new ShaderAssetPayloadSerializer(),
            new TextAssetPayloadSerializer(),
            new MaterialAssetPayloadSerializer(),
            new PlatformMaterialAssetPayloadSerializer(),
            new AnimationClipAssetPayloadSerializer(),
            new AudioAssetPayloadSerializer(),
            new SceneAssetPayloadSerializer(),
            new BlueprintAssetPayloadSerializer(),
            new EffectAssetPayloadSerializer(),
            new GraphicTemplateAssetPayloadSerializer()
        };

        /// <summary>
        /// Registered payload serializers indexed by the value kind stored in the payload header.
        /// </summary>
        static readonly Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> SerializersByValueKind = BuildSerializersByValueKind();

        /// <summary>
        /// Gets the number of registered payload serializers, built-in and module-provided.
        /// </summary>
        public static int Count {
            get {
                lock (Gate) {
                    return Serializers.Count;
                }
            }
        }

        /// <summary>
        /// Adds the payload serializer of an asset type owned by an optional module. Registering a serializer of the same type again is a no-op, so a module can call its registration from every entry point; a different serializer claiming an already used value kind is rejected because stored payloads would become ambiguous.
        /// </summary>
        /// <param name="serializer">Serializer to add.</param>
        /// <exception cref="InvalidOperationException">Another serializer type already owns the value kind.</exception>
        public static void Register(IEditorAssetPayloadSerializer serializer) {
            if (serializer == null) {
                throw new ArgumentNullException(nameof(serializer));
            }

            lock (Gate) {
                IEditorAssetPayloadSerializer existing;
                if (SerializersByValueKind.TryGetValue(serializer.ValueKind, out existing)) {
                    if (existing.GetType() == serializer.GetType()) {
                        return;
                    }

                    throw new InvalidOperationException(
                        $"Editor asset value kind '{(ushort)serializer.ValueKind}' is already registered by '{existing.GetType().Name}'; '{serializer.GetType().Name}' cannot claim it.");
                }

                Serializers.Add(serializer);
                SerializersByValueKind.Add(serializer.ValueKind, serializer);
            }
        }

        /// <summary>
        /// Resolves the serializer that owns the supplied asset instance.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>Owning serializer, or null when no registered serializer handles the asset.</returns>
        public static IEditorAssetPayloadSerializer FindByAsset(Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            lock (Gate) {
                for (int index = 0; index < Serializers.Count; index++) {
                    if (Serializers[index].Handles(asset)) {
                        return Serializers[index];
                    }
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
            lock (Gate) {
                IEditorAssetPayloadSerializer serializer;
                if (SerializersByValueKind.TryGetValue(valueKind, out serializer)) {
                    return serializer;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the value-kind lookup and rejects two serializers claiming the same value kind, which would make stored payloads ambiguous.
        /// </summary>
        /// <returns>Lookup from stored value kind to the serializer that reads it.</returns>
        static Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> BuildSerializersByValueKind() {
            Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer> serializersByValueKind =
                new Dictionary<EditorAssetBinaryValueKind, IEditorAssetPayloadSerializer>(Serializers.Count);
            for (int index = 0; index < Serializers.Count; index++) {
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

namespace helengine.editor {
    /// <summary>
    /// Registers the owners of keys in the generic data block of import-settings sidecars and gives typed access to their entries.
    /// Built-in owners are registered by the constructor; plugins register theirs on <see cref="Shared"/>. A key nobody registered
    /// stays in <see cref="SidecarData"/> as raw bytes and is written back unchanged.
    /// </summary>
    public sealed class SidecarDataEntryRegistry {
        /// <summary>
        /// Shared registry instance used by the editor asset import pipeline.
        /// </summary>
        public static readonly SidecarDataEntryRegistry Shared = new SidecarDataEntryRegistry();

        /// <summary>
        /// Endianness of every entry payload.
        /// </summary>
        const EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Registered owners keyed by entry key (ordinal, like the keys in the sidecar).
        /// </summary>
        readonly Dictionary<string, ISidecarDataEntryDefinition> DefinitionsByKey = new Dictionary<string, ISidecarDataEntryDefinition>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes the registry with the built-in owners.
        /// </summary>
        public SidecarDataEntryRegistry() {
            RegisterDefinition(new GeneratedSceneImportStampEntryDefinition());
        }

        /// <summary>
        /// Registers the owner of one key.
        /// </summary>
        /// <param name="definition">Owner to register.</param>
        public void RegisterDefinition(ISidecarDataEntryDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (string.IsNullOrWhiteSpace(definition.Key)) {
                throw new InvalidOperationException("Sidecar data entry definitions must publish a non-empty key.");
            } else if (definition.ValueType == null) {
                throw new InvalidOperationException($"Sidecar data entry definition '{definition.Key}' must publish a value type.");
            } else if (DefinitionsByKey.ContainsKey(definition.Key)) {
                throw new InvalidOperationException($"Sidecar data entry key '{definition.Key}' is already registered.");
            }

            DefinitionsByKey.Add(definition.Key, definition);
        }

        /// <summary>
        /// Returns whether a key has a registered owner.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>True when an owner is registered for the key.</returns>
        public bool IsRegistered(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Sidecar data key must be provided.", nameof(key));
            }

            return DefinitionsByKey.ContainsKey(key);
        }

        /// <summary>
        /// Encodes a typed value with its owner (version byte first) and stores it in the data block, replacing any earlier entry.
        /// </summary>
        /// <typeparam name="TValue">Value type registered for the key.</typeparam>
        /// <param name="data">Data block to store the entry in.</param>
        /// <param name="key">Registered entry key.</param>
        /// <param name="value">Value to store.</param>
        public void Write<TValue>(SidecarData data, string key, TValue value) where TValue : class {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            } else if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            ISidecarDataEntryDefinition definition = GetDefinition(key);
            if (value.GetType() != definition.ValueType) {
                throw new InvalidOperationException($"Sidecar data entry '{key}' stores '{definition.ValueType.Name}' values, not '{value.GetType().Name}'.");
            }

            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness)) {
                writer.WriteByte(definition.CurrentVersion);
                definition.Write(writer, value);
            }

            data.SetRaw(key, stream.ToArray());
        }

        /// <summary>
        /// Reads the typed value stored under a key.
        /// </summary>
        /// <typeparam name="TValue">Value type registered for the key.</typeparam>
        /// <param name="data">Data block to read from.</param>
        /// <param name="key">Registered entry key.</param>
        /// <param name="value">Decoded value when the entry exists; null otherwise.</param>
        /// <returns>True when the entry exists.</returns>
        /// <exception cref="InvalidDataException">The entry exists but its owner cannot read it (another version, truncated or trailing bytes).</exception>
        public bool TryRead<TValue>(SidecarData data, string key, out TValue value) where TValue : class {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            ISidecarDataEntryDefinition definition = GetDefinition(key);
            if (definition.ValueType != typeof(TValue)) {
                throw new InvalidOperationException($"Sidecar data entry '{key}' stores '{definition.ValueType.Name}' values, not '{typeof(TValue).Name}'.");
            }

            if (!data.TryGetRaw(key, out byte[] payload)) {
                value = null;
                return false;
            } else if (payload.Length == 0) {
                throw new InvalidDataException($"Sidecar data entry '{key}' is empty; it has no version byte.");
            }

            using MemoryStream stream = new MemoryStream(payload, writable: false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, PayloadEndianness);
            byte version = reader.ReadByte();
            object decoded;
            try {
                decoded = definition.Read(reader, version);
            } catch (EndOfStreamException exception) {
                throw new InvalidDataException($"Sidecar data entry '{key}' (version {version}) is truncated.", exception);
            } catch (InvalidDataException exception) {
                throw new InvalidDataException($"Sidecar data entry '{key}' cannot be read: {exception.Message}", exception);
            }

            if (stream.Position != stream.Length) {
                throw new InvalidDataException($"Sidecar data entry '{key}' (version {version}) has {stream.Length - stream.Position} unread trailing bytes.");
            } else if (decoded == null || decoded.GetType() != definition.ValueType) {
                throw new InvalidOperationException($"Sidecar data entry definition '{key}' did not return a '{definition.ValueType.Name}' value.");
            }

            value = (TValue)decoded;
            return true;
        }

        /// <summary>
        /// Resolves the owner of a key.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>The registered owner.</returns>
        ISidecarDataEntryDefinition GetDefinition(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Sidecar data key must be provided.", nameof(key));
            } else if (!DefinitionsByKey.TryGetValue(key, out ISidecarDataEntryDefinition definition)) {
                throw new InvalidOperationException($"No owner is registered for sidecar data key '{key}'.");
            } else {
                return definition;
            }
        }
    }
}

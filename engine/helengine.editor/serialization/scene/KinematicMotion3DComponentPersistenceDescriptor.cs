namespace helengine.editor {
    /// <summary>
    /// Persists 3D kinematic-motion component state inside scene files.
    /// </summary>
    public sealed class KinematicMotion3DComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized 3D kinematic-motion scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Gets the runtime component type handled by this descriptor.
        /// </summary>
        public Type ComponentType => typeof(KinematicMotion3DComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.KinematicMotion3DComponent";

        /// <summary>
        /// Serializes one live kinematic-motion component into a scene component record.
        /// </summary>
        /// <param name="component">Live kinematic-motion component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (component is not KinematicMotion3DComponent motionComponent) {
                throw new InvalidOperationException("KinematicMotion3D persistence descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            writer.WriteFloat3(motionComponent.StartLocalPosition);
            writer.WriteFloat3(motionComponent.EndLocalPosition);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(motionComponent.TravelDurationSeconds));
            writer.WriteByte(motionComponent.PingPong ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live kinematic-motion component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that would receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live kinematic-motion component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"KinematicMotion3D component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported kinematic motion component payload version '{version}'.");
            }

            KinematicMotion3DComponent component = new KinematicMotion3DComponent {
                StartLocalPosition = reader.ReadFloat3(),
                EndLocalPosition = reader.ReadFloat3(),
                TravelDurationSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                PingPong = reader.ReadByte() != 0
            };
            return component;
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Persists 3D rigid-body component state inside scene files.
    /// </summary>
    public sealed class RigidBody3DComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized 3D rigid-body scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Gets the runtime component type handled by this descriptor.
        /// </summary>
        public Type ComponentType => typeof(RigidBody3DComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.RigidBody3DComponent";

        /// <summary>
        /// Serializes one live rigid-body component into a scene component record.
        /// </summary>
        /// <param name="component">Live rigid-body component instance to serialize.</param>
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
            if (component is not RigidBody3DComponent rigidBodyComponent) {
                throw new InvalidOperationException("RigidBody3D persistence descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            writer.WriteByte((byte)rigidBodyComponent.BodyKind);
            writer.WriteByte(rigidBodyComponent.UseGravity ? (byte)1 : (byte)0);
            writer.WriteSingle((float)rigidBodyComponent.Mass);
            writer.WriteSingle((float)rigidBodyComponent.GravityScale);
            writer.WriteFloat3(rigidBodyComponent.LinearVelocity);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live rigid-body component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that would receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live rigid-body component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"RigidBody3D component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported rigid body component payload version '{version}'.");
            }

            RigidBody3DComponent component = new RigidBody3DComponent {
                BodyKind = (BodyKind3D)reader.ReadByte(),
                UseGravity = reader.ReadByte() != 0,
                Mass = reader.ReadSingle(),
                GravityScale = reader.ReadSingle(),
                LinearVelocity = reader.ReadFloat3()
            };
            return component;
        }
    }
}

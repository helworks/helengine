namespace helengine.editor {
    /// <summary>
    /// Persists 3D box-collider component state inside scene files.
    /// </summary>
    public sealed class BoxCollider3DComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized 3D box-collider scene records.
        /// </summary>
        const byte CurrentVersion = 2;

        /// <summary>
        /// Gets the runtime component type handled by this descriptor.
        /// </summary>
        public Type ComponentType => typeof(BoxCollider3DComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.BoxCollider3DComponent";

        /// <summary>
        /// Serializes one live box-collider component into a scene component record.
        /// </summary>
        /// <param name="component">Live box-collider component instance to serialize.</param>
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
            if (component is not BoxCollider3DComponent boxColliderComponent) {
                throw new InvalidOperationException("BoxCollider3D persistence descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            writer.WriteFloat3(boxColliderComponent.Size);
            writer.WriteUInt16(boxColliderComponent.CollisionLayer);
            writer.WriteUInt16(boxColliderComponent.CollisionMask);
            writer.WriteByte(boxColliderComponent.IsTrigger ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live box-collider component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that would receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live box-collider component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"BoxCollider3D component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1 && version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported box collider component payload version '{version}'.");
            }

            BoxCollider3DComponent component = new BoxCollider3DComponent {
                Size = reader.ReadFloat3()
            };
            if (version >= 2) {
                component.CollisionLayer = reader.ReadUInt16();
                component.CollisionMask = reader.ReadUInt16();
                component.IsTrigger = reader.ReadByte() != 0;
            }

            return component;
        }
    }
}

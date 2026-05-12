namespace helengine.editor {
    /// <summary>
    /// Persists eligible scripted components through reflected named editor payloads when no explicit descriptor exists.
    /// </summary>
    public sealed class AutomaticScriptComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Reflected schema builder used for scripted component member discovery.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder SchemaBuilder;

        /// <summary>
        /// Optional shared script type resolver used for loaded gameplay modules.
        /// </summary>
        readonly IScriptTypeResolver ScriptTypeResolver;

        /// <summary>
        /// Initializes one automatic scripted-component persistence descriptor.
        /// </summary>
        /// <param name="schemaBuilder">Reflected schema builder used for scripted component discovery.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        public AutomaticScriptComponentPersistenceDescriptor(
            ScriptComponentReflectionSchemaBuilder schemaBuilder,
            IScriptTypeResolver scriptTypeResolver = null) {
            SchemaBuilder = schemaBuilder ?? throw new ArgumentNullException(nameof(schemaBuilder));
            ScriptTypeResolver = scriptTypeResolver;
        }

        /// <summary>
        /// Gets the broad component root handled by the automatic descriptor.
        /// </summary>
        public Type ComponentType => typeof(Component);

        /// <summary>
        /// Gets the synthetic type id that identifies the automatic fallback descriptor itself.
        /// </summary>
        public string ComponentTypeId => "helengine.AutomaticScriptComponentPersistenceDescriptor";

        /// <summary>
        /// Serializes one eligible scripted component into a tolerant named-field payload.
        /// </summary>
        /// <param name="component">Live scripted component instance to serialize.</param>
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

            ScriptComponentReflectionSchema schema = SchemaBuilder.Build(component.GetType());

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                writer.WriteField(member.Name, fieldWriter => WriteSupportedValue(fieldWriter, member.ValueType, member.GetValue(component)));
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = BuildComponentTypeId(schema.ComponentType),
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one reflected named-field payload back into a scripted component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live scripted component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            Type componentType = ResolveComponentType(record.ComponentTypeId);
            ScriptComponentReflectionSchema schema = SchemaBuilder.Build(componentType);
            Component component = CreateComponent(componentType);
            byte[] payload = record.Payload ?? Array.Empty<byte>();
            if (payload.Length == 0) {
                return component;
            }

            if (TryDeserializeTaggedPayload(component, schema, payload)) {
                return component;
            }

            DeserializeRuntimePayload(component, schema, payload);
            return component;
        }

        /// <summary>
        /// Attempts to deserialize one payload through the tolerant tagged editor scene-component format.
        /// </summary>
        /// <param name="component">Target component instance receiving restored member values.</param>
        /// <param name="schema">Reflected schema that defines the member layout.</param>
        /// <param name="payload">Serialized component payload bytes.</param>
        /// <returns>True when the payload matched the tagged editor format; otherwise false.</returns>
        static bool TryDeserializeTaggedPayload(Component component, ScriptComponentReflectionSchema schema, byte[] payload) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            } else if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            try {
                EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(payload);
                for (int index = 0; index < schema.Members.Count; index++) {
                    ScriptComponentReflectionMember member = schema.Members[index];
                    if (reader.TryGetFieldReader(member.Name, out EngineBinaryReader fieldReader)) {
                        using (fieldReader) {
                            member.SetValue(component, ReadSupportedValue(fieldReader, member.ValueType));
                        }
                    }
                }

                return true;
            } catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidOperationException) {
                return false;
            }
        }

        /// <summary>
        /// Deserializes one payload through the strict ordinal runtime scripted-component format.
        /// </summary>
        /// <param name="component">Target component instance receiving restored member values.</param>
        /// <param name="schema">Reflected schema that defines the member layout.</param>
        /// <param name="payload">Serialized component payload bytes.</param>
        static void DeserializeRuntimePayload(Component component, ScriptComponentReflectionSchema schema, byte[] payload) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            } else if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != AutomaticScriptComponentRuntimeDeserializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported automatic scripted component payload version '{version}'.");
            }

            int memberCount = reader.ReadInt32();
            if (memberCount != schema.Members.Count) {
                throw new InvalidOperationException(
                    $"Automatic scripted component payload expected {schema.Members.Count} members but contained {memberCount}.");
            }

            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                member.SetValue(component, ReadSupportedValue(reader, member.ValueType));
            }
        }

        /// <summary>
        /// Builds the stable persisted component type id for one scripted component type.
        /// </summary>
        /// <param name="componentType">Scripted component type whose persisted id should be produced.</param>
        /// <returns>Stable persisted component type id.</returns>
        public static string BuildComponentTypeId(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (string.IsNullOrWhiteSpace(componentType.FullName)) {
                throw new InvalidOperationException("Scripted component types must expose a full name.");
            }

            string assemblyName = componentType.Assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(assemblyName)) {
                throw new InvalidOperationException($"Scripted component type '{componentType.FullName}' must belong to one named assembly.");
            }

            return componentType.FullName + ", " + assemblyName;
        }

        /// <summary>
        /// Resolves one persisted scripted component type id back to its runtime type.
        /// </summary>
        /// <param name="componentTypeId">Persisted scripted component type id.</param>
        /// <returns>Resolved scripted component type.</returns>
        Type ResolveComponentType(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            Type componentType = Type.GetType(componentTypeId, false);
            if (componentType == null && ScriptTypeResolver != null) {
                componentType = ScriptTypeResolver.Resolve(componentTypeId);
            }
            if (componentType == null) {
                throw new InvalidOperationException($"Scripted component type '{componentTypeId}' could not be resolved.");
            }

            return componentType;
        }

        /// <summary>
        /// Creates one empty scripted component instance from its resolved runtime type.
        /// </summary>
        /// <param name="componentType">Resolved scripted component type.</param>
        /// <returns>Instantiated scripted component.</returns>
        Component CreateComponent(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                throw new InvalidOperationException($"Automatic script-component persistence requires a {nameof(Component)} type.");
            }

            var constructor = componentType.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic) {
                throw new InvalidOperationException($"Scripted component type '{componentType.FullName}' must expose a public parameterless constructor.");
            }

            object instance = Activator.CreateInstance(componentType);
            if (instance is not Component component) {
                throw new InvalidOperationException($"Scripted component type '{componentType.FullName}' could not be instantiated.");
            }

            return component;
        }

        /// <summary>
        /// Writes one reflected member value into one named field payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the member payload.</param>
        /// <param name="valueType">Runtime value type being serialized.</param>
        /// <param name="value">Current member value.</param>
        internal static void WriteSupportedValue(EngineBinaryWriter writer, Type valueType, object value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (valueType == typeof(string)) {
                writer.WriteString((string)value);
            } else if (valueType == typeof(bool)) {
                writer.WriteByte((bool)value ? (byte)1 : (byte)0);
            } else if (valueType == typeof(byte)) {
                writer.WriteByte((byte)value);
            } else if (valueType == typeof(ushort)) {
                writer.WriteUInt16((ushort)value);
            } else if (valueType == typeof(int)) {
                writer.WriteInt32((int)value);
            } else if (valueType == typeof(uint)) {
                writer.WriteUInt32((uint)value);
            } else if (valueType == typeof(long)) {
                writer.WriteInt64((long)value);
            } else if (valueType == typeof(float)) {
                writer.WriteSingle((float)value);
            } else if (valueType == typeof(int2)) {
                writer.WriteInt2((int2)value);
            } else if (valueType == typeof(int4)) {
                writer.WriteInt4((int4)value);
            } else if (valueType == typeof(float2)) {
                writer.WriteFloat2((float2)value);
            } else if (valueType == typeof(float3)) {
                writer.WriteFloat3((float3)value);
            } else if (valueType == typeof(float4)) {
                writer.WriteFloat4((float4)value);
            } else if (valueType == typeof(byte4)) {
                SceneComponentBinaryFieldEncoding.WriteByte4(writer, (byte4)value);
            } else if (valueType == typeof(SceneEntityReference)) {
                writer.WriteSceneEntityReference((SceneEntityReference)value);
            } else {
                throw new InvalidOperationException($"Automatic script-component persistence does not support member type '{valueType.FullName}'.");
            }
        }

        /// <summary>
        /// Reads one reflected member value from one named field payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <returns>Decoded member value.</returns>
        internal static object ReadSupportedValue(EngineBinaryReader reader, Type valueType) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (valueType == typeof(string)) {
                return reader.ReadString();
            }
            if (valueType == typeof(bool)) {
                return reader.ReadByte() != 0;
            }
            if (valueType == typeof(byte)) {
                return reader.ReadByte();
            }
            if (valueType == typeof(ushort)) {
                return reader.ReadUInt16();
            }
            if (valueType == typeof(int)) {
                return reader.ReadInt32();
            }
            if (valueType == typeof(uint)) {
                return reader.ReadUInt32();
            }
            if (valueType == typeof(long)) {
                return reader.ReadInt64();
            }
            if (valueType == typeof(float)) {
                return reader.ReadSingle();
            }
            if (valueType == typeof(int2)) {
                return reader.ReadInt2();
            }
            if (valueType == typeof(int4)) {
                return reader.ReadInt4();
            }
            if (valueType == typeof(float2)) {
                return reader.ReadFloat2();
            }
            if (valueType == typeof(float3)) {
                return reader.ReadFloat3();
            }
            if (valueType == typeof(float4)) {
                return reader.ReadFloat4();
            }
            if (valueType == typeof(byte4)) {
                return SceneComponentBinaryFieldEncoding.ReadByte4(reader);
            }
            if (valueType == typeof(SceneEntityReference)) {
                return reader.ReadSceneEntityReference();
            }

            throw new InvalidOperationException($"Automatic script-component persistence does not support member type '{valueType.FullName}'.");
        }
    }
}

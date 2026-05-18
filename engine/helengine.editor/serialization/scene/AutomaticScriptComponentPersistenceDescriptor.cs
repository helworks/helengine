using System.Globalization;
using System.Reflection;

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

            if (TryWriteLeafValue(writer, valueType, value)) {
                return;
            }
            if (valueType.IsEnum) {
                WriteEnumValue(writer, valueType, value);
                return;
            }
            if (TryWriteArrayValue(writer, valueType, value)) {
                return;
            }
            if (IsSupportedNestedObjectType(valueType)) {
                WriteNestedObjectValue(writer, valueType, value);
                return;
            }

            throw new InvalidOperationException($"Automatic script-component persistence does not support member type '{valueType.FullName}'.");
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

            if (TryReadLeafValue(reader, valueType, out object leafValue)) {
                return leafValue;
            }
            if (valueType.IsEnum) {
                return ReadEnumValue(reader, valueType);
            }
            if (TryReadArrayValue(reader, valueType, out object arrayValue)) {
                return arrayValue;
            }
            if (IsSupportedNestedObjectType(valueType)) {
                return ReadNestedObjectValue(reader, valueType);
            }

            throw new InvalidOperationException($"Automatic script-component persistence does not support member type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Attempts to write one directly supported leaf value without any recursive member traversal.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value payload.</param>
        /// <param name="valueType">Runtime value type being serialized.</param>
        /// <param name="value">Current member value.</param>
        /// <returns>True when the value type was handled as one direct leaf value.</returns>
        static bool TryWriteLeafValue(EngineBinaryWriter writer, Type valueType, object value) {
            if (valueType == typeof(string)) {
                writer.WriteString((string)value);
                return true;
            }
            if (valueType == typeof(bool)) {
                writer.WriteByte((bool)value ? (byte)1 : (byte)0);
                return true;
            }
            if (valueType == typeof(byte)) {
                writer.WriteByte((byte)value);
                return true;
            }
            if (valueType == typeof(ushort)) {
                writer.WriteUInt16((ushort)value);
                return true;
            }
            if (valueType == typeof(int)) {
                writer.WriteInt32((int)value);
                return true;
            }
            if (valueType == typeof(uint)) {
                writer.WriteUInt32((uint)value);
                return true;
            }
            if (valueType == typeof(long)) {
                writer.WriteInt64((long)value);
                return true;
            }
            if (valueType == typeof(float)) {
                writer.WriteSingle((float)value);
                return true;
            }
            if (valueType == typeof(double)) {
                writer.WriteDouble((double)value);
                return true;
            }
            if (valueType == typeof(int2)) {
                writer.WriteInt2((int2)value);
                return true;
            }
            if (valueType == typeof(int4)) {
                writer.WriteInt4((int4)value);
                return true;
            }
            if (valueType == typeof(float2)) {
                writer.WriteFloat2((float2)value);
                return true;
            }
            if (valueType == typeof(float3)) {
                writer.WriteFloat3((float3)value);
                return true;
            }
            if (valueType == typeof(float4)) {
                writer.WriteFloat4((float4)value);
                return true;
            }
            if (valueType == typeof(byte4)) {
                SceneComponentBinaryFieldEncoding.WriteByte4(writer, (byte4)value);
                return true;
            }
            if (valueType == typeof(SceneEntityReference)) {
                writer.WriteSceneEntityReference((SceneEntityReference)value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to read one directly supported leaf value without any recursive member traversal.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded leaf value when supported.</param>
        /// <returns>True when the value type was handled as one direct leaf value.</returns>
        static bool TryReadLeafValue(EngineBinaryReader reader, Type valueType, out object value) {
            if (valueType == typeof(string)) {
                value = reader.ReadString();
                return true;
            }
            if (valueType == typeof(bool)) {
                value = reader.ReadByte() != 0;
                return true;
            }
            if (valueType == typeof(byte)) {
                value = reader.ReadByte();
                return true;
            }
            if (valueType == typeof(ushort)) {
                value = reader.ReadUInt16();
                return true;
            }
            if (valueType == typeof(int)) {
                value = reader.ReadInt32();
                return true;
            }
            if (valueType == typeof(uint)) {
                value = reader.ReadUInt32();
                return true;
            }
            if (valueType == typeof(long)) {
                value = reader.ReadInt64();
                return true;
            }
            if (valueType == typeof(float)) {
                value = reader.ReadSingle();
                return true;
            }
            if (valueType == typeof(double)) {
                value = reader.ReadDouble();
                return true;
            }
            if (valueType == typeof(int2)) {
                value = reader.ReadInt2();
                return true;
            }
            if (valueType == typeof(int4)) {
                value = reader.ReadInt4();
                return true;
            }
            if (valueType == typeof(float2)) {
                value = reader.ReadFloat2();
                return true;
            }
            if (valueType == typeof(float3)) {
                value = reader.ReadFloat3();
                return true;
            }
            if (valueType == typeof(float4)) {
                value = reader.ReadFloat4();
                return true;
            }
            if (valueType == typeof(byte4)) {
                value = SceneComponentBinaryFieldEncoding.ReadByte4(reader);
                return true;
            }
            if (valueType == typeof(SceneEntityReference)) {
                value = reader.ReadSceneEntityReference();
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Writes one enum member value using its declared underlying integral storage type.
        /// </summary>
        /// <param name="writer">Destination writer receiving the enum payload.</param>
        /// <param name="enumType">Declared enum type being serialized.</param>
        /// <param name="value">Current enum value.</param>
        static void WriteEnumValue(EngineBinaryWriter writer, Type enumType, object value) {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object underlyingValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            WriteSupportedValue(writer, underlyingType, underlyingValue);
        }

        /// <summary>
        /// Reads one enum member value using its declared underlying integral storage type.
        /// </summary>
        /// <param name="reader">Source reader positioned at the enum payload.</param>
        /// <param name="enumType">Declared enum type expected for the payload.</param>
        /// <returns>Decoded enum value.</returns>
        static object ReadEnumValue(EngineBinaryReader reader, Type enumType) {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object underlyingValue = ReadSupportedValue(reader, underlyingType);
            return Enum.ToObject(enumType, underlyingValue);
        }

        /// <summary>
        /// Attempts to write one array value whose element type is recursively supported by automatic reflected persistence.
        /// </summary>
        /// <param name="writer">Destination writer receiving the array payload.</param>
        /// <param name="valueType">Runtime value type being serialized.</param>
        /// <param name="value">Current member value.</param>
        /// <returns>True when the supplied type was an array handled by reflected persistence.</returns>
        static bool TryWriteArrayValue(EngineBinaryWriter writer, Type valueType, object value) {
            if (!valueType.IsArray || valueType.GetArrayRank() != 1) {
                return false;
            }

            Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
            Array values = value as Array;
            if (values == null) {
                writer.WriteInt32(-1);
                return true;
            }

            writer.WriteInt32(values.Length);
            for (int index = 0; index < values.Length; index++) {
                WriteSupportedValue(writer, elementType, values.GetValue(index));
            }

            return true;
        }

        /// <summary>
        /// Attempts to read one array value whose element type is recursively supported by automatic reflected persistence.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded array value when supported.</param>
        /// <returns>True when the supplied type was an array handled by reflected persistence.</returns>
        static bool TryReadArrayValue(EngineBinaryReader reader, Type valueType, out object value) {
            if (!valueType.IsArray || valueType.GetArrayRank() != 1) {
                value = null;
                return false;
            }

            Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
            int length = reader.ReadInt32();
            if (length == -1) {
                value = null;
                return true;
            }
            if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            }

            Array values = Array.CreateInstance(elementType, length);
            for (int index = 0; index < length; index++) {
                values.SetValue(ReadSupportedValue(reader, elementType), index);
            }

            value = values;
            return true;
        }

        /// <summary>
        /// Returns whether the supplied type can be serialized as one nested authored object by recursively traversing writable public members.
        /// </summary>
        /// <param name="valueType">Runtime value type to inspect.</param>
        /// <returns>True when the type can be serialized as one nested authored object.</returns>
        static bool IsSupportedNestedObjectType(Type valueType) {
            if (valueType == null) {
                return false;
            }
            if (valueType == typeof(string) || !valueType.IsClass || valueType.IsAbstract) {
                return false;
            }
            if (typeof(Component).IsAssignableFrom(valueType) || typeof(Entity).IsAssignableFrom(valueType)) {
                return false;
            }

            return valueType.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Writes one nested authored object by recursively serializing its writable public members in deterministic ordinal order.
        /// </summary>
        /// <param name="writer">Destination writer receiving the nested object payload.</param>
        /// <param name="valueType">Runtime object type being serialized.</param>
        /// <param name="value">Current nested object value.</param>
        static void WriteNestedObjectValue(EngineBinaryWriter writer, Type valueType, object value) {
            writer.WriteByte(value == null ? (byte)0 : (byte)1);
            if (value == null) {
                return;
            }

            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                WriteSupportedValue(writer, GetMemberValueType(member), GetMemberValue(member, value));
            }
        }

        /// <summary>
        /// Reads one nested authored object by recursively deserializing its writable public members in deterministic ordinal order.
        /// </summary>
        /// <param name="reader">Source reader positioned at the nested object payload.</param>
        /// <param name="valueType">Runtime object type expected for the payload.</param>
        /// <returns>Decoded nested object instance or null when the payload omitted the object.</returns>
        static object ReadNestedObjectValue(EngineBinaryReader reader, Type valueType) {
            if (reader.ReadByte() == 0) {
                return null;
            }

            object value = Activator.CreateInstance(valueType) ?? throw new InvalidOperationException($"Nested authored object type '{valueType.FullName}' could not be instantiated.");
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                SetMemberValue(member, value, ReadSupportedValue(reader, GetMemberValueType(member)));
            }

            return value;
        }

        /// <summary>
        /// Gets the deterministically ordered writable public members that participate in nested authored-object serialization.
        /// </summary>
        /// <param name="valueType">Runtime object type whose writable public members should be returned.</param>
        /// <returns>Deterministically ordered writable public members.</returns>
        static IReadOnlyList<MemberInfo> GetSerializableMembers(Type valueType) {
            return valueType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSerializableMember)
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Returns whether one public instance member is eligible for nested authored-object serialization.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>True when the member should participate in nested authored-object serialization.</returns>
        static bool IsSerializableMember(MemberInfo memberInfo) {
            if (memberInfo.IsDefined(typeof(ScenePersistenceIgnoreAttribute), false)) {
                return false;
            }

            if (memberInfo is PropertyInfo propertyInfo) {
                if (propertyInfo.GetMethod == null || !propertyInfo.GetMethod.IsPublic) {
                    return false;
                }
                if (propertyInfo.SetMethod == null || !propertyInfo.SetMethod.IsPublic) {
                    return false;
                }
                if (propertyInfo.GetIndexParameters().Length != 0) {
                    return false;
                }

                return true;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                if (!fieldInfo.IsPublic || fieldInfo.IsStatic || fieldInfo.IsInitOnly) {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the runtime value type stored by one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member whose value type should be returned.</param>
        /// <returns>Runtime value type stored by the member.</returns>
        static Type GetMemberValueType(MemberInfo memberInfo) {
            if (memberInfo is PropertyInfo propertyInfo) {
                return propertyInfo.PropertyType;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                return fieldInfo.FieldType;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Reads the current value from one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member whose value should be read.</param>
        /// <param name="instance">Object instance whose current member value should be returned.</param>
        /// <returns>Current member value.</returns>
        static object GetMemberValue(MemberInfo memberInfo, object instance) {
            if (memberInfo is PropertyInfo propertyInfo) {
                return propertyInfo.GetValue(instance);
            }
            if (memberInfo is FieldInfo fieldInfo) {
                return fieldInfo.GetValue(instance);
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Assigns one value onto one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member that should receive the value.</param>
        /// <param name="instance">Object instance receiving the value.</param>
        /// <param name="value">Decoded value to assign.</param>
        static void SetMemberValue(MemberInfo memberInfo, object instance, object value) {
            if (memberInfo is PropertyInfo propertyInfo) {
                propertyInfo.SetValue(instance, value);
                return;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                fieldInfo.SetValue(instance, value);
                return;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }
    }
}

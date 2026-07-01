#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;
namespace helengine {
    /// <summary>
    /// Deserializes packaged scripted component payloads that were rewritten into strict ordinal runtime form.
    /// </summary>
    public sealed class AutomaticScriptComponentRuntimeDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version used by packaged automatic scripted component records.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id handled by this runtime deserializer.
        /// </summary>
        readonly string ComponentTypeIdValue;

        /// <summary>
        /// Resolved runtime component type materialized by this deserializer.
        /// </summary>
        readonly Type ComponentTypeValue;

        /// <summary>
        /// Deterministically ordered writable public instance members restored from the packaged payload.
        /// </summary>
        readonly MemberInfo[] Members;

        /// <summary>
        /// Runtime value types for the ordered writable public instance members.
        /// </summary>
        readonly Type[] MemberTypes;

        /// <summary>
        /// Initializes one automatic scripted-component runtime deserializer.
        /// </summary>
        /// <param name="componentTypeId">Stable serialized component type id handled by the deserializer.</param>
        /// <param name="componentType">Resolved runtime component type materialized by the deserializer.</param>
        public AutomaticScriptComponentRuntimeDeserializer(string componentTypeId, Type componentType) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                throw new InvalidOperationException($"Automatic scripted runtime deserializers require a {nameof(Component)} type.");
            }

            ComponentTypeIdValue = componentTypeId;
            ComponentTypeValue = componentType;
            Members = LoadMembers(componentType);
            MemberTypes = LoadMemberTypes(Members);
        }

        /// <summary>
        /// Gets the stable serialized component type id handled by this runtime deserializer.
        /// </summary>
        public string ComponentTypeId => ComponentTypeIdValue;

        /// <summary>
        /// Materializes one runtime scripted component from its packaged ordinal payload.
        /// </summary>
        /// <param name="record">Packaged scene record to deserialize.</param>
        /// <param name="referenceResolver">Resolver used to rebuild packaged asset references.</param>
        /// <returns>Loaded runtime component instance.</returns>
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeIdValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Automatic scripted runtime deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            Component component = CreateComponent(ComponentTypeValue);
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported automatic scripted component payload version '{version}'.");
            }

            int memberCount = reader.ReadInt32();
            if (memberCount != Members.Length) {
                throw new InvalidOperationException(
                    $"Packaged scripted component '{ComponentTypeIdValue}' expected {Members.Length} members but payload contained {memberCount}.");
            }

            for (int index = 0; index < Members.Length; index++) {
                SetMemberValue(component, Members[index], ReadSupportedValue(reader, MemberTypes[index], referenceResolver));
            }

            return component;
        }

        /// <summary>
        /// Loads the deterministically ordered writable public instance members for one scripted component type.
        /// </summary>
        /// <param name="componentType">Scripted component type to inspect.</param>
        /// <returns>Ordered writable public instance members.</returns>
        static MemberInfo[] LoadMembers(Type componentType) {
            return componentType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSupportedMember)
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Loads the runtime value types for one ordered member array.
        /// </summary>
        /// <param name="members">Ordered writable public instance members.</param>
        /// <returns>Runtime value types aligned with the supplied member order.</returns>
        static Type[] LoadMemberTypes(MemberInfo[] members) {
            if (members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            Type[] memberTypes = new Type[members.Length];
            for (int index = 0; index < members.Length; index++) {
                memberTypes[index] = GetMemberType(members[index]);
            }

            return memberTypes;
        }

        /// <summary>
        /// Returns whether one public instance member is eligible for automatic scripted runtime deserialization.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>True when the member should participate in ordinal payload restore.</returns>
        static bool IsSupportedMember(MemberInfo memberInfo) {
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
        /// Returns the runtime value type stored by one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member whose value type should be returned.</param>
        /// <returns>Runtime value type stored by the member.</returns>
        static Type GetMemberType(MemberInfo memberInfo) {
            if (memberInfo is PropertyInfo propertyInfo) {
                return propertyInfo.PropertyType;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                return fieldInfo.FieldType;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Creates one empty scripted component instance from its resolved runtime type.
        /// </summary>
        /// <param name="componentType">Resolved scripted component type.</param>
        /// <returns>Instantiated scripted component.</returns>
        static Component CreateComponent(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
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
        /// Assigns one decoded member value onto the target scripted component instance.
        /// </summary>
        /// <param name="component">Scripted component receiving the decoded member value.</param>
        /// <param name="memberInfo">Writable reflected member that should receive the value.</param>
        /// <param name="value">Decoded value to assign.</param>
        static void SetMemberValue(Component component, MemberInfo memberInfo, object value) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (memberInfo is PropertyInfo propertyInfo) {
                propertyInfo.SetValue(component, value);
                return;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                fieldInfo.SetValue(component, value);
                return;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Reads one supported packaged scripted-member value from the current reader position.
        /// </summary>
        /// <param name="reader">Reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <returns>Decoded member value.</returns>
        static object ReadSupportedValue(EngineBinaryReader reader, Type valueType, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                SceneAssetReference reference = ReadOptionalReference(reader);
                return AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(valueType, reference, referenceResolver);
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(valueType)) {
                return ReadAssetReferenceArrayValue(reader, valueType, referenceResolver);
            }
            if (TryReadEngineSerializedPayload(reader, valueType, out object payloadValue)) {
                return payloadValue;
            }

            if (TryReadLeafValue(reader, valueType, out object leafValue)) {
                return leafValue;
            }
            if (valueType.IsEnum) {
                return ReadEnumValue(reader, valueType, referenceResolver);
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                return ReadDictionaryValue(reader, valueType, dictionaryKeyType, dictionaryValueType, referenceResolver);
            }
            if (TryReadArrayValue(reader, valueType, referenceResolver, out object arrayValue)) {
                return arrayValue;
            }
            if (IsSupportedNestedObjectType(valueType)) {
                return ReadNestedObjectValue(reader, valueType, referenceResolver);
            }

            throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support member type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Attempts to read one engine-owned serialized payload member.
        /// </summary>
        /// <param name="reader">Reader positioned at the value payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded payload value when supported.</param>
        /// <returns>True when the value type was handled as one engine-owned serialized payload.</returns>
        static bool TryReadEngineSerializedPayload(EngineBinaryReader reader, Type valueType, out object value) {
            if (valueType != typeof(EngineSerializedPayload)) {
                value = null;
                return false;
            }
            if (reader.ReadByte() == 0) {
                value = null;
                return true;
            }

            string formatId = reader.ReadString();
            byte[] serializedBytes = reader.ReadByteArray();
            value = EngineSerializedPayload.Restore(formatId, serializedBytes);
            return true;
        }

        /// <summary>
        /// Reads one supported packaged asset-reference array and resolves each element back into the runtime assets required by the array element type.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded reference-array payload.</param>
        /// <param name="valueType">Runtime array type expected for the payload.</param>
        /// <param name="referenceResolver">Resolver used to rebuild packaged assets.</param>
        /// <returns>Resolved runtime asset array or null when the payload omitted the reference array.</returns>
        static object ReadAssetReferenceArrayValue(EngineBinaryReader reader, Type valueType, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (!AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(valueType)) {
                throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support asset-backed array type '{valueType.FullName}'.");
            }

            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            }
            if (length < -1) {
                throw new InvalidOperationException("Asset-reference array length cannot be negative.");
            }

            Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Asset-reference array type '{valueType.FullName}' must expose one element type.");
            Array resolvedValues = Array.CreateInstance(elementType, length);
            for (int index = 0; index < length; index++) {
                SceneAssetReference reference = ReadOptionalReference(reader);
                if (reference == null) {
                    continue;
                }

                resolvedValues.SetValue(AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(elementType, reference, referenceResolver), index);
            }

            return resolvedValues;
        }

        /// <summary>
        /// Reads one optional scene asset reference from the packaged ordinal payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded reference payload.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            return SceneAssetReferenceFactory.ReadOptionalReference(reader);
        }

        /// <summary>
        /// Attempts to read one directly supported leaf value without any recursive member traversal.
        /// </summary>
        /// <param name="reader">Reader positioned at the value payload.</param>
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
                value = new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
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
        /// Reads one enum member value using its declared underlying integral storage type.
        /// </summary>
        /// <param name="reader">Reader positioned at the enum payload.</param>
        /// <param name="enumType">Declared enum type expected for the payload.</param>
        /// <returns>Decoded enum value.</returns>
        static object ReadEnumValue(EngineBinaryReader reader, Type enumType, RuntimeSceneAssetReferenceResolver referenceResolver) {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object underlyingValue = ReadSupportedValue(reader, underlyingType, referenceResolver);
            return Enum.ToObject(enumType, underlyingValue);
        }

        /// <summary>
        /// Reads one dictionary value whose key type belongs to the supported deterministic subset and whose values are already handled by automatic scripted runtime deserialization.
        /// </summary>
        /// <param name="reader">Reader positioned at the dictionary payload.</param>
        /// <param name="dictionaryType">Declared reflected dictionary type expected by the payload.</param>
        /// <param name="dictionaryKeyType">Declared dictionary key type.</param>
        /// <param name="dictionaryValueType">Declared dictionary value type.</param>
        /// <param name="referenceResolver">Resolver used to restore any asset-backed values contained in the dictionary.</param>
        /// <returns>Decoded dictionary instance or null when the payload omitted the dictionary.</returns>
        static object ReadDictionaryValue(
            EngineBinaryReader reader,
            Type dictionaryType,
            Type dictionaryKeyType,
            Type dictionaryValueType,
            RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (dictionaryType == null) {
                throw new ArgumentNullException(nameof(dictionaryType));
            } else if (dictionaryKeyType == null) {
                throw new ArgumentNullException(nameof(dictionaryKeyType));
            } else if (dictionaryValueType == null) {
                throw new ArgumentNullException(nameof(dictionaryValueType));
            }
            if (!ScenePersistenceDictionaryTypeSupport.IsSupportedDictionaryKeyType(dictionaryKeyType)) {
                throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support dictionary key type '{dictionaryKeyType.FullName}'.");
            }

            int count = reader.ReadInt32();
            if (count == -1) {
                return null;
            }
            if (count < -1) {
                throw new InvalidOperationException("Dictionary entry count cannot be negative.");
            }

            object instance = Activator.CreateInstance(dictionaryType) ?? throw new InvalidOperationException($"Dictionary type '{dictionaryType.FullName}' could not be instantiated.");
            System.Collections.IDictionary dictionary = instance as System.Collections.IDictionary;
            if (dictionary == null) {
                throw new InvalidOperationException($"Automatic scripted runtime deserialization expected one dictionary instance for '{dictionaryType.FullName}'.");
            }

            for (int index = 0; index < count; index++) {
                object key = ReadSupportedValue(reader, dictionaryKeyType, referenceResolver);
                object dictionaryValue = ReadSupportedValue(reader, dictionaryValueType, referenceResolver);
                if (key == null) {
                    throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support null dictionary keys for '{dictionaryType.FullName}'.");
                }
                if (dictionary.Contains(key)) {
                    throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support duplicate dictionary keys for '{dictionaryType.FullName}'.");
                }

                dictionary.Add(key, dictionaryValue);
            }

            return instance;
        }

        /// <summary>
        /// Attempts to read one array value whose element type is recursively supported by automatic scripted runtime deserialization.
        /// </summary>
        /// <param name="reader">Reader positioned at the array payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded array value when supported.</param>
        /// <returns>True when the supplied type was an array handled by automatic scripted runtime deserialization.</returns>
        static bool TryReadArrayValue(EngineBinaryReader reader, Type valueType, RuntimeSceneAssetReferenceResolver referenceResolver, out object value) {
            if (!valueType.IsArray || valueType.GetArrayRank() != 1) {
                value = null;
                return false;
            }
            if (valueType == typeof(byte[])) {
                throw new InvalidOperationException("Automatic scripted runtime deserialization does not support raw byte[] members. Use one engine-managed binary payload type instead.");
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
                values.SetValue(ReadSupportedValue(reader, elementType, referenceResolver), index);
            }

            value = values;
            return true;
        }

        /// <summary>
        /// Returns whether the supplied type can be deserialized as one nested authored object or struct by recursively traversing writable public members.
        /// </summary>
        /// <param name="valueType">Runtime value type to inspect.</param>
        /// <returns>True when the type can be deserialized as one nested authored object.</returns>
        static bool IsSupportedNestedObjectType(Type valueType) {
            if (valueType == null) {
                return false;
            }
            if (valueType == typeof(string) || valueType.IsAbstract) {
                return false;
            }
            if (!valueType.IsClass && !valueType.IsValueType) {
                return false;
            }
            if (typeof(Component).IsAssignableFrom(valueType) || typeof(Entity).IsAssignableFrom(valueType)) {
                return false;
            }
            if (valueType.IsValueType) {
                return true;
            }

            return valueType.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Reads one nested authored object or struct by recursively deserializing its writable public members in deterministic ordinal order.
        /// </summary>
        /// <param name="reader">Reader positioned at the nested object payload.</param>
        /// <param name="valueType">Runtime object type expected for the payload.</param>
        /// <returns>Decoded nested object instance or null when the payload omitted the object.</returns>
        static object ReadNestedObjectValue(EngineBinaryReader reader, Type valueType, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (reader.ReadByte() == 0) {
                if (valueType != null && valueType.IsValueType) {
                    return Activator.CreateInstance(valueType);
                }

                return null;
            }

            object value = Activator.CreateInstance(valueType) ?? throw new InvalidOperationException($"Nested authored object type '{valueType.FullName}' could not be instantiated.");
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                SetObjectMemberValue(value, member, ReadSupportedValue(reader, GetMemberType(member), referenceResolver));
            }

            return value;
        }

        /// <summary>
        /// Gets the deterministically ordered writable public members that participate in nested authored-object deserialization.
        /// </summary>
        /// <param name="valueType">Runtime object type whose writable public members should be returned.</param>
        /// <returns>Deterministically ordered writable public members.</returns>
        static IReadOnlyList<MemberInfo> GetSerializableMembers(Type valueType) {
            return valueType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSupportedMember)
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Assigns one decoded nested-object member value onto the supplied object instance.
        /// </summary>
        /// <param name="instance">Object instance receiving the decoded member value.</param>
        /// <param name="memberInfo">Writable reflected member that should receive the value.</param>
        /// <param name="value">Decoded value to assign.</param>
        static void SetObjectMemberValue(object instance, MemberInfo memberInfo, object value) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
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
#endif

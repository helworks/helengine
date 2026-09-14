#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;

namespace helengine {
    /// <summary>
    /// Decodes reflected automatic scene-persistence payloads by walking the deterministic ordinal layout shared by the editor
    /// persistence descriptor and the packaged runtime deserializer. The walk owns every generic branch (engine-owned payloads,
    /// direct leaf values, enums, dictionaries, arrays and nested authored objects) and delegates the asset-backed member types
    /// to the host-supplied <see cref="IScenePersistenceAssetValueReader"/>, so both hosts read one identical binary layout.
    /// </summary>
    public sealed class ScenePersistenceValueWalker {
        /// <summary>
        /// Host-supplied reader used for the asset-backed member types that each host restores through its own asset pipeline.
        /// </summary>
        readonly IScenePersistenceAssetValueReader AssetValueReader;

        /// <summary>
        /// Host-facing name prefixed onto every unsupported-payload diagnostic raised by the walk.
        /// </summary>
        readonly string DiagnosticScope;

        /// <summary>
        /// Initializes one reflected scene-persistence read walk for the supplied host.
        /// </summary>
        /// <param name="assetValueReader">Reader used for the asset-backed member types owned by the host.</param>
        /// <param name="diagnosticScope">Host-facing name prefixed onto unsupported-payload diagnostics, for example "Automatic script-component persistence".</param>
        public ScenePersistenceValueWalker(IScenePersistenceAssetValueReader assetValueReader, string diagnosticScope) {
            if (assetValueReader == null) {
                throw new ArgumentNullException(nameof(assetValueReader));
            }
            if (string.IsNullOrWhiteSpace(diagnosticScope)) {
                throw new ArgumentException("Diagnostic scope must be provided.", nameof(diagnosticScope));
            }

            AssetValueReader = assetValueReader;
            DiagnosticScope = diagnosticScope;
        }

        /// <summary>
        /// Reads one supported member value from the current reader position, recursing through nested collections and objects.
        /// </summary>
        /// <param name="reader">Reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <returns>Decoded member value.</returns>
        public object ReadValue(EngineBinaryReader reader, Type valueType) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (AssetValueReader.TryReadAssetValue(reader, valueType, out object assetValue)) {
                return assetValue;
            }
            if (TryReadEngineSerializedPayload(reader, valueType, out object payloadValue)) {
                return payloadValue;
            }
            if (TryReadLeafValue(reader, valueType, out object leafValue)) {
                return leafValue;
            }
            if (valueType.IsEnum) {
                return ReadEnumValue(reader, valueType);
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                return ReadDictionaryValue(reader, valueType, dictionaryKeyType, dictionaryValueType);
            }
            if (TryReadArrayValue(reader, valueType, out object arrayValue)) {
                return arrayValue;
            }
            if (IsSupportedNestedObjectType(valueType)) {
                return ReadNestedObjectValue(reader, valueType);
            }

            throw new InvalidOperationException($"{DiagnosticScope} does not support member type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Returns whether the supplied type participates in the reflected walk as one nested authored object or struct whose
        /// writable public members are traversed recursively.
        /// </summary>
        /// <param name="valueType">Runtime value type to inspect.</param>
        /// <returns>True when the type is walked as one nested authored object.</returns>
        public static bool IsSupportedNestedObjectType(Type valueType) {
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
        /// Gets the deterministically ordered writable public members that participate in nested authored-object traversal.
        /// </summary>
        /// <param name="valueType">Runtime object type whose writable public members should be returned.</param>
        /// <returns>Writable public members ordered by ordinal member name.</returns>
        public static IReadOnlyList<MemberInfo> GetSerializableMembers(Type valueType) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            MemberInfo[] members = valueType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSerializableMember)
                .ToArray();

            for (int index = 0; index < members.Length; index++) {
                if (members[index].IsDefined(typeof(ScenePersistenceAppendAttribute), false)) {
                    throw new InvalidOperationException(
                        $"Nested serialized type '{valueType.FullName}' cannot use {nameof(ScenePersistenceAppendAttribute)} because nested payloads have no member-count framing.");
                }
            }

            return members
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Returns whether one public instance member is eligible for reflected scene persistence.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>True when the member should participate in the reflected walk.</returns>
        public static bool IsSerializableMember(MemberInfo memberInfo) {
            if (memberInfo == null) {
                throw new ArgumentNullException(nameof(memberInfo));
            }
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
        public static Type GetMemberValueType(MemberInfo memberInfo) {
            if (memberInfo is PropertyInfo propertyInfo) {
                return propertyInfo.PropertyType;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                return fieldInfo.FieldType;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Assigns one decoded value onto one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member that should receive the value.</param>
        /// <param name="instance">Object instance receiving the value.</param>
        /// <param name="value">Decoded value to assign.</param>
        public static void SetMemberValue(MemberInfo memberInfo, object instance, object value) {
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
        object ReadEnumValue(EngineBinaryReader reader, Type enumType) {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object underlyingValue = ReadValue(reader, underlyingType);
            return Enum.ToObject(enumType, underlyingValue);
        }

        /// <summary>
        /// Reads one dictionary value whose key type belongs to the supported deterministic subset and whose values are already
        /// handled by the reflected walk.
        /// </summary>
        /// <param name="reader">Reader positioned at the dictionary payload.</param>
        /// <param name="dictionaryType">Declared reflected dictionary type expected by the payload.</param>
        /// <param name="dictionaryKeyType">Declared dictionary key type.</param>
        /// <param name="dictionaryValueType">Declared dictionary value type.</param>
        /// <returns>Decoded dictionary instance or null when the payload omitted the dictionary.</returns>
        object ReadDictionaryValue(EngineBinaryReader reader, Type dictionaryType, Type dictionaryKeyType, Type dictionaryValueType) {
            if (dictionaryType == null) {
                throw new ArgumentNullException(nameof(dictionaryType));
            } else if (dictionaryKeyType == null) {
                throw new ArgumentNullException(nameof(dictionaryKeyType));
            } else if (dictionaryValueType == null) {
                throw new ArgumentNullException(nameof(dictionaryValueType));
            }
            if (!ScenePersistenceDictionaryTypeSupport.IsSupportedDictionaryKeyType(dictionaryKeyType)) {
                throw new InvalidOperationException($"{DiagnosticScope} does not support dictionary key type '{dictionaryKeyType.FullName}'.");
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
                throw new InvalidOperationException($"{DiagnosticScope} expected one dictionary instance for '{dictionaryType.FullName}'.");
            }

            for (int index = 0; index < count; index++) {
                object key = ReadValue(reader, dictionaryKeyType);
                object dictionaryValue = ReadValue(reader, dictionaryValueType);
                if (key == null) {
                    throw new InvalidOperationException($"{DiagnosticScope} does not support null dictionary keys for '{dictionaryType.FullName}'.");
                }
                if (dictionary.Contains(key)) {
                    throw new InvalidOperationException($"{DiagnosticScope} does not support duplicate dictionary keys for '{dictionaryType.FullName}'.");
                }

                dictionary.Add(key, dictionaryValue);
            }

            return instance;
        }

        /// <summary>
        /// Attempts to read one array value whose element type is recursively supported by the reflected walk.
        /// </summary>
        /// <param name="reader">Reader positioned at the array payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded array value when supported.</param>
        /// <returns>True when the supplied type was an array handled by the reflected walk.</returns>
        bool TryReadArrayValue(EngineBinaryReader reader, Type valueType, out object value) {
            if (!valueType.IsArray || valueType.GetArrayRank() != 1) {
                value = null;
                return false;
            }
            if (valueType == typeof(byte[])) {
                throw new InvalidOperationException($"{DiagnosticScope} does not support raw byte[] members. Use one engine-managed binary payload type instead.");
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
                values.SetValue(ReadValue(reader, elementType), index);
            }

            value = values;
            return true;
        }

        /// <summary>
        /// Reads one nested authored object or struct by recursively decoding its writable public members in ordinal member order.
        /// </summary>
        /// <param name="reader">Reader positioned at the nested object payload.</param>
        /// <param name="valueType">Runtime object type expected for the payload.</param>
        /// <returns>Decoded nested object instance, the default struct value, or null when the payload omitted the object.</returns>
        object ReadNestedObjectValue(EngineBinaryReader reader, Type valueType) {
            if (reader.ReadByte() == 0) {
                if (valueType.IsValueType) {
                    return Activator.CreateInstance(valueType);
                }

                return null;
            }

            object value = Activator.CreateInstance(valueType) ?? throw new InvalidOperationException($"Nested authored object type '{valueType.FullName}' could not be instantiated.");
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                SetMemberValue(member, value, ReadValue(reader, GetMemberValueType(member)));
            }

            return value;
        }
    }
}
#endif

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
                SetMemberValue(component, Members[index], ReadSupportedValue(reader, MemberTypes[index]));
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
        static object ReadSupportedValue(EngineBinaryReader reader, Type valueType) {
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
                return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }
            if (valueType == typeof(SceneEntityReference)) {
                return reader.ReadSceneEntityReference();
            }

            throw new InvalidOperationException($"Automatic scripted runtime deserialization does not support member type '{valueType.FullName}'.");
        }
    }
}

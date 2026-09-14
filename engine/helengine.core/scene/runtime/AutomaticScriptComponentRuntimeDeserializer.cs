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
        /// Host-facing diagnostic scope reported when the shared reflected walk rejects one packaged member payload.
        /// </summary>
        const string WalkerDiagnosticScope = "Automatic scripted runtime deserialization";

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
        /// Number of current ordinal members required by the component payload.
        /// </summary>
        readonly int MemberCount;

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
            MemberCount = Members.Length;
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
            ScenePersistenceValueWalker walker = new ScenePersistenceValueWalker(
                new RuntimeScenePersistenceAssetValueReader(referenceResolver),
                WalkerDiagnosticScope);
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte? receivedVersion = null;
            int? receivedMemberCount = null;
            try {
                receivedVersion = reader.ReadByte();
                if (receivedVersion != CurrentVersion) {
                    throw new InvalidOperationException(
                        $"Unsupported automatic scripted component payload received version '{receivedVersion}'; current version '{CurrentVersion}' is required. Regenerate/rebuild the asset in the current format.");
                }

                int memberCount = reader.ReadInt32();
                receivedMemberCount = memberCount;
                if (memberCount != MemberCount) {
                    throw new InvalidOperationException(
                        $"Unsupported automatic scripted component payload received member count '{memberCount}'; current member count '{MemberCount}' is required. Regenerate/rebuild the asset in the current format.");
                }

                for (int index = 0; index < memberCount; index++) {
                    ScenePersistenceValueWalker.SetMemberValue(Members[index], component, walker.ReadValue(reader, MemberTypes[index]));
                }

                if (stream.Position != stream.Length) {
                    throw new InvalidOperationException(
                        $"Automatic scripted component payload contains trailing data after current member count '{MemberCount}'. Regenerate/rebuild the asset in the current format.");
                }
            } catch (EndOfStreamException exception) {
                string versionText = receivedVersion.HasValue
                    ? $"received version '{receivedVersion.Value}', current version '{CurrentVersion}'"
                    : $"current version '{CurrentVersion}' (received version unavailable)";
                string memberCountText = receivedMemberCount.HasValue
                    ? $"received member count '{receivedMemberCount.Value}', current member count '{MemberCount}'"
                    : $"current member count '{MemberCount}' (received member count unavailable)";
                throw new InvalidOperationException(
                    $"Automatic scripted component payload is truncated ({versionText}; {memberCountText}). Regenerate/rebuild the asset in the current format.",
                    exception);
            }

            return component;
        }

        /// <summary>
        /// Loads the deterministically ordered writable public instance members for one scripted component type.
        /// </summary>
        /// <param name="componentType">Scripted component type to inspect.</param>
        /// <returns>Ordered writable public instance members.</returns>
        static MemberInfo[] LoadMembers(Type componentType) {
            return ScenePersistenceMemberOrdering.OrderMembers(componentType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(ScenePersistenceValueWalker.IsSerializableMember));
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
                memberTypes[index] = ScenePersistenceValueWalker.GetMemberValueType(members[index]);
            }

            return memberTypes;
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
    }
}
#endif

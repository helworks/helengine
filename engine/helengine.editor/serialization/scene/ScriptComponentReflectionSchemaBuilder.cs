using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds deterministic reflected persistence schemas for scripted component types.
    /// </summary>
    public sealed class ScriptComponentReflectionSchemaBuilder {
        /// <summary>
        /// Cached schemas keyed by reflected component type.
        /// </summary>
        readonly Dictionary<Type, ScriptComponentReflectionSchema> SchemasByComponentType;

        /// <summary>
        /// Initializes an empty reflected schema builder.
        /// </summary>
        public ScriptComponentReflectionSchemaBuilder() {
            SchemasByComponentType = new Dictionary<Type, ScriptComponentReflectionSchema>();
        }

        /// <summary>
        /// Builds or returns one cached reflected schema for the supplied component type.
        /// </summary>
        /// <param name="componentType">Scripted component type to inspect.</param>
        /// <returns>Deterministic reflected schema for the component type.</returns>
        public ScriptComponentReflectionSchema Build(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                throw new InvalidOperationException($"Reflected script-component schemas require a {nameof(Component)} type.");
            }

            if (!SchemasByComponentType.TryGetValue(componentType, out ScriptComponentReflectionSchema schema)) {
                schema = CreateSchema(componentType);
                SchemasByComponentType.Add(componentType, schema);
            }

            return schema;
        }

        /// <summary>
        /// Builds one reflected schema from the public instance members of the supplied component type.
        /// </summary>
        /// <param name="componentType">Scripted component type to inspect.</param>
        /// <returns>Deterministic reflected schema for the component type.</returns>
        ScriptComponentReflectionSchema CreateSchema(Type componentType) {
            MemberInfo[] members = componentType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSupportedMember)
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();

            List<ScriptComponentReflectionMember> reflectedMembers = new List<ScriptComponentReflectionMember>(members.Length);
            for (int index = 0; index < members.Length; index++) {
                reflectedMembers.Add(new ScriptComponentReflectionMember(members[index]));
            }

            return new ScriptComponentReflectionSchema(componentType, reflectedMembers);
        }

        /// <summary>
        /// Returns whether one public instance member is eligible for automatic script-component persistence.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>True when the member should participate in reflected schema generation.</returns>
        bool IsSupportedMember(MemberInfo memberInfo) {
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
    }
}

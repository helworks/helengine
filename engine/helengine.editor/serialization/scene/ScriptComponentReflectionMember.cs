using helengine.baseplatform.Definitions;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Describes one public reflected member that participates in automatic script-component persistence.
    /// </summary>
    public sealed class ScriptComponentReflectionMember {
        /// <summary>
        /// Backing member info used for value access.
        /// </summary>
        readonly MemberInfo MemberInfoValue;
        /// <summary>
        /// Stable synthetic member name used when the schema exposes one builder-owned runtime extension rather than one reflected property or field.
        /// </summary>
        readonly string SyntheticName;
        /// <summary>
        /// Runtime value type used by one synthetic member definition.
        /// </summary>
        readonly Type SyntheticValueType;
        /// <summary>
        /// Reads one synthetic member value from a live component instance.
        /// </summary>
        readonly Func<Component, object> SyntheticValueGetter;
        /// <summary>
        /// Writes one synthetic member value onto a live component instance.
        /// </summary>
        readonly Action<Component, object> SyntheticValueSetter;
        /// <summary>
        /// Optional builder-owned synthetic member definition that produced this schema entry.
        /// </summary>
        readonly PlatformComponentMemberDefinition PlatformComponentMemberDefinitionValue;

        /// <summary>
        /// Initializes one reflected script-component member descriptor.
        /// </summary>
        /// <param name="memberInfo">Public field or property that participates in automatic persistence.</param>
        public ScriptComponentReflectionMember(MemberInfo memberInfo) {
            if (memberInfo == null) {
                throw new ArgumentNullException(nameof(memberInfo));
            }
            if (memberInfo is PropertyInfo propertyInfo) {
                if (!propertyInfo.CanRead || !propertyInfo.CanWrite) {
                    throw new InvalidOperationException($"Reflected property '{propertyInfo.Name}' must be readable and writable.");
                }
                if (propertyInfo.GetIndexParameters().Length != 0) {
                    throw new InvalidOperationException($"Indexed property '{propertyInfo.Name}' is not supported by automatic script-component persistence.");
                }
                if (propertyInfo.GetMethod == null || !propertyInfo.GetMethod.IsPublic || propertyInfo.SetMethod == null || !propertyInfo.SetMethod.IsPublic) {
                    throw new InvalidOperationException($"Reflected property '{propertyInfo.Name}' must expose public get and set accessors.");
                }
            } else if (memberInfo is FieldInfo fieldInfo) {
                if (!fieldInfo.IsPublic || fieldInfo.IsStatic || fieldInfo.IsInitOnly) {
                    throw new InvalidOperationException($"Reflected field '{fieldInfo.Name}' must be one writable public instance field.");
                }
            } else {
                throw new InvalidOperationException($"Reflected member '{memberInfo.Name}' is not a supported property or field.");
            }

            MemberInfoValue = memberInfo;
        }

        /// <summary>
        /// Initializes one synthetic script-component member descriptor backed by explicit value access delegates.
        /// </summary>
        /// <param name="name">Stable synthetic member name.</param>
        /// <param name="valueType">Runtime value type used by the synthetic member.</param>
        /// <param name="valueGetter">Delegate that reads the synthetic value from one live component instance.</param>
        /// <param name="valueSetter">Delegate that writes the synthetic value onto one live component instance.</param>
        /// <param name="platformComponentMemberDefinition">Optional builder-owned synthetic member definition that produced this schema entry.</param>
        public ScriptComponentReflectionMember(
            string name,
            Type valueType,
            Func<Component, object> valueGetter,
            Action<Component, object> valueSetter,
            PlatformComponentMemberDefinition platformComponentMemberDefinition = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(name));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (valueGetter == null) {
                throw new ArgumentNullException(nameof(valueGetter));
            }
            if (valueSetter == null) {
                throw new ArgumentNullException(nameof(valueSetter));
            }

            SyntheticName = name;
            SyntheticValueType = valueType;
            SyntheticValueGetter = valueGetter;
            SyntheticValueSetter = valueSetter;
            PlatformComponentMemberDefinitionValue = platformComponentMemberDefinition;
        }

        /// <summary>
        /// Gets the stable persisted member name.
        /// </summary>
        public string Name => MemberInfoValue != null ? MemberInfoValue.Name : SyntheticName;

        /// <summary>
        /// Gets whether the reflected member is one property.
        /// </summary>
        public bool IsProperty => MemberInfoValue is PropertyInfo;

        /// <summary>
        /// Gets whether the reflected member is one field.
        /// </summary>
        public bool IsField => MemberInfoValue is FieldInfo;

        /// <summary>
        /// Gets a value indicating whether the schema member is synthetic rather than backed by one reflected property or field.
        /// </summary>
        public bool IsSynthetic => MemberInfoValue == null;

        /// <summary>
        /// Gets the builder-owned synthetic member definition that produced this schema entry when one exists.
        /// </summary>
        public PlatformComponentMemberDefinition PlatformComponentMemberDefinition => PlatformComponentMemberDefinitionValue;

        /// <summary>
        /// Gets the runtime value type stored by this member.
        /// </summary>
        public Type ValueType {
            get {
                if (MemberInfoValue is PropertyInfo propertyInfo) {
                    return propertyInfo.PropertyType;
                }
                if (MemberInfoValue is FieldInfo fieldInfo) {
                    return fieldInfo.FieldType;
                }
                if (SyntheticValueType != null) {
                    return SyntheticValueType;
                }

                throw new InvalidOperationException($"Schema member '{Name}' is not a supported property, field, or synthetic value.");
            }
        }

        /// <summary>
        /// Reads the current value from one component instance.
        /// </summary>
        /// <param name="component">Component instance whose value should be read.</param>
        /// <returns>Current member value.</returns>
        public object GetValue(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (MemberInfoValue is PropertyInfo propertyInfo) {
                return propertyInfo.GetValue(component);
            }
            if (MemberInfoValue is FieldInfo fieldInfo) {
                return fieldInfo.GetValue(component);
            }
            if (SyntheticValueGetter != null) {
                return SyntheticValueGetter(component);
            }

            throw new InvalidOperationException($"Schema member '{Name}' is not a supported property, field, or synthetic value.");
        }

        /// <summary>
        /// Writes one value back onto one component instance.
        /// </summary>
        /// <param name="component">Component instance receiving the value.</param>
        /// <param name="value">Value to assign.</param>
        public void SetValue(Component component, object value) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (MemberInfoValue is PropertyInfo propertyInfo) {
                propertyInfo.SetValue(component, value);
                return;
            }
            if (MemberInfoValue is FieldInfo fieldInfo) {
                fieldInfo.SetValue(component, value);
                return;
            }
            if (SyntheticValueSetter != null) {
                SyntheticValueSetter(component, value);
                return;
            }

            throw new InvalidOperationException($"Schema member '{Name}' is not a supported property, field, or synthetic value.");
        }
    }
}

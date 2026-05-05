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
        /// Gets the stable persisted member name.
        /// </summary>
        public string Name => MemberInfoValue.Name;

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

                throw new InvalidOperationException($"Reflected member '{MemberInfoValue.Name}' is not a supported property or field.");
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

            throw new InvalidOperationException($"Reflected member '{MemberInfoValue.Name}' is not a supported property or field.");
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

            throw new InvalidOperationException($"Reflected member '{MemberInfoValue.Name}' is not a supported property or field.");
        }
    }
}

using System.Reflection;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Reads and writes one non-public instance member of an editor session by name. Session state
    /// that has moved onto a collaborating service is reached through the forwarding property that
    /// replaced the original field, so fixtures keep asserting the same observable session state
    /// without caring which collaborator now stores it.
    /// </summary>
    internal static class EditorSessionPrivateMemberAccessor {
        /// <summary>
        /// Reads one non-public instance field, or the forwarding property of the same name.
        /// </summary>
        /// <param name="target">Object that owns the member.</param>
        /// <param name="memberName">Exact member name to read.</param>
        /// <returns>Current member value.</returns>
        public static object GetValue(object target, string memberName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            FieldInfo field = FindField(target.GetType(), memberName);
            if (field != null) {
                return field.GetValue(target);
            }

            PropertyInfo property = FindProperty(target.GetType(), memberName);
            if (property == null) {
                throw new InvalidOperationException($"'{target.GetType().Name}' declares no non-public field or property named '{memberName}'.");
            }

            return property.GetValue(target);
        }

        /// <summary>
        /// Assigns one non-public instance field, or the forwarding property of the same name.
        /// </summary>
        /// <param name="target">Object that owns the member.</param>
        /// <param name="memberName">Exact member name to assign.</param>
        /// <param name="value">Value to store.</param>
        public static void SetValue(object target, string memberName, object value) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            FieldInfo field = FindField(target.GetType(), memberName);
            if (field != null) {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = FindProperty(target.GetType(), memberName);
            if (property == null) {
                throw new InvalidOperationException($"'{target.GetType().Name}' declares no non-public field or property named '{memberName}'.");
            }
            if (!property.CanWrite) {
                throw new InvalidOperationException($"'{target.GetType().Name}.{memberName}' is read-only and cannot be assigned by a fixture.");
            }

            property.SetValue(target, value);
        }

        /// <summary>
        /// Finds one non-public instance field declared on the supplied type or any of its base types.
        /// </summary>
        /// <param name="type">Most-derived type to start the search from.</param>
        /// <param name="memberName">Exact field name to resolve.</param>
        /// <returns>Matching field metadata, or null when no base type declares it.</returns>
        static FieldInfo FindField(Type type, string memberName) {
            Type currentType = type;
            while (currentType != null) {
                FieldInfo field = currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Finds one non-public instance property declared on the supplied type or any of its base types.
        /// </summary>
        /// <param name="type">Most-derived type to start the search from.</param>
        /// <param name="memberName">Exact property name to resolve.</param>
        /// <returns>Matching property metadata, or null when no base type declares it.</returns>
        static PropertyInfo FindProperty(Type type, string memberName) {
            Type currentType = type;
            while (currentType != null) {
                PropertyInfo property = currentType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) {
                    return property;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}

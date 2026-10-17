using System.Collections.Generic;

namespace helengine {
    /// <summary>
    /// Centralizes dictionary-shape validation rules shared by reflected scene persistence and runtime deserialization.
    /// </summary>
    public static class ScenePersistenceDictionaryTypeSupport {
        /// <summary>
        /// Supported non-enum scalar key types that preserve one deterministic ordering contract across managed and native persistence paths.
        /// </summary>
        static readonly HashSet<Type> SupportedScalarKeyTypes = new HashSet<Type> {
            typeof(string),
            typeof(byte),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long)
        };

        /// <summary>
        /// Returns whether the supplied type is one closed generic dictionary and exposes its key and value types when present.
        /// </summary>
        /// <param name="valueType">Candidate reflected member type.</param>
        /// <param name="keyType">Resolved dictionary key type when the candidate is one dictionary; otherwise null.</param>
        /// <param name="dictionaryValueType">Resolved dictionary value type when the candidate is one dictionary; otherwise null.</param>
        /// <returns>True when the supplied type is one closed <see cref="Dictionary{TKey, TValue}"/>; otherwise false.</returns>
        public static bool IsDictionaryType(Type valueType, out Type keyType, out Type dictionaryValueType) {
            if (valueType != null
                && valueType.IsGenericType
                && valueType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                Type[] genericArguments = valueType.GetGenericArguments();
                keyType = genericArguments[0];
                dictionaryValueType = genericArguments[1];
                return true;
            }

            keyType = null;
            dictionaryValueType = null;
            return false;
        }

        /// <summary>
        /// Returns whether the supplied type is supported as one persisted dictionary key.
        /// </summary>
        /// <param name="keyType">Reflected dictionary key type under evaluation.</param>
        /// <returns>True when the key type belongs to the supported deterministic subset; otherwise false.</returns>
        public static bool IsSupportedDictionaryKeyType(Type keyType) {
            if (keyType == null) {
                return false;
            }

            if (SupportedScalarKeyTypes.Contains(keyType)) {
                return true;
            }

            if (!keyType.IsEnum) {
                return false;
            }

            return SupportedScalarKeyTypes.Contains(Enum.GetUnderlyingType(keyType));
        }

        /// <summary>
        /// Compares two persisted dictionary keys using the deterministic ordering contract required by shared scene persistence.
        /// </summary>
        /// <param name="leftKey">Left dictionary key.</param>
        /// <param name="rightKey">Right dictionary key.</param>
        /// <param name="keyType">Declared dictionary key type.</param>
        /// <returns>Negative when the left key sorts first, zero when both keys are equal, or positive when the right key sorts first.</returns>
        public static int CompareKeys(object leftKey, object rightKey, Type keyType) {
            if (keyType == null) {
                throw new ArgumentNullException(nameof(keyType));
            }
            if (!IsSupportedDictionaryKeyType(keyType)) {
                throw new InvalidOperationException($"Dictionary key type '{keyType.FullName}' is not supported by shared scene persistence.");
            }
            if (leftKey == null || rightKey == null) {
                throw new InvalidOperationException("Shared scene persistence dictionary keys cannot be null.");
            }

            if (keyType == typeof(string)) {
                return string.CompareOrdinal((string)leftKey, (string)rightKey);
            }

            if (leftKey is IComparable comparableKey) {
                return comparableKey.CompareTo(rightKey);
            }

            throw new InvalidOperationException($"Dictionary key type '{keyType.FullName}' does not expose one supported deterministic comparer.");
        }
    }
}

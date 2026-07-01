using helengine.baseplatform.Definitions;
using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Converts platform-specific synthetic component member values between metadata, editor row types, and serialized payload strings.
    /// </summary>
    public static class PlatformComponentMemberValueUtility {
        /// <summary>
        /// Resolves the managed runtime type used by one synthetic platform member definition.
        /// </summary>
        /// <param name="definition">Synthetic member definition to classify.</param>
        /// <returns>Managed runtime type used by the member.</returns>
        public static Type ResolveValueType(PlatformComponentMemberDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return ResolveValueType(definition.ValueKind);
        }

        /// <summary>
        /// Resolves the managed runtime type used by one synthetic platform member value kind.
        /// </summary>
        /// <param name="valueKind">Synthetic member value kind to classify.</param>
        /// <returns>Managed runtime type used by the member.</returns>
        public static Type ResolveValueType(PlatformComponentMemberValueKind valueKind) {
            return valueKind switch {
                PlatformComponentMemberValueKind.String => typeof(string),
                PlatformComponentMemberValueKind.Boolean => typeof(bool),
                PlatformComponentMemberValueKind.Int32 => typeof(int),
                PlatformComponentMemberValueKind.Single => typeof(float),
                _ => throw new InvalidOperationException($"Unsupported platform component member value kind '{valueKind}'.")
            };
        }

        /// <summary>
        /// Resolves the default inspector row kind used by one synthetic platform member definition.
        /// </summary>
        /// <param name="definition">Synthetic member definition to classify.</param>
        /// <returns>Default inspector row kind used by the member.</returns>
        public static ComponentPropertyRowKind ResolveRowKind(PlatformComponentMemberDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return definition.ValueKind switch {
                PlatformComponentMemberValueKind.Boolean => ComponentPropertyRowKind.Boolean,
                PlatformComponentMemberValueKind.String => ComponentPropertyRowKind.Scalar,
                PlatformComponentMemberValueKind.Int32 => ComponentPropertyRowKind.Scalar,
                PlatformComponentMemberValueKind.Single => ComponentPropertyRowKind.Scalar,
                _ => throw new InvalidOperationException($"Unsupported platform component member value kind '{definition.ValueKind}'.")
            };
        }

        /// <summary>
        /// Parses one serialized synthetic platform member value into its typed editor/runtime representation.
        /// </summary>
        /// <param name="definition">Synthetic member definition that owns the serialized value.</param>
        /// <param name="serializedValue">Serialized value to parse.</param>
        /// <returns>Typed value parsed from the serialized string.</returns>
        public static object ParseValue(PlatformComponentMemberDefinition definition, string serializedValue) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            string value = serializedValue ?? string.Empty;
            return definition.ValueKind switch {
                PlatformComponentMemberValueKind.String => value,
                PlatformComponentMemberValueKind.Boolean => bool.Parse(value),
                PlatformComponentMemberValueKind.Int32 => int.Parse(value, CultureInfo.InvariantCulture),
                PlatformComponentMemberValueKind.Single => float.Parse(value, CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unsupported platform component member value kind '{definition.ValueKind}'.")
            };
        }

        /// <summary>
        /// Serializes one typed synthetic platform member value into its persisted string representation.
        /// </summary>
        /// <param name="definition">Synthetic member definition that owns the value.</param>
        /// <param name="value">Typed value to serialize.</param>
        /// <returns>Serialized string representation used by detached platform overrides.</returns>
        public static string SerializeValue(PlatformComponentMemberDefinition definition, object value) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return definition.ValueKind switch {
                PlatformComponentMemberValueKind.String => value as string ?? string.Empty,
                PlatformComponentMemberValueKind.Boolean => ((bool)value).ToString(CultureInfo.InvariantCulture),
                PlatformComponentMemberValueKind.Int32 => ((int)value).ToString(CultureInfo.InvariantCulture),
                PlatformComponentMemberValueKind.Single => ((float)value).ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unsupported platform component member value kind '{definition.ValueKind}'.")
            };
        }
    }
}

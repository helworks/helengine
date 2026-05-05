using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Generates ordinal runtime deserializer source for one reflected scripted component schema.
    /// </summary>
    public sealed class ScriptComponentPlayerDeserializerGenerator {
        /// <summary>
        /// Generates ordinal runtime deserializer source for the supplied reflected scripted component schema.
        /// </summary>
        /// <param name="schema">Reflected scripted component schema that should drive the generated source.</param>
        /// <returns>Generated ordinal runtime deserializer source.</returns>
        public string Generate(ScriptComponentReflectionSchema schema) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (string.IsNullOrWhiteSpace(schema.ComponentType.FullName)) {
                throw new InvalidOperationException("Scripted component schemas must expose a full component type name.");
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {");
            builder.AppendLine("    using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);");
            builder.AppendLine("    using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);");
            builder.AppendLine($"    {schema.ComponentType.FullName} component = new {schema.ComponentType.FullName}();");
            builder.AppendLine("    byte version = reader.ReadByte();");
            builder.AppendLine("    if (version != AutomaticScriptComponentRuntimeDeserializer.CurrentVersion) {");
            builder.AppendLine("        throw new InvalidOperationException($\"Unsupported automatic scripted component payload version '{version}'.\");");
            builder.AppendLine("    }");
            builder.AppendLine("    int memberCount = reader.ReadInt32();");
            builder.AppendLine($"    if (memberCount != {schema.Members.Count}) {{");
            builder.AppendLine($"        throw new InvalidOperationException($\"Expected {schema.Members.Count} packaged scripted members but payload contained {{memberCount}}.\");");
            builder.AppendLine("    }");

            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                builder.AppendLine($"    component.{member.Name} = {BuildReadExpression(member.ValueType)};");
            }

            builder.AppendLine("    return component;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds one runtime reader expression for the supplied member value type.
        /// </summary>
        /// <param name="valueType">Runtime member value type that should be read from the ordinal payload.</param>
        /// <returns>Generated reader expression.</returns>
        string BuildReadExpression(Type valueType) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (valueType == typeof(string)) {
                return "reader.ReadString()";
            }
            if (valueType == typeof(bool)) {
                return "reader.ReadByte() != 0";
            }
            if (valueType == typeof(byte)) {
                return "reader.ReadByte()";
            }
            if (valueType == typeof(ushort)) {
                return "reader.ReadUInt16()";
            }
            if (valueType == typeof(int)) {
                return "reader.ReadInt32()";
            }
            if (valueType == typeof(uint)) {
                return "reader.ReadUInt32()";
            }
            if (valueType == typeof(long)) {
                return "reader.ReadInt64()";
            }
            if (valueType == typeof(float)) {
                return "reader.ReadSingle()";
            }
            if (valueType == typeof(int2)) {
                return "reader.ReadInt2()";
            }
            if (valueType == typeof(int4)) {
                return "reader.ReadInt4()";
            }
            if (valueType == typeof(float2)) {
                return "reader.ReadFloat2()";
            }
            if (valueType == typeof(float3)) {
                return "reader.ReadFloat3()";
            }
            if (valueType == typeof(float4)) {
                return "reader.ReadFloat4()";
            }
            if (valueType == typeof(byte4)) {
                return "new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte())";
            }
            if (valueType == typeof(SceneEntityReference)) {
                return "reader.ReadSceneEntityReference()";
            }

            throw new InvalidOperationException($"Ordinal scripted component deserializer generation does not support member type '{valueType.FullName}'.");
        }
    }
}

using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Generates ordinal runtime deserializer source for one reflected scripted component schema.
    /// </summary>
    public sealed class ScriptComponentPlayerDeserializerGenerator {
        /// <summary>
        /// Prefix applied to generated native deserializer class names.
        /// </summary>
        const string NativeDeserializerClassPrefix = "GeneratedRuntime";

        /// <summary>
        /// Suffix applied to generated native deserializer class names.
        /// </summary>
        const string NativeDeserializerClassSuffix = "Deserializer";

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
        /// Returns whether the supplied reflected component schema can emit one native runtime deserializer.
        /// </summary>
        /// <param name="schema">Reflected component schema to inspect.</param>
        /// <returns>True when the schema can emit one native runtime deserializer.</returns>
        public bool CanGenerateNativeDeserializer(ScriptComponentReflectionSchema schema) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (schema.ComponentType.GetConstructor(Type.EmptyTypes) == null) {
                return false;
            }

            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                if (!TryBuildNativeReadExpression(member.ValueType, out _)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the generated native deserializer class name for one reflected component schema.
        /// </summary>
        /// <param name="schema">Reflected component schema that will drive the generated class.</param>
        /// <returns>Stable generated native deserializer class name.</returns>
        public string BuildNativeDeserializerClassName(ScriptComponentReflectionSchema schema) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (string.IsNullOrWhiteSpace(schema.ComponentType.Name)) {
                throw new InvalidOperationException("Scripted component schemas must expose a stable component type name.");
            }

            return NativeDeserializerClassPrefix + SanitizeIdentifier(schema.ComponentType.Name) + NativeDeserializerClassSuffix;
        }

        /// <summary>
        /// Generates one native runtime deserializer header for the supplied reflected component schema.
        /// </summary>
        /// <param name="schema">Reflected component schema that should drive the generated native header.</param>
        /// <returns>Generated native header text.</returns>
        public string GenerateNativeDeserializerHeader(ScriptComponentReflectionSchema schema) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (!CanGenerateNativeDeserializer(schema)) {
                throw new InvalidOperationException($"Native runtime deserializer generation does not support component type '{schema.ComponentType.FullName}'.");
            }

            string className = BuildNativeDeserializerClassName(schema);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("#pragma once");
            builder.AppendLine("#ifdef DrawText");
            builder.AppendLine("#undef DrawText");
            builder.AppendLine("#endif");
            builder.AppendLine("#include <cstdint>");
            builder.AppendLine();
            builder.AppendLine("class IRuntimeComponentDeserializer;");
            builder.AppendLine("class Component;");
            builder.AppendLine("class SceneComponentAssetRecord;");
            builder.AppendLine("class RuntimeSceneAssetReferenceResolver;");
            builder.AppendLine();
            builder.AppendLine("#include \"IRuntimeComponentDeserializer.hpp\"");
            builder.AppendLine("#include \"runtime/native_string.hpp\"");
            builder.AppendLine("#include \"Component.hpp\"");
            builder.AppendLine("#include \"SceneComponentAssetRecord.hpp\"");
            builder.AppendLine("#include \"RuntimeSceneAssetReferenceResolver.hpp\"");
            builder.AppendLine();
            builder.AppendLine($"class {className} : public IRuntimeComponentDeserializer");
            builder.AppendLine("{");
            builder.AppendLine("public:");
            builder.AppendLine($"    virtual ~{className}() = default;");
            builder.AppendLine();
            builder.AppendLine("    const std::string& get_ComponentTypeId();");
            builder.AppendLine();
            builder.AppendLine("    ::Component* Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver);");
            builder.AppendLine("private:");
            builder.AppendLine("    static std::string ComponentType;");
            builder.AppendLine();
            builder.AppendLine("    static uint8_t CurrentVersion;");
            builder.AppendLine();
            builder.AppendLine("    static int32_t MemberCount;");
            builder.AppendLine("};");
            return builder.ToString();
        }

        /// <summary>
        /// Generates one native runtime deserializer source file for the supplied reflected component schema.
        /// </summary>
        /// <param name="schema">Reflected component schema that should drive the generated native source.</param>
        /// <returns>Generated native source text.</returns>
        public string GenerateNativeDeserializerSource(ScriptComponentReflectionSchema schema) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (!CanGenerateNativeDeserializer(schema)) {
                throw new InvalidOperationException($"Native runtime deserializer generation does not support component type '{schema.ComponentType.FullName}'.");
            }

            string className = BuildNativeDeserializerClassName(schema);
            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(schema.ComponentType);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("#ifdef DrawText");
            builder.AppendLine("#undef DrawText");
            builder.AppendLine("#endif");
            builder.AppendLine($"#include \"{className}.hpp\"");
            builder.AppendLine("#include \"runtime/native_exceptions.hpp\"");
            builder.AppendLine("#include \"runtime/native_string.hpp\"");
            builder.AppendLine("#include \"system/io/memory-stream.hpp\"");
            builder.AppendLine("#include \"EngineBinaryReader.hpp\"");
            builder.AppendLine("#include \"EngineBinaryEndianness.hpp\"");
            builder.AppendLine("#include \"runtime/array.hpp\"");
            builder.AppendLine($"#include \"{schema.ComponentType.Name}.hpp\"");
            builder.AppendLine();
            builder.AppendLine($"const std::string& {className}::get_ComponentTypeId()");
            builder.AppendLine("{");
            builder.AppendLine("return ComponentType;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"::Component* {className}::Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver)");
            builder.AppendLine("{");
            builder.AppendLine("    if (record == nullptr)");
            builder.AppendLine("    {");
            builder.AppendLine("throw new ArgumentNullException(\"record\");");
            builder.AppendLine("    }");
            builder.AppendLine("    if (!String::Equals(record->get_ComponentTypeId(), ComponentType, StringComparison::Ordinal))");
            builder.AppendLine("    {");
            builder.AppendLine($"throw new InvalidOperationException(std::string(\"Generated runtime component deserializer cannot deserialize '\") + record->get_ComponentTypeId() + std::string(\"'.\"));");
            builder.AppendLine("    }");
            builder.AppendLine("{");
            builder.AppendLine("::MemoryStream *stream = ([&]() {");
            builder.AppendLine("auto __ctor_arg_00000001 = ([&]() {");
            builder.AppendLine("Array<uint8_t>* __coalesce_00000002 = record->get_Payload();");
            builder.AppendLine("return __coalesce_00000002 != nullptr ? __coalesce_00000002 : Array<uint8_t>::Empty();");
            builder.AppendLine("})();");
            builder.AppendLine("auto __ctor_arg_00000003 = false;");
            builder.AppendLine("return new ::MemoryStream(__ctor_arg_00000001, __ctor_arg_00000003);");
            builder.AppendLine("})();");
            builder.AppendLine("{");
            builder.AppendLine("::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);");
            builder.AppendLine("const uint8_t version = reader->ReadByte();");
            builder.AppendLine("    if (version != CurrentVersion)");
            builder.AppendLine("    {");
            builder.AppendLine("throw new InvalidOperationException(std::string(\"Unsupported automatic scripted component payload version '\") + std::to_string(version) + std::string(\"'.\"));");
            builder.AppendLine("    }");
            builder.AppendLine("const int32_t memberCount = reader->ReadInt32();");
            builder.AppendLine("    if (memberCount != MemberCount)");
            builder.AppendLine("    {");
            builder.AppendLine("throw new InvalidOperationException(std::string(\"Expected \") + std::to_string(MemberCount) + std::string(\" packaged scripted members but payload contained \") + std::to_string(memberCount) + std::string(\".\"));");
            builder.AppendLine("    }");
            builder.AppendLine($"::{schema.ComponentType.Name} *component = new ::{schema.ComponentType.Name}();");
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                builder.AppendLine(BuildNativeAssignmentStatement(member, BuildNativeReadExpression(member.ValueType)));
            }

            builder.AppendLine("return component;}");
            builder.AppendLine("}");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"std::string {className}::ComponentType = \"{EscapeForCppString(componentTypeId)}\";");
            builder.AppendLine();
            builder.AppendLine($"uint8_t {className}::CurrentVersion = 1;");
            builder.AppendLine();
            builder.AppendLine($"int32_t {className}::MemberCount = {schema.Members.Count};");
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

        /// <summary>
        /// Builds one native runtime reader expression for the supplied member value type.
        /// </summary>
        /// <param name="valueType">Runtime member value type that should be read from the ordinal payload.</param>
        /// <returns>Generated native reader expression.</returns>
        string BuildNativeReadExpression(Type valueType) {
            if (!TryBuildNativeReadExpression(valueType, out string expression)) {
                throw new InvalidOperationException($"Native scripted component deserializer generation does not support member type '{valueType?.FullName}'.");
            }

            return expression;
        }

        /// <summary>
        /// Attempts to build one native runtime reader expression for the supplied member value type.
        /// </summary>
        /// <param name="valueType">Runtime member value type that should be read from the ordinal payload.</param>
        /// <param name="expression">Generated native reader expression when supported.</param>
        /// <returns>True when the supplied member value type is supported by native generation.</returns>
        bool TryBuildNativeReadExpression(Type valueType, out string expression) {
            expression = string.Empty;
            if (valueType == null) {
                return false;
            }

            if (valueType == typeof(string)) {
                expression = "reader->ReadString()";
                return true;
            }
            if (valueType == typeof(bool)) {
                expression = "reader->ReadByte() != 0";
                return true;
            }
            if (valueType == typeof(byte)) {
                expression = "reader->ReadByte()";
                return true;
            }
            if (valueType == typeof(ushort)) {
                expression = "reader->ReadUInt16()";
                return true;
            }
            if (valueType == typeof(int)) {
                expression = "reader->ReadInt32()";
                return true;
            }
            if (valueType == typeof(uint)) {
                expression = "reader->ReadUInt32()";
                return true;
            }
            if (valueType == typeof(long)) {
                expression = "reader->ReadInt64()";
                return true;
            }
            if (valueType == typeof(float)) {
                expression = "reader->ReadSingle()";
                return true;
            }
            if (valueType == typeof(int2)) {
                expression = "reader->ReadInt2()";
                return true;
            }
            if (valueType == typeof(int4)) {
                expression = "reader->ReadInt4()";
                return true;
            }
            if (valueType == typeof(float2)) {
                expression = "reader->ReadFloat2()";
                return true;
            }
            if (valueType == typeof(float3)) {
                expression = "reader->ReadFloat3()";
                return true;
            }
            if (valueType == typeof(float4)) {
                expression = "reader->ReadFloat4()";
                return true;
            }
            if (valueType == typeof(byte4)) {
                expression = "::byte4(reader->ReadByte(), reader->ReadByte(), reader->ReadByte(), reader->ReadByte())";
                return true;
            }
            if (valueType == typeof(SceneEntityReference)) {
                expression = "reader->ReadSceneEntityReference()";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds the generated native assignment target for one reflected member.
        /// </summary>
        /// <param name="member">Reflected member that should receive the decoded runtime value.</param>
        /// <returns>Generated native assignment target.</returns>
        string BuildNativeWriteTarget(ScriptComponentReflectionMember member) {
            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }

            return member.IsProperty
                ? $"set_{member.Name}"
                : member.Name;
        }

        /// <summary>
        /// Builds one complete native assignment statement for the supplied reflected member.
        /// </summary>
        /// <param name="member">Reflected member that should receive the decoded runtime value.</param>
        /// <param name="expression">Native expression that evaluates to the decoded runtime value.</param>
        /// <returns>Complete native assignment statement.</returns>
        string BuildNativeAssignmentStatement(ScriptComponentReflectionMember member, string expression) {
            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }
            if (string.IsNullOrWhiteSpace(expression)) {
                throw new ArgumentException("Native assignment expression must be provided.", nameof(expression));
            }

            return member.IsProperty
                ? $"component->{BuildNativeWriteTarget(member)}({expression});"
                : $"component->{BuildNativeWriteTarget(member)} = {expression};";
        }

        /// <summary>
        /// Escapes one managed string so it can appear inside a generated C++ string literal.
        /// </summary>
        /// <param name="value">Managed string value to escape.</param>
        /// <returns>Escaped C++ string literal contents.</returns>
        static string EscapeForCppString(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        /// <summary>
        /// Rewrites one arbitrary managed type name into a stable generated native identifier segment.
        /// </summary>
        /// <param name="value">Managed type name to sanitize.</param>
        /// <returns>Identifier-safe text containing only letters, digits, or underscores.</returns>
        static string SanitizeIdentifier(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Identifier text must be provided.", nameof(value));
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            return builder.ToString();
        }
    }
}

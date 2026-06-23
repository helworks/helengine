using System.Text;
using System.Reflection;

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
                if (!TryBuildNativeReadExpression(member.ValueType, BuildNativeReaderVariableName(), BuildNativeNestedHelperMap(schema), out _)) {
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
            foreach (Type includeType in CollectNativeIncludeTypes(schema)) {
                if (includeType != schema.ComponentType) {
                    builder.AppendLine($"#include \"{includeType.Name}.hpp\"");
                }
            }
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
            foreach (KeyValuePair<Type, string> helperEntry in BuildNativeNestedHelperMap(schema).OrderBy(entry => entry.Value, StringComparer.Ordinal)) {
                builder.AppendLine();
                builder.AppendLine($"    static {BuildNativeValueTypeName(helperEntry.Key)} {helperEntry.Value}(::EngineBinaryReader* reader);");
            }
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
            builder.AppendLine("#include \"runtime/native_dictionary.hpp\"");
            builder.AppendLine("#include \"system/io/memory-stream.hpp\"");
            builder.AppendLine("#include \"EngineBinaryReader.hpp\"");
            builder.AppendLine("#include \"EngineBinaryEndianness.hpp\"");
            builder.AppendLine("#include \"runtime/array.hpp\"");
            builder.AppendLine("#include \"runtime/finally.hpp\"");
            builder.AppendLine($"#include \"{schema.ComponentType.Name}.hpp\"");
            foreach (Type includeType in CollectNativeIncludeTypes(schema)) {
                if (includeType != schema.ComponentType) {
                    builder.AppendLine($"#include \"{includeType.Name}.hpp\"");
                }
            }
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
            builder.AppendLine("{");
            builder.AppendLine("::MemoryStream *stream = ([&]() {");
            builder.AppendLine("auto __ctor_arg_00000001 = ([&]() {");
            builder.AppendLine("Array<uint8_t>* __coalesce_00000002 = record->get_Payload();");
            builder.AppendLine("return __coalesce_00000002 != nullptr ? __coalesce_00000002 : Array<uint8_t>::Empty();");
            builder.AppendLine("})();");
            builder.AppendLine("auto __ctor_arg_00000003 = false;");
            builder.AppendLine("return new ::MemoryStream(__ctor_arg_00000001, __ctor_arg_00000003);");
            builder.AppendLine("})();");
            builder.AppendLine("auto __usingDisposeGuard_00000004 = he_cpp_make_scope_exit([&]() {");
            builder.AppendLine("if (stream != nullptr) {");
            builder.AppendLine("stream->Dispose();");
            builder.AppendLine("delete stream;");
            builder.AppendLine("}");
            builder.AppendLine("});");
            builder.AppendLine("{");
            builder.AppendLine("::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);");
            builder.AppendLine("auto __usingDisposeGuard_00000005 = he_cpp_make_scope_exit([&]() {");
            builder.AppendLine("if (reader != nullptr) {");
            builder.AppendLine("reader->Dispose();");
            builder.AppendLine("delete reader;");
            builder.AppendLine("}");
            builder.AppendLine("});");
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
            Dictionary<Type, string> nativeNestedHelperNames = BuildNativeNestedHelperMap(schema);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                builder.AppendLine(BuildNativeAssignmentStatement(member, BuildNativeReadExpression(member.ValueType, BuildNativeReaderVariableName(), nativeNestedHelperNames)));
            }

            builder.AppendLine("return component;}");
            builder.AppendLine("}");
            builder.AppendLine("}");
            foreach (KeyValuePair<Type, string> helperEntry in nativeNestedHelperNames.OrderBy(entry => entry.Value, StringComparer.Ordinal)) {
                builder.AppendLine();
                builder.Append(BuildNativeNestedHelperMethodSource(className, helperEntry.Key, helperEntry.Value));
            }
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
            return BuildManagedReadExpression(valueType, "reader");
        }

        /// <summary>
        /// Builds one managed runtime reader expression for the supplied member value type using the supplied reader variable name.
        /// </summary>
        /// <param name="valueType">Runtime member value type that should be read from the ordinal payload.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated managed reader expression.</returns>
        string BuildManagedReadExpression(Type valueType, string readerVariableName) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (string.IsNullOrWhiteSpace(readerVariableName)) {
                throw new ArgumentException("Reader variable name must be provided.", nameof(readerVariableName));
            }

            if (valueType == typeof(string)) {
                return readerVariableName + ".ReadString()";
            }
            if (valueType == typeof(bool)) {
                return readerVariableName + ".ReadByte() != 0";
            }
            if (valueType == typeof(byte)) {
                return readerVariableName + ".ReadByte()";
            }
            if (valueType == typeof(ushort)) {
                return readerVariableName + ".ReadUInt16()";
            }
            if (valueType == typeof(int)) {
                return readerVariableName + ".ReadInt32()";
            }
            if (valueType == typeof(uint)) {
                return readerVariableName + ".ReadUInt32()";
            }
            if (valueType == typeof(long)) {
                return readerVariableName + ".ReadInt64()";
            }
            if (valueType == typeof(float)) {
                return readerVariableName + ".ReadSingle()";
            }
            if (valueType == typeof(double)) {
                return readerVariableName + ".ReadDouble()";
            }
            if (valueType == typeof(int2)) {
                return readerVariableName + ".ReadInt2()";
            }
            if (valueType == typeof(int4)) {
                return readerVariableName + ".ReadInt4()";
            }
            if (valueType == typeof(float2)) {
                return readerVariableName + ".ReadFloat2()";
            }
            if (valueType == typeof(float3)) {
                return readerVariableName + ".ReadFloat3()";
            }
            if (valueType == typeof(float4)) {
                return readerVariableName + ".ReadFloat4()";
            }
            if (valueType == typeof(byte4)) {
                return $"new byte4({readerVariableName}.ReadByte(), {readerVariableName}.ReadByte(), {readerVariableName}.ReadByte(), {readerVariableName}.ReadByte())";
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                return BuildManagedAssetReferenceReadExpression(valueType, readerVariableName);
            }
            if (valueType == typeof(SceneEntityReference)) {
                return readerVariableName + ".ReadSceneEntityReference()";
            }
            if (valueType.IsEnum) {
                return $"({BuildManagedTypeName(valueType)}){BuildManagedReadExpression(Enum.GetUnderlyingType(valueType), readerVariableName)}";
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                return BuildManagedDictionaryReadExpression(valueType, dictionaryKeyType, dictionaryValueType, readerVariableName);
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
                return $"{readerVariableName}.ReadArray(innerReader => {BuildManagedReadExpression(elementType, "innerReader")})";
            }
            if (IsSupportedNestedObjectType(valueType)) {
                return BuildManagedNestedObjectReadExpression(valueType, readerVariableName);
            }

            throw new InvalidOperationException($"Ordinal scripted component deserializer generation does not support member type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Builds one native runtime reader expression for the supplied member value type.
        /// </summary>
        /// <param name="valueType">Runtime member value type that should be read from the ordinal payload.</param>
        /// <returns>Generated native reader expression.</returns>
        string BuildNativeReadExpression(Type valueType, string readerVariableName, IReadOnlyDictionary<Type, string> nativeNestedHelperNames) {
            if (!TryBuildNativeReadExpression(valueType, readerVariableName, nativeNestedHelperNames, out string expression)) {
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
        bool TryBuildNativeReadExpression(Type valueType, string readerVariableName, IReadOnlyDictionary<Type, string> nativeNestedHelperNames, out string expression) {
            expression = string.Empty;
            if (valueType == null) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(readerVariableName)) {
                return false;
            }
            if (nativeNestedHelperNames == null) {
                return false;
            }

            if (valueType == typeof(string)) {
                expression = readerVariableName + "->ReadString()";
                return true;
            }
            if (valueType == typeof(bool)) {
                expression = readerVariableName + "->ReadByte() != 0";
                return true;
            }
            if (valueType == typeof(byte)) {
                expression = readerVariableName + "->ReadByte()";
                return true;
            }
            if (valueType == typeof(ushort)) {
                expression = readerVariableName + "->ReadUInt16()";
                return true;
            }
            if (valueType == typeof(int)) {
                expression = readerVariableName + "->ReadInt32()";
                return true;
            }
            if (valueType == typeof(uint)) {
                expression = readerVariableName + "->ReadUInt32()";
                return true;
            }
            if (valueType == typeof(long)) {
                expression = readerVariableName + "->ReadInt64()";
                return true;
            }
            if (valueType == typeof(float)) {
                expression = readerVariableName + "->ReadSingle()";
                return true;
            }
            if (valueType == typeof(double)) {
                expression = readerVariableName + "->ReadDouble()";
                return true;
            }
            if (valueType == typeof(int2)) {
                expression = readerVariableName + "->ReadInt2()";
                return true;
            }
            if (valueType == typeof(int4)) {
                expression = readerVariableName + "->ReadInt4()";
                return true;
            }
            if (valueType == typeof(float2)) {
                expression = readerVariableName + "->ReadFloat2()";
                return true;
            }
            if (valueType == typeof(float3)) {
                expression = readerVariableName + "->ReadFloat3()";
                return true;
            }
            if (valueType == typeof(float4)) {
                expression = readerVariableName + "->ReadFloat4()";
                return true;
            }
            if (valueType == typeof(byte4)) {
                expression = "([&]() { "
                    + "::byte4 value {}; "
                    + "value.X = " + readerVariableName + "->ReadByte(); "
                    + "value.Y = " + readerVariableName + "->ReadByte(); "
                    + "value.Z = " + readerVariableName + "->ReadByte(); "
                    + "value.W = " + readerVariableName + "->ReadByte(); "
                    + "return value; "
                    + "})()";
                return true;
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                expression = BuildNativeAssetReferenceReadExpression(valueType, readerVariableName);
                return true;
            }
            if (valueType == typeof(SceneEntityReference)) {
                expression = readerVariableName + "->ReadSceneEntityReference()";
                return true;
            }
            if (valueType.IsEnum) {
                if (!TryBuildNativeReadExpression(Enum.GetUnderlyingType(valueType), readerVariableName, nativeNestedHelperNames, out string underlyingExpression)) {
                    return false;
                }

                expression = $"static_cast<{BuildNativeValueTypeName(valueType)}>({underlyingExpression})";
                return true;
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                if (!ScenePersistenceDictionaryTypeSupport.IsSupportedDictionaryKeyType(dictionaryKeyType)) {
                    return false;
                }
                if (!TryBuildNativeReadExpression(dictionaryKeyType, BuildNativeReaderVariableName(), nativeNestedHelperNames, out _)) {
                    return false;
                }
                if (!TryBuildNativeReadExpression(dictionaryValueType, BuildNativeReaderVariableName(), nativeNestedHelperNames, out _)) {
                    return false;
                }

                expression = BuildNativeInlineDictionaryExpression(valueType, dictionaryKeyType, dictionaryValueType, readerVariableName, nativeNestedHelperNames);
                return true;
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
                if (!TryBuildNativeArrayReadExpression(elementType, readerVariableName, nativeNestedHelperNames, out expression)) {
                    return false;
                }
                return true;
            }
            if (nativeNestedHelperNames.TryGetValue(valueType, out string helperName)) {
                expression = helperName + "(" + readerVariableName + ")";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to build one native array reader expression for the supplied element value type.
        /// </summary>
        /// <param name="elementType">Array element type that should be read from the ordinal payload.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <param name="nativeNestedHelperNames">Known helper method names for nested authored object types.</param>
        /// <param name="expression">Generated native array reader expression when supported.</param>
        /// <returns>True when the supplied array element type is supported by native generation.</returns>
        bool TryBuildNativeArrayReadExpression(
            Type elementType,
            string readerVariableName,
            IReadOnlyDictionary<Type, string> nativeNestedHelperNames,
            out string expression) {
            expression = string.Empty;
            if (elementType == null) {
                return false;
            }

            if (TryBuildNativeReadExpression(elementType, BuildNativeReaderVariableName(), nativeNestedHelperNames, out _)) {
                expression = BuildNativeInlineArrayExpression(elementType, readerVariableName, nativeNestedHelperNames);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds one inline native array reader expression for element types that do not require nested object helper methods.
        /// </summary>
        /// <param name="elementType">Array element type that should be read from the ordinal payload.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <param name="nativeNestedHelperNames">Known helper method names for nested authored object types.</param>
        /// <returns>Generated native array reader expression.</returns>
        string BuildNativeInlineArrayExpression(Type elementType, string readerVariableName, IReadOnlyDictionary<Type, string> nativeNestedHelperNames) {
            string elementValueTypeName = BuildNativeValueTypeName(elementType);
            string elementReadExpression = BuildNativeReadExpression(elementType, BuildNativeReaderVariableName(), nativeNestedHelperNames);
            return "([&]() { "
                + "const int32_t length = " + readerVariableName + "->ReadInt32(); "
                + "if (length == -1) { return static_cast<Array<" + elementValueTypeName + ">*>(nullptr); } "
                + "if (length < -1) { throw new InvalidOperationException(\"Array length cannot be negative.\"); } "
                + "if (length == 0) { return Array<" + elementValueTypeName + ">::Empty(); } "
                + "Array<" + elementValueTypeName + "> *values = new Array<" + elementValueTypeName + ">(length); "
                + "for (int32_t index = 0; index < length; index++) { (*values)[index] = " + elementReadExpression + "; } "
                + "return values; "
                + "})()";
        }

        /// <summary>
        /// Builds one managed dictionary reader expression for the supplied reflected dictionary member type.
        /// </summary>
        /// <param name="dictionaryType">Reflected dictionary member type being restored.</param>
        /// <param name="dictionaryKeyType">Supported reflected dictionary key type.</param>
        /// <param name="dictionaryValueType">Reflected dictionary value type.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated managed dictionary reader expression.</returns>
        string BuildManagedDictionaryReadExpression(Type dictionaryType, Type dictionaryKeyType, Type dictionaryValueType, string readerVariableName) {
            string dictionaryTypeName = BuildManagedTypeName(dictionaryType);
            string dictionaryKeyTypeName = BuildManagedTypeName(dictionaryKeyType);
            string dictionaryValueTypeName = BuildManagedTypeName(dictionaryValueType);
            string keyReadExpression = BuildManagedReadExpression(dictionaryKeyType, readerVariableName);
            string valueReadExpression = BuildManagedReadExpression(dictionaryValueType, readerVariableName);
            return "(() => { "
                + "int entryCount = " + readerVariableName + ".ReadInt32(); "
                + "if (entryCount == -1) { return (" + dictionaryTypeName + ")null; } "
                + "if (entryCount < -1) { throw new InvalidOperationException(\"Dictionary entry count cannot be negative.\"); } "
                + dictionaryTypeName + " dictionary = new " + dictionaryTypeName + "(); "
                + "for (int index = 0; index < entryCount; index++) { "
                + dictionaryKeyTypeName + " key = " + keyReadExpression + "; "
                + "if (dictionary.ContainsKey(key)) { throw new InvalidOperationException(\"Dictionary payload contains duplicate keys.\"); } "
                + dictionaryValueTypeName + " value = " + valueReadExpression + "; "
                + "dictionary.Add(key, value); "
                + "} "
                + "return dictionary; "
                + "})()";
        }

        /// <summary>
        /// Builds one inline native dictionary reader expression for reflected dictionary member types.
        /// </summary>
        /// <param name="dictionaryType">Reflected dictionary member type being restored.</param>
        /// <param name="dictionaryKeyType">Supported reflected dictionary key type.</param>
        /// <param name="dictionaryValueType">Reflected dictionary value type.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <param name="nativeNestedHelperNames">Known helper method names for nested authored object types.</param>
        /// <returns>Generated native dictionary reader expression.</returns>
        string BuildNativeInlineDictionaryExpression(
            Type dictionaryType,
            Type dictionaryKeyType,
            Type dictionaryValueType,
            string readerVariableName,
            IReadOnlyDictionary<Type, string> nativeNestedHelperNames) {
            string dictionaryValueTypeName = BuildNativeValueTypeName(dictionaryType);
            string dictionaryInstanceTypeName = BuildNativeDictionaryInstanceTypeName(dictionaryKeyType, dictionaryValueType);
            string dictionaryKeyTypeName = BuildNativeValueTypeName(dictionaryKeyType);
            string dictionaryElementValueTypeName = BuildNativeValueTypeName(dictionaryValueType);
            string keyReadExpression = BuildNativeReadExpression(dictionaryKeyType, BuildNativeReaderVariableName(), nativeNestedHelperNames);
            string valueReadExpression = BuildNativeReadExpression(dictionaryValueType, BuildNativeReaderVariableName(), nativeNestedHelperNames);
            return "([&]() { "
                + "const int32_t entryCount = " + readerVariableName + "->ReadInt32(); "
                + "if (entryCount == -1) { return static_cast<" + dictionaryValueTypeName + ">(nullptr); } "
                + "if (entryCount < -1) { throw new InvalidOperationException(\"Dictionary entry count cannot be negative.\"); } "
                + dictionaryValueTypeName + " dictionary = new " + dictionaryInstanceTypeName + "(); "
                + "for (int32_t index = 0; index < entryCount; index++) { "
                + dictionaryKeyTypeName + " key = " + keyReadExpression + "; "
                + "if (dictionary->ContainsKey(key)) { throw new InvalidOperationException(\"Dictionary payload contains duplicate keys.\"); } "
                + dictionaryElementValueTypeName + " value = " + valueReadExpression + "; "
                + "dictionary->Add(key, value); "
                + "} "
                + "return dictionary; "
                + "})()";
        }

        /// <summary>
        /// Builds one managed nested-object reader expression for the supplied authored object type.
        /// </summary>
        /// <param name="valueType">Nested authored object type that should be materialized.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated managed nested-object reader expression.</returns>
        string BuildManagedNestedObjectReadExpression(Type valueType, string readerVariableName) {
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            StringBuilder builder = new StringBuilder();
            builder.Append('(');
            builder.Append(readerVariableName);
            builder.Append(".ReadByte() == 0 ? null : new ");
            builder.Append(BuildManagedTypeName(valueType));
            builder.Append(" { ");
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                if (index > 0) {
                    builder.Append(", ");
                }

                builder.Append(member.Name);
                builder.Append(" = ");
                builder.Append(BuildManagedReadExpression(GetSerializableMemberType(member), readerVariableName));
            }

            builder.Append(" })");
            return builder.ToString();
        }

        /// <summary>
        /// Builds one managed runtime reader expression for one asset-backed reflected member.
        /// </summary>
        /// <param name="valueType">Asset-backed reflected member type being restored.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated managed reader expression.</returns>
        string BuildManagedAssetReferenceReadExpression(Type valueType, string readerVariableName) {
            string resolverMethodName = BuildManagedAssetResolverMethodName(valueType);
            string managedTypeName = BuildManagedTypeName(valueType);
            return "(() => { "
                + "SceneAssetReference reference = " + BuildManagedOptionalReferenceReadExpression(readerVariableName) + "; "
                + "return reference == null ? (" + managedTypeName + ")null : referenceResolver." + resolverMethodName + "(reference); "
                + "})()";
        }

        /// <summary>
        /// Builds one managed expression that reads one optional scene asset reference from the current ordinal payload.
        /// </summary>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated managed reference-reader expression.</returns>
        static string BuildManagedOptionalReferenceReadExpression(string readerVariableName) {
            if (string.IsNullOrWhiteSpace(readerVariableName)) {
                throw new ArgumentException("Reader variable name must be provided.", nameof(readerVariableName));
            }

            return "("
                + readerVariableName + ".ReadByte() == 0 ? null : new SceneAssetReference { "
                + "SourceKind = (SceneAssetReferenceSourceKind)" + readerVariableName + ".ReadInt32(), "
                + "RelativePath = " + readerVariableName + ".ReadString(), "
                + "ProviderId = " + readerVariableName + ".ReadString(), "
                + "AssetId = " + readerVariableName + ".ReadString() })";
        }

        /// <summary>
        /// Builds the managed runtime scene-asset resolver method name used for one asset-backed reflected member type.
        /// </summary>
        /// <param name="valueType">Asset-backed reflected member type under evaluation.</param>
        /// <returns>Managed resolver method name.</returns>
        static string BuildManagedAssetResolverMethodName(Type valueType) {
            if (valueType == typeof(FontAsset)) {
                return "ResolveFont";
            }
            if (valueType == typeof(RuntimeTexture)) {
                return "ResolveTexture";
            }
            if (valueType == typeof(RuntimeModel)) {
                return "ResolveModel";
            }
            if (valueType == typeof(RuntimeMaterial)) {
                return "ResolveMaterial";
            }
            if (valueType == typeof(AnimationClipAsset)) {
                return "ResolveAnimationClip";
            }

            throw new InvalidOperationException($"Ordinal scripted component deserializer generation does not support asset-backed member type '{valueType?.FullName}'.");
        }

        /// <summary>
        /// Builds one native runtime reader expression for one asset-backed reflected member.
        /// </summary>
        /// <param name="valueType">Asset-backed reflected member type being restored.</param>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated native reader expression.</returns>
        string BuildNativeAssetReferenceReadExpression(Type valueType, string readerVariableName) {
            string nativeValueTypeName = BuildNativeValueTypeName(valueType);
            string resolverMethodName = BuildManagedAssetResolverMethodName(valueType);
            return "([&]() { "
                + "::SceneAssetReference* reference = " + BuildNativeOptionalReferenceReadExpression(readerVariableName) + "; "
                + "if (reference == nullptr) { return static_cast<" + nativeValueTypeName + ">(nullptr); } "
                + "auto cleanup = he_cpp_make_scope_exit([&]() { delete reference; }); "
                + "if (referenceResolver == nullptr) { throw new InvalidOperationException(\"Runtime scene asset reference resolver is required.\"); } "
                + "return referenceResolver->" + resolverMethodName + "(reference); "
                + "})()";
        }

        /// <summary>
        /// Builds one native expression that reads one optional scene asset reference from the current ordinal payload.
        /// </summary>
        /// <param name="readerVariableName">Reader variable name to reference inside the emitted expression.</param>
        /// <returns>Generated native reference-reader expression.</returns>
        static string BuildNativeOptionalReferenceReadExpression(string readerVariableName) {
            if (string.IsNullOrWhiteSpace(readerVariableName)) {
                throw new ArgumentException("Reader variable name must be provided.", nameof(readerVariableName));
            }

            return "([&]() { "
                + "if (" + readerVariableName + "->ReadByte() == 0) { return static_cast<::SceneAssetReference*>(nullptr); } "
                + "::SceneAssetReference* value = new ::SceneAssetReference(); "
                + "value->set_SourceKind(static_cast<::SceneAssetReferenceSourceKind>(" + readerVariableName + "->ReadInt32())); "
                + "value->set_RelativePath(" + readerVariableName + "->ReadString()); "
                + "value->set_ProviderId(" + readerVariableName + "->ReadString()); "
                + "value->set_AssetId(" + readerVariableName + "->ReadString()); "
                + "return value; "
                + "})()";
        }

        /// <summary>
        /// Builds the helper-method declarations required to materialize nested authored object types in generated native deserializers.
        /// </summary>
        /// <param name="schema">Reflected component schema that drives the generated native deserializer.</param>
        /// <returns>Stable mapping from nested authored object type to generated helper method name.</returns>
        Dictionary<Type, string> BuildNativeNestedHelperMap(ScriptComponentReflectionSchema schema) {
            Dictionary<Type, string> helperNames = new Dictionary<Type, string>();
            CollectNativeNestedHelperTypes(schema.ComponentType, helperNames);
            return helperNames;
        }

        /// <summary>
        /// Recursively discovers nested authored object or struct types that require helper methods in generated native deserializers.
        /// </summary>
        /// <param name="rootType">Root reflected type whose member graph should be inspected.</param>
        /// <param name="helperNames">Accumulated helper method names keyed by nested authored object type.</param>
        void CollectNativeNestedHelperTypes(Type rootType, IDictionary<Type, string> helperNames) {
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(rootType);
            for (int index = 0; index < members.Count; index++) {
                Type memberType = GetSerializableMemberType(members[index]);
                CollectNativeNestedHelperTypesForValue(memberType, helperNames);
            }
        }

        /// <summary>
        /// Recursively discovers helper-backed nested authored object or struct types inside one reflected member value type.
        /// </summary>
        /// <param name="valueType">Reflected member value type to inspect.</param>
        /// <param name="helperNames">Accumulated helper method names keyed by nested authored object type.</param>
        void CollectNativeNestedHelperTypesForValue(Type valueType, IDictionary<Type, string> helperNames) {
            if (valueType == null) {
                return;
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out _, out Type dictionaryValueType)) {
                CollectNativeNestedHelperTypesForValue(dictionaryValueType, helperNames);
                return;
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                CollectNativeNestedHelperTypesForValue(valueType.GetElementType(), helperNames);
                return;
            }
            if (!IsSupportedNestedObjectType(valueType)) {
                return;
            }
            if (helperNames.ContainsKey(valueType)) {
                return;
            }

            helperNames.Add(valueType, "Read" + SanitizeIdentifier(valueType.Name));
            CollectNativeNestedHelperTypes(valueType, helperNames);
        }

        /// <summary>
        /// Collects the additional generated native header includes required by recursively supported member value types.
        /// </summary>
        /// <param name="schema">Reflected component schema that drives the generated native deserializer.</param>
        /// <returns>Deterministically ordered generated native include types.</returns>
        IReadOnlyList<Type> CollectNativeIncludeTypes(ScriptComponentReflectionSchema schema) {
            HashSet<Type> includeTypes = new HashSet<Type>();
            for (int index = 0; index < schema.Members.Count; index++) {
                CollectNativeIncludeTypesForValue(schema.Members[index].ValueType, includeTypes);
            }

            return includeTypes.OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Recursively collects generated native header includes required by one reflected member value type.
        /// </summary>
        /// <param name="valueType">Reflected member value type to inspect.</param>
        /// <param name="includeTypes">Accumulated generated native include types.</param>
        void CollectNativeIncludeTypesForValue(Type valueType, ISet<Type> includeTypes) {
            if (valueType == null) {
                return;
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                CollectNativeIncludeTypesForValue(dictionaryKeyType, includeTypes);
                CollectNativeIncludeTypesForValue(dictionaryValueType, includeTypes);
                return;
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                CollectNativeIncludeTypesForValue(valueType.GetElementType(), includeTypes);
                return;
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                includeTypes.Add(valueType);
                includeTypes.Add(typeof(SceneAssetReference));
                includeTypes.Add(typeof(SceneAssetReferenceSourceKind));
            }
            if (valueType.IsEnum || IsSupportedNestedObjectType(valueType) || valueType == typeof(SceneEntityReference)) {
                includeTypes.Add(valueType);
            }
            if (IsSupportedNestedObjectType(valueType)) {
                IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
                for (int index = 0; index < members.Count; index++) {
                    CollectNativeIncludeTypesForValue(GetSerializableMemberType(members[index]), includeTypes);
                }
            }
        }

        /// <summary>
        /// Builds one generated native helper-method source block that materializes one nested authored object or struct type.
        /// </summary>
        /// <param name="className">Generated native deserializer class name that owns the helper method.</param>
        /// <param name="valueType">Nested authored object type materialized by the helper method.</param>
        /// <param name="helperName">Generated helper method name.</param>
        /// <returns>Generated native helper-method source block.</returns>
        string BuildNativeNestedHelperMethodSource(string className, Type valueType, string helperName) {
            IReadOnlyList<MemberInfo> members = GetSerializableMembers(valueType);
            Dictionary<Type, string> nativeNestedHelperNames = new Dictionary<Type, string>();
            CollectNativeNestedHelperTypes(valueType, nativeNestedHelperNames);
            nativeNestedHelperNames[valueType] = helperName;
            string valueAccessOperator = valueType.IsValueType ? "." : "->";
            string valueConstructionExpression = valueType.IsValueType
                ? $"{BuildNativeValueTypeName(valueType)} value {{}};"
                : $"{BuildNativeValueTypeName(valueType)} value = new ::{valueType.Name}();";
            string omittedValueExpression = valueType.IsValueType
                ? $"{BuildNativeValueTypeName(valueType)}()"
                : "nullptr";

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{BuildNativeValueTypeName(valueType)} {className}::{helperName}(::EngineBinaryReader* reader)");
            builder.AppendLine("{");
            builder.AppendLine("    if (reader == nullptr)");
            builder.AppendLine("    {");
            builder.AppendLine("throw new ArgumentNullException(\"reader\");");
            builder.AppendLine("    }");
            builder.AppendLine("    if (reader->ReadByte() == 0)");
            builder.AppendLine("    {");
            builder.AppendLine($"return {omittedValueExpression};");
            builder.AppendLine("    }");
            builder.AppendLine($"    {valueConstructionExpression}");
            for (int index = 0; index < members.Count; index++) {
                MemberInfo member = members[index];
                string assignmentTarget = BuildNativeMemberAssignmentTarget(member);
                string expression = BuildNativeReadExpression(GetSerializableMemberType(member), "reader", nativeNestedHelperNames);
                if (member is PropertyInfo) {
                    builder.AppendLine($"    value{valueAccessOperator}{assignmentTarget}({expression});");
                } else {
                    builder.AppendLine($"    value{valueAccessOperator}{assignmentTarget} = {expression};");
                }
            }
            builder.AppendLine("    return value;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Gets the stable native reader variable name used inside generated helper expressions.
        /// </summary>
        /// <returns>Stable native reader variable name.</returns>
        static string BuildNativeReaderVariableName() {
            return "reader";
        }

        /// <summary>
        /// Builds the native value type name used by generated deserializer expressions.
        /// </summary>
        /// <param name="valueType">Managed reflected member type whose generated native value type name should be returned.</param>
        /// <returns>Generated native value type name.</returns>
        static string BuildNativeValueTypeName(Type valueType) {
            if (valueType == typeof(string)) {
                return "std::string";
            }
            if (valueType == typeof(bool)) {
                return "bool";
            }
            if (valueType == typeof(byte)) {
                return "uint8_t";
            }
            if (valueType == typeof(ushort)) {
                return "uint16_t";
            }
            if (valueType == typeof(int)) {
                return "int32_t";
            }
            if (valueType == typeof(uint)) {
                return "uint32_t";
            }
            if (valueType == typeof(long)) {
                return "int64_t";
            }
            if (valueType == typeof(float)) {
                return "float";
            }
            if (valueType == typeof(double)) {
                return "double";
            }
            if (valueType == typeof(int2)) {
                return "::int2";
            }
            if (valueType == typeof(int4)) {
                return "::int4";
            }
            if (valueType == typeof(float2)) {
                return "::float2";
            }
            if (valueType == typeof(float3)) {
                return "::float3";
            }
            if (valueType == typeof(float4)) {
                return "::float4";
            }
            if (valueType == typeof(byte4)) {
                return "::byte4";
            }
            if (valueType == typeof(FontAsset)) {
                return "::FontAsset*";
            }
            if (valueType == typeof(RuntimeTexture)) {
                return "::RuntimeTexture*";
            }
            if (valueType == typeof(RuntimeModel)) {
                return "::RuntimeModel*";
            }
            if (valueType == typeof(RuntimeMaterial)) {
                return "::RuntimeMaterial*";
            }
            if (valueType == typeof(AnimationClipAsset)) {
                return "::AnimationClipAsset*";
            }
            if (valueType == typeof(SceneEntityReference)) {
                return "::SceneEntityReference*";
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out Type dictionaryKeyType, out Type dictionaryValueType)) {
                return $"{BuildNativeDictionaryInstanceTypeName(dictionaryKeyType, dictionaryValueType)}*";
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
                return $"Array<{BuildNativeValueTypeName(elementType)}>*";
            }
            if (valueType.IsEnum) {
                return $"::{valueType.Name}";
            }
            if (IsSupportedNestedObjectType(valueType)) {
                return valueType.IsValueType
                    ? $"::{valueType.Name}"
                    : $"::{valueType.Name}*";
            }

            throw new InvalidOperationException($"Native scripted component deserializer generation does not support member type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Builds the native dictionary instance type name used by generated deserializer expressions.
        /// </summary>
        /// <param name="dictionaryKeyType">Supported reflected dictionary key type.</param>
        /// <param name="dictionaryValueType">Reflected dictionary value type.</param>
        /// <returns>Generated native dictionary instance type name.</returns>
        static string BuildNativeDictionaryInstanceTypeName(Type dictionaryKeyType, Type dictionaryValueType) {
            return $"Dictionary<{BuildNativeValueTypeName(dictionaryKeyType)}, {BuildNativeValueTypeName(dictionaryValueType)}>";
        }

        /// <summary>
        /// Builds the managed type name used by generated ordinal runtime deserializer source.
        /// </summary>
        /// <param name="valueType">Managed reflected member type whose managed type name should be returned.</param>
        /// <returns>Generated managed type name.</returns>
        static string BuildManagedTypeName(Type valueType) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (valueType.IsArray && valueType.GetArrayRank() == 1) {
                Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Array type '{valueType.FullName}' must expose one element type.");
                return $"{BuildManagedTypeName(elementType)}[]";
            }
            if (valueType.IsGenericType) {
                Type genericDefinition = valueType.GetGenericTypeDefinition();
                string genericTypeName = genericDefinition.FullName?.Replace('+', '.') ?? genericDefinition.Name;
                int arityDelimiterIndex = genericTypeName.IndexOf('`');
                if (arityDelimiterIndex >= 0) {
                    genericTypeName = genericTypeName[..arityDelimiterIndex];
                }

                string genericArguments = string.Join(", ", valueType.GetGenericArguments().Select(BuildManagedTypeName));
                return $"global::{genericTypeName}<{genericArguments}>";
            }

            string fullName = valueType.FullName?.Replace('+', '.') ?? valueType.Name;
            return $"global::{fullName}";
        }

        /// <summary>
        /// Returns whether the supplied type can be deserialized as one nested authored object or struct by recursively traversing writable public members.
        /// </summary>
        /// <param name="valueType">Runtime value type to inspect.</param>
        /// <returns>True when the type can be deserialized as one nested authored object or struct.</returns>
        static bool IsSupportedNestedObjectType(Type valueType) {
            if (valueType == null) {
                return false;
            }
            if (valueType == typeof(string) || valueType.IsAbstract) {
                return false;
            }
            if (valueType.IsEnum) {
                return false;
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                return false;
            }
            if (IsDirectlySupportedScalarValueType(valueType)) {
                return false;
            }
            if (!valueType.IsClass && !valueType.IsValueType) {
                return false;
            }
            if (ScenePersistenceDictionaryTypeSupport.IsDictionaryType(valueType, out _, out _)) {
                return false;
            }
            if (typeof(Component).IsAssignableFrom(valueType) || typeof(Entity).IsAssignableFrom(valueType)) {
                return false;
            }
            if (valueType.IsValueType) {
                return true;
            }

            return valueType.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Returns whether the supplied type already has one direct scalar read path in generated scripted-component deserializers and therefore must not be treated as one nested object include/helper type.
        /// </summary>
        /// <param name="valueType">Reflected member value type under evaluation.</param>
        /// <returns>True when the type is read directly from the binary reader without nested helper generation.</returns>
        static bool IsDirectlySupportedScalarValueType(Type valueType) {
            if (valueType == null) {
                return false;
            }

            return valueType == typeof(bool)
                || valueType == typeof(byte)
                || valueType == typeof(ushort)
                || valueType == typeof(int)
                || valueType == typeof(uint)
                || valueType == typeof(long)
                || valueType == typeof(float)
                || valueType == typeof(double);
        }

        /// <summary>
        /// Gets the deterministically ordered writable public members that participate in nested authored-object deserializer generation.
        /// </summary>
        /// <param name="valueType">Runtime object type whose writable public members should be returned.</param>
        /// <returns>Deterministically ordered writable public members.</returns>
        static IReadOnlyList<MemberInfo> GetSerializableMembers(Type valueType) {
            return valueType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSerializableMember)
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Returns whether one public instance member is eligible for nested authored-object deserializer generation.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>True when the member should participate in nested authored-object deserializer generation.</returns>
        static bool IsSerializableMember(MemberInfo memberInfo) {
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

        /// <summary>
        /// Gets the runtime value type stored by one writable reflected member.
        /// </summary>
        /// <param name="memberInfo">Writable public instance member whose value type should be returned.</param>
        /// <returns>Runtime value type stored by the member.</returns>
        static Type GetSerializableMemberType(MemberInfo memberInfo) {
            if (memberInfo is PropertyInfo propertyInfo) {
                return propertyInfo.PropertyType;
            }
            if (memberInfo is FieldInfo fieldInfo) {
                return fieldInfo.FieldType;
            }

            throw new InvalidOperationException($"Reflected member '{memberInfo?.Name}' is not a supported property or field.");
        }

        /// <summary>
        /// Builds the generated native assignment target for one nested authored-object member.
        /// </summary>
        /// <param name="memberInfo">Writable reflected member that should receive the decoded runtime value.</param>
        /// <returns>Generated native assignment target.</returns>
        static string BuildNativeMemberAssignmentTarget(MemberInfo memberInfo) {
            if (memberInfo == null) {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            return memberInfo is PropertyInfo
                ? "set_" + memberInfo.Name
                : memberInfo.Name;
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

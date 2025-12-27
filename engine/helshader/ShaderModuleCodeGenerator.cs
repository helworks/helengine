using helengine;

namespace helshader {
    /// <summary>
    /// Generates C# shader module source files from compiled metadata.
    /// </summary>
    public class ShaderModuleCodeGenerator {
        /// <summary>
        /// Fully qualified name of the generated module type.
        /// </summary>
        const string ModuleTypeName = "helengine.HelengineShaderModule";

        /// <summary>
        /// Generates a shader module source file.
        /// </summary>
        /// <param name="moduleData">Module metadata.</param>
        /// <param name="outputDir">Directory for generated source files.</param>
        /// <returns>Code generation result.</returns>
        public ShaderModuleCodegenResult Generate(ShaderModuleData moduleData, string outputDir) {
            if (moduleData == null) {
                throw new ArgumentNullException(nameof(moduleData));
            }

            if (string.IsNullOrWhiteSpace(outputDir)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDir));
            }

            Directory.CreateDirectory(outputDir);
            string sourcePath = Path.Combine(outputDir, $"{moduleData.ModuleName}.shader.cs");

            ShaderCodeWriter writer = new ShaderCodeWriter();
            WriteHeader(writer);
            WriteNamespaceStart(writer);
            WriteModuleClass(writer, moduleData);
            WriteNamespaceEnd(writer);

            File.WriteAllText(sourcePath, writer.GetText());
            return new ShaderModuleCodegenResult(sourcePath, ModuleTypeName);
        }

        /// <summary>
        /// Writes the standard file header and using directives.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        void WriteHeader(ShaderCodeWriter writer) {
            writer.WriteLine("using System;");
            writer.WriteLine("using System.IO;");
            writer.WriteLine("");
        }

        /// <summary>
        /// Writes the namespace opening brace.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        void WriteNamespaceStart(ShaderCodeWriter writer) {
            writer.WriteLine("namespace helengine {");
            writer.IncreaseIndent();
        }

        /// <summary>
        /// Writes the namespace closing brace.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        void WriteNamespaceEnd(ShaderCodeWriter writer) {
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the module class definition.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="moduleData">Module metadata.</param>
        void WriteModuleClass(ShaderCodeWriter writer, ShaderModuleData moduleData) {
            writer.WriteLine("/// <summary>");
            writer.WriteLine($"/// Generated shader module for {moduleData.ModuleName}.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public sealed class HelengineShaderModule : IShaderModule {");
            writer.IncreaseIndent();
            WriteBuildDefinitionMethod(writer, moduleData);
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the BuildDefinition method for the module.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="moduleData">Module metadata.</param>
        void WriteBuildDefinitionMethod(ShaderCodeWriter writer, ShaderModuleData moduleData) {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Builds the shader module definition from the module root.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("/// <param name=\"moduleRoot\">Directory containing the module binaries.</param>");
            writer.WriteLine("/// <returns>Shader module definition.</returns>");
            writer.WriteLine("public ShaderModuleDefinition BuildDefinition(string moduleRoot) {");
            writer.IncreaseIndent();
            writer.WriteLine("if (string.IsNullOrWhiteSpace(moduleRoot)) {");
            writer.IncreaseIndent();
            writer.WriteLine("throw new ArgumentException(\"Module root must be provided.\", nameof(moduleRoot));");
            writer.DecreaseIndent();
            writer.WriteLine("}");
            writer.WriteLine("");
            WriteProgramArray(writer, moduleData.Programs);
            writer.WriteLine("");
            WriteBinaryArray(writer, moduleData.Binaries);
            writer.WriteLine("");
            writer.WriteLine($"return new ShaderModuleDefinition(\"{moduleData.ModuleName}\", programs, binaries);");
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the program definition array.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="programs">Program metadata list.</param>
        void WriteProgramArray(ShaderCodeWriter writer, ShaderProgramData[] programs) {
            writer.WriteLine("ShaderProgramDefinition[] programs = new ShaderProgramDefinition[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < programs.Length; i++) {
                ShaderProgramData program = programs[i];
                WriteProgramDefinition(writer, program, i < programs.Length - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("};");
        }

        /// <summary>
        /// Writes a single shader program definition entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="program">Program metadata.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteProgramDefinition(ShaderCodeWriter writer, ShaderProgramData program, bool trailingComma) {
            writer.WriteLine("new ShaderProgramDefinition(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{program.Name}\",");
            writer.WriteLine($"ShaderStage.{program.Stage},");
            writer.WriteLine($"\"{program.EntryPoint}\",");
            WriteBindingsArray(writer, program.Bindings);
            writer.WriteLine(",");
            WriteSignatureArray(writer, "ShaderVertexElement", program.Inputs);
            writer.WriteLine(",");
            WriteSignatureArray(writer, "ShaderVertexElement", program.Outputs);
            writer.WriteLine(",");
            WriteVariantsArray(writer, program.Variants);
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Writes the bindings array for a shader program.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="bindings">Binding list.</param>
        void WriteBindingsArray(ShaderCodeWriter writer, ShaderBinding[] bindings) {
            if (bindings.Length == 0) {
                writer.WriteLine("Array.Empty<ShaderBinding>()");
                return;
            }

            writer.WriteLine("new ShaderBinding[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < bindings.Length; i++) {
                ShaderBinding binding = bindings[i];
                WriteBinding(writer, binding, i < bindings.Length - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes a single shader binding entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="binding">Binding data.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteBinding(ShaderCodeWriter writer, ShaderBinding binding, bool trailingComma) {
            writer.WriteLine("new ShaderBinding(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{binding.Name}\",");
            writer.WriteLine($"ShaderResourceType.{binding.Type},");
            writer.WriteLine($"{binding.Set},");
            writer.WriteLine($"{binding.Slot},");
            writer.WriteLine($"{binding.Size},");
            WriteMembersArray(writer, binding.Members);
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Writes the constant buffer member array.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="members">Member list.</param>
        void WriteMembersArray(ShaderCodeWriter writer, IReadOnlyList<ShaderConstantMember> members) {
            if (members.Count == 0) {
                writer.WriteLine("Array.Empty<ShaderConstantMember>()");
                return;
            }

            writer.WriteLine("new ShaderConstantMember[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < members.Count; i++) {
                ShaderConstantMember member = members[i];
                WriteMember(writer, member, i < members.Count - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes a single constant buffer member entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="member">Member data.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteMember(ShaderCodeWriter writer, ShaderConstantMember member, bool trailingComma) {
            writer.WriteLine("new ShaderConstantMember(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{member.Name}\",");
            writer.WriteLine($"\"{member.Type}\",");
            writer.WriteLine($"{member.Offset},");
            writer.WriteLine($"{member.Size}");
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Writes a signature element array.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="typeName">Element type name.</param>
        /// <param name="elements">Element list.</param>
        void WriteSignatureArray(ShaderCodeWriter writer, string typeName, ShaderVertexElement[] elements) {
            if (elements.Length == 0) {
                writer.WriteLine($"Array.Empty<{typeName}>()");
                return;
            }

            writer.WriteLine($"new {typeName}[] {{");
            writer.IncreaseIndent();
            for (int i = 0; i < elements.Length; i++) {
                ShaderVertexElement element = elements[i];
                WriteSignatureElement(writer, element, i < elements.Length - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes a signature element entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="element">Signature element data.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteSignatureElement(ShaderCodeWriter writer, ShaderVertexElement element, bool trailingComma) {
            writer.WriteLine("new ShaderVertexElement(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{element.Semantic}\",");
            writer.WriteLine($"{element.Index},");
            writer.WriteLine($"\"{element.Format}\"");
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Writes the variant array.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="variants">Variant list.</param>
        void WriteVariantsArray(ShaderCodeWriter writer, ShaderVariant[] variants) {
            if (variants.Length == 0) {
                writer.WriteLine("Array.Empty<ShaderVariant>()");
                return;
            }

            writer.WriteLine("new ShaderVariant[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < variants.Length; i++) {
                ShaderVariant variant = variants[i];
                WriteVariant(writer, variant, i < variants.Length - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes a single variant entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="variant">Variant data.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteVariant(ShaderCodeWriter writer, ShaderVariant variant, bool trailingComma) {
            writer.WriteLine("new ShaderVariant(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{variant.Name}\",");
            WriteStringArray(writer, variant.Defines);
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Writes an array of string literals.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="values">String list.</param>
        void WriteStringArray(ShaderCodeWriter writer, IReadOnlyList<string> values) {
            if (values.Count == 0) {
                writer.WriteLine("Array.Empty<string>()");
                return;
            }

            writer.WriteLine("new string[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < values.Count; i++) {
                string value = values[i];
                if (value == null) {
                    value = string.Empty;
                }
                value = EscapeForLiteral(value);
                string suffix = i < values.Count - 1 ? "," : string.Empty;
                writer.WriteLine($"\"{value}\"{suffix}");
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the shader binary array.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="binaries">Binary metadata list.</param>
        void WriteBinaryArray(ShaderCodeWriter writer, ShaderBinaryData[] binaries) {
            writer.WriteLine("ShaderProgramBinary[] binaries = new ShaderProgramBinary[] {");
            writer.IncreaseIndent();
            for (int i = 0; i < binaries.Length; i++) {
                ShaderBinaryData binary = binaries[i];
                WriteBinary(writer, binary, i < binaries.Length - 1);
            }
            writer.DecreaseIndent();
            writer.WriteLine("};");
        }

        /// <summary>
        /// Writes a single binary entry.
        /// </summary>
        /// <param name="writer">Code writer.</param>
        /// <param name="binary">Binary data.</param>
        /// <param name="trailingComma">True when a trailing comma should be added.</param>
        void WriteBinary(ShaderCodeWriter writer, ShaderBinaryData binary, bool trailingComma) {
            string relativePath = EscapeForLiteral(binary.RelativePath);
            writer.WriteLine("new ShaderProgramBinary(");
            writer.IncreaseIndent();
            writer.WriteLine($"\"{binary.ProgramName}\",");
            writer.WriteLine($"ShaderStage.{binary.Stage},");
            writer.WriteLine($"\"{binary.Target}\",");
            writer.WriteLine($"\"{binary.Variant}\",");
            writer.WriteLine($"Path.GetFullPath(Path.Combine(moduleRoot, \"{relativePath}\"))");
            writer.DecreaseIndent();
            writer.WriteLine(trailingComma ? ")," : ")");
        }

        /// <summary>
        /// Escapes a string for use in a C# string literal.
        /// </summary>
        /// <param name="value">Value to escape.</param>
        /// <returns>Escaped string.</returns>
        string EscapeForLiteral(string value) {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

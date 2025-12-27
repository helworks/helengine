using helengine;

namespace helshader {
    /// <summary>
    /// Builds shader module metadata from reflection inputs and manifest data.
    /// </summary>
    public class ShaderModuleDataBuilder {
        /// <summary>
        /// Stage resolver for mapping stage strings.
        /// </summary>
        readonly ShaderStageResolver stageResolver;

        /// <summary>
        /// Resource type resolver for binding types.
        /// </summary>
        readonly ShaderResourceTypeResolver resourceResolver;

        /// <summary>
        /// Reflection reader for loading metadata.
        /// </summary>
        readonly ShaderReflectionReader reflectionReader;

        /// <summary>
        /// Output namer used to locate reflection files.
        /// </summary>
        readonly ShaderOutputNamer outputNamer;

        /// <summary>
        /// Initializes a new module data builder.
        /// </summary>
        public ShaderModuleDataBuilder() {
            stageResolver = new ShaderStageResolver();
            resourceResolver = new ShaderResourceTypeResolver();
            reflectionReader = new ShaderReflectionReader();
            outputNamer = new ShaderOutputNamer();
        }

        /// <summary>
        /// Builds shader module metadata for a shader entry.
        /// </summary>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="variants">Variants to include.</param>
        /// <param name="targets">Targets to include.</param>
        /// <param name="paths">Resolved output paths.</param>
        /// <returns>Shader module metadata.</returns>
        public ShaderModuleData Build(
            ShaderManifestShader shader,
            ShaderManifestVariant[] variants,
            string[] targets,
            ShaderPathInfo paths) {
            if (shader == null) {
                throw new ArgumentNullException(nameof(shader));
            }

            if (variants == null || variants.Length == 0) {
                throw new ArgumentException("At least one variant is required.", nameof(variants));
            }

            if (targets == null || targets.Length == 0) {
                throw new ArgumentException("At least one target is required.", nameof(targets));
            }

            if (paths == null) {
                throw new ArgumentNullException(nameof(paths));
            }

            List<ShaderProgramData> programs = new List<ShaderProgramData>();
            List<ShaderBinaryData> binaries = new List<ShaderBinaryData>();

            for (int entryIndex = 0; entryIndex < shader.Entries.Length; entryIndex++) {
                ShaderManifestEntryPoint entry = shader.Entries[entryIndex];
                ShaderStage stage = stageResolver.Parse(entry.Stage);
                string programName = $"{shader.Name}.{stageResolver.GetStageSuffix(stage)}";

                ShaderProgramData programData = BuildProgramData(shader, entry, stage, programName, variants, paths);
                programs.Add(programData);

                AppendBinaryData(binaries, shader, stage, programName, variants, targets, paths);
            }

            return new ShaderModuleData(shader.Name, programs.ToArray(), binaries.ToArray());
        }

        /// <summary>
        /// Builds shader program metadata for a specific entry point.
        /// </summary>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="entry">Entry point definition.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="programName">Program name.</param>
        /// <param name="variants">Variant list.</param>
        /// <param name="paths">Resolved output paths.</param>
        /// <returns>Program metadata.</returns>
        ShaderProgramData BuildProgramData(
            ShaderManifestShader shader,
            ShaderManifestEntryPoint entry,
            ShaderStage stage,
            string programName,
            ShaderManifestVariant[] variants,
            ShaderPathInfo paths) {
            ShaderReflectionEntry reflection = LoadReflection(shader, stage, variants[0], paths);
            ValidateReflectionConsistency(shader, stage, variants, paths, reflection);

            ShaderBinding[] bindings = BuildBindings(reflection.Bindings);
            ShaderVertexElement[] inputs = BuildSignatureElements(reflection.Inputs);
            ShaderVertexElement[] outputs = BuildSignatureElements(reflection.Outputs);
            ShaderVariant[] variantData = BuildVariants(variants);

            return new ShaderProgramData(programName, stage, entry.Entry, bindings, inputs, outputs, variantData);
        }

        /// <summary>
        /// Appends binary metadata entries for the program.
        /// </summary>
        /// <param name="binaries">Binary list to populate.</param>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="programName">Program name.</param>
        /// <param name="variants">Variant list.</param>
        /// <param name="targets">Target list.</param>
        /// <param name="paths">Resolved output paths.</param>
        void AppendBinaryData(
            List<ShaderBinaryData> binaries,
            ShaderManifestShader shader,
            ShaderStage stage,
            string programName,
            ShaderManifestVariant[] variants,
            string[] targets,
            ShaderPathInfo paths) {
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++) {
                string target = targets[targetIndex];
                for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++) {
                    ShaderManifestVariant variant = variants[variantIndex];
                    string fileName = outputNamer.GetBinaryFileName(shader.Name, stage, target, variant.Name);
                    string binaryPath = Path.Combine(paths.BinaryDir, fileName);
                    string relativePath = Path.GetRelativePath(paths.ModuleDir, binaryPath);
                    binaries.Add(new ShaderBinaryData(programName, stage, target, variant.Name, relativePath));
                }
            }
        }

        /// <summary>
        /// Loads reflection data for a shader entry and variant.
        /// </summary>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="variant">Variant definition.</param>
        /// <param name="paths">Resolved output paths.</param>
        /// <returns>Reflection entry.</returns>
        ShaderReflectionEntry LoadReflection(
            ShaderManifestShader shader,
            ShaderStage stage,
            ShaderManifestVariant variant,
            ShaderPathInfo paths) {
            string stageSuffix = stageResolver.GetStageSuffix(stage);
            string reflectionPath = Path.Combine(paths.ReflectionDir, $"{shader.Name}.{stageSuffix}.{variant.Name}.json");
            return reflectionReader.Load(reflectionPath);
        }

        /// <summary>
        /// Validates that reflection data is consistent across variants.
        /// </summary>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="variants">Variant list.</param>
        /// <param name="paths">Resolved output paths.</param>
        /// <param name="reference">Reference reflection entry.</param>
        void ValidateReflectionConsistency(
            ShaderManifestShader shader,
            ShaderStage stage,
            ShaderManifestVariant[] variants,
            ShaderPathInfo paths,
            ShaderReflectionEntry reference) {
            for (int i = 1; i < variants.Length; i++) {
                ShaderManifestVariant variant = variants[i];
                ShaderReflectionEntry candidate = LoadReflection(shader, stage, variant, paths);
                if (!AreBindingsEquivalent(reference.Bindings, candidate.Bindings)) {
                    throw new InvalidOperationException($"Reflection bindings differ across variants for shader '{shader.Name}' stage '{stage}'.");
                }

                if (!AreSignaturesEquivalent(reference.Inputs, candidate.Inputs)) {
                    throw new InvalidOperationException($"Reflection inputs differ across variants for shader '{shader.Name}' stage '{stage}'.");
                }

                if (!AreSignaturesEquivalent(reference.Outputs, candidate.Outputs)) {
                    throw new InvalidOperationException($"Reflection outputs differ across variants for shader '{shader.Name}' stage '{stage}'.");
                }
            }
        }

        /// <summary>
        /// Builds runtime binding metadata from reflection bindings.
        /// </summary>
        /// <param name="bindings">Reflection bindings.</param>
        /// <returns>Runtime binding array.</returns>
        ShaderBinding[] BuildBindings(ShaderReflectionBinding[] bindings) {
            if (bindings == null || bindings.Length == 0) {
                return Array.Empty<ShaderBinding>();
            }

            ShaderBinding[] results = new ShaderBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++) {
                ShaderReflectionBinding binding = bindings[i];
                ShaderResourceType type = resourceResolver.Parse(binding.Type);
                ShaderConstantMember[] members = BuildMembers(binding.Members);
                results[i] = new ShaderBinding(binding.Name, type, binding.Set, binding.Slot, binding.Size, members);
            }

            return results;
        }

        /// <summary>
        /// Builds constant buffer member metadata.
        /// </summary>
        /// <param name="members">Reflection members.</param>
        /// <returns>Runtime member array.</returns>
        ShaderConstantMember[] BuildMembers(ShaderReflectionMember[] members) {
            if (members == null || members.Length == 0) {
                return Array.Empty<ShaderConstantMember>();
            }

            ShaderConstantMember[] results = new ShaderConstantMember[members.Length];
            for (int i = 0; i < members.Length; i++) {
                ShaderReflectionMember member = members[i];
                results[i] = new ShaderConstantMember(member.Name, member.Type, member.Offset, member.Size);
            }

            return results;
        }

        /// <summary>
        /// Builds signature element metadata from reflection data.
        /// </summary>
        /// <param name="elements">Reflection signature elements.</param>
        /// <returns>Runtime signature array.</returns>
        ShaderVertexElement[] BuildSignatureElements(ShaderReflectionSignatureElement[] elements) {
            if (elements == null || elements.Length == 0) {
                return Array.Empty<ShaderVertexElement>();
            }

            ShaderVertexElement[] results = new ShaderVertexElement[elements.Length];
            for (int i = 0; i < elements.Length; i++) {
                ShaderReflectionSignatureElement element = elements[i];
                results[i] = new ShaderVertexElement(element.Semantic, element.Index, element.Format);
            }

            return results;
        }

        /// <summary>
        /// Builds runtime variant metadata from manifest variants.
        /// </summary>
        /// <param name="variants">Manifest variants.</param>
        /// <returns>Runtime variant array.</returns>
        ShaderVariant[] BuildVariants(ShaderManifestVariant[] variants) {
            ShaderVariant[] results = new ShaderVariant[variants.Length];
            for (int i = 0; i < variants.Length; i++) {
                ShaderManifestVariant variant = variants[i];
                results[i] = new ShaderVariant(variant.Name, variant.Defines);
            }

            return results;
        }

        /// <summary>
        /// Compares binding arrays for equivalence.
        /// </summary>
        /// <param name="left">Left bindings.</param>
        /// <param name="right">Right bindings.</param>
        /// <returns>True when bindings match.</returns>
        bool AreBindingsEquivalent(ShaderReflectionBinding[] left, ShaderReflectionBinding[] right) {
            if (left == null && right == null) {
                return true;
            }

            if (left == null || right == null) {
                return false;
            }

            if (left.Length != right.Length) {
                return false;
            }

            for (int i = 0; i < left.Length; i++) {
                ShaderReflectionBinding leftBinding = left[i];
                ShaderReflectionBinding rightBinding = right[i];
                if (!string.Equals(leftBinding.Name, rightBinding.Name, StringComparison.Ordinal) ||
                    !string.Equals(leftBinding.Type, rightBinding.Type, StringComparison.OrdinalIgnoreCase) ||
                    leftBinding.Set != rightBinding.Set ||
                    leftBinding.Slot != rightBinding.Slot ||
                    leftBinding.Size != rightBinding.Size ||
                    !AreMembersEquivalent(leftBinding.Members, rightBinding.Members)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares member arrays for equivalence.
        /// </summary>
        /// <param name="left">Left member array.</param>
        /// <param name="right">Right member array.</param>
        /// <returns>True when members match.</returns>
        bool AreMembersEquivalent(ShaderReflectionMember[] left, ShaderReflectionMember[] right) {
            if (left == null && right == null) {
                return true;
            }

            if (left == null || right == null) {
                return false;
            }

            if (left.Length != right.Length) {
                return false;
            }

            for (int i = 0; i < left.Length; i++) {
                ShaderReflectionMember leftMember = left[i];
                ShaderReflectionMember rightMember = right[i];
                if (!string.Equals(leftMember.Name, rightMember.Name, StringComparison.Ordinal) ||
                    !string.Equals(leftMember.Type, rightMember.Type, StringComparison.OrdinalIgnoreCase) ||
                    leftMember.Offset != rightMember.Offset ||
                    leftMember.Size != rightMember.Size) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares signature arrays for equivalence.
        /// </summary>
        /// <param name="left">Left signature array.</param>
        /// <param name="right">Right signature array.</param>
        /// <returns>True when signatures match.</returns>
        bool AreSignaturesEquivalent(ShaderReflectionSignatureElement[] left, ShaderReflectionSignatureElement[] right) {
            if (left == null && right == null) {
                return true;
            }

            if (left == null || right == null) {
                return false;
            }

            if (left.Length != right.Length) {
                return false;
            }

            for (int i = 0; i < left.Length; i++) {
                ShaderReflectionSignatureElement leftElement = left[i];
                ShaderReflectionSignatureElement rightElement = right[i];
                if (!string.Equals(leftElement.Semantic, rightElement.Semantic, StringComparison.OrdinalIgnoreCase) ||
                    leftElement.Index != rightElement.Index ||
                    !string.Equals(leftElement.Format, rightElement.Format, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }
    }
}

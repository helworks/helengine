using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Writes generated C++ source fragments that embed runtime scene, startup, code-module, and physics feature data.
    /// </summary>
public sealed class EditorRuntimeNativeManifestWriter {
    /// <summary>
    /// Stable scene id used by generated boot-scene startup routing.
    /// </summary>
    const string GeneratedBootSceneId = "GeneratedBootScene";

    /// <summary>
    /// Stable canonical Nintendo DS startup-scene path used to avoid generated-build startup paths that native NitroFS boot cannot open.
    /// </summary>
    const string NintendoDsGeneratedBootSceneRelativePath = "cooked/scenes/generatedbootscene.hasset";

    /// <summary>
    /// Stable canonical Nintendo 3DS startup-scene path used to avoid generated-build startup paths that native RomFS boot cannot open.
    /// </summary>
    const string Nintendo3DsGeneratedBootSceneRelativePath = "cooked/scenes/generatedbootscene.hasset";

        /// <summary>
        /// Writes the generated runtime manifest source files into the generated-core runtime folder.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        /// <param name="cookedManifest">Final cooked manifest whose runtime data should be embedded into native source.</param>
        public void Write(string generatedCoreRootPath, PlatformBuildManifest cookedManifest) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
            Directory.CreateDirectory(runtimeRootPath);

            WriteStartupManifestSource(runtimeRootPath, cookedManifest);
            WriteSceneCatalogManifestSource(runtimeRootPath, cookedManifest);
            WriteCodeModuleManifestSource(runtimeRootPath, cookedManifest);
            WritePhysics3DSceneFeatureManifestSource(runtimeRootPath, cookedManifest);
            WriteStandardPlatformInputManifestSource(runtimeRootPath, cookedManifest);
            EditorGeneratedCoreRegenerationService.WriteGeneratedCoreTranslationUnit(generatedCoreRootPath);
        }

        /// <summary>
        /// Writes the generated startup-scene manifest header and implementation.
        /// </summary>
        /// <param name="runtimeRootPath">Runtime source folder inside the generated core tree.</param>
        /// <param name="cookedManifest">Final cooked manifest whose startup scene should be embedded.</param>
        void WriteStartupManifestSource(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            string startupSceneRelativePath = ResolveStartupSceneRelativePath(cookedManifest);
            string headerPath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.hpp");
            string sourcePath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.cpp");

            File.WriteAllText(headerPath, BuildStartupManifestHeaderContents());
            File.WriteAllText(sourcePath, BuildStartupManifestSourceContents(cookedManifest, startupSceneRelativePath));
        }

        /// <summary>
        /// Writes the generated runtime scene-catalog manifest header and implementation.
        /// </summary>
        /// <param name="runtimeRootPath">Runtime source folder inside the generated core tree.</param>
        /// <param name="cookedManifest">Final cooked manifest whose built scene layout should be embedded.</param>
        void WriteSceneCatalogManifestSource(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            string headerPath = Path.Combine(runtimeRootPath, "runtime_scene_catalog_manifest.hpp");
            string sourcePath = Path.Combine(runtimeRootPath, "runtime_scene_catalog_manifest.cpp");

            File.WriteAllText(headerPath, BuildSceneCatalogManifestHeaderContents());
            File.WriteAllText(sourcePath, BuildSceneCatalogManifestSourceContents(cookedManifest));
        }

        /// <summary>
        /// Writes the generated code-module residency manifest header and implementation.
        /// </summary>
        /// <param name="runtimeRootPath">Runtime source folder inside the generated core tree.</param>
        /// <param name="cookedManifest">Final cooked manifest whose code modules should be embedded.</param>
        void WriteCodeModuleManifestSource(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            string headerPath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.hpp");
            string sourcePath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.cpp");

            File.WriteAllText(headerPath, BuildCodeModuleManifestHeaderContents());
            File.WriteAllText(sourcePath, BuildCodeModuleManifestSourceContents(cookedManifest.CodeModules));
        }

        /// <summary>
        /// Writes the generated scene-physics feature manifest header and implementation.
        /// </summary>
        /// <param name="runtimeRootPath">Runtime source folder inside the generated core tree.</param>
        /// <param name="cookedManifest">Final cooked manifest whose scene feature masks should be embedded.</param>
        void WritePhysics3DSceneFeatureManifestSource(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            string headerPath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.hpp");
            string sourcePath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.cpp");

            File.WriteAllText(headerPath, BuildPhysics3DSceneFeatureManifestHeaderContents());
            File.WriteAllText(sourcePath, BuildPhysics3DSceneFeatureManifestSourceContents(cookedManifest));
        }

        /// <summary>
        /// Writes the generated standard-platform-input manifest header and implementation.
        /// </summary>
        /// <param name="runtimeRootPath">Runtime source folder inside the generated core tree.</param>
        /// <param name="cookedManifest">Final cooked manifest whose standard platform actions should be embedded.</param>
        void WriteStandardPlatformInputManifestSource(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            string headerPath = Path.Combine(runtimeRootPath, "runtime_standard_platform_input_manifest.hpp");
            string sourcePath = Path.Combine(runtimeRootPath, "runtime_standard_platform_input_manifest.cpp");

            File.WriteAllText(headerPath, BuildStandardPlatformInputManifestHeaderContents());
            File.WriteAllText(sourcePath, BuildStandardPlatformInputManifestSourceContents(cookedManifest));
        }

        /// <summary>
        /// Builds the generated runtime startup manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildStartupManifestHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("const char* he_get_runtime_startup_scene_relative_path();");
            builder.AppendLine("const char* he_get_runtime_platform_name();");
            builder.AppendLine("const char* he_get_runtime_platform_version();");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime scene-catalog manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildSceneCatalogManifestHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("#include <cstddef>");
            builder.AppendLine();
            builder.AppendLine("struct HERuntimeSceneCatalogEntry {");
            builder.AppendLine("    const char* SceneId;");
            builder.AppendLine("    const char* CookedRelativePath;");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeSceneCatalogEntry* he_runtime_scene_catalog_entries(std::size_t* count);");
            builder.AppendLine("const char* he_runtime_scene_cooked_relative_path(const char* sceneId);");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime startup manifest implementation.
        /// </summary>
        /// <param name="startupSceneRelativePath">Cooked runtime scene path embedded into the native player.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildStartupManifestSourceContents(PlatformBuildManifest cookedManifest, string startupSceneRelativePath) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_startup_manifest.hpp\"");
            builder.AppendLine();
            builder.AppendLine("static const char kRuntimeStartupSceneRelativePath[] = \"" + EscapeCppStringLiteral(startupSceneRelativePath) + "\";");
            builder.AppendLine("static const char kRuntimePlatformName[] = \"" + EscapeCppStringLiteral(cookedManifest.PlatformName) + "\";");
            builder.AppendLine("static const char kRuntimePlatformVersion[] = \"" + EscapeCppStringLiteral(cookedManifest.PlatformVersion) + "\";");
            builder.AppendLine();
            builder.AppendLine("const char* he_get_runtime_startup_scene_relative_path() {");
            builder.AppendLine("    return kRuntimeStartupSceneRelativePath;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("const char* he_get_runtime_platform_name() {");
            builder.AppendLine("    return kRuntimePlatformName;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("const char* he_get_runtime_platform_version() {");
            builder.AppendLine("    return kRuntimePlatformVersion;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime scene-catalog manifest implementation.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest whose scene layout should be embedded into native source.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildSceneCatalogManifestSourceContents(PlatformBuildManifest cookedManifest) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (cookedManifest.Scenes == null || cookedManifest.Scenes.Length == 0) {
                throw new InvalidOperationException("Cooked manifest did not define any built scenes.");
            }

            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_scene_catalog_manifest.hpp\"");
            builder.AppendLine();
            builder.AppendLine("#include <cstring>");
            builder.AppendLine("#include <stdexcept>");
            builder.AppendLine();
            builder.AppendLine("static const HERuntimeSceneCatalogEntry kRuntimeSceneCatalogEntries[] = {");
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                string cookedRelativePath = ResolveCookedRelativePath(cookedManifest, scene);
                builder.Append("    { \"");
                builder.Append(EscapeCppStringLiteral(scene.SceneId));
                builder.Append("\", \"");
                builder.Append(EscapeCppStringLiteral(cookedRelativePath));
                builder.AppendLine("\" },");
            }

            builder.AppendLine("};");
            builder.AppendLine("static const std::size_t kRuntimeSceneCatalogEntryCount = sizeof(kRuntimeSceneCatalogEntries) / sizeof(kRuntimeSceneCatalogEntries[0]);");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeSceneCatalogEntry* he_runtime_scene_catalog_entries(std::size_t* count) {");
            builder.AppendLine("    if (count != nullptr) {");
            builder.AppendLine("        *count = kRuntimeSceneCatalogEntryCount;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return kRuntimeSceneCatalogEntries;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("const char* he_runtime_scene_cooked_relative_path(const char* sceneId) {");
            builder.AppendLine("    if (sceneId == nullptr || sceneId[0] == '\\0') {");
            builder.AppendLine("        throw std::invalid_argument(\"Runtime scene id is required.\");");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    for (std::size_t index = 0; index < kRuntimeSceneCatalogEntryCount; index++) {");
            builder.AppendLine("        const HERuntimeSceneCatalogEntry& entry = kRuntimeSceneCatalogEntries[index];");
            builder.AppendLine("        if (std::strcmp(entry.SceneId, sceneId) == 0) {");
            builder.AppendLine("            return entry.CookedRelativePath;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    throw std::runtime_error(\"Runtime scene id was not found in the scene catalog manifest.\");");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime code-module manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildCodeModuleManifestHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("#include <cstddef>");
            builder.AppendLine();
            builder.AppendLine("enum class HERuntimeCodeModuleLoadState {");
            builder.AppendLine("    ResidentAtStartup = 0,");
            builder.AppendLine("    SceneResident = 1,");
            builder.AppendLine("    Unloadable = 2");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("struct HERuntimeCodeModuleEntry {");
            builder.AppendLine("    const char* ModuleId;");
            builder.AppendLine("    const char* RuntimeSpecializationId;");
            builder.AppendLine("    HERuntimeCodeModuleLoadState LoadState;");
            builder.AppendLine("    const char* const* DependencyModuleIds;");
            builder.AppendLine("    std::size_t DependencyModuleCount;");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeCodeModuleEntry* he_runtime_code_module_entries(std::size_t* count);");
            builder.AppendLine("HERuntimeCodeModuleLoadState he_runtime_code_module_load_state(const char* moduleId);");
            builder.AppendLine("bool he_runtime_code_module_can_unload(const char* moduleId);");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime scene-physics feature manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildPhysics3DSceneFeatureManifestHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("#include <cstddef>");
            builder.AppendLine("#include <cstdint>");
            builder.AppendLine();
            builder.AppendLine("struct HERuntimePhysics3DSceneFeatureEntry {");
            builder.AppendLine("    const char* SceneId;");
            builder.AppendLine("    std::uint32_t FeatureFlags;");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimePhysics3DSceneFeatureEntry* he_runtime_physics3d_scene_feature_entries(std::size_t* count);");
            builder.AppendLine("std::uint32_t he_runtime_physics3d_scene_feature_flags(const char* sceneId);");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated standard-platform-input manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildStandardPlatformInputManifestHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("#include <cstddef>");
            builder.AppendLine();
            builder.AppendLine("struct HERuntimeStandardPlatformActionEntry {");
            builder.AppendLine("    int ActionId;");
            builder.AppendLine("    int DeviceKind;");
            builder.AppendLine("    int ControlKind;");
            builder.AppendLine("    int DeviceIndex;");
            builder.AppendLine("    int ControlIndex;");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeStandardPlatformActionEntry* he_runtime_standard_platform_action_entries(std::size_t* count);");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime code-module manifest implementation.
        /// </summary>
        /// <param name="codeModules">Cooked code-module records to embed into native source.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildCodeModuleManifestSourceContents(PlatformBuildCodeModule[] codeModules) {
            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_code_module_manifest.hpp\"");
            builder.AppendLine();
            builder.AppendLine("#include <cstring>");
            builder.AppendLine("#include <stdexcept>");
            builder.AppendLine();

            if (codeModules != null) {
                for (int index = 0; index < codeModules.Length; index++) {
                    PlatformBuildCodeModule codeModule = codeModules[index];
                    if (codeModule.DependencyModuleIds.Length == 0) {
                        continue;
                    }

                    builder.Append("static const char* const kRuntimeCodeModuleDependencies_");
                    builder.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.AppendLine("[] = {");
                    for (int dependencyIndex = 0; dependencyIndex < codeModule.DependencyModuleIds.Length; dependencyIndex++) {
                        builder.Append("    \"");
                        builder.Append(EscapeCppStringLiteral(codeModule.DependencyModuleIds[dependencyIndex]));
                        builder.AppendLine("\",");
                    }

                    builder.AppendLine("};");
                    builder.AppendLine();
                }
            }

            if (codeModules == null || codeModules.Length == 0) {
                builder.AppendLine("static const HERuntimeCodeModuleEntry* kRuntimeCodeModuleEntries = nullptr;");
                builder.AppendLine("static const std::size_t kRuntimeCodeModuleEntryCount = 0;");
            } else {
                builder.AppendLine("static const HERuntimeCodeModuleEntry kRuntimeCodeModuleEntries[] = {");
                for (int index = 0; index < codeModules.Length; index++) {
                    PlatformBuildCodeModule codeModule = codeModules[index];
                    string loadStateExpression = ResolveCodeModuleLoadStateExpression(codeModule.LoadScopes);
                    builder.Append("    { \"");
                    builder.Append(EscapeCppStringLiteral(codeModule.ModuleId));
                    builder.Append("\", \"");
                    builder.Append(EscapeCppStringLiteral(codeModule.RuntimeSpecializationId));
                    builder.Append("\", ");
                    builder.Append(loadStateExpression);
                    builder.Append(", ");
                    if (codeModule.DependencyModuleIds.Length == 0) {
                        builder.Append("nullptr, 0");
                    } else {
                        builder.Append("kRuntimeCodeModuleDependencies_");
                        builder.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        builder.Append(", ");
                        builder.Append(codeModule.DependencyModuleIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }

                    builder.AppendLine(" },");
                }

                builder.AppendLine("};");
                builder.Append("static const std::size_t kRuntimeCodeModuleEntryCount = sizeof(kRuntimeCodeModuleEntries) / sizeof(kRuntimeCodeModuleEntries[0]);");
                builder.AppendLine();
            }

            builder.AppendLine();
            builder.AppendLine("const HERuntimeCodeModuleEntry* he_runtime_code_module_entries(std::size_t* count) {");
            builder.AppendLine("    if (count != nullptr) {");
            builder.AppendLine("        *count = kRuntimeCodeModuleEntryCount;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return kRuntimeCodeModuleEntries;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("HERuntimeCodeModuleLoadState he_runtime_code_module_load_state(const char* moduleId) {");
            builder.AppendLine("    if (moduleId == nullptr || moduleId[0] == '\\0') {");
            builder.AppendLine("        throw std::invalid_argument(\"Runtime code module id is required.\");");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    for (std::size_t index = 0; index < kRuntimeCodeModuleEntryCount; index++) {");
            builder.AppendLine("        const HERuntimeCodeModuleEntry& entry = kRuntimeCodeModuleEntries[index];");
            builder.AppendLine("        if (std::strcmp(entry.ModuleId, moduleId) == 0) {");
            builder.AppendLine("            return entry.LoadState;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    throw std::runtime_error(\"Runtime code module was not found in the residency manifest.\");");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("bool he_runtime_code_module_can_unload(const char* moduleId) {");
            builder.AppendLine("    return he_runtime_code_module_load_state(moduleId) != HERuntimeCodeModuleLoadState::ResidentAtStartup;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime scene-physics feature manifest implementation.
        /// </summary>
        /// <param name="cookedManifest">Final cooked manifest whose scene feature masks should be embedded.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildPhysics3DSceneFeatureManifestSourceContents(PlatformBuildManifest cookedManifest) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_physics3d_scene_feature_manifest.hpp\"");
            builder.AppendLine();
            builder.AppendLine("#include <cstring>");
            builder.AppendLine("#include <stdexcept>");
            builder.AppendLine();

            if (cookedManifest.Scenes == null || cookedManifest.Scenes.Length == 0) {
                builder.AppendLine("static const HERuntimePhysics3DSceneFeatureEntry* kRuntimePhysics3DSceneFeatureEntries = nullptr;");
                builder.AppendLine("static const std::size_t kRuntimePhysics3DSceneFeatureEntryCount = 0;");
            } else {
                builder.AppendLine("static const HERuntimePhysics3DSceneFeatureEntry kRuntimePhysics3DSceneFeatureEntries[] = {");
                for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                    PlatformBuildScene scene = cookedManifest.Scenes[index];
                    uint featureFlags = ResolveScenePhysics3DFeatureFlags(scene);
                    builder.Append("    { \"");
                    builder.Append(EscapeCppStringLiteral(scene.SceneId));
                    builder.Append("\", ");
                    builder.Append(featureFlags.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.AppendLine("u },");
                }

                builder.AppendLine("};");
                builder.AppendLine("static const std::size_t kRuntimePhysics3DSceneFeatureEntryCount = sizeof(kRuntimePhysics3DSceneFeatureEntries) / sizeof(kRuntimePhysics3DSceneFeatureEntries[0]);");
            }

            builder.AppendLine();
            builder.AppendLine("const HERuntimePhysics3DSceneFeatureEntry* he_runtime_physics3d_scene_feature_entries(std::size_t* count) {");
            builder.AppendLine("    if (count != nullptr) {");
            builder.AppendLine("        *count = kRuntimePhysics3DSceneFeatureEntryCount;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return kRuntimePhysics3DSceneFeatureEntries;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("std::uint32_t he_runtime_physics3d_scene_feature_flags(const char* sceneId) {");
            builder.AppendLine("    if (sceneId == nullptr || sceneId[0] == '\\0') {");
            builder.AppendLine("        throw std::invalid_argument(\"Runtime scene id is required.\");");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    for (std::size_t index = 0; index < kRuntimePhysics3DSceneFeatureEntryCount; index++) {");
            builder.AppendLine("        const HERuntimePhysics3DSceneFeatureEntry& entry = kRuntimePhysics3DSceneFeatureEntries[index];");
            builder.AppendLine("        if (std::strcmp(entry.SceneId, sceneId) == 0) {");
            builder.AppendLine("            return entry.FeatureFlags;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    throw std::runtime_error(\"Runtime scene id was not found in the physics feature manifest.\");");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated standard-platform-input manifest implementation.
        /// </summary>
        /// <param name="cookedManifest">Final cooked manifest whose standard platform actions should be embedded.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildStandardPlatformInputManifestSourceContents(PlatformBuildManifest cookedManifest) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_standard_platform_input_manifest.hpp\"");
            builder.AppendLine();

            List<StandardPlatformActionBinding> bindings = cookedManifest.StandardPlatformInputConfiguration?.Bindings ?? [];
            if (bindings.Count == 0) {
                builder.AppendLine("static const HERuntimeStandardPlatformActionEntry* kRuntimeStandardPlatformActionEntries = nullptr;");
                builder.AppendLine("static const std::size_t kRuntimeStandardPlatformActionEntryCount = 0;");
            } else {
                builder.AppendLine("static const HERuntimeStandardPlatformActionEntry kRuntimeStandardPlatformActionEntries[] = {");
                for (int index = 0; index < bindings.Count; index++) {
                    StandardPlatformActionBinding binding = bindings[index];
                    builder.Append("    { ");
                    builder.Append(((int)binding.Action).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(", ");
                    builder.Append(((int)binding.Control.DeviceKind).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(", ");
                    builder.Append(((int)binding.Control.ControlKind).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(", ");
                    builder.Append(binding.Control.DeviceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(", ");
                    builder.Append(binding.Control.ControlIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.AppendLine(" },");
                }
                builder.AppendLine("};");
                builder.Append("static const std::size_t kRuntimeStandardPlatformActionEntryCount = ");
                builder.Append(bindings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine(";");
            }

            builder.AppendLine();
            builder.AppendLine("const HERuntimeStandardPlatformActionEntry* he_runtime_standard_platform_action_entries(std::size_t* count) {");
            builder.AppendLine("    if (count != nullptr) {");
            builder.AppendLine("        *count = kRuntimeStandardPlatformActionEntryCount;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return kRuntimeStandardPlatformActionEntries;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Resolves the cooked runtime scene path embedded into the generated startup source.
        /// </summary>
        /// <param name="cookedManifest">Final cooked manifest that carries the startup scene metadata.</param>
        /// <returns>Runtime-relative cooked scene path.</returns>
        static string ResolveStartupSceneRelativePath(PlatformBuildManifest cookedManifest) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            if (cookedManifest.Scenes == null || string.IsNullOrWhiteSpace(cookedManifest.StartupSceneId)) {
                throw new InvalidOperationException("Cooked manifest did not define a startup scene.");
            }

            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                if (!string.Equals(scene.SceneId, cookedManifest.StartupSceneId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (scene.ResolvedMetadata != null) {
                    for (int metadataIndex = 0; metadataIndex < scene.ResolvedMetadata.Length; metadataIndex++) {
                        KeyValuePair<string, string> entry = scene.ResolvedMetadata[metadataIndex];
                        if (string.Equals(entry.Key, PlatformBuildSceneMetadataKeys.CookedRelativePath, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(entry.Value)) {
                            return NormalizeRuntimeSceneRelativePath(cookedManifest, scene.SceneId, entry.Value.Replace('\\', '/'));
                        }
                    }
                }

                throw new InvalidOperationException($"Startup scene '{scene.SceneId}' did not resolve a cooked-relative-path metadata entry.");
            }

            throw new InvalidOperationException($"Startup scene '{cookedManifest.StartupSceneId}' was not found in the cooked manifest.");
        }

        /// <summary>
        /// Resolves the cooked runtime path for one built scene entry.
        /// </summary>
        /// <param name="scene">Built scene entry to inspect.</param>
        /// <returns>Cooked runtime-relative scene payload path.</returns>
        static string ResolveCookedRelativePath(PlatformBuildManifest cookedManifest, PlatformBuildScene scene) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }
            if (scene.ResolvedMetadata == null) {
                throw new InvalidOperationException($"Built scene '{scene.SceneId}' did not define any resolved metadata.");
            }

            for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                KeyValuePair<string, string> metadata = scene.ResolvedMetadata[index];
                if (string.Equals(metadata.Key, PlatformBuildSceneMetadataKeys.CookedRelativePath, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(metadata.Value)) {
                    return NormalizeRuntimeSceneRelativePath(cookedManifest, scene.SceneId, metadata.Value.Replace('\\', '/'));
                }
            }

            throw new InvalidOperationException($"Built scene '{scene.SceneId}' did not define a cooked relative path.");
        }

        /// <summary>
        /// Normalizes one runtime scene path for platform-specific startup-scene boot contracts.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest whose target platform owns the runtime contract.</param>
        /// <param name="sceneId">Stable scene id that owns the cooked scene path.</param>
        /// <param name="cookedRelativePath">Cooked scene path resolved from the cooked manifest.</param>
        /// <returns>Runtime scene path that should be embedded into native manifests.</returns>
        static string NormalizeRuntimeSceneRelativePath(PlatformBuildManifest cookedManifest, string sceneId, string cookedRelativePath) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            } else if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            if (string.Equals(sceneId, GeneratedBootSceneId, StringComparison.Ordinal)) {
                if (string.Equals(cookedManifest.PlatformName, "ds", StringComparison.OrdinalIgnoreCase)) {
                    return NintendoDsGeneratedBootSceneRelativePath;
                } else if (string.Equals(cookedManifest.PlatformName, "3ds", StringComparison.OrdinalIgnoreCase)) {
                    return Nintendo3DsGeneratedBootSceneRelativePath;
                }
            }

            return cookedRelativePath;
        }

        /// <summary>
        /// Resolves the generated runtime code-module load state for one module.
        /// </summary>
        /// <param name="loadScopes">Authored load scopes from the cooked code module.</param>
        /// <returns>C++ enumeration expression for the embedded runtime load state.</returns>
        static string ResolveCodeModuleLoadStateExpression(string[] loadScopes) {
            if (loadScopes == null || loadScopes.Length == 0) {
                return "HERuntimeCodeModuleLoadState::Unloadable";
            }

            for (int index = 0; index < loadScopes.Length; index++) {
                string loadScope = loadScopes[index];
                if (string.Equals(loadScope, "always-loaded", StringComparison.OrdinalIgnoreCase)) {
                    return "HERuntimeCodeModuleLoadState::ResidentAtStartup";
                }
            }

            for (int index = 0; index < loadScopes.Length; index++) {
                string loadScope = loadScopes[index];
                if (string.Equals(loadScope, "scene-loaded", StringComparison.OrdinalIgnoreCase)) {
                    return "HERuntimeCodeModuleLoadState::SceneResident";
                }
            }

            return "HERuntimeCodeModuleLoadState::Unloadable";
        }

        /// <summary>
        /// Resolves the compact 3D physics feature mask stored in one scene metadata record.
        /// </summary>
        /// <param name="scene">Resolved scene whose metadata should be inspected.</param>
        /// <returns>Compact 3D physics feature mask for the supplied scene.</returns>
        static uint ResolveScenePhysics3DFeatureFlags(PlatformBuildScene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }
            if (scene.ResolvedMetadata == null) {
                return 0u;
            }

            for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                KeyValuePair<string, string> entry = scene.ResolvedMetadata[index];
                if (!string.Equals(entry.Key, PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.Value)) {
                    return 0u;
                }

                return uint.Parse(entry.Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            return 0u;
        }

        /// <summary>
        /// Escapes one string for safe embedding inside a C++ string literal.
        /// </summary>
        /// <param name="value">String value to escape.</param>
        /// <returns>Escaped literal contents without the surrounding quotes.</returns>
        static string EscapeCppStringLiteral(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int index = 0; index < value.Length; index++) {
                char current = value[index];
                if (current == '\\') {
                    builder.Append("\\\\");
                } else if (current == '"') {
                    builder.Append("\\\"");
                } else if (current == '\n') {
                    builder.Append("\\n");
                } else if (current == '\r') {
                    builder.Append("\\r");
                } else if (current == '\t') {
                    builder.Append("\\t");
                } else {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}

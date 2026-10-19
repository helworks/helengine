using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace helengine.editor {
    /// <summary>Writes the authoritative scene roots and generated source candidates used by a native symbol-based code partition.</summary>
    public sealed class EditorSceneCodeModuleSourcePlanWriter {
        /// <summary>Rejects code-overlay profiles that omit template visibility or object/callback lifetime protection before conversion begins.</summary>
        /// <param name="options">Fully resolved target codegen settings, including profile defaults.</param>
        public static void ValidateCodegenPolicy(IReadOnlyDictionary<string, string> options) {
            ArgumentNullException.ThrowIfNull(options);
            foreach (string required in new[] { "codegen-header-template-definitions", "codegen-module-lifetime-pins" }) {
                if (!options.TryGetValue(required, out string value) || !bool.TryParse(value, out bool enabled) || !enabled) {
                    throw new InvalidOperationException($"Scene code modules require '{required}=true'.");
                }
            }
        }

        /// <summary>Creates a code partition input from the actual cooked scenes and compiled module outputs without treating header references as residency.</summary>
        /// <param name="manifest">Cooked scene and artifact selection.</param>
        /// <param name="coreRoot">Generated source tree containing Core and selected gameplay dependency sources.</param>
        /// <param name="codeRoot">Generated authored module source outputs.</param>
        /// <param name="contentRoot">Cooked content root used to resolve packaged scenes.</param>
        /// <param name="moduleIds">Compiled authored module identities.</param>
        /// <param name="resolver">Resolver for the actual compiled scripted component types.</param>
        /// <param name="definition">Target persistence schema extensions.</param>
        /// <param name="compactMessages">Whether the target uses compact exception diagnostics.</param>
        /// <param name="nativeExceptions">Whether the target supports native exception unwinding.</param>
        /// <returns>Absolute source-plan JSON path consumed by the target native builder.</returns>
        public string Write(PlatformBuildManifest manifest, string coreRoot, string codeRoot, string contentRoot,
            IReadOnlyList<string> moduleIds, IScriptTypeResolver resolver, PlatformDefinition definition,
            bool compactMessages, bool nativeExceptions) {
            ArgumentNullException.ThrowIfNull(manifest);
            string planRoot = Path.Combine(coreRoot, "scene-modules");
            Directory.CreateDirectory(planRoot);
            Dictionary<string, Type[]> sceneTypes = new(StringComparer.Ordinal);
            foreach (PlatformBuildScene scene in manifest.Scenes) {
                string path = scene.ResolvedMetadata.Single(pair => pair.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath).Value;
                string fullPath = ResolveContentPath(contentRoot, path);
                sceneTypes.Add(scene.SceneId, EditorGeneratedCoreRegenerationService.DiscoverAutomaticRuntimeComponentTypesFromCookedScenes([fullPath], resolver, manifest.NumericPolicy).ToArray());
            }
            HashSet<string> bootstrapIds = new(StringComparer.Ordinal) { manifest.StartupSceneId };
            if (!string.IsNullOrEmpty(manifest.LoadingScreenSceneId)) bootstrapIds.Add(manifest.LoadingScreenSceneId);
            foreach (string sceneId in bootstrapIds) {
                if (!sceneTypes.ContainsKey(sceneId)) throw new InvalidOperationException($"Resident bootstrap scene '{sceneId}' is absent from the cooked catalog.");
            }
            Type[] allTypes = sceneTypes.Values.SelectMany(types => types).Distinct().ToArray();
            IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> runtimeManifests = EditorGeneratedCoreRegenerationService.DiscoverGeneratedRuntimeModuleManifests(
                EditorGeneratedCoreRegenerationService.ResolveGeneratedRuntimeModuleManifestAssemblies());
            IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> activeRuntimeManifests = EditorGeneratedCoreRegenerationService.ResolveActiveGeneratedRuntimeModuleManifests(runtimeManifests, allTypes);
            HashSet<Type> bootstrapTypes = new();
            foreach (string sceneId in bootstrapIds) bootstrapTypes.UnionWith(sceneTypes[sceneId]);
            HashSet<Type> residentTypes = new(bootstrapTypes);
            ScriptComponentPlayerDeserializerGenerator generator = new(compactMessages, nativeExceptions);
            IReadOnlyList<ScriptComponentReflectionSchema> schemas = EditorGeneratedCoreRegenerationService.DiscoverAutomaticRuntimeComponentSchemas(
                new ScriptComponentReflectionSchemaBuilder(), generator, allTypes, definition);
            Dictionary<Type, ScriptComponentReflectionSchema> schemaByType = schemas.ToDictionary(schema => schema.ComponentType);
            File.WriteAllText(Path.Combine(coreRoot, "GeneratedRuntimeComponentDeserializerRegistration.cpp"),
                EditorGeneratedCoreRegenerationService.BuildGeneratedRuntimeComponentDeserializerRegistrationSource(
                    schemas.Where(schema => residentTypes.Contains(schema.ComponentType)).ToArray(), generator));
            EditorGeneratedCoreRegenerationService.EmitGeneratedRuntimeModuleRegistration(coreRoot, bootstrapTypes.ToArray());
            Dictionary<string, string> authoredOwnerByStem = DiscoverAuthoredSourceOwners(codeRoot, moduleIds);
            HashSet<string> genericResidentSources = new(StringComparer.Ordinal);
            foreach (System.Reflection.Assembly assembly in allTypes.Select(type => type.Assembly)
                .Concat(activeRuntimeManifests.Select(manifest => manifest.RegistrationType.Assembly)).Distinct()) {
                foreach (Type type in assembly.GetTypes()) {
                    string stem = type.Name.Replace('`', '_');
                    string sourcePath = Path.Combine(coreRoot, stem + ".cpp");
                    if (File.Exists(sourcePath)) {
                        authoredOwnerByStem[stem] = assembly.GetName().Name;
                        if (EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(type, Path.Combine(coreRoot, stem + ".hpp"))) {
                            genericResidentSources.Add(sourcePath);
                        }
                    }
                }
            }
            JsonArray factories = new();
            foreach (ScriptComponentReflectionSchema schema in schemas) {
                string factory = generator.BuildNativeDeserializerClassName(schema);
                string componentSource = Path.Combine(coreRoot, schema.ComponentType.Name + ".cpp");
                string factorySource = Path.Combine(coreRoot, factory + ".cpp");
                if (!File.Exists(componentSource) || !File.Exists(factorySource)) throw new FileNotFoundException($"Scene component '{schema.ComponentType.FullName}' has no generated source pair.");
                authoredOwnerByStem[factory] = residentTypes.Contains(schema.ComponentType) ? "resident" : schema.ComponentType.Assembly.GetName().Name;
                factories.Add(new JsonObject {
                    ["componentTypeId"] = schema.ComponentType.FullName,
                    ["componentSourcePath"] = componentSource,
                    ["deserializerSourcePath"] = factorySource,
                    ["deserializerClassName"] = factory,
                    ["resident"] = residentTypes.Contains(schema.ComponentType)
                });
            }
            JsonArray candidates = new();
            JsonArray residentRoots = new();
            foreach (KeyValuePair<string, string> candidate in DiscoverSourceCandidates(coreRoot, authoredOwnerByStem)) {
                string path = candidate.Key;
                string owner = candidate.Value;
                candidates.Add(new JsonObject { ["sourcePath"] = path, ["ownerModuleId"] = owner });
                if (IsKernelSupportSource(coreRoot, path)) residentRoots.Add(path);
            }
            foreach (Type type in residentTypes) {
                residentRoots.Add(Path.Combine(coreRoot, type.Name + ".cpp"));
                residentRoots.Add(Path.Combine(coreRoot, generator.BuildNativeDeserializerClassName(schemaByType[type]) + ".cpp"));
            }
            foreach (string sourcePath in genericResidentSources) residentRoots.Add(sourcePath);
            JsonArray scenes = new();
            foreach (PlatformBuildScene scene in manifest.Scenes) {
                JsonArray roots = new();
                foreach (Type type in sceneTypes[scene.SceneId]) {
                    roots.Add(Path.Combine(coreRoot, type.Name + ".cpp"));
                    roots.Add(Path.Combine(coreRoot, generator.BuildNativeDeserializerClassName(schemaByType[type]) + ".cpp"));
                }
                foreach (GeneratedRuntimeModuleManifestAttribute runtimeManifest in EditorGeneratedCoreRegenerationService.ResolveActiveGeneratedRuntimeModuleManifests(runtimeManifests, sceneTypes[scene.SceneId])) {
                    string registrationPath = Path.Combine(coreRoot, runtimeManifest.RegistrationType.Name + ".cpp");
                    if (!File.Exists(registrationPath)) throw new FileNotFoundException("Activated scene runtime has no generated registration source.", registrationPath);
                    roots.Add(registrationPath);
                    if (bootstrapIds.Contains(scene.SceneId)) residentRoots.Add(registrationPath);
                }
                scenes.Add(new JsonObject {
                    ["sceneId"] = scene.SceneId,
                    ["cookedRelativePath"] = scene.ResolvedMetadata.Single(pair => pair.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath).Value,
                    ["isResident"] = bootstrapIds.Contains(scene.SceneId),
                    ["rootSources"] = roots,
                    ["assetPaths"] = JsonNode.Parse(scene.ResolvedMetadata.Single(pair => pair.Key == "cooked-dependency-paths").Value)
                });
            }
            JsonObject document = new() {
                ["numericPolicy"] = manifest.NumericPolicy.Identity,
                ["schemaVersion"] = 1, ["generatedCoreRoot"] = Path.GetFullPath(coreRoot),
                ["candidates"] = candidates, ["residentRoots"] = residentRoots,
                ["scenes"] = scenes, ["componentFactories"] = factories,
                ["bootstrapSceneIds"] = new JsonArray(bootstrapIds.OrderBy(id => id, StringComparer.Ordinal).Select(id => JsonValue.Create(id)).ToArray())
            };
            string outputPath = Path.Combine(planRoot, "source-plan.json");
            File.WriteAllText(outputPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return outputPath;
        }

        /// <summary>Assigns actual generated class units to authored projects while excluding converter harnesses and profile metadata from type ownership.</summary>
        /// <param name="codeRoot">Root containing the converted output directory for each authored project.</param>
        /// <param name="moduleIds">Project identities whose physical class definitions participate in the partition.</param>
        /// <returns>Class source basenames mapped to their first authored project, with identical transitive definitions deduplicated.</returns>
        internal static Dictionary<string, string> DiscoverAuthoredSourceOwners(string codeRoot, IReadOnlyList<string> moduleIds) {
            Dictionary<string, string> owners = new(StringComparer.Ordinal);
            foreach (string moduleId in moduleIds) {
                string moduleRoot = Path.Combine(codeRoot, moduleId);
                if (!Directory.Exists(moduleRoot)) throw new DirectoryNotFoundException(moduleRoot);
                foreach (string path in Directory.EnumerateFiles(moduleRoot, "*.cpp", SearchOption.TopDirectoryOnly)) {
                    if (IsAggregationSource(path) || Path.GetFileName(path) == "feature_manifest.cpp") continue;
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (!owners.TryAdd(stem, moduleId) && owners[stem] != moduleId) {
                        string previous = Path.Combine(codeRoot, owners[stem], stem + ".cpp");
                        if (!File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(path))) {
                            throw new InvalidOperationException($"Generated source '{stem}' has conflicting transitive authored definitions.");
                        }
                    }
                }
            }
            return owners;
        }

        /// <summary>Selects individual native compilation units from the official merged tree, keeping runtime support resident and excluding aggregate harnesses.</summary>
        /// <param name="coreRoot">Single authoritative combined source tree produced by the normal codegen merge service.</param>
        /// <param name="authoredOwners">Validated ownership of actual authored class basenames.</param>
        /// <returns>Absolute candidate paths and their authored owner or resident runtime support identity.</returns>
        internal static Dictionary<string, string> DiscoverSourceCandidates(string coreRoot, IReadOnlyDictionary<string, string> authoredOwners) {
            Dictionary<string, string> candidates = new(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(coreRoot, "*.cpp", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)) {
                string relative = Path.GetRelativePath(coreRoot, path).Replace('\\', '/');
                if (relative.StartsWith("scene-modules/", StringComparison.Ordinal) || IsAggregationSource(path)) continue;
                string stem = Path.GetFileNameWithoutExtension(path);
                string owner = relative.StartsWith("runtime/", StringComparison.Ordinal) ? "resident" :
                    authoredOwners.TryGetValue(stem, out string authoredOwner) ? authoredOwner : "resident";
                candidates.Add(path, owner);
            }
            return candidates;
        }

        /// <summary>Identifies the exact converter and engine unity harnesses that include other units rather than defining an owned class.</summary>
        /// <param name="path">Generated source path being considered for ownership or native candidate compilation.</param>
        /// <returns>True only for known aggregation translation units, leaving every actual class subject to conflict validation.</returns>
        internal static bool IsAggregationSource(string path) {
            string name = Path.GetFileName(path);
            return name == "generated_unity.cpp" || name == "helengine_core_unity.cpp";
        }

        /// <summary>Roots generated runtime ABI support and bootstrap dispatchers without retaining every eligible Core class.</summary>
        /// <param name="coreRoot">Authoritative combined generated source root.</param>
        /// <param name="path">Individual candidate translation unit.</param>
        /// <returns>True for runtime support or the two generated bootstrap registration entry points.</returns>
        internal static bool IsKernelSupportSource(string coreRoot, string path) {
            string relative = Path.GetRelativePath(coreRoot, path).Replace('\\', '/');
            return relative.StartsWith("runtime/", StringComparison.Ordinal) ||
                relative == "GeneratedRuntimeComponentDeserializerRegistration.cpp" ||
                relative == "GeneratedRuntimeModuleRegistration.cpp";
        }

        /// <summary>Resolves a declared runtime-relative payload under the actual execution content root and rejects traversal.</summary>
        /// <param name="root">Cooked content root.</param>
        /// <param name="relativePath">Canonical runtime path.</param>
        /// <returns>The existing payload's absolute path.</returns>
        static string ResolveContentPath(string root, string relativePath) {
            string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) throw new FileNotFoundException($"Invalid or missing cooked scene '{relativePath}'.", fullPath);
            return fullPath;
        }
    }
}

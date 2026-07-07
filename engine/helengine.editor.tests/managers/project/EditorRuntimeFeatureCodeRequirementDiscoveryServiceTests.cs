using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies generic runtime feature requirements can be discovered from used runtime types and active generated runtime modules.
/// </summary>
public sealed class EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests {
    /// <summary>
    /// Verifies built-in runtime component ids from cooked scene metadata resolve without requiring an assembly-qualified script type id.
    /// </summary>
    [Fact]
    public void Collect_when_scene_metadata_contains_built_in_component_id_does_not_require_assembly_qualified_name() {
        EditorRuntimeFeatureCodeRequirementDiscoveryService service = new(null);

        PlatformBuildRequiredRuntimeFeature[] requiredFeatures = service.Collect(
            new PlatformBuildManifest(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                "windows",
                "1.0.0",
                "Main",
                [
                    new PlatformBuildScene(
                        "Main",
                        "Main",
                        "cooked/scenes/Main.hasset",
                        [],
                        [
                            new KeyValuePair<string, string>(
                                PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds,
                                "helengine.AnimationPlayerComponent")
                        ])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>())));

        Assert.Empty(requiredFeatures);
    }

    /// <summary>
    /// Verifies used runtime types contribute their own feature attributes and activate generated runtime module registration requirements.
    /// </summary>
    [Fact]
    public void Collect_when_used_runtime_type_activates_generated_module_returns_type_and_plugin_requirements() {
        string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(GeneratedRuntimeModuleRegistrationTestComponent));
        DictionaryScriptTypeResolver scriptTypeResolver = new();
        scriptTypeResolver.Register(componentTypeId, typeof(GeneratedRuntimeModuleRegistrationTestComponent));
        EditorRuntimeFeatureCodeRequirementDiscoveryService service = new(scriptTypeResolver);

        PlatformBuildRequiredRuntimeFeature[] requiredFeatures = service.Collect(
            new PlatformBuildManifest(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                "windows",
                "1.0.0",
                "Main",
                [
                    new PlatformBuildScene(
                        "Main",
                        "Main",
                        "cooked/scenes/Main.hasset",
                        [],
                        [
                            new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds, componentTypeId)
                        ])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>())));

        Assert.Collection(
            requiredFeatures.OrderBy(requirement => requirement.FeatureId, StringComparer.Ordinal),
            requirement => {
                Assert.Equal("host_file_system", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Plugin, requirement.SourceKind);
                Assert.Equal("editor-tests-runtime-module", requirement.SourceId);
            },
            requirement => {
                Assert.Equal("test_runtime_feature", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.RuntimeType, requirement.SourceKind);
                Assert.Equal(typeof(GeneratedRuntimeModuleRegistrationTestComponent).FullName, requirement.SourceId);
            });
    }
}

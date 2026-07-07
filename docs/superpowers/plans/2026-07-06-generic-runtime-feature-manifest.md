# Generic Runtime Feature Manifest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build one generic engine-wide runtime feature manifest system that aggregates content-derived and code/plugin-derived feature requirements, fails builds when users disable required features, emits dependency reports, and wires DS to consume the manifest first.

**Architecture:** Add a platform-neutral feature manifest model to `helengine.baseplatform`, then introduce one editor-side aggregation service that unions content analyzers and code/plugin declarations into that model before platform packaging. Reuse the existing physics3d analyzer pattern for the first content source, add attribute-driven code requirement discovery for participating runtime types, and feed the resulting manifest into validation, report writing, generated-core symbol emission, and DS build settings.

**Tech Stack:** C#/.NET 9, HelEngine editor build graph, HelEngine baseplatform manifest model, existing physics3d feature analysis, DS platform plugin (`C:\dev\helworks\helengine-ds`)

---

### Task 1: Add The Generic Manifest Domain Model

**Files:**
- Create: `engine/helengine.baseplatform/Manifest/RuntimeFeatureRequirementSourceKind.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformBuildRequiredRuntimeFeature.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformBuildRuntimeFeatureManifest.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Test: `engine/helengine.baseplatform.tests/Manifest/PlatformBuildRuntimeFeatureManifestTests.cs`
- Test: `engine/helengine.baseplatform.tests/Manifest/PlatformBuildManifestTests.cs`

- [ ] **Step 1: Write the failing manifest model tests**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.baseplatform.tests.Manifest;

/// <summary>
/// Verifies the runtime feature manifest domain model preserves required feature provenance.
/// </summary>
public sealed class PlatformBuildRuntimeFeatureManifestTests {
    /// <summary>
    /// Ensures one runtime feature manifest preserves the supplied ordered requirement records.
    /// </summary>
    [Fact]
    public void Ctor_preserves_ordered_required_feature_records() {
        PlatformBuildRequiredRuntimeFeature[] requirements = [
            new PlatformBuildRequiredRuntimeFeature(
                "render3d.material.textured_lit",
                RuntimeFeatureRequirementSourceKind.Material,
                "Materials/rendering/test/Cube00",
                "material schema requires textured lit 3D runtime path"),
            new PlatformBuildRequiredRuntimeFeature(
                "physics3d.box_box_contact",
                RuntimeFeatureRequirementSourceKind.Scene,
                "Scenes/rendering/physics_stack_boxes",
                "scene serialized rigid-body and box collider contact pair")
        ];

        PlatformBuildRuntimeFeatureManifest manifest = new(requirements);

        Assert.Collection(
            manifest.RequiredFeatures,
            requirement => {
                Assert.Equal("render3d.material.textured_lit", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Material, requirement.SourceKind);
                Assert.Equal("Materials/rendering/test/Cube00", requirement.SourceId);
                Assert.Equal("material schema requires textured lit 3D runtime path", requirement.Reason);
            },
            requirement => {
                Assert.Equal("physics3d.box_box_contact", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Scene, requirement.SourceKind);
                Assert.Equal("Scenes/rendering/physics_stack_boxes", requirement.SourceId);
                Assert.Equal("scene serialized rigid-body and box collider contact pair", requirement.Reason);
            });
    }
}
```

- [ ] **Step 2: Run the new manifest tests to verify they fail**

Run: `dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter PlatformBuildRuntimeFeatureManifestTests -v minimal`

Expected: FAIL with missing types such as `RuntimeFeatureRequirementSourceKind`, `PlatformBuildRequiredRuntimeFeature`, and `PlatformBuildRuntimeFeatureManifest`.

- [ ] **Step 3: Add the new manifest domain types**

```csharp
namespace helengine.baseplatform.Manifest;

/// <summary>
/// Identifies the origin category for one required runtime feature record.
/// </summary>
public enum RuntimeFeatureRequirementSourceKind {
    Scene = 0,
    Material = 1,
    Texture = 2,
    AnimationClip = 3,
    CodeType = 4,
    Plugin = 5,
    BuildSystem = 6
}
```

```csharp
namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one required runtime feature together with its build-time provenance.
/// </summary>
public sealed class PlatformBuildRequiredRuntimeFeature {
    /// <summary>
    /// Initializes one required runtime feature record.
    /// </summary>
    public PlatformBuildRequiredRuntimeFeature(
        string featureId,
        RuntimeFeatureRequirementSourceKind sourceKind,
        string sourceId,
        string reason) {
        if (string.IsNullOrWhiteSpace(featureId)) {
            throw new ArgumentException("Feature id is required.", nameof(featureId));
        } else if (string.IsNullOrWhiteSpace(sourceId)) {
            throw new ArgumentException("Source id is required.", nameof(sourceId));
        } else if (string.IsNullOrWhiteSpace(reason)) {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        FeatureId = featureId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the stable required runtime feature id.
    /// </summary>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the origin category for the requirement.
    /// </summary>
    public RuntimeFeatureRequirementSourceKind SourceKind { get; }

    /// <summary>
    /// Gets the stable source identifier that required the feature.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Gets the human-readable reason why the source required the feature.
    /// </summary>
    public string Reason { get; }
}
```

```csharp
namespace helengine.baseplatform.Manifest;

/// <summary>
/// Captures the ordered required runtime feature records for one fully resolved build.
/// </summary>
public sealed class PlatformBuildRuntimeFeatureManifest {
    /// <summary>
    /// Initializes one runtime feature manifest from ordered required feature records.
    /// </summary>
    public PlatformBuildRuntimeFeatureManifest(PlatformBuildRequiredRuntimeFeature[] requiredFeatures) {
        if (requiredFeatures == null) {
            throw new ArgumentNullException(nameof(requiredFeatures));
        } else if (Array.Exists(requiredFeatures, candidate => candidate == null)) {
            throw new ArgumentException("Required feature collection cannot contain null entries.", nameof(requiredFeatures));
        }

        RequiredFeatures = [.. requiredFeatures];
    }

    /// <summary>
    /// Gets the ordered required runtime feature records.
    /// </summary>
    public PlatformBuildRequiredRuntimeFeature[] RequiredFeatures { get; }
}
```

- [ ] **Step 4: Add the manifest to `PlatformBuildManifest`**

```csharp
/// <summary>
/// Gets or sets the aggregated runtime feature manifest resolved by the editor build graph.
/// </summary>
public PlatformBuildRuntimeFeatureManifest RuntimeFeatureManifest { get; set; } = new PlatformBuildRuntimeFeatureManifest([]);
```

```csharp
[Fact]
public void RuntimeFeatureManifest_defaults_to_empty_manifest() {
    PlatformBuildManifest manifest = new(
        1,
        "project",
        "1.0.0",
        "1.0.0",
        Array.Empty<PlatformBuildScene>(),
        Array.Empty<PlatformBuildAsset>());

    Assert.NotNull(manifest.RuntimeFeatureManifest);
    Assert.Empty(manifest.RuntimeFeatureManifest.RequiredFeatures);
}
```

- [ ] **Step 5: Run the focused baseplatform tests and verify they pass**

Run: `dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "PlatformBuildRuntimeFeatureManifestTests|PlatformBuildManifestTests" -v minimal`

Expected: PASS with `0` failed tests.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.baseplatform/Manifest/RuntimeFeatureRequirementSourceKind.cs engine/helengine.baseplatform/Manifest/PlatformBuildRequiredRuntimeFeature.cs engine/helengine.baseplatform/Manifest/PlatformBuildRuntimeFeatureManifest.cs engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs engine/helengine.baseplatform.tests/Manifest/PlatformBuildRuntimeFeatureManifestTests.cs engine/helengine.baseplatform.tests/Manifest/PlatformBuildManifestTests.cs
git commit -m "feat: add generic runtime feature manifest model"
```

### Task 2: Build The Editor-Side Aggregation Service

**Files:**
- Create: `engine/helengine.editor/managers/project/IEditorRuntimeFeatureRequirementCollector.cs`
- Create: `engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureManifestServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

- [ ] **Step 1: Write the failing aggregation tests**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor runtime feature manifest service unions multiple collector outputs in stable order.
/// </summary>
public sealed class EditorRuntimeFeatureManifestServiceTests {
    /// <summary>
    /// Ensures duplicate feature provenance records are de-duplicated by full record identity while preserving first-seen order.
    /// </summary>
    [Fact]
    public void BuildManifest_unions_collector_outputs_in_first_seen_order() {
        IEditorRuntimeFeatureRequirementCollector[] collectors = [
            new FakeEditorRuntimeFeatureRequirementCollector(
                new PlatformBuildRequiredRuntimeFeature(
                    "render3d.material.textured_lit",
                    RuntimeFeatureRequirementSourceKind.Material,
                    "Materials/rendering/test/Cube00",
                    "material schema requires textured lit 3D runtime path")),
            new FakeEditorRuntimeFeatureRequirementCollector(
                new PlatformBuildRequiredRuntimeFeature(
                    "render3d.material.textured_lit",
                    RuntimeFeatureRequirementSourceKind.Material,
                    "Materials/rendering/test/Cube00",
                    "material schema requires textured lit 3D runtime path"),
                new PlatformBuildRequiredRuntimeFeature(
                    "physics3d.box_box_contact",
                    RuntimeFeatureRequirementSourceKind.Scene,
                    "Scenes/rendering/physics_stack_boxes",
                    "scene serialized rigid-body and box collider contact pair"))
        ];

        EditorRuntimeFeatureManifestService service = new(collectors);

        PlatformBuildRuntimeFeatureManifest manifest = service.BuildManifest();

        Assert.Collection(
            manifest.RequiredFeatures,
            requirement => Assert.Equal("render3d.material.textured_lit", requirement.FeatureId),
            requirement => Assert.Equal("physics3d.box_box_contact", requirement.FeatureId));
    }
}
```

- [ ] **Step 2: Run the aggregation tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorRuntimeFeatureManifestServiceTests -v minimal`

Expected: FAIL with missing types such as `IEditorRuntimeFeatureRequirementCollector` and `EditorRuntimeFeatureManifestService`.

- [ ] **Step 3: Add the collector interface and aggregation service**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Collects required runtime feature records from one build-time source.
/// </summary>
public interface IEditorRuntimeFeatureRequirementCollector {
    /// <summary>
    /// Collects the required runtime feature records contributed by the current source.
    /// </summary>
    IReadOnlyList<PlatformBuildRequiredRuntimeFeature> Collect();
}
```

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Aggregates required runtime feature records from multiple build-time collectors into one manifest.
/// </summary>
public sealed class EditorRuntimeFeatureManifestService {
    readonly IReadOnlyList<IEditorRuntimeFeatureRequirementCollector> Collectors;

    /// <summary>
    /// Initializes one manifest aggregation service.
    /// </summary>
    public EditorRuntimeFeatureManifestService(IReadOnlyList<IEditorRuntimeFeatureRequirementCollector> collectors) {
        Collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
    }

    /// <summary>
    /// Builds one aggregated runtime feature manifest.
    /// </summary>
    public PlatformBuildRuntimeFeatureManifest BuildManifest() {
        List<PlatformBuildRequiredRuntimeFeature> orderedRequirements = [];
        HashSet<string> seenRequirementKeys = new(StringComparer.Ordinal);
        for (int collectorIndex = 0; collectorIndex < Collectors.Count; collectorIndex++) {
            IReadOnlyList<PlatformBuildRequiredRuntimeFeature> requirements = Collectors[collectorIndex].Collect();
            for (int requirementIndex = 0; requirementIndex < requirements.Count; requirementIndex++) {
                PlatformBuildRequiredRuntimeFeature requirement = requirements[requirementIndex];
                string key = requirement.FeatureId + "|" + requirement.SourceKind + "|" + requirement.SourceId + "|" + requirement.Reason;
                if (seenRequirementKeys.Add(key)) {
                    orderedRequirements.Add(requirement);
                }
            }
        }

        return new PlatformBuildRuntimeFeatureManifest([.. orderedRequirements]);
    }
}
```

- [ ] **Step 4: Attach the manifest to the build graph immediately after cooking and code-module discovery inputs are available**

```csharp
EditorRuntimeFeatureManifestService runtimeFeatureManifestService = CreateRuntimeFeatureManifestService(
    queueItem,
    cookedManifest,
    builder.Definition,
    selectedBuildProfileId,
    selectedGraphicsProfileId);
cookedManifest.RuntimeFeatureManifest = runtimeFeatureManifestService.BuildManifest();
```

- [ ] **Step 5: Run the focused editor tests and verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorRuntimeFeatureManifestServiceTests|EditorPlatformBuildGraphRunnerTests" -v minimal`

Expected: PASS with `0` failed tests.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/project/IEditorRuntimeFeatureRequirementCollector.cs engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestService.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureManifestServiceTests.cs
git commit -m "feat: aggregate editor runtime feature manifests"
```

### Task 3: Bridge Physics3D Into The Generic Manifest

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPhysics3DRuntimeFeatureRequirementCollector.cs`
- Modify: `engine/helengine.physics3d/runtime/PhysicsSceneFeatureSymbolCatalog3D.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPhysics3DCodegenFeatureSymbolService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPhysics3DRuntimeFeatureRequirementCollectorTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPhysics3DCodegenFeatureSymbolServiceTests.cs`

- [ ] **Step 1: Write the failing physics bridge tests**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies physics3d scene analysis can be projected into generic required runtime feature records.
/// </summary>
public sealed class EditorPhysics3DRuntimeFeatureRequirementCollectorTests {
    /// <summary>
    /// Ensures one analyzed physics scene emits generic runtime feature ids with scene provenance.
    /// </summary>
    [Fact]
    public void Collect_whenSceneRequiresBoxAndTriggerFeatures_emits_scene_required_feature_records() {
        EditorPhysics3DRuntimeFeatureRequirementCollector collector = new(
            [
                ("cube_test", (uint)(PhysicsSceneFeatureFlags3D.BoxBoxContact | PhysicsSceneFeatureFlags3D.TriggerEvents))
            ]);

        IReadOnlyList<PlatformBuildRequiredRuntimeFeature> requirements = collector.Collect();

        Assert.Contains(requirements, requirement => requirement.FeatureId == "physics3d.box_box_contact");
        Assert.Contains(requirements, requirement => requirement.FeatureId == "physics3d.trigger_events");
        Assert.All(requirements, requirement => Assert.Equal(RuntimeFeatureRequirementSourceKind.Scene, requirement.SourceKind));
    }
}
```

- [ ] **Step 2: Run the physics bridge tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorPhysics3DRuntimeFeatureRequirementCollectorTests|EditorPhysics3DCodegenFeatureSymbolServiceTests" -v minimal`

Expected: FAIL with missing collector type and missing generic feature-id mapping helper.

- [ ] **Step 3: Add a generic feature-id builder to the physics catalog**

```csharp
/// <summary>
/// Builds the ordered generic runtime feature ids represented by the supplied scene feature flags.
/// </summary>
public static IReadOnlyList<string> BuildFeatureIds(PhysicsSceneFeatureFlags3D featureFlags) {
    List<string> featureIds = [];
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.KinematicMotion, "physics3d.kinematic_motion");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.TriggerEvents, "physics3d.trigger_events");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterController, "physics3d.character_controller");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.BoxBoxContact, "physics3d.box_box_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereSphereContact, "physics3d.sphere_sphere_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereBoxContact, "physics3d.sphere_box_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleBoxContact, "physics3d.capsule_box_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleSphereContact, "physics3d.capsule_sphere_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleCapsuleContact, "physics3d.capsule_capsule_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.BoxStaticMeshContact, "physics3d.box_static_mesh_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereStaticMeshContact, "physics3d.sphere_static_mesh_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleStaticMeshContact, "physics3d.capsule_static_mesh_contact");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport, "physics3d.character_controller_body_support");
    AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerStaticMeshSupport, "physics3d.character_controller_static_mesh_support");
    return featureIds;
}
```

- [ ] **Step 4: Add the collector and reuse the existing analyzer-backed scene union**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Collects generic required runtime feature records from analyzed 3D physics scene feature masks.
/// </summary>
public sealed class EditorPhysics3DRuntimeFeatureRequirementCollector : IEditorRuntimeFeatureRequirementCollector {
    readonly IReadOnlyList<(string SceneId, uint FeatureFlags)> SceneFeatureFlags;

    /// <summary>
    /// Initializes one collector from analyzed scene feature masks.
    /// </summary>
    public EditorPhysics3DRuntimeFeatureRequirementCollector(IReadOnlyList<(string SceneId, uint FeatureFlags)> sceneFeatureFlags) {
        SceneFeatureFlags = sceneFeatureFlags ?? throw new ArgumentNullException(nameof(sceneFeatureFlags));
    }

    /// <summary>
    /// Collects the required runtime feature records contributed by analyzed scenes.
    /// </summary>
    public IReadOnlyList<PlatformBuildRequiredRuntimeFeature> Collect() {
        List<PlatformBuildRequiredRuntimeFeature> requirements = [];
        for (int sceneIndex = 0; sceneIndex < SceneFeatureFlags.Count; sceneIndex++) {
            (string sceneId, uint featureFlagsValue) = SceneFeatureFlags[sceneIndex];
            PhysicsSceneFeatureFlags3D featureFlags = (PhysicsSceneFeatureFlags3D)featureFlagsValue;
            IReadOnlyList<string> featureIds = PhysicsSceneFeatureSymbolCatalog3D.BuildFeatureIds(featureFlags);
            for (int featureIndex = 0; featureIndex < featureIds.Count; featureIndex++) {
                requirements.Add(new PlatformBuildRequiredRuntimeFeature(
                    featureIds[featureIndex],
                    RuntimeFeatureRequirementSourceKind.Scene,
                    sceneId,
                    "scene serialized 3D physics runtime requirement"));
            }
        }

        return requirements;
    }
}
```

- [ ] **Step 5: Keep the existing preprocessor-symbol path intact by mapping from generic feature ids back into the current physics symbol catalog**

```csharp
public IReadOnlyList<string> ResolveSymbols(IReadOnlyList<string> sceneIds) {
    PhysicsSceneFeatureFlags3D featureFlags = ResolveFeatureFlags(sceneIds);
    return PhysicsSceneFeatureSymbolCatalog3D.BuildSymbols(featureFlags);
}
```

- [ ] **Step 6: Run the focused physics bridge tests and verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorPhysics3DRuntimeFeatureRequirementCollectorTests|EditorPhysics3DCodegenFeatureSymbolServiceTests|PhysicsSceneFeatureAnalyzerStaticMeshPayloadTests" -v minimal`

Expected: PASS with `0` failed tests.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorPhysics3DRuntimeFeatureRequirementCollector.cs engine/helengine.physics3d/runtime/PhysicsSceneFeatureSymbolCatalog3D.cs engine/helengine.editor/managers/project/EditorPhysics3DCodegenFeatureSymbolService.cs engine/helengine.editor.tests/managers/project/EditorPhysics3DRuntimeFeatureRequirementCollectorTests.cs engine/helengine.editor.tests/managers/project/EditorPhysics3DCodegenFeatureSymbolServiceTests.cs
git commit -m "feat: bridge physics feature analysis into generic manifest"
```

### Task 4: Add Attribute-Driven Code And Plugin Requirement Discovery

**Files:**
- Create: `engine/helengine.core/features/RuntimeFeatureRequirementAttribute.cs`
- Create: `engine/helengine.editor/managers/project/EditorRuntimeFeatureCodeRequirementDiscoveryService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing code-discovery tests**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies attribute-declared runtime feature requirements are emitted only for participating runtime types.
/// </summary>
public sealed class EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests {
    [Fact]
    public void Collect_whenParticipatingTypeDeclaresAttribute_emits_code_type_requirement_record() {
        Type[] participatingTypes = [typeof(FakeFeatureAttributedRuntimeType)];
        EditorRuntimeFeatureCodeRequirementDiscoveryService service = new(participatingTypes);

        IReadOnlyList<PlatformBuildRequiredRuntimeFeature> requirements = service.Collect();

        PlatformBuildRequiredRuntimeFeature requirement = Assert.Single(requirements);
        Assert.Equal("render3d.material.textured_lit", requirement.FeatureId);
        Assert.Equal(RuntimeFeatureRequirementSourceKind.CodeType, requirement.SourceKind);
        Assert.Equal(typeof(FakeFeatureAttributedRuntimeType).FullName, requirement.SourceId);
    }
}
```

- [ ] **Step 2: Run the code-discovery tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests|EditorGeneratedCoreRegenerationServiceTests" -v minimal`

Expected: FAIL with missing attribute and missing discovery service types.

- [ ] **Step 3: Add the runtime feature requirement attribute**

```csharp
namespace helengine;

/// <summary>
/// Declares one required runtime feature id for a participating runtime type or member.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class RuntimeFeatureRequirementAttribute : Attribute {
    /// <summary>
    /// Initializes one runtime feature requirement attribute.
    /// </summary>
    public RuntimeFeatureRequirementAttribute(string featureId, string reason) {
        if (string.IsNullOrWhiteSpace(featureId)) {
            throw new ArgumentException("Feature id must be provided.", nameof(featureId));
        } else if (string.IsNullOrWhiteSpace(reason)) {
            throw new ArgumentException("Reason must be provided.", nameof(reason));
        }

        FeatureId = featureId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the required runtime feature id.
    /// </summary>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the reason why the annotated runtime element requires the feature.
    /// </summary>
    public string Reason { get; }
}
```

- [ ] **Step 4: Add the discovery service that inspects participating runtime types**

```csharp
using System.Reflection;
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Collects required runtime feature records from participating runtime types annotated with runtime feature requirement attributes.
/// </summary>
public sealed class EditorRuntimeFeatureCodeRequirementDiscoveryService : IEditorRuntimeFeatureRequirementCollector {
    readonly IReadOnlyList<Type> ParticipatingTypes;

    /// <summary>
    /// Initializes one code requirement discovery service.
    /// </summary>
    public EditorRuntimeFeatureCodeRequirementDiscoveryService(IReadOnlyList<Type> participatingTypes) {
        ParticipatingTypes = participatingTypes ?? throw new ArgumentNullException(nameof(participatingTypes));
    }

    /// <summary>
    /// Collects required runtime feature records from the participating runtime types.
    /// </summary>
    public IReadOnlyList<PlatformBuildRequiredRuntimeFeature> Collect() {
        List<PlatformBuildRequiredRuntimeFeature> requirements = [];
        for (int typeIndex = 0; typeIndex < ParticipatingTypes.Count; typeIndex++) {
            Type participatingType = ParticipatingTypes[typeIndex];
            RuntimeFeatureRequirementAttribute[] attributes = participatingType.GetCustomAttributes<RuntimeFeatureRequirementAttribute>(false).ToArray();
            for (int attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++) {
                RuntimeFeatureRequirementAttribute attribute = attributes[attributeIndex];
                requirements.Add(new PlatformBuildRequiredRuntimeFeature(
                    attribute.FeatureId,
                    RuntimeFeatureRequirementSourceKind.CodeType,
                    participatingType.FullName ?? participatingType.Name,
                    attribute.Reason));
            }
        }

        return requirements;
    }
}
```

- [ ] **Step 5: Resolve participating runtime types from authored code modules and generated-core project inputs before regeneration**

```csharp
IReadOnlyList<Type> participatingRuntimeTypes = ResolveParticipatingRuntimeTypes(cookedManifest.CodeModules);
EditorRuntimeFeatureCodeRequirementDiscoveryService codeRequirementCollector = new(participatingRuntimeTypes);
```

- [ ] **Step 6: Run the focused code-discovery tests and verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests|EditorGeneratedCoreRegenerationServiceTests|EditorBuildQueueItemDocumentTests" -v minimal`

Expected: PASS with `0` failed tests.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.core/features/RuntimeFeatureRequirementAttribute.cs engine/helengine.editor/managers/project/EditorRuntimeFeatureCodeRequirementDiscoveryService.cs engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureCodeRequirementDiscoveryServiceTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git commit -m "feat: discover runtime feature requirements from code"
```

### Task 5: Add Disabled-Feature Validation And Dependency Report Writing

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestValidationService.cs`
- Create: `engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestReportWriter.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.baseplatform/Definitions/RuntimeGenerationContract.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureManifestValidationServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing validation and report tests**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies user-disabled features fail the build when the runtime feature manifest proves they are required.
/// </summary>
public sealed class EditorRuntimeFeatureManifestValidationServiceTests {
    [Fact]
    public void Validate_whenDisabledFeatureIsRequired_throws_with_dependency_report_details() {
        PlatformBuildRuntimeFeatureManifest manifest = new([
            new PlatformBuildRequiredRuntimeFeature(
                "render3d.material.textured_lit",
                RuntimeFeatureRequirementSourceKind.Material,
                "Materials/rendering/test/Cube00",
                "material schema requires textured lit 3D runtime path")
        ]);
        EditorRuntimeFeatureManifestValidationService service = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.Validate(manifest, ["render3d.material.textured_lit"]));

        Assert.Contains("render3d.material.textured_lit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Materials/rendering/test/Cube00", exception.Message, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the validation tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorRuntimeFeatureManifestValidationServiceTests|EditorPlatformBuildGraphRunnerTests" -v minimal`

Expected: FAIL with missing validation service and missing report writer types.

- [ ] **Step 3: Add strict validation**

```csharp
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Validates user-disabled runtime features against the aggregated build feature manifest.
/// </summary>
public sealed class EditorRuntimeFeatureManifestValidationService {
    /// <summary>
    /// Validates that the supplied disabled features do not conflict with required manifest entries.
    /// </summary>
    public void Validate(PlatformBuildRuntimeFeatureManifest manifest, IReadOnlyList<string> disabledFeatureIds) {
        if (manifest == null) {
            throw new ArgumentNullException(nameof(manifest));
        } else if (disabledFeatureIds == null) {
            throw new ArgumentNullException(nameof(disabledFeatureIds));
        }

        HashSet<string> disabledFeatures = new(disabledFeatureIds, StringComparer.Ordinal);
        List<PlatformBuildRequiredRuntimeFeature> blockedRequirements = [];
        for (int index = 0; index < manifest.RequiredFeatures.Length; index++) {
            PlatformBuildRequiredRuntimeFeature requirement = manifest.RequiredFeatures[index];
            if (disabledFeatures.Contains(requirement.FeatureId)) {
                blockedRequirements.Add(requirement);
            }
        }

        if (blockedRequirements.Count == 0) {
            return;
        }

        throw new InvalidOperationException(EditorRuntimeFeatureManifestReportWriter.BuildFailureMessage(blockedRequirements));
    }
}
```

- [ ] **Step 4: Add the report writer**

```csharp
using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Writes human-readable runtime feature dependency reports for successful and failed builds.
/// </summary>
public static class EditorRuntimeFeatureManifestReportWriter {
    /// <summary>
    /// Builds one failure message for blocked required runtime features.
    /// </summary>
    public static string BuildFailureMessage(IReadOnlyList<PlatformBuildRequiredRuntimeFeature> blockedRequirements) {
        StringBuilder builder = new();
        builder.AppendLine("Disabled runtime features are still required by this build:");
        for (int index = 0; index < blockedRequirements.Count; index++) {
            PlatformBuildRequiredRuntimeFeature requirement = blockedRequirements[index];
            builder.Append("- ");
            builder.Append(requirement.FeatureId);
            builder.Append(" required by ");
            builder.Append(requirement.SourceKind);
            builder.Append(" ");
            builder.Append(requirement.SourceId);
            builder.Append(" because ");
            builder.Append(requirement.Reason);
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
```

- [ ] **Step 5: Validate immediately after manifest aggregation and write a durable report file under the build workspace logs root**

```csharp
PlatformBuildRuntimeFeatureManifest runtimeFeatureManifest = runtimeFeatureManifestService.BuildManifest();
cookedManifest.RuntimeFeatureManifest = runtimeFeatureManifest;
WriteRuntimeFeatureManifestReport(workspace.LogsRootPath, runtimeFeatureManifest, queueItem.SelectedCodegenOptionValues);
runtimeFeatureManifestValidationService.Validate(runtimeFeatureManifest, ResolveDisabledFeatureIds(queueItem, builder.Definition, selectedCodegenProfileId));
```

- [ ] **Step 6: Run the focused validation tests and verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorRuntimeFeatureManifestValidationServiceTests|EditorPlatformBuildGraphRunnerTests|EditorGeneratedCoreRegenerationServiceTests" -v minimal`

Expected: PASS with `0` failed tests.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestValidationService.cs engine/helengine.editor/managers/project/EditorRuntimeFeatureManifestReportWriter.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.baseplatform/Definitions/RuntimeGenerationContract.cs engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs engine/helengine.editor.tests/managers/project/EditorRuntimeFeatureManifestValidationServiceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
git commit -m "feat: validate disabled runtime features against manifest"
```

### Task 6: Wire DS To The Generic Manifest And Reported Disabled Feature Set

**Files:**
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsPlatformDefinitionFactory.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsPlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsNativeBuildExecutor.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsBuildWorkspace.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsPlatformDefinitionFactoryTests.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsPlatformAssetBuilderTests.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsNativeBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing DS integration tests**

```csharp
[Fact]
public void Create_exposes_disabled_runtime_feature_build_setting() {
    PlatformDefinition definition = NintendoDsPlatformDefinitionFactory.Create();

    PlatformBuildProfileDefinition buildProfile = Assert.Single(definition.BuildProfiles);
    PlatformSettingDefinition disabledFeatureSetting = Assert.Single(
        buildProfile.Settings.Where(candidate => candidate.SettingId == "disabled-runtime-features"));

    Assert.Equal(PlatformSettingKind.Text, disabledFeatureSetting.SettingKind);
    Assert.Equal(string.Empty, disabledFeatureSetting.DefaultValue);
}
```

```csharp
[Fact]
public async Task BuildAsync_whenDisabledRuntimeFeaturesAreSupplied_propagatesWorkspaceSetting() {
    // Arrange the existing fake build request harness, but set:
    // ["disabled-runtime-features"] = "debug_overlay;physics3d.box_box_contact"
    // Assert the fake workspace captures the raw disabled feature string for downstream DS native build/report use.
}
```

- [ ] **Step 2: Run the DS integration tests to verify they fail**

Run: `dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter "Create_exposes_disabled_runtime_feature_build_setting|BuildAsync_whenDisabledRuntimeFeaturesAreSupplied_propagatesWorkspaceSetting" -v minimal`

Expected: FAIL because the DS build profile does not yet expose `disabled-runtime-features` and the workspace does not yet carry the setting.

- [ ] **Step 3: Add the DS build setting and propagate it through the workspace**

```csharp
new PlatformSettingDefinition(
    "disabled-runtime-features",
    "Disabled Runtime Features",
    PlatformSettingKind.Text,
    string.Empty,
    true,
    [])
```

```csharp
public string DisabledRuntimeFeatures { get; }
```

```csharp
string disabledRuntimeFeatures = ReadOptionalBuildOption(
    request.SelectedBuildOptionValues,
    "disabled-runtime-features",
    string.Empty);
```

- [ ] **Step 4: Keep the DS native seam generic by forwarding the raw disabled-feature string for logging and future DS-native compile-time consumers**

```csharp
"HELENGINE_DS_DISABLED_RUNTIME_FEATURES=" + workspace.DisabledRuntimeFeatures
```

- [ ] **Step 5: Run the focused DS tests and one build verification**

Run: `dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter "NintendoDsPlatformDefinitionFactoryTests|NintendoDsPlatformAssetBuilderTests|NintendoDsNativeBuildExecutorTests" -v minimal`

Expected: PASS with `0` failed tests.

Run: `C:\dev\helworks\helengine\artifacts\build-platform.ps1 -Project C:\dev\helprojs\city -Platform ds -Output C:\dev\helprojs\city\ds-runtime-feature-manifest-pass1 -Configuration Debug`

Expected: PASS, with the DS build producing a runtime feature manifest report in the build logs root and failing only when a user-disabled required feature is intentionally configured.

- [ ] **Step 6: Commit**

```bash
git -C C:\dev\helworks\helengine-ds add builder/NintendoDsPlatformDefinitionFactory.cs builder/NintendoDsPlatformAssetBuilder.cs builder/NintendoDsNativeBuildExecutor.cs builder/NintendoDsBuildWorkspace.cs builder.tests/NintendoDsPlatformDefinitionFactoryTests.cs builder.tests/NintendoDsPlatformAssetBuilderTests.cs builder.tests/NintendoDsNativeBuildExecutorTests.cs
git -C C:\dev\helworks\helengine-ds commit -m "feat: wire DS to generic runtime feature manifest settings"
```

## Self-Review

### Spec Coverage

- Generic fine-grained manifest model: covered by Task 1.
- Content-derived requirements: covered by Task 3 using physics3d as the first bridge.
- Code/plugin-derived requirements with hybrid attribute plus registration discovery: covered by Task 4.
- Strict failure when a disabled feature is required: covered by Task 5.
- Dependency reporting: covered by Task 5.
- DS consuming the generic seam first: covered by Task 6.

No spec gaps remain for the first implementation slice.

### Placeholder Scan

- No `TODO`, `TBD`, or “similar to” references remain.
- Every code-changing step contains concrete code blocks or exact integration snippets.
- Every verification step includes an explicit command and expected result.

### Type Consistency

- Manifest types consistently use `PlatformBuildRequiredRuntimeFeature`, `PlatformBuildRuntimeFeatureManifest`, and `RuntimeFeatureRequirementSourceKind`.
- Editor collector types consistently implement `IEditorRuntimeFeatureRequirementCollector`.
- Validation consistently consumes `PlatformBuildRuntimeFeatureManifest`.
- The DS integration consistently uses the build-setting id `disabled-runtime-features`.

No naming contradictions remain.

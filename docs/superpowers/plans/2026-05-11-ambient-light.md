# Ambient Light Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a global `AmbientLightComponent` that affects 3D materials, serializes like the existing light families, and stacks on Windows through a dedicated DirectX11 ambient-light channel.

**Architecture:** Implement `AmbientLightComponent` as a new `LightComponent` family in core, route it through the existing editor/runtime scene pipelines, then feed it into DirectX11 as an accumulated ambient term outside the current 4-slot direct-light buffer. Keep the direct-light slot contract intact and verify that multiple ambient lights stack correctly on Windows.

**Tech Stack:** C#/.NET 9, xUnit, helengine core scene serialization, helengine editor persistence/packaging, DirectX11 forward renderer, HLSL built-in shader pipeline.

---

### Task 1: Add the Core Ambient Light Family

**Files:**
- Create: `engine/helengine.core/components/AmbientLightComponent.cs`
- Modify: `engine/helengine.core/model/LightType.cs`
- Test: `engine/helengine.editor.tests/rendering/LightComponentTests.cs`

- [ ] **Step 1: Write the failing core light-family tests**

```csharp
[Fact]
public void AmbientLightComponent_WhenCreated_UsesAmbientLightDefaults() {
    AmbientLightComponent lightComponent = new AmbientLightComponent();

    Assert.Equal(LightType.Ambient, lightComponent.LightType);
    Assert.False(lightComponent.ShadowsEnabled);
    Assert.Equal(ShadowMapMode.Disabled, lightComponent.ShadowMapMode);
    Assert.Equal(1f, lightComponent.Intensity);
    Assert.Equal(new float4(1f, 1f, 1f, 1f), lightComponent.Color);
}

[Fact]
public void LightDirectionUtility_WhenResolvingAmbientLight_ThrowsBecauseAmbientLightsHaveNoDirection() {
    Entity entity = new Entity();
    entity.InitComponents();
    AmbientLightComponent lightComponent = new AmbientLightComponent();
    entity.AddComponent(lightComponent);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => LightDirectionUtility.GetLightDirection(lightComponent));
    Assert.Contains("Ambient", exception.Message);
}
```

- [ ] **Step 2: Run the light-component tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter LightComponentTests --no-restore
```

Expected:

```text
FAIL because AmbientLightComponent and LightType.Ambient do not exist yet.
```

- [ ] **Step 3: Implement the minimal core light-family changes**

Create `engine/helengine.core/components/AmbientLightComponent.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Represents one global ambient light contribution applied to lit 3D materials.
    /// </summary>
    public class AmbientLightComponent : LightComponent {
        /// <summary>
        /// Initializes one ambient light with non-shadowing defaults.
        /// </summary>
        public AmbientLightComponent() : base(LightType.Ambient) {
            ShadowsEnabled = false;
            ShadowMapMode = ShadowMapMode.Disabled;
            ShadowStrength = 0f;
        }
    }
}
```

Update `engine/helengine.core/model/LightType.cs`:

```csharp
public enum LightType : byte {
    Directional = 0,
    Point = 1,
    Spot = 2,
    Ambient = 3
}
```

Update `engine/helengine.core/utils/LightDirectionUtility.cs` so ambient lights fail explicitly:

```csharp
if (lightComponent is AmbientLightComponent) {
    throw new InvalidOperationException("Ambient lights do not expose a direction.");
}
```

- [ ] **Step 4: Run the light-component tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter LightComponentTests --no-restore
```

Expected:

```text
PASS with the new AmbientLightComponent defaults and direction failure behavior.
```

- [ ] **Step 5: Commit the core ambient-light family**

```bash
git add engine/helengine.core/components/AmbientLightComponent.cs engine/helengine.core/model/LightType.cs engine/helengine.core/utils/LightDirectionUtility.cs engine/helengine.editor.tests/rendering/LightComponentTests.cs
git commit -m "Add ambient light core component"
```

### Task 2: Add Scene Persistence, Runtime Deserialization, and Editor Creation

**Files:**
- Create: `engine/helengine.editor/serialization/scene/AmbientLightComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/LightComponentScenePayloadSerializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/EditorSession.cs` add-menu handler registration or the file that owns the add-light handlers
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/LightComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`

- [ ] **Step 1: Write the failing persistence, runtime-load, and add-menu tests**

Add to `LightComponentPersistenceDescriptorTests.cs`:

```csharp
[Fact]
public void AmbientLightDescriptor_WhenRoundTripped_PreservesSharedFields() {
    AmbientLightComponentPersistenceDescriptor descriptor = new AmbientLightComponentPersistenceDescriptor();
    AmbientLightComponent lightComponent = new AmbientLightComponent {
        Color = new float4(0.2f, 0.3f, 0.4f, 1f),
        Intensity = 1.75f,
        ShadowsEnabled = false,
        ShadowMapMode = ShadowMapMode.Disabled,
        ShadowStrength = 0f
    };

    SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
    AmbientLightComponent loadedLight = Assert.IsType<AmbientLightComponent>(descriptor.DeserializeComponent(record, null, null));

    Assert.Equal(lightComponent.Color, loadedLight.Color);
    Assert.Equal(lightComponent.Intensity, loadedLight.Intensity);
    Assert.False(loadedLight.ShadowsEnabled);
    Assert.Equal(ShadowMapMode.Disabled, loadedLight.ShadowMapMode);
    Assert.Equal(0f, loadedLight.ShadowStrength);
}
```

Add to `RuntimeSceneLoadServiceTests.cs`:

```csharp
[Fact]
public void Load_WhenSceneContainsAmbientLightComponent_MaterializesTheComponent() {
    RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
        Core.Instance.ContentManager,
        TempRootPath,
        ShaderCompileTarget.DirectX11);
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
    SceneAsset sceneAsset = new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = "ambient-root",
                Name = "Ambient",
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "helengine.AmbientLightComponent",
                        ComponentIndex = 0,
                        Payload = WriteAmbientLightComponentPayload()
                    }
                }
            }
        }
    };

    IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
    AmbientLightComponent ambientLight = Assert.IsType<AmbientLightComponent>(Assert.Single(loadedRoots[0].Components, component => component is AmbientLightComponent));

    Assert.Equal(new float4(0.15f, 0.2f, 0.25f, 1f), ambientLight.Color);
    Assert.Equal(2.25f, ambientLight.Intensity);
    Assert.False(ambientLight.ShadowsEnabled);
}
```

Add to `EditorSessionAddMenuTests.cs`:

```csharp
[Fact]
public void HandleAddAmbientLightRequested_CreatesAmbientLightEntityAndSelectsIt() {
    EditorSession session = CreateSessionForAddCommands();

    InvokePrivate(session, "HandleAddAmbientLightRequested");

    EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
    AmbientLightComponent lightComponent = Assert.IsType<AmbientLightComponent>(Assert.Single(selectedEntity.Components, component => component is AmbientLightComponent));

    Assert.Equal("Ambient Light", selectedEntity.Name);
    Assert.False(lightComponent.ShadowsEnabled);
    Assert.Equal(1, GetHierarchyNodeCount(session));
}
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "LightComponentPersistenceDescriptorTests|RuntimeSceneLoadServiceTests|EditorSessionAddMenuTests" --no-restore
```

Expected:

```text
FAIL because the ambient persistence descriptor, runtime deserializer, payload helpers, and add-menu creation path do not exist yet.
```

- [ ] **Step 3: Implement the minimal persistence, runtime-load, and editor-creation path**

Create `engine/helengine.editor/serialization/scene/AmbientLightComponentPersistenceDescriptor.cs` by mirroring the common-light handling used by the other descriptor files:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Persists authored ambient light components inside editor scene assets.
    /// </summary>
    public class AmbientLightComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        public Type ComponentType => typeof(AmbientLightComponent);
        public string ComponentTypeId => "helengine.AmbientLightComponent";

        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (component is not AmbientLightComponent) {
                throw new InvalidOperationException("Ambient light descriptor can only serialize ambient light components.");
            }

            AmbientLightComponent lightComponent = (AmbientLightComponent)component;
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        public Component DeserializeComponent(SceneComponentAssetRecord record, SceneAsset asset, ISceneAssetReferenceResolver resolver) {
            AmbientLightComponent lightComponent = new AmbientLightComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            LightComponentTaggedFieldEncoding.ReadCommonFields(reader, lightComponent);
            return lightComponent;
        }
    }
}
```

Extend `LightComponentScenePayloadSerializer.cs`:

```csharp
public static void WriteAmbientLight(EngineBinaryWriter writer, AmbientLightComponent lightComponent) {
    if (writer == null) {
        throw new ArgumentNullException(nameof(writer));
    } else if (lightComponent == null) {
        throw new ArgumentNullException(nameof(lightComponent));
    }

    WriteCommonLightFields(writer, lightComponent);
}

public static AmbientLightComponent ReadAmbientLight(EngineBinaryReader reader) {
    return ReadAmbientLight(reader, CurrentVersion);
}

public static AmbientLightComponent ReadAmbientLight(EngineBinaryReader reader, byte version) {
    if (reader == null) {
        throw new ArgumentNullException(nameof(reader));
    } else if (version != CurrentVersion) {
        throw new InvalidOperationException($"Unsupported ambient light payload version '{version}'.");
    }

    AmbientLightComponent lightComponent = new AmbientLightComponent();
    ReadCommonLightFields(reader, lightComponent);
    return lightComponent;
}
```

Create `engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Materializes packaged ambient light scene payloads at runtime.
    /// </summary>
    public sealed class RuntimeAmbientLightComponentDeserializer : IRuntimeComponentDeserializer {
        const string ComponentType = "helengine.AmbientLightComponent";

        public string ComponentTypeId => ComponentType;

        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver resolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported ambient light payload version '{version}'.");
            }

            return LightComponentScenePayloadSerializer.ReadAmbientLight(reader, version);
        }
    }
}
```

Register the new type in:

```csharp
// engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs
registry.Register(new RuntimeAmbientLightComponentDeserializer());

// engine/helengine.editor/EditorSession.cs
persistenceRegistry.Register(new AmbientLightComponentPersistenceDescriptor());
```

Add one editor creation path by following the existing point/spot/directional handlers:

```csharp
void HandleAddAmbientLightRequested() {
    EditorEntity entity = SceneCreationService.CreateAmbientLight();
    sceneHierarchyPanel.Refresh();
    EditorSelectionService.SelectEntity(entity);
    EditorSceneMutationService.RaiseSceneMutated();
}
```

and in `EditorSceneCreationService.cs`:

```csharp
public EditorEntity CreateAmbientLight() {
    EditorEntity entity = CreateUserEntity("Ambient Light");
    entity.AddComponent(new AmbientLightComponent());
    return AddRootEntityAndReturn(entity);
}
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "LightComponentPersistenceDescriptorTests|RuntimeSceneLoadServiceTests|EditorSessionAddMenuTests" --no-restore
```

Expected:

```text
PASS with ambient-light persistence, runtime load, and add-menu creation working.
```

- [ ] **Step 5: Commit the persistence and editor-creation layer**

```bash
git add engine/helengine.editor/serialization/scene/AmbientLightComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs engine/helengine.core/scene/LightComponentScenePayloadSerializer.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor.tests/serialization/scene/LightComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs
git commit -m "Add ambient light scene persistence"
```

### Task 3: Add Windows Export Packaging Support for Ambient Lights

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing packaging regression**

Add to `EditorWindowsBuildScenePackagerTests.cs`:

```csharp
[Fact]
public void Package_WhenSceneContainsAmbientLight_RewritesTheRuntimePayload() {
    string sceneId = "Scenes/AmbientScene.helen";
    WriteSceneAsset(sceneId, new SceneAsset {
        Id = sceneId,
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = "ambient-root",
                Name = "Ambient",
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "helengine.AmbientLightComponent",
                        ComponentIndex = 0,
                        Payload = WriteAmbientLightTaggedPayload()
                    }
                }
            }
        }
    });

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
    packager.Package(new[] { sceneId }, BuildRootPath);

    SceneAsset packagedSceneAsset;
    using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
        packagedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    }

    SceneComponentAssetRecord ambientRecord = Assert.Single(packagedSceneAsset.RootEntities[0].Components);
    using MemoryStream payloadStream = new MemoryStream(ambientRecord.Payload ?? Array.Empty<byte>(), false);
    using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);

    Assert.Equal(LightComponentScenePayloadSerializer.CurrentVersion, reader.ReadByte());
    AmbientLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadAmbientLight(reader, LightComponentScenePayloadSerializer.CurrentVersion);
    Assert.Equal(new float4(0.1f, 0.2f, 0.3f, 1f), lightComponent.Color);
    Assert.Equal(1.8f, lightComponent.Intensity);
}
```

- [ ] **Step 2: Run the packaging test to verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter EditorWindowsBuildScenePackagerTests --no-restore
```

Expected:

```text
FAIL because the packaging transform service does not yet recognize AmbientLightComponent records.
```

- [ ] **Step 3: Implement the minimal packaging rewrite support**

Extend `SceneComponentPackagingTransformService.cs`:

```csharp
const string AmbientLightComponentTypeId = "helengine.AmbientLightComponent";
```

Register the descriptor alongside the other light descriptors:

```csharp
PersistenceRegistry.Register(new AmbientLightComponentPersistenceDescriptor());
```

Include the ambient type in the supported component-type checks:

```csharp
|| string.Equals(componentTypeId, AmbientLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
```

Add the rewrite path:

```csharp
if (string.Equals(record.ComponentTypeId, AmbientLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
    transformedRecord = RewriteAmbientLightComponentRecord(record);
}
```

and implement:

```csharp
SceneComponentAssetRecord RewriteAmbientLightComponentRecord(SceneComponentAssetRecord record) {
    Component component = new AmbientLightComponentPersistenceDescriptor().DeserializeComponent(record, null, null);
    if (component is not AmbientLightComponent lightComponent) {
        throw new InvalidOperationException($"Expected ambient light descriptor to materialize '{AmbientLightComponentTypeId}'.");
    }

    using MemoryStream stream = new MemoryStream();
    using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
    writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
    LightComponentScenePayloadSerializer.WriteAmbientLight(writer, lightComponent);
    return new SceneComponentAssetRecord {
        ComponentTypeId = AmbientLightComponentTypeId,
        ComponentIndex = record.ComponentIndex,
        Payload = stream.ToArray()
    };
}
```

- [ ] **Step 4: Run the packaging regression to verify it passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter EditorWindowsBuildScenePackagerTests --no-restore
```

Expected:

```text
PASS with packaged ambient-light payloads rewritten into runtime binary form.
```

- [ ] **Step 5: Commit the packaging support**

```bash
git add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "Package ambient light scene records"
```

### Task 4: Add DirectX11 Ambient-Light Accumulation and Shader Support

**Files:**
- Modify: `engine/helengine.directx11/materials/DirectX11ForwardLightShaderData.cs`
- Modify: `engine/helengine.directx11/rendering/DirectX11ForwardLightShaderDataBuilder.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.editor/shaders/builtin/ForwardStandardShader.hlsl`
- Test: `engine/helengine.editor.tests/rendering/DirectX11ForwardLightBindingTests.cs`
- Test: `engine/helengine.editor.tests/rendering/ForwardStandardShaderTests.cs`

- [ ] **Step 1: Write the failing DirectX11 stacking and shader-contract tests**

Add to `DirectX11ForwardLightBindingTests.cs`:

```csharp
[Fact]
public void BuildForwardLightShaderData_WhenAmbientLightsStack_SumsAmbientContributionOutsideExplicitSlots() {
    DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();
    AmbientLightComponent redAmbient = new AmbientLightComponent {
        Color = new float4(1f, 0f, 0f, 1f),
        Intensity = 0.25f
    };
    AmbientLightComponent blueAmbient = new AmbientLightComponent {
        Color = new float4(0f, 0f, 1f, 1f),
        Intensity = 0.5f
    };
    redAmbient.Parent = CreateInitializedEntity();
    blueAmbient.Parent = CreateInitializedEntity();

    DirectX11ForwardLightShaderData data = builder.Build(new[] {
        new RenderFrameLightSubmission(redAmbient),
        new RenderFrameLightSubmission(blueAmbient)
    });

    Assert.Equal(new float4(0.25f, 0f, 0.5f, 0f), data.AmbientColor);
    Assert.Equal(0f, data.LightMetadata.X);
}
```

Add to `ForwardStandardShaderTests.cs`:

```csharp
[Fact]
public void ForwardStandardShader_WhenLoaded_DeclaresAmbientLightBuffer() {
    string shaderSource = File.ReadAllText(GetForwardStandardShaderPath());

    Assert.Contains("cbuffer ForwardAmbientBuffer", shaderSource);
    Assert.Contains("float4 ambientColor", shaderSource);
    Assert.Contains("color += surfaceColor * ambientColor.rgb", shaderSource);
}
```

- [ ] **Step 2: Run the focused DirectX11 and shader tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "DirectX11ForwardLightBindingTests|ForwardStandardShaderTests" --no-restore
```

Expected:

```text
FAIL because there is no dedicated ambient shader field or ambient stacking path yet.
```

- [ ] **Step 3: Implement the minimal DirectX11 ambient channel**

Extend `DirectX11ForwardLightShaderData.cs`:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DirectX11ForwardLightShaderData {
    public float4 LightMetadata { get; set; }
    public float4 AmbientColor { get; set; }
    public DirectX11ForwardLightSlotShaderData Light0 { get; set; }
    public DirectX11ForwardLightSlotShaderData Light1 { get; set; }
    public DirectX11ForwardLightSlotShaderData Light2 { get; set; }
    public DirectX11ForwardLightSlotShaderData Light3 { get; set; }
}
```

Update `DirectX11ForwardLightShaderDataBuilder.cs` so ambient lights accumulate separately:

```csharp
public DirectX11ForwardLightShaderData Build(IReadOnlyList<RenderFrameLightSubmission> selectedLights) {
    if (selectedLights == null) {
        throw new ArgumentNullException(nameof(selectedLights));
    }

    DirectX11ForwardLightShaderData data = new DirectX11ForwardLightShaderData();
    int activeLightCount = 0;
    float3 ambientSum = float3.Zero;

    for (int lightIndex = 0; lightIndex < selectedLights.Count; lightIndex++) {
        LightComponent light = selectedLights[lightIndex].Light;
        if (light is AmbientLightComponent) {
            float4 color = light.Color;
            ambientSum.X += color.X * light.Intensity;
            ambientSum.Y += color.Y * light.Intensity;
            ambientSum.Z += color.Z * light.Intensity;
            continue;
        }

        if (activeLightCount >= MaximumPackedLightCount) {
            continue;
        }

        DirectX11ForwardLightSlotShaderData slot = BuildSlot(selectedLights[lightIndex]);
        SetSlot(ref data, activeLightCount, slot);
        activeLightCount++;
    }

    data.LightMetadata = new float4(activeLightCount, 0f, 0f, 0f);
    data.AmbientColor = new float4(ambientSum.X, ambientSum.Y, ambientSum.Z, 0f);
    return data;
}
```

Update `ForwardStandardShader.hlsl`:

```hlsl
cbuffer ForwardAmbientBuffer : register(b1)
{
    float4 ambientColor;
};

cbuffer ForwardLightBuffer : register(b2)
{
    float4 lightMetadata;
    ...
};

cbuffer ShadowBuffer : register(b3)
{
    ...
};

cbuffer BaseColorBuffer : register(b4)
{
    float4 baseColor;
};
```

and in `PS`:

```hlsl
float3 color = surfaceColor * ambientColor.rgb;
```

Update `DirectX11Renderer3D.cs` buffer binding names so the ambient cbuffer is created, uploaded, and preserved alongside the existing engine-owned constant buffers.

- [ ] **Step 4: Run the focused DirectX11 and shader tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "DirectX11ForwardLightBindingTests|ForwardStandardShaderTests" --no-restore
```

Expected:

```text
PASS with ambient accumulation and shader contract verified.
```

- [ ] **Step 5: Commit the DirectX11 ambient-light channel**

```bash
git add engine/helengine.directx11/materials/DirectX11ForwardLightShaderData.cs engine/helengine.directx11/rendering/DirectX11ForwardLightShaderDataBuilder.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.editor/shaders/builtin/ForwardStandardShader.hlsl engine/helengine.editor.tests/rendering/DirectX11ForwardLightBindingTests.cs engine/helengine.editor.tests/rendering/ForwardStandardShaderTests.cs
git commit -m "Add DirectX11 ambient light accumulation"
```

### Task 5: Full Verification and Windows Build Graph Regression

**Files:**
- Test: `engine/helengine.editor.tests\managers\project\EditorPlatformBuildGraphRunnerTests.cs`
- Test: `engine/helengine.editor.tests\helengine.editor.tests.csproj`

- [ ] **Step 1: Add the failing Windows build-graph regression if one does not already cover light-family packaging generically**

If no existing Windows build graph test covers the new light family end-to-end, add one focused assertion to `EditorPlatformBuildGraphRunnerTests.cs` that packages a scene containing `AmbientLightComponent` and verifies the native Windows build completes.

```csharp
[Fact]
public void Execute_WhenBuildingSceneContainingAmbientLightForWindows_Succeeds() {
    string sceneId = WriteCommittedAmbientLightScene();

    ExecuteWindowsBuildGraph(sceneId);
}
```

- [ ] **Step 2: Run the focused runtime/export verification**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorPlatformBuildGraphRunnerTests|RuntimeSceneLoadServiceTests|EditorWindowsBuildScenePackagerTests" --no-restore
```

Expected:

```text
PASS with ambient-light runtime load, packaging, and Windows native build graph verification.
```

- [ ] **Step 3: Run the full editor test suite**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore
```

Expected:

```text
PASS with zero failures.
```

- [ ] **Step 4: Run the full solution build**

Run:

```powershell
rtk dotnet build helengine.ui\helengine.sln --no-restore
```

Expected:

```text
31 projects, 0 errors
```

- [ ] **Step 5: Commit the final verification/test sweep**

```bash
git add engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
git commit -m "Verify ambient light Windows build flow"
```

## Spec Coverage Check

- New first-class `AmbientLightComponent`: covered by Task 1.
- Scene persistence and runtime load: covered by Task 2.
- Editor creation/add-menu path: covered by Task 2.
- Windows export packaging: covered by Task 3.
- Dedicated DirectX11 ambient channel with stacking: covered by Task 4.
- Full Windows build/runtime verification: covered by Task 5.

## Placeholder Scan

- No `TODO`, `TBD`, or “similar to Task N” placeholders remain.
- Every task names exact files and concrete test names.
- Each code-changing step includes concrete code snippets rather than narrative-only instructions.

## Type Consistency Check

- `AmbientLightComponent`, `LightType.Ambient`, `AmbientLightComponentPersistenceDescriptor`, and `RuntimeAmbientLightComponentDeserializer` are named consistently across all tasks.
- The serialized type id is consistently `helengine.AmbientLightComponent`.
- DirectX11 ambient accumulation is consistently modeled as `AmbientColor` outside the explicit light-slot count.

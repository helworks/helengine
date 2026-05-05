# Editor Scene Tagged Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace positional editor scene component payloads with tolerant tagged field containers, then package them into strict runtime payloads for every built-in runtime scene component.

**Architecture:** Editor scene descriptors serialize named fields into one shared tagged container that ignores unknown fields and defaults missing fields. The cook step parses those tagged payloads and emits strict runtime payloads through shared strict payload codecs that are also used by runtime deserializers.

**Tech Stack:** C#, xUnit, custom `EngineBinaryReader` / `EngineBinaryWriter`, editor scene persistence descriptors, runtime scene loaders, Windows scene packaging.

---

### Task 1: Add shared tagged editor payload infrastructure

**Files:**
- Create: `engine/helengine.editor/serialization/scene/EditorTaggedSceneComponentFieldWriter.cs`
- Create: `engine/helengine.editor/serialization/scene/EditorTaggedSceneComponentFieldReader.cs`
- Create: `engine/helengine.editor/serialization/scene/SceneComponentBinaryFieldEncoding.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorTaggedSceneComponentFieldReaderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Read_WhenPayloadContainsUnknownField_IgnoresTheUnknownField() { }

[Fact]
public void Read_WhenFieldIsMissing_LeavesTheDestinationAtItsDefaultValue() { }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorTaggedSceneComponentFieldReaderTests`
Expected: FAIL because the tagged reader/writer types do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class EditorTaggedSceneComponentFieldWriter { }
public sealed class EditorTaggedSceneComponentFieldReader { }
public static class SceneComponentBinaryFieldEncoding { }
```

- [ ] **Step 4: Expand implementation to pass the tests**

```csharp
public void WriteField(string fieldName, Action<EngineBinaryWriter> writeFieldPayload) { ... }
public bool TryGetFieldReader(string fieldName, out EngineBinaryReader reader) { ... }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorTaggedSceneComponentFieldReaderTests`
Expected: PASS

### Task 2: Convert editor descriptors to tagged payloads

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/TextComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/FPSComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/RoundedRectComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/DirectionalLightComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/PointLightComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/SpotLightComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/DemoMenuBuildComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/DemoMenuPanelComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/DemoMenuItemComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/DemoMenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/CameraComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/TextComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/FPSComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/LightComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write failing tests for tolerant behavior**

```csharp
[Fact]
public void Deserialize_WhenCameraPayloadOmitsRenderSettings_KeepsDefaultRenderSettings() { }

[Fact]
public void Deserialize_WhenLightPayloadContainsUnknownField_IgnoresTheField() { }
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter CameraComponentPersistenceDescriptorTests|LightComponentPersistenceDescriptorTests`
Expected: FAIL because the current descriptors still expect positional payloads.

- [ ] **Step 3: Migrate each descriptor**

```csharp
EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
writer.WriteField("Text", fieldWriter => fieldWriter.WriteString(textComponent.Text));
```

- [ ] **Step 4: Run the descriptor and scene-save tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter CameraComponentPersistenceDescriptorTests|MeshComponentPersistenceDescriptorTests|TextComponentPersistenceDescriptorTests|FPSComponentPersistenceDescriptorTests|LightComponentPersistenceDescriptorTests|SceneSaveServiceTests`
Expected: PASS

### Task 3: Add shared strict runtime payload codecs

**Files:**
- Create: `engine/helengine.core/scene/runtime/CameraSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/MeshSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/TextSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/FpsSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/RoundedRectSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/DemoMenuBuildSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/DemoMenuPanelSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/DemoMenuItemSceneComponentPayloadCodec.cs`
- Create: `engine/helengine.core/scene/runtime/DemoMenuSelectedDescriptionSceneComponentPayloadCodec.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeTextComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeRoundedRectComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeDemoMenuBuildComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeDemoMenuPanelComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeDemoMenuItemComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeDemoMenuSelectedDescriptionComponentDeserializer.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write one failing runtime-load regression per codec family**

```csharp
[Fact]
public void Load_WhenSceneContainsCameraComponent_MaterializesTheComponent() { }
```

- [ ] **Step 2: Run the runtime scene-load tests to verify the failure**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter RuntimeSceneLoadServiceTests`
Expected: FAIL while the new codec classes are missing.

- [ ] **Step 3: Add the strict codec classes and wire the runtime deserializers to them**

```csharp
public static CameraComponent Read(EngineBinaryReader reader) { ... }
public static void Write(EngineBinaryWriter writer, CameraComponent component) { ... }
```

- [ ] **Step 4: Re-run the runtime scene-load tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter RuntimeSceneLoadServiceTests`
Expected: PASS

### Task 4: Make packaging the canonical bridge

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write failing packager coverage for tagged editor payloads across supported component families**

```csharp
[Fact]
public void Package_WhenSceneContainsDirectionalLight_ProducesStrictRuntimePayload() { }
```

- [ ] **Step 2: Run the packager tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildScenePackagerTests`
Expected: FAIL because only a subset of components are transformed today.

- [ ] **Step 3: Rewrite the transform service to read tagged editor fields and emit strict runtime payloads**

```csharp
if (string.Equals(record.ComponentTypeId, DirectionalLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
    transformedRecord = RewriteDirectionalLightComponentRecord(record);
    return true;
}
```

- [ ] **Step 4: Re-run the packager tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildScenePackagerTests`
Expected: PASS

### Task 5: Verify end-to-end editor save/load and packaging

**Files:**
- Modify as needed from earlier tasks
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Run the focused integration suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter SceneSaveServiceTests|SceneFileLoadServiceTests|DemoDiscSceneWriterTests|RuntimeSceneLoadServiceTests|EditorPlatformBuildScenePackagerTests`
Expected: PASS or a narrowed list of unrelated failures.

- [ ] **Step 2: Fix any integration fallout**

```csharp
// Adjust component payload writers/readers or packager transforms only where the tests point.
```

- [ ] **Step 3: Re-run the same integration suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter SceneSaveServiceTests|SceneFileLoadServiceTests|DemoDiscSceneWriterTests|RuntimeSceneLoadServiceTests|EditorPlatformBuildScenePackagerTests`
Expected: PASS

### Task 6: Final verification and commit

**Files:**
- Modify: `docs/superpowers/specs/2026-05-04-editor-scene-tagged-persistence-design.md`
- Modify: `docs/superpowers/plans/2026-05-04-editor-scene-tagged-persistence.md`

- [ ] **Step 1: Run the final focused command**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter CameraComponentPersistenceDescriptorTests|MeshComponentPersistenceDescriptorTests|TextComponentPersistenceDescriptorTests|FPSComponentPersistenceDescriptorTests|LightComponentPersistenceDescriptorTests|SceneSaveServiceTests|SceneFileLoadServiceTests|RuntimeSceneLoadServiceTests|EditorPlatformBuildScenePackagerTests`
Expected: PASS

- [ ] **Step 2: Inspect the diff**

Run: `rtk git status --short`
Expected: Only the intended persistence, packaging, runtime, test, and docs files are modified.

- [ ] **Step 3: Commit**

```bash
rtk git add docs/superpowers/specs/2026-05-04-editor-scene-tagged-persistence-design.md docs/superpowers/plans/2026-05-04-editor-scene-tagged-persistence.md engine/helengine.editor/serialization/scene engine/helengine.core/scene/runtime engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests
rtk git commit -m "feat: add tolerant editor scene component payloads"
```

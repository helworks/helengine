# Scene Memory Probe Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic, scene-authorable `SceneMemoryProbeComponent` to `helengine.core` that can drive fixed scene-transition memory probes and log stable lightweight runtime counters without a custom serializer.

**Architecture:** Extend the automatic reflected component persistence/runtime path to support the probe’s authored shape first, because the current codec does not support `double`, enums, or arrays of simple classes. Then add the probe component and its small authored step model in `helengine.core`, followed by focused runtime tests and one authored city probe scene for end-to-end validation.

**Tech Stack:** C#/.NET 9, HelEngine core runtime, automatic reflected scene persistence, generated player deserializers, xUnit, Windows export pipeline.

---

## File Structure

### Core runtime files

- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeActionKind.cs`
- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeStep.cs`
- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeComponent.cs`
- Create: `engine/helengine.core/diagnostics/SceneMemoryProbeMeasurement.cs`
- Create: `engine/helengine.core/diagnostics/SceneMemoryProbeLogFormatter.cs`

### Automatic persistence / runtime generation files

- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Modify: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`

### Test files

- Create: `engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableComponent.cs`
- Create: `engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableStep.cs`
- Create: `engine/helengine.editor.tests/SceneMemoryProbeComponentTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`

### City integration files

- Create: `C:\dev\helprojs\city\assets\codebase\diagnostics.tools\SceneMemoryProbeSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`

---

### Task 1: Extend Automatic Reflected Component Persistence For Probe-Shaped Data

**Files:**
- Create: `engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableComponent.cs`
- Create: `engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableStep.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Modify: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`

- [ ] **Step 1: Write the failing reflected-persistence test for arrays of simple authored classes**

```csharp
[Fact]
public void SerializeAndDeserialize_WhenScriptComponentContainsStepArray_RoundTripsDoubleEnumAndNestedObjects() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    TestSceneMemoryProbeSerializableComponent component = new TestSceneMemoryProbeSerializableComponent {
        ProbeName = "menu-soak",
        Loop = true,
        Steps = new[] {
            new TestSceneMemoryProbeSerializableStep {
                ActionKind = TestSceneMemoryProbeSerializableActionKind.Wait,
                SceneId = "Scenes/DemoDiscMainMenu.helen",
                DurationSeconds = 5.0d,
                Label = "idle-menu"
            },
            new TestSceneMemoryProbeSerializableStep {
                ActionKind = TestSceneMemoryProbeSerializableActionKind.LoadSceneSingle,
                SceneId = "Scenes/AxisTest.helen",
                DurationSeconds = 0d,
                Label = "load-axis"
            }
        }
    };

    SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
    TestSceneMemoryProbeSerializableComponent restored = Assert.IsType<TestSceneMemoryProbeSerializableComponent>(
        descriptor.DeserializeComponent(record, null, null));

    Assert.Equal("menu-soak", restored.ProbeName);
    Assert.True(restored.Loop);
    Assert.Equal(2, restored.Steps.Length);
    Assert.Equal(TestSceneMemoryProbeSerializableActionKind.Wait, restored.Steps[0].ActionKind);
    Assert.Equal(5.0d, restored.Steps[0].DurationSeconds);
    Assert.Equal("load-axis", restored.Steps[1].Label);
}
```

- [ ] **Step 2: Write the failing generated-player-deserializer test for the same schema**

```csharp
[Fact]
public void GenerateNativeDeserializerSource_WhenSchemaContainsStepArray_EmitsNestedArrayReaderPath() {
    ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TestSceneMemoryProbeSerializableComponent));
    ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

    Assert.True(generator.CanGenerateNativeDeserializer(schema));

    string source = generator.GenerateNativeDeserializerSource(schema);

    Assert.Contains("ReadDouble()", source, StringComparison.Ordinal);
    Assert.Contains("for (int32_t", source, StringComparison.Ordinal);
    Assert.Contains("new ::TestSceneMemoryProbeSerializableStep()", source, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Run the red tests and verify they fail for unsupported member types**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SerializeAndDeserialize_WhenScriptComponentContainsStepArray_RoundTripsDoubleEnumAndNestedObjects|FullyQualifiedName~GenerateNativeDeserializerSource_WhenSchemaContainsStepArray_EmitsNestedArrayReaderPath" --nologo -v q
```

Expected:

```text
FAIL
Automatic script-component persistence does not support member type 'System.Double'
```

or

```text
FAIL
Native scripted component deserializer generation does not support member type '...[]'
```

- [ ] **Step 4: Implement minimal codec support for `double`, enums, arrays, and simple nested authored classes**

Add support in `AutomaticScriptComponentPersistenceDescriptor` and `AutomaticScriptComponentRuntimeDeserializer` along this shape:

```csharp
if (valueType == typeof(double)) {
    writer.WriteDouble((double)value);
    return;
}
if (valueType.IsEnum) {
    writer.WriteInt32((int)value);
    return;
}
if (valueType.IsArray) {
    WriteArrayValue(writer, valueType.GetElementType(), (Array)value);
    return;
}
if (IsSupportedNestedObjectType(valueType)) {
    WriteNestedObjectValue(writer, valueType, value);
    return;
}
```

and:

```csharp
if (valueType == typeof(double)) {
    return reader.ReadDouble();
}
if (valueType.IsEnum) {
    return Enum.ToObject(valueType, reader.ReadInt32());
}
if (valueType.IsArray) {
    return ReadArrayValue(reader, valueType.GetElementType());
}
if (IsSupportedNestedObjectType(valueType)) {
    return ReadNestedObjectValue(reader, valueType);
}
```

Extend `ScriptComponentPlayerDeserializerGenerator` with the matching runtime/native generation helpers instead of hard-coding only primitive cases:

```csharp
if (valueType == typeof(double)) {
    return "reader.ReadDouble()";
}
if (valueType.IsEnum) {
    return $"({valueType.FullName})reader.ReadInt32()";
}
if (valueType.IsArray) {
    return BuildArrayReadExpression(valueType.GetElementType());
}
if (IsSupportedNestedObjectType(valueType)) {
    return BuildNestedObjectReadExpression(valueType);
}
```

- [ ] **Step 5: Run the focused codec tests and verify they pass**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests" --nologo -v q
```

Expected:

```text
Passed
```

- [ ] **Step 6: Commit the codec/runtime-generation support**

```bash
git add engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableComponent.cs engine/helengine.editor.tests/testing/TestSceneMemoryProbeSerializableStep.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs
git commit -m "feat: support reflected probe-step payloads"
```

### Task 2: Add The Generic Scene Memory Probe Runtime Types

**Files:**
- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeActionKind.cs`
- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeStep.cs`
- Create: `engine/helengine.core/diagnostics/SceneMemoryProbeMeasurement.cs`
- Create: `engine/helengine.core/diagnostics/SceneMemoryProbeLogFormatter.cs`
- Create: `engine/helengine.core/components/diagnostics/SceneMemoryProbeComponent.cs`
- Create: `engine/helengine.editor.tests/SceneMemoryProbeComponentTests.cs`

- [ ] **Step 1: Write the failing runtime tests for step progression, scene actions, looping, and logging**

Add tests like:

```csharp
[Fact]
public void Update_WhenCurrentStepIsWait_DoesNotAdvanceUntilDurationElapses() { }

[Fact]
public void Update_WhenCurrentStepLoadsSingleScene_LoadsRequestedSceneOnce() { }

[Fact]
public void Update_WhenCurrentStepLoadsAdditiveScene_LoadsRequestedSceneAdditively() { }

[Fact]
public void Update_WhenCurrentStepUnloadsScene_UnloadsRequestedSceneOnce() { }

[Fact]
public void Update_WhenFinalStepCompletesAndLoopIsEnabled_RestartsFromFirstStep() { }

[Fact]
public void Update_WhenProbeRuns_LogsStableSceneMemoryProbeLines() { }
```

Use a deterministic `TestClockDrivenCore`, real `SceneManager`, and captured `Logger.MessageLogged` entries rather than mocking the entire runtime.

- [ ] **Step 2: Run the probe tests and verify they fail because the component type does not exist yet**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMemoryProbeComponentTests" --nologo -v q
```

Expected:

```text
FAIL
The type or namespace name 'SceneMemoryProbeComponent' could not be found
```

- [ ] **Step 3: Add the authored step and action model**

Create the action enum:

```csharp
namespace helengine {
    /// <summary>
    /// Identifies one authored scene-memory probe action.
    /// </summary>
    public enum SceneMemoryProbeActionKind {
        Wait = 0,
        LoadSceneSingle = 1,
        LoadSceneAdditive = 2,
        UnloadScene = 3
    }
}
```

Create the step model:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one authored scene-memory probe step.
    /// </summary>
    public sealed class SceneMemoryProbeStep {
        public SceneMemoryProbeActionKind ActionKind { get; set; }
        public string SceneId { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4: Add the minimal measurement and formatting helpers**

Measurement container:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one emitted scene-memory probe checkpoint.
    /// </summary>
    public sealed class SceneMemoryProbeMeasurement {
        public string ProbeName { get; set; } = string.Empty;
        public int CycleIndex { get; set; }
        public int StepIndex { get; set; }
        public string Label { get; set; } = string.Empty;
        public SceneMemoryProbeActionKind ActionKind { get; set; }
        public ulong ResidentBytes { get; set; }
        public ulong CommittedBytes { get; set; }
        public string LoadedSceneIds { get; set; } = string.Empty;
        public int Drawables2DCount { get; set; }
        public int Drawables3DCount { get; set; }
        public int DrawCallCount { get; set; }
        public int ActiveOwnedTextureCount { get; set; }
        public int ActiveOwnedFontCount { get; set; }
        public int ActiveOwnedModelCount { get; set; }
        public int ActiveOwnedMaterialCount { get; set; }
    }
```

Formatter:

```csharp
public static string Format(SceneMemoryProbeMeasurement measurement) {
    return $"[SceneMemoryProbe] probe={measurement.ProbeName} cycle={measurement.CycleIndex} step={measurement.StepIndex} label={measurement.Label} action={measurement.ActionKind} resident_bytes={measurement.ResidentBytes} committed_bytes={measurement.CommittedBytes} scenes={measurement.LoadedSceneIds} drawables2d={measurement.Drawables2DCount} drawables3d={measurement.Drawables3DCount} draw_calls={measurement.DrawCallCount} owned_textures={measurement.ActiveOwnedTextureCount} owned_fonts={measurement.ActiveOwnedFontCount} owned_models={measurement.ActiveOwnedModelCount} owned_materials={measurement.ActiveOwnedMaterialCount}";
}
```

- [ ] **Step 5: Implement the probe component with fixed-order execution**

Implement the component around this shape:

```csharp
public sealed class SceneMemoryProbeComponent : UpdateComponent {
    double CurrentStepElapsedSecondsValue;
    int CurrentStepIndexValue;
    int CurrentCycleIndexValue;
    bool StartedValue;

    public SceneMemoryProbeStep[] Steps { get; set; } = Array.Empty<SceneMemoryProbeStep>();
    public bool Loop { get; set; }
    public bool StartAutomatically { get; set; } = true;
    public double InitialDelaySeconds { get; set; }
    public string ProbeName { get; set; } = string.Empty;

    public override void Update() {
        base.Update();
        if (!StartedValue) {
            StartProbeIfNeeded();
            return;
        }

        AdvanceCurrentStep();
    }
}
```

Key implementation points:

- validate `Steps` on start
- sample `RuntimeDiagnosticsService.CaptureMemoryCounters(...)`
- build one `SceneMemoryProbeMeasurement`
- log through `Logger.WriteLine(SceneMemoryProbeLogFormatter.Format(measurement))`
- execute scene actions only once per step
- advance on the next update after a non-wait action
- let `SceneManager` exceptions surface

- [ ] **Step 6: Run the focused probe tests and verify they pass**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMemoryProbeComponentTests" --nologo -v q
```

Expected:

```text
Passed
```

- [ ] **Step 7: Commit the new probe runtime types**

```bash
git add engine/helengine.core/components/diagnostics/SceneMemoryProbeActionKind.cs engine/helengine.core/components/diagnostics/SceneMemoryProbeStep.cs engine/helengine.core/components/diagnostics/SceneMemoryProbeComponent.cs engine/helengine.core/diagnostics/SceneMemoryProbeMeasurement.cs engine/helengine.core/diagnostics/SceneMemoryProbeLogFormatter.cs engine/helengine.editor.tests/SceneMemoryProbeComponentTests.cs
git commit -m "feat: add scene memory probe component"
```

### Task 3: Validate Dynamic Authoring And Player Build Generation

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing authoring/build tests for the real probe component**

Add one automatic persistence test for the actual runtime type:

```csharp
[Fact]
public void SerializeAndDeserialize_WhenSceneMemoryProbeComponentUsesStepArray_RoundTripsThroughAutomaticPersistence() { }
```

Add one Windows packager/regeneration test that proves the generated payload and generated deserializer include the probe model:

```csharp
[Fact]
public void PackageBuild_WhenSceneContainsSceneMemoryProbeComponent_RewritesRuntimePayloadForStepArray() { }

[Fact]
public void Regenerate_WhenCoreContainsSceneMemoryProbeComponent_EmitsGeneratedRuntimeDeserializerSupport() { }
```

- [ ] **Step 2: Run the focused authoring/build tests and verify they fail on missing probe-specific support**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMemoryProbeComponent|FullyQualifiedName~PackageBuild_WhenSceneContainsSceneMemoryProbeComponent_RewritesRuntimePayloadForStepArray|FullyQualifiedName~Regenerate_WhenCoreContainsSceneMemoryProbeComponent_EmitsGeneratedRuntimeDeserializerSupport" --nologo -v q
```

Expected:

```text
FAIL
```

with a mismatch in generated payload or missing generated runtime deserializer content.

- [ ] **Step 3: Wire the minimum build-generation support needed for the real component type**

Use the same automatic component path rather than adding a bespoke descriptor. The relevant verification snippets should exist after implementation:

```csharp
string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMemoryProbeComponent));
Assert.Contains("SceneMemoryProbeStep", generatedSource, StringComparison.Ordinal);
Assert.Contains("ReadDouble()", generatedSource, StringComparison.Ordinal);
Assert.Contains("LoadSceneSingle", generatedSource, StringComparison.Ordinal);
```

Do not add a `SceneMemoryProbeComponentPersistenceDescriptor` or `RuntimeSceneMemoryProbeComponentDeserializer` unless a concrete test demonstrates the automatic path still cannot carry the probe type after Task 1.

- [ ] **Step 4: Run the focused authoring/build tests and verify they pass**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" --nologo -v q
```

Expected:

```text
Passed
```

- [ ] **Step 5: Commit the authoring/build validation work**

```bash
git add engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git commit -m "test: validate scene memory probe authoring and generation"
```

### Task 4: Author A City Probe Scene And Verify End-To-End Windows Export Behavior

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\diagnostics.tools\SceneMemoryProbeSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`

- [ ] **Step 1: Write the failing city authoring test for the generated probe scene**

Add a city authoring test in the existing city scene-authoring suite:

```csharp
[Fact]
public void DeserializeCitySceneMemoryProbeSceneAsset_RootContainsSceneMemoryProbeComponentWithSteps() { }
```

Assert:

- the generated scene exists
- one root entity contains `helengine.SceneMemoryProbeComponent, helengine.core`
- the payload restores at least one `Wait` step and one scene-load step

- [ ] **Step 2: Run the city authoring test and verify it fails because the probe scene is not authored yet**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~DeserializeCitySceneMemoryProbeSceneAsset_RootContainsSceneMemoryProbeComponentWithSteps" --nologo -v q
```

Expected:

```text
FAIL
```

- [ ] **Step 3: Add one minimal generated city probe scene**

Author a simple scene factory with a root entity that adds the component:

```csharp
entity.AddComponent(new SceneMemoryProbeComponent {
    ProbeName = "menu-memory-probe",
    StartAutomatically = true,
    InitialDelaySeconds = 2.0d,
    Loop = true,
    Steps = new[] {
        new SceneMemoryProbeStep {
            ActionKind = SceneMemoryProbeActionKind.Wait,
            SceneId = string.Empty,
            DurationSeconds = 5.0d,
            Label = "idle-menu"
        },
        new SceneMemoryProbeStep {
            ActionKind = SceneMemoryProbeActionKind.LoadSceneSingle,
            SceneId = "Scenes/AxisTest.helen",
            DurationSeconds = 0d,
            Label = "load-axis"
        },
        new SceneMemoryProbeStep {
            ActionKind = SceneMemoryProbeActionKind.Wait,
            SceneId = string.Empty,
            DurationSeconds = 5.0d,
            Label = "idle-axis"
        },
        new SceneMemoryProbeStep {
            ActionKind = SceneMemoryProbeActionKind.LoadSceneSingle,
            SceneId = "Scenes/DemoDiscMainMenu.helen",
            DurationSeconds = 0d,
            Label = "return-menu"
        }
    }
});
```

Then register that generated scene in `GeneratedAuthoringSceneWriteService`.

- [ ] **Step 4: Re-run the city authoring test and verify it passes**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~DeserializeCitySceneMemoryProbeSceneAsset_RootContainsSceneMemoryProbeComponentWithSteps" --nologo -v q
```

Expected:

```text
Passed
```

- [ ] **Step 5: Build the Windows export and verify the probe logs appear**

Run:

```powershell
dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -c Debug --no-build -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
Start-Process -FilePath C:\dev\helprojs\output\windows\helengine_windows.exe -WorkingDirectory C:\dev\helprojs\output\windows -PassThru
```

Expected:

```text
Build completed for platform 'windows'
```

and runtime log output containing lines that start with:

```text
[SceneMemoryProbe]
```

- [ ] **Step 6: Commit the city integration scene**

```bash
git add C:\dev\helprojs\city\assets\codebase\diagnostics.tools\SceneMemoryProbeSceneFactory.cs C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs
git commit -m "feat: add city scene memory probe scene"
```

## Self-Review

### Spec coverage

- The automatic dynamic authoring requirement is covered by Task 1 and Task 3.
- The generic runtime component and step execution model are covered by Task 2.
- The lightweight measurement requirement is covered by Task 2.
- The city integration scene promised by the spec is covered by Task 4.

### Placeholder scan

- No `TODO` or `TBD` placeholders remain.
- Each code-changing task includes concrete file paths, tests, commands, and code snippets.

### Type consistency

- The plan uses `SceneMemoryProbeComponent`, `SceneMemoryProbeStep`, and `SceneMemoryProbeActionKind` consistently.
- The step schema consistently uses `SceneId`, `DurationSeconds`, and `Label`.
- The logging shape consistently uses `SceneMemoryProbeMeasurement` and `SceneMemoryProbeLogFormatter`.

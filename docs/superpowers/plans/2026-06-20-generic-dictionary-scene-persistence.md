# Generic Dictionary Scene Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe generic `Dictionary<TKey, TValue>` persistence support across editor save, managed runtime load, and native generated runtime deserializers, then remove the bespoke `SceneMapComponent` persistence stack.

**Architecture:** Extend the existing generic reflected persistence/value systems instead of adding a `SceneMapComponent`-only path. Keep one managed binary format for dictionaries, mirror it in native generated deserializers, and make `SceneMapComponent` flow through the same generic systems as other components once dictionary support exists.

**Tech Stack:** C#, xUnit, shared reflected scene persistence, generated native C++ deserializer emission, `rtk dotnet test`

---

## File Structure

### Existing files to modify

- `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
  Shared editor reflected save/load fallback. This will gain dictionary detection, validation, ordering, write, and read support.
- `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
  Shared managed runtime reflected deserializer. This will gain dictionary decoding that exactly matches the editor reflected format.
- `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
  Native generated deserializer emitter. This will gain dictionary decoding emission for supported key/value shapes.
- `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
  Generated runtime deserializer orchestration. This will stop treating `SceneMapComponent` as bespoke once the generic path is in place.
- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  Explicit built-in runtime registration. This will stop registering `RuntimeSceneMapComponentDeserializer`.
- `engine/helengine.editor/EditorSession.cs`
  Explicit editor persistence registration. This will stop registering `SceneMapComponentPersistenceDescriptor`.

### Existing files likely to delete

- `engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs`
- `engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs`

### Existing test files to modify

- `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
  Add reflected dictionary persistence tests and invalid-key/duplicate-key coverage.
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Add managed runtime dictionary-load tests and `SceneMapComponent` generic round-trip/load coverage.
- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  Add native generated dictionary code emission assertions and update `SceneMapComponent` explicit-deserializer expectations.
- `engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs`
  Replace bespoke-descriptor expectations with source-audit/removal assertions or delete the file once coverage is moved.

### Optional new shared helper file

- `engine/helengine.core/scene/runtime/ScenePersistenceDictionaryTypeSupport.cs`
  If the type validation logic starts duplicating between editor persistence, managed runtime loading, and codegen filtering, split the dictionary shape/key support rules into one focused shared helper. Only create this file if it removes duplication cleanly.

## Task 1: Add failing editor reflected dictionary persistence tests

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`

- [ ] **Step 1: Write the failing test for `Dictionary<string, string>` round-trip**

Add a new test named:

```csharp
[Fact]
public void SerializeAndDeserialize_WhenScriptComponentContainsStringDictionary_RoundTripsDictionaryEntriesInDeterministicKeyOrder() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    TestDictionaryScriptComponent component = new TestDictionaryScriptComponent();
    component.Labels.Add("OptionsMenu", "OptionsMenuScene");
    component.Labels.Add("MainMenu", "MainMenuScene");

    SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
    TestDictionaryScriptComponent loaded = Assert.IsType<TestDictionaryScriptComponent>(descriptor.DeserializeComponent(record, null, null));

    Assert.Equal("MainMenuScene", loaded.Labels["MainMenu"]);
    Assert.Equal("OptionsMenuScene", loaded.Labels["OptionsMenu"]);
}
```

- [ ] **Step 2: Write the failing test for supported enum/integer key dictionaries**

Add a new test named:

```csharp
[Fact]
public void SerializeAndDeserialize_WhenScriptComponentContainsEnumAndIntegerKeyDictionaries_RoundTripsSupportedKeys() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    TestDictionaryKeyScriptComponent component = new TestDictionaryKeyScriptComponent();
    component.IntegerLabels.Add(7, "Seven");
    component.ModeLabels.Add(TestDictionaryMode.Secondary, "SecondaryScene");

    SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
    TestDictionaryKeyScriptComponent loaded = Assert.IsType<TestDictionaryKeyScriptComponent>(descriptor.DeserializeComponent(record, null, null));

    Assert.Equal("Seven", loaded.IntegerLabels[7]);
    Assert.Equal("SecondaryScene", loaded.ModeLabels[TestDictionaryMode.Secondary]);
}
```

- [ ] **Step 3: Write the failing test for unsupported dictionary key types**

Add a new test named:

```csharp
[Fact]
public void SerializeComponent_WhenScriptComponentContainsUnsupportedDictionaryKey_ThrowsInvalidOperationException() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    TestUnsupportedDictionaryKeyScriptComponent component = new TestUnsupportedDictionaryKeyScriptComponent();
    component.InvalidKeys.Add(new float2(1f, 2f), "Bad");

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(component, 0, new EntityComponentSaveState()));

    Assert.Contains("Dictionary", exception.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Write the failing test for duplicate dictionary keys in payload**

Add a new test named:

```csharp
[Fact]
public void DeserializeComponent_WhenDictionaryPayloadContainsDuplicateKeys_ThrowsInvalidOperationException() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    SceneComponentAssetRecord record = BuildDuplicateDictionaryRecord();

    Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, null));
}
```

- [ ] **Step 5: Run the focused editor persistence tests to verify failure**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal
```

Expected: FAIL in the new dictionary tests because dictionary member types are not yet supported by the automatic descriptor.

- [ ] **Step 6: Commit the red tests**

```bash
git add engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs
git commit -m "test: cover generic dictionary editor persistence"
```

## Task 2: Implement editor reflected dictionary persistence support

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Create if needed: `engine/helengine.core/scene/runtime/ScenePersistenceDictionaryTypeSupport.cs`

- [ ] **Step 1: Add dictionary shape detection and supported-key validation**

Implement focused helpers that:

- detect `Dictionary<TKey, TValue>`
- extract key and value types
- accept only `string`, `byte`, `ushort`, `int`, `uint`, `long`, and enums over those integral types
- reject all other key types with one clear `InvalidOperationException`

If the same validation logic starts to duplicate in multiple files, move it immediately into a dedicated shared helper file instead of copying it.

- [ ] **Step 2: Add dictionary write support**

Implement dictionary writing inside `WriteSupportedValue(...)`:

- sort entries deterministically by key
- write entry count
- write each key via the existing supported-value path
- write each value via the existing supported-value path

Use ordinal string ordering for `string` keys and numeric ordering for integral/enum keys.

- [ ] **Step 3: Add dictionary read support**

Implement dictionary reading inside `ReadSupportedValue(...)`:

- read entry count
- reject negative counts
- create the destination dictionary instance
- read each key and value
- reject duplicate keys instead of silently overwriting

- [ ] **Step 4: Add any small test-only helper component types needed by the new tests**

Keep these helper types inside `AutomaticScriptComponentPersistenceDescriptorTests.cs` unless an existing test fixture pattern already centralizes them there.

- [ ] **Step 5: Run the focused editor persistence tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal
```

Expected: PASS, including the new dictionary tests and the existing generic persistence coverage.

- [ ] **Step 6: Commit the editor persistence implementation**

```bash
git add engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.core/scene/runtime/ScenePersistenceDictionaryTypeSupport.cs
git commit -m "feat: add generic dictionary editor persistence"
```

If no shared helper file was needed, omit it from `git add`.

## Task 3: Add failing managed runtime dictionary load tests and implement the runtime path

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Modify if created in Task 2: `engine/helengine.core/scene/runtime/ScenePersistenceDictionaryTypeSupport.cs`

- [ ] **Step 1: Write the failing managed runtime dictionary-load test**

Add a new test named:

```csharp
[Fact]
public void Load_WhenAutomaticComponentContainsStringDictionary_RestoresDictionaryEntries() {
    SceneAsset sceneAsset = BuildRuntimeSceneAssetWithAutomaticDictionaryComponent();
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(Core.Instance.SceneAssetReferenceResolver, RuntimeComponentRegistry.CreateDefault());

    IReadOnlyList<Entity> roots = loadService.Load(sceneAsset);
    TestRuntimeDictionaryComponent component = roots.SelectMany(root => root.Components).OfType<TestRuntimeDictionaryComponent>().Single();

    Assert.Equal("MainMenuScene", component.Labels["MainMenu"]);
    Assert.Equal("OptionsMenuScene", component.Labels["OptionsMenu"]);
}
```

- [ ] **Step 2: Write the failing duplicate-key runtime test**

Add a new test named:

```csharp
[Fact]
public void Load_WhenAutomaticComponentDictionaryPayloadContainsDuplicateKeys_ThrowsInvalidOperationException() {
    SceneAsset sceneAsset = BuildRuntimeSceneAssetWithDuplicateDictionaryKeys();
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(Core.Instance.SceneAssetReferenceResolver, RuntimeComponentRegistry.CreateDefault());

    Assert.Throws<InvalidOperationException>(() => loadService.Load(sceneAsset));
}
```

- [ ] **Step 3: Run the focused managed runtime tests to verify failure**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected: FAIL in the new dictionary tests because the managed reflected runtime deserializer does not yet understand dictionary values.

- [ ] **Step 4: Implement dictionary support in the managed reflected runtime deserializer**

Update `AutomaticScriptComponentRuntimeDeserializer` so its supported-value path mirrors the editor reflected dictionary format exactly:

- same key-type validation
- same entry-count format
- same deterministic semantics
- same duplicate-key rejection

Do not introduce a second binary shape for runtime.

- [ ] **Step 5: Run the focused managed runtime tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected: PASS, including the new dictionary runtime tests.

- [ ] **Step 6: Commit the managed runtime implementation**

```bash
git add engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.core/scene/runtime/ScenePersistenceDictionaryTypeSupport.cs
git commit -m "feat: add generic dictionary runtime deserialization"
```

If no shared helper file exists, omit it from `git add`.

## Task 4: Add failing native generated dictionary codegen tests and implement code emission

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Modify if needed: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`

- [ ] **Step 1: Write the failing native codegen emission test**

Add a new test named:

```csharp
[Fact]
public void Emit_generated_automatic_runtime_component_deserializers_whenComponentContainsSupportedDictionary_emitsDictionaryDecodeLogic() {
    string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers-dictionaries");
    Directory.CreateDirectory(generatedCoreRootPath);

    EditorGeneratedCoreRegenerationService.EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath, [typeof(TestGeneratedDictionaryComponent)]);

    string source = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeTestGeneratedDictionaryComponentDeserializer.cpp"));
    Assert.Contains("Dictionary", source, StringComparison.Ordinal);
    Assert.Contains("ReadInt32()", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Write the failing explicit-overlap test for `SceneMapComponent`**

Add or update a test so it asserts that once generic dictionary support is complete, generated automatic runtime deserializers should include `SceneMapComponent` instead of treating it as bespoke.

- [ ] **Step 3: Run the focused codegen tests to verify failure**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal
```

Expected: FAIL because the generated native deserializer emitter does not yet know how to emit dictionary decoding logic.

- [ ] **Step 4: Implement native generated dictionary decoding emission**

Update `ScriptComponentPlayerDeserializerGenerator` so supported reflected dictionary members emit native code that:

- constructs the target dictionary
- reads entry count
- loops entries
- decodes keys with the supported key subset
- decodes values through the existing generated supported-value logic
- rejects duplicate keys

Keep the emitted binary contract aligned with the managed path byte-for-byte.

- [ ] **Step 5: Update generation orchestration only if the generator change requires it**

If `EditorGeneratedCoreRegenerationService` needs small schema filtering or registration adjustments for dictionary-backed components, make those changes here and keep them minimal.

- [ ] **Step 6: Run the focused codegen tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal
```

Expected: PASS with the new dictionary codegen assertions and the existing generated-runtime coverage.

- [ ] **Step 7: Commit the native codegen implementation**

```bash
git add engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git commit -m "feat: emit generic dictionary native deserializers"
```

If `EditorGeneratedCoreRegenerationService.cs` did not change, omit it from `git add`.

## Task 5: Remove bespoke `SceneMapComponent` persistence and runtime deserializer plumbing

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Delete: `engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs`
- Modify or delete: `engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing migration tests**

Add coverage that proves:

- `SceneMapComponent` round-trips through `AutomaticScriptComponentPersistenceDescriptor`
- runtime scene loading restores `SceneMapComponent.Mappings` through the generic path
- no explicit scene-map descriptor/deserializer registration is required anymore

If a source-audit test is cleaner than one more runtime behavior test, add it explicitly.

- [ ] **Step 2: Run the focused `SceneMapComponent` tests to verify failure**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected: FAIL once the new generic expectations are added and before the bespoke stack is removed.

- [ ] **Step 3: Remove explicit editor registration**

Delete `persistenceRegistry.Register(new SceneMapComponentPersistenceDescriptor());` from `EditorSession.CreateComponentPersistenceRegistry`.

- [ ] **Step 4: Remove explicit runtime registration**

Delete `registry.Register(new RuntimeSceneMapComponentDeserializer());` from the built-in runtime deserializer registration path.

- [ ] **Step 5: Delete the bespoke scene-map persistence classes**

Delete:

- `engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs`
- `engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs`

Then move any still-valuable test assertions into generic persistence/runtime test files before removing the dedicated bespoke test file if it no longer has a reason to exist.

- [ ] **Step 6: Run the focused `SceneMapComponent` tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneMap|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal
```

Expected: PASS with `SceneMapComponent` now covered by the shared generic systems.

- [ ] **Step 7: Commit the `SceneMapComponent` migration**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs
git rm engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs
git commit -m "refactor: move scene map persistence to generic dictionaries"
```

If the dedicated `SceneMapComponentPersistenceDescriptorTests.cs` file becomes obsolete, remove it in the same commit with `git rm`.

## Task 6: Run the final focused verification slice

**Files:**
- Test only: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Run the editor persistence regression slice**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal
```

Expected: PASS

- [ ] **Step 2: Run the managed runtime scene-load regression slice**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected: PASS

- [ ] **Step 3: Run the generated native deserializer regression slice**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal
```

Expected: PASS

- [ ] **Step 4: Run the full scene-serialization regression slice**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~serialization.scene" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the final verification checkpoint**

```bash
git add -A
git commit -m "test: verify generic dictionary scene persistence"
```

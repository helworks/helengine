# helengine.files Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split write-side serialization and packaging code out of `helengine.core` into a new `helengine.files` project while keeping runtime readers and packaged-scene deserialization in `helengine.core`.

**Architecture:** `helengine.core` remains the runtime load surface for player builds and keeps only read-side deserializers plus runtime models. `helengine.files` owns all write-side serializers, binary writer primitives, and export helpers. `helengine.editor` keeps orchestration and switches to the new writer namespace when it emits packages or saves files. To avoid type collisions during the migration, the new project uses the `helengine.files` namespace for writer classes instead of reusing the core namespace. Any file that needs both read and write variants of the same serializer must use explicit aliases such as `CoreAssetSerializer = helengine.AssetSerializer` and `FileAssetSerializer = helengine.files.AssetSerializer`.

**Tech Stack:** .NET 9.0, C#, existing engine project-reference layout, xUnit test projects, MSBuild solution wiring.

---

### Task 1: Scaffold `helengine.files` and wire it into the solution

**Files:**
- Create: `engine/helengine.files/helengine.files.csproj`
- Create: `engine/helengine.files.tests/helengine.files.tests.csproj`
- Modify: `helengine.ui/helengine.sln`
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Modify: `engine/helengine.files/helengine.files.csproj` to reference `..\helengine.core\helengine.core.csproj`
- Modify: `engine/helengine.files.tests/helengine.files.tests.csproj` to reference `..\helengine.files\helengine.files.csproj` and `..\helengine.core\helengine.core.csproj`

- [ ] **Step 1: Write the failing build check**

Run:

```bash
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.files/helengine.files.csproj --no-restore
```

Expected: fail because the project does not exist yet.

- [ ] **Step 2: Add the project files and references**

Create the two new SDK-style project files and add them to `helengine.ui/helengine.sln`. Add a project reference from `helengine.editor` to `helengine.files` so editor write paths can import `helengine.files` without touching the runtime namespace. Keep `helengine.core` free of any dependency on `helengine.files`.

Minimum project file shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
  </ItemGroup>
</Project>
```

The test project should reference both `helengine.files` and `helengine.core`.

- [ ] **Step 3: Verify the new project graph builds**

Run:

```bash
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.files/helengine.files.csproj --no-restore
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.editor/helengine.editor.csproj --no-restore -p:UseCommonOutputDirectory=true
```

Expected: `helengine.files` compiles, and `helengine.editor` still compiles against the new reference graph.

- [ ] **Step 4: Commit the project scaffolding**

```bash
git add engine/helengine.files/helengine.files.csproj engine/helengine.files.tests/helengine.files.tests.csproj helengine.ui/helengine.sln engine/helengine.editor/helengine.editor.csproj
git commit -m "refactor: scaffold helengine.files project"
```

---

### Task 2: Move the binary writer layer into `helengine.files`

**Files:**
- Create: `engine/helengine.files/serialization/EngineBinaryWriter.cs`
- Create: `engine/helengine.files/serialization/BinaryWriterLE.cs`
- Create: `engine/helengine.files/serialization/BinaryWriterBE.cs`
- Create: `engine/helengine.files/serialization/EngineBinaryHeaderSerializer.cs`
- Modify: `engine/helengine.core/serialization/EngineBinaryWriter.cs`
- Modify: `engine/helengine.core/serialization/BinaryWriterLE.cs`
- Modify: `engine/helengine.core/serialization/BinaryWriterBE.cs`
- Modify: `engine/helengine.core/serialization/EngineBinaryHeaderSerializer.cs`
- Modify: `engine/helengine.core/serialization/EngineBinaryReader.cs` only if shared helpers need to be moved out of the writer base class

- [ ] **Step 1: Add a focused failing test for writer namespace separation**

Create a small test in `engine/helengine.files.tests/serialization/EngineBinaryWriterTests.cs` that references `helengine.files.EngineBinaryWriter.Create(...)` and writes a 16-bit and 32-bit value to a memory stream. This test should fail until the new writer namespace exists.

```csharp
using System.IO;
using CoreFontAssetBinarySerializer = helengine.FontAssetBinarySerializer;
using FileFontAssetBinarySerializer = helengine.files.FontAssetBinarySerializer;
using Xunit;

public class EngineBinaryWriterTests {
    [Fact]
    public void Create_LittleEndian_WritesExpectedBytes() {
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);

        writer.WriteUInt16(0x1234);
        writer.WriteInt32(0x55667788);

        Assert.Equal(new byte[] { 0x34, 0x12, 0x88, 0x77, 0x66, 0x55 }, stream.ToArray());
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --no-restore --filter EngineBinaryWriterTests
```

Expected: fail because the `helengine.files` writer classes do not exist yet.

- [ ] **Step 3: Implement the writer layer in `helengine.files`**

Move or recreate the current writer implementation under the new namespace `helengine.files`:

```csharp
namespace helengine.files {
    public abstract class EngineBinaryWriter : IDisposable {
        protected readonly Stream BaseStream;
        readonly bool LeaveOpen;
        protected EngineBinaryWriter(Stream stream, bool leaveOpen = true) { /* same contract */ }
        public abstract EngineBinaryEndianness Endianness { get; }
        public static EngineBinaryWriter Create(Stream stream, EngineBinaryEndianness endianness, bool leaveOpen = true) { /* choose BinaryWriterLE/BinaryWriterBE */ }
        public void WriteByte(byte value) { /* same contract */ }
        public abstract void WriteUInt16(ushort value);
        public abstract void WriteInt32(int value);
        public abstract void WriteUInt32(uint value);
        public abstract void WriteInt64(long value);
        public void WriteInt2(int2 value) { /* same contract */ }
        public void WriteInt4(int4 value) { /* same contract */ }
        public void WriteFloat2(float2 value) { /* same contract */ }
        public void WriteFloat3(float3 value) { /* same contract */ }
        public void WriteFloat4(float4 value) { /* same contract */ }
        public void WriteSingle(float value) { /* same contract */ }
        public void WriteString(string value) { /* same contract */ }
        public void WriteByteArray(byte[] value) { /* same contract */ }
        public void WriteSceneEntityReference(SceneEntityReference reference) { /* same contract */ }
        public void WriteArray<T>(T[] values, Action<EngineBinaryWriter, T> writeElement) { /* same contract */ }
        public void Dispose() { /* same contract */ }
    }
}
```

Move `BinaryWriterLE` and `BinaryWriterBE` into the same `helengine.files` namespace and move the header write path into a writer-only `EngineBinaryHeaderSerializer.Write(...)` in `helengine.files`. Leave the reader-side header logic in core for now.

- [ ] **Step 4: Verify the writer test passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --no-restore --filter EngineBinaryWriterTests
```

Expected: PASS.

- [ ] **Step 5: Commit the writer-layer move**

```bash
git add engine/helengine.files/serialization/EngineBinaryWriter.cs engine/helengine.files/serialization/BinaryWriterLE.cs engine/helengine.files/serialization/BinaryWriterBE.cs engine/helengine.files/serialization/EngineBinaryHeaderSerializer.cs engine/helengine.files.tests/serialization/EngineBinaryWriterTests.cs engine/helengine.core/serialization/EngineBinaryWriter.cs engine/helengine.core/serialization/BinaryWriterLE.cs engine/helengine.core/serialization/BinaryWriterBE.cs engine/helengine.core/serialization/EngineBinaryHeaderSerializer.cs
git commit -m "refactor: move binary writers into helengine.files"
```

---

### Task 3: Split asset and font serializers into reader-only core and writer-side files

**Files:**
- Create: `engine/helengine.files/assets/AssetSerializer.cs`
- Create: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Create: `engine/helengine.files/assets/font/FontAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/AssetSerializer.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/font/FontAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/content/AssetContentProcessor.cs`
- Modify: `engine/helengine.core/content/RuntimeContentManagerConfiguration.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs` only if its imports need to distinguish the core reader from the files writer

- [ ] **Step 1: Add a focused round-trip test against the new files namespace**

Create `engine/helengine.files.tests/assets/FontAssetWriterTests.cs` that serializes a `FontAsset` via `helengine.files.FontAssetBinarySerializer.Serialize(...)`, then reads it back through `helengine.core.FontAssetBinarySerializer.Deserialize(...)`. Keep the test minimal: a font name, one character, and a tiny texture payload.

```csharp
using System.IO;
using helengine.files;
using Xunit;

public class FontAssetWriterTests {
    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsCharacterData() {
        // Build a minimal FontAsset with FontInfo, SourceTextureAsset, and one character.
        // Serialize via helengine.files.FontAssetBinarySerializer.
        // Deserialize via helengine.FontAssetBinarySerializer and assert the same values.
    }
}
```

- [ ] **Step 2: Run the test and confirm the writer namespace is missing**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --no-restore --filter FontAssetWriterTests
```

Expected: fail until the files-side asset serializers exist.

- [ ] **Step 3: Implement the asset serializers in `helengine.files`**

Create writer-side counterparts in `helengine.files` using the same format contract but a different namespace:

```csharp
namespace helengine.files {
    public static class AssetSerializer {
        public static void Serialize(Stream stream, Asset asset) { /* editor/export write path */ }
        public static byte[] SerializeToBytes(Asset asset) { /* helper */ }
    }
}
```

```csharp
namespace helengine.files {
    public static class EditorAssetBinarySerializer {
        public static void Serialize(Stream stream, Asset asset) { /* packaged asset write path */ }
    }
}
```

```csharp
namespace helengine.files {
    public static class FontAssetBinarySerializer {
        public static void Serialize(Stream stream, FontAsset asset) { /* packaged font write path */ }
    }
}
```

Leave the core versions read-only so `helengine.core` still deserializes packaged assets and fonts at runtime.

- [ ] **Step 4: Verify the round-trip test passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --no-restore --filter FontAssetWriterTests
```

Expected: PASS.

- [ ] **Step 5: Commit the asset/font serializer split**

```bash
git add engine/helengine.files/assets/AssetSerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.files/assets/font/FontAssetBinarySerializer.cs engine/helengine.files.tests/assets/FontAssetWriterTests.cs engine/helengine.core/assets/AssetSerializer.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.core/assets/font/FontAssetBinarySerializer.cs engine/helengine.core/content/AssetContentProcessor.cs engine/helengine.core/content/RuntimeContentManagerConfiguration.cs
git commit -m "refactor: split asset writers into helengine.files"
```

---

### Task 4: Update editor and shader export paths to consume `helengine.files`

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/serialization/ShaderCacheMetadataBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorFileTemplateService.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/shaders/ShaderModuleManager.cs`
- Modify: `engine/helengine.editor/shaders/ShaderPackageBuilder.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs`
- Modify: `engine/helengine.editor.tests/ContentManagerTests.cs`

- [ ] **Step 1: Add a failing import check in an editor write path**

Pick one editor write path, such as `SceneSaveService.cs`, and change the test to reference the files namespace explicitly:

```csharp
using helengine.files;

// ...
AssetSerializer.Serialize(stream, asset);
```

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter SceneSaveServiceTests
```

Expected: fail until the editor project references `helengine.files` and the call sites are switched.

- [ ] **Step 2: Redirect editor writes to `helengine.files`**

Update the editor write call sites to use the new namespace and keep the orchestration classes in editor. If a file needs both reader and writer variants of the same type, use aliases so the compiler never sees ambiguous serializer names:

```csharp
using CoreAssetSerializer = helengine.AssetSerializer;
using FileAssetSerializer = helengine.files.AssetSerializer;
using CoreFontAssetBinarySerializer = helengine.FontAssetBinarySerializer;
using FileFontAssetBinarySerializer = helengine.files.FontAssetBinarySerializer;
```

```csharp
using helengine.files;
```

Then call:

```csharp
AssetSerializer.Serialize(stream, asset);
FontAssetBinarySerializer.Serialize(stream, fontAsset);
EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
EngineBinaryHeaderSerializer.Write(stream, header);
```

Only the `using` target changes; the orchestration classes stay where they are.

- [ ] **Step 3: Verify the editor write-path tests pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter SceneSaveServiceTests
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter EditorWindowsBuildScenePackagerTests
```

Expected: PASS.

- [ ] **Step 4: Commit the editor migration**

```bash
git add engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/serialization/ShaderCacheMetadataBinarySerializer.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor/managers/asset/EditorFileTemplateService.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/shaders/ShaderModuleManager.cs engine/helengine.editor/shaders/ShaderPackageBuilder.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/AssetImportManagerTests.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs engine/helengine.editor.tests/ContentManagerTests.cs
git commit -m "refactor: route editor writes through helengine.files"
```

---

### Task 5: Remove writer methods from `helengine.core` and verify runtime reader-only builds

**Files:**
- Modify: `engine/helengine.core/assets/AssetSerializer.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/font/FontAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/serialization/EngineBinaryHeaderSerializer.cs`
- Modify: `engine/helengine.core/serialization/EngineBinaryWriter.cs`
- Modify: `engine/helengine.core/serialization/BinaryWriterLE.cs`
- Modify: `engine/helengine.core/serialization/BinaryWriterBE.cs`
- Modify: `engine/helengine.core/shaders/packages/ShaderModulePackageWriter.cs`
- Modify: `engine/helengine.core/content/RuntimeContentManagerConfiguration.cs`
- Modify: `engine/helengine.core/content/AssetContentProcessor.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
- Modify: `engine/helengine.editor.tests` and any remaining runtime-facing tests that still import core write methods

- [ ] **Step 1: Add a core build guard that fails on writer references**

Add a tiny test or compile-time check that core no longer references `helengine.files` and that all runtime deserializers still compile without writer helpers. Use the current `helengine.core` test project and target a runtime reader path such as `RuntimeSceneLoadServiceTests`.

- [ ] **Step 2: Delete or strip the writer members from core**

Remove the write-side methods from the core classes after the editor call sites have been redirected. The remaining core API should be read-only:

```csharp
public static Asset Deserialize(Stream stream) { ... }
public static Asset DeserializeFromBytes(byte[] data) { ... }
```

and similarly for font and editor asset readers.

- [ ] **Step 3: Verify core and files build cleanly**

Run:

```bash
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.core/helengine.core.csproj --no-restore -p:UseCommonOutputDirectory=true
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.files/helengine.files.csproj --no-restore -p:UseCommonOutputDirectory=true
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.editor/helengine.editor.csproj --no-restore -p:UseCommonOutputDirectory=true
```

Expected: all three build successfully with the writers removed from core.

- [ ] **Step 4: Commit the reader-only cleanup**

```bash
git add engine/helengine.core/assets/AssetSerializer.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.core/assets/font/FontAssetBinarySerializer.cs engine/helengine.core/serialization/EngineBinaryHeaderSerializer.cs engine/helengine.core/serialization/EngineBinaryWriter.cs engine/helengine.core/serialization/BinaryWriterLE.cs engine/helengine.core/serialization/BinaryWriterBE.cs engine/helengine.core/shaders/packages/ShaderModulePackageWriter.cs engine/helengine.core/content/RuntimeContentManagerConfiguration.cs engine/helengine.core/content/AssetContentProcessor.cs
git commit -m "refactor: make helengine.core reader-only for packaged content"
```

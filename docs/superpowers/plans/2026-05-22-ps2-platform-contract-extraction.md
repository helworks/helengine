# PS2 Platform Contract Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove PS2-specific cooked runtime contracts from `helengine` and make `helengine-ps2` own the PS2 cooked schema and runtime payload loading end to end.

**Architecture:** `helengine` keeps only generic platform metadata, generic asset authoring, and generic build-graph orchestration. `helengine-ps2` gains a managed `helengine.ps2` project plus a metadata-only plugin manifest, owns PS2 cooked material/texture/model payload formats, and feeds opaque packaged outputs into the native PS2 runtime. The biggest structural change is replacing `ModelAsset.Ps2PackedMeshBytes` with a PS2-owned sidecar cooked model payload.

**Tech Stack:** C#, .NET, helengine editor/build graph, helengine baseplatform contracts, external platform plugin manifests, C++ PS2 runtime host, PCSX2 verification.

---

### Task 1: Lock The Generic External Platform Plugin Boundary In `helengine.platforms`

**Files:**
- Modify: `engine/helengine.platforms/PlatformInstallationEntry.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationStore.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationResolver.cs`
- Create: `engine/helengine.platforms/PlatformPluginManifestDocument.cs`
- Modify: `engine/helengine.platforms.tests/PlatformInstallationResolverTests.cs`
- Modify: `engine/helengine.platforms.tests/AvailablePlatformProviderResolverTests.cs`
- Test: `engine/helengine.platforms.tests/helengine.platforms.tests.csproj`

- [ ] **Step 1: Write the failing plugin-boundary tests**

```csharp
[Fact]
public void TryLoadPlatform_WhenPluginManifestContainsRuntimePayloadTypeMetadata_Throws() {
    string settingsRootPath = Path.Combine(TempDirectoryPath, "user_settings");
    Directory.CreateDirectory(settingsRootPath);
    File.WriteAllText(Path.Combine(settingsRootPath, "platforms.json"), """
    {
      "platforms": [
        {
          "engineVersion": "1.0.0-custom",
          "platformId": "ps2",
          "displayName": "PlayStation 2",
          "playerSourceRootPath": "../helengine-ps2",
          "pluginManifestPath": "../helengine-ps2/platform-plugin.json"
        }
      ]
    }
    """);
    File.WriteAllText(Path.Combine(TempDirectoryPath, "helengine-ps2", "platform-plugin.json"), """
    {
      "platformId": "ps2",
      "displayName": "PlayStation 2",
      "runtimePayloadTypes": [ "helengine.ps2.Ps2MaterialAsset" ]
    }
    """);

    PlatformInstallationResolver resolver = new PlatformInstallationResolver(settingsRootPath);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.TryLoadPlatform("1.0.0-custom", "ps2", out _));
    Assert.Contains("runtime payload CLR types", exception.Message, StringComparison.Ordinal);
}

[Fact]
public void TryLoadPlatform_WhenPluginManifestContainsOnlyGenericMetadata_Succeeds() {
    string settingsRootPath = Path.Combine(TempDirectoryPath, "user_settings");
    Directory.CreateDirectory(settingsRootPath);
    File.WriteAllText(Path.Combine(settingsRootPath, "platforms.json"), """
    {
      "platforms": [
        {
          "engineVersion": "1.0.0-custom",
          "platformId": "ps2",
          "displayName": "PlayStation 2",
          "builderAssemblyPath": "../helengine-ps2/builder/helengine.ps2.builder.dll",
          "playerSourceRootPath": "../helengine-ps2",
          "pluginManifestPath": "../helengine-ps2/platform-plugin.json"
        }
      ]
    }
    """);
    File.WriteAllText(Path.Combine(TempDirectoryPath, "helengine-ps2", "platform-plugin.json"), """
    {
      "platformId": "ps2",
      "displayName": "PlayStation 2",
      "builderAssemblyPath": "builder/helengine.ps2.builder.dll"
    }
    """);

    PlatformInstallationResolver resolver = new PlatformInstallationResolver(settingsRootPath);

    bool resolved = resolver.TryLoadPlatform("1.0.0-custom", "ps2", out AvailablePlatformDescriptor platform);
    Assert.True(resolved);
    Assert.Equal("ps2", platform.Id);
}
```

- [ ] **Step 2: Run the focused platform-discovery tests and verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --filter "FullyQualifiedName~PlatformInstallationResolverTests|FullyQualifiedName~AvailablePlatformProviderResolverTests"
```

Expected: FAIL because the current platform installation model has no plugin manifest path or metadata-only manifest validation.

- [ ] **Step 3: Add plugin-manifest support and metadata-only validation to `helengine.platforms`**

```csharp
/// <summary>
/// Validates one external platform plugin manifest and rejects platform-owned runtime contract metadata.
/// </summary>
/// <param name="manifestFilePath">Absolute plugin-manifest path.</param>
/// <returns>Loaded metadata-only plugin manifest document.</returns>
public static PlatformPluginManifestDocument LoadPluginManifest(string manifestFilePath) {
    JsonObject manifest = LoadManifestObject(manifestFilePath);

    if (manifest["runtimePayloadTypes"] != null) {
        throw new InvalidOperationException("External platform plugin manifests must not declare runtime payload CLR types.");
    } else if (manifest["serializerHooks"] != null) {
        throw new InvalidOperationException("External platform plugin manifests must not declare serializer hooks into helengine.");
    }

    return BuildPluginManifestDocument(manifestFilePath, manifest);
}
```

- [ ] **Step 4: Extend installation entries to point at plugin manifests without exposing runtime payload types**

```csharp
public PlatformInstallationEntry(
    string engineVersion,
    string platformId,
    string displayName,
    string builderAssemblyPath,
    string playerSourceRootPath,
    string generatedCoreCppRootPath = "",
    string codegenToolPath = "",
    string pluginManifestPath = "") {
    PluginManifestPath = pluginManifestPath ?? string.Empty;
}
```

- [ ] **Step 5: Run the focused platform tests and verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --filter "FullyQualifiedName~PlatformInstallationResolverTests|FullyQualifiedName~AvailablePlatformProviderResolverTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the boundary-only platform-discovery change**

```powershell
rtk git add engine/helengine.platforms/PlatformInstallationEntry.cs engine/helengine.platforms/PlatformInstallationStore.cs engine/helengine.platforms/PlatformInstallationResolver.cs engine/helengine.platforms/PlatformPluginManifestDocument.cs engine/helengine.platforms.tests/PlatformInstallationResolverTests.cs engine/helengine.platforms.tests/AvailablePlatformProviderResolverTests.cs
rtk git commit -m "Add metadata-only external platform manifest validation"
```

### Task 2: Create `helengine.ps2` In The PS2 Repository And Publish A Metadata-Only Manifest

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\helengine.ps2.csproj`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Plugin\Ps2PlatformPluginManifest.cs`
- Create: `C:\dev\helworks\helengine-ps2\platform-plugin.json`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformDefinitionFactoryTests.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`

- [ ] **Step 1: Write the failing PS2 manifest tests**

```csharp
[Fact]
public void PlatformPluginManifest_WhenSerialized_DoesNotContainRuntimePayloadTypeDeclarations() {
    JsonObject manifest = Ps2PlatformPluginManifest.Create();

    Assert.Null(manifest["runtimePayloadTypes"]);
    Assert.Null(manifest["serializerHooks"]);
    Assert.Equal("ps2", manifest["platformId"]?.GetValue<string>());
}
```

- [ ] **Step 2: Run the focused PS2 manifest tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests"
```

Expected: FAIL because the manifest project and manifest file do not exist yet.

- [ ] **Step 3: Add the managed project and metadata-only manifest**

```csharp
/// <summary>
/// Creates the external platform plugin manifest consumed by the helengine editor.
/// </summary>
public static class Ps2PlatformPluginManifest {
    /// <summary>
    /// Creates the metadata-only PS2 platform manifest payload.
    /// </summary>
    /// <returns>Serialized manifest object.</returns>
    public static JsonObject Create() {
        return new JsonObject {
            ["platformId"] = "ps2",
            ["displayName"] = "PlayStation 2",
            ["builderAssemblyPath"] = "builder/helengine.ps2.builder.dll",
            ["definitionFactoryType"] = "helengine.ps2.builder.Ps2PlatformDefinitionFactory"
        };
    }
}
```

- [ ] **Step 4: Emit the external manifest file from the PS2 repo**

```json
{
  "platformId": "ps2",
  "displayName": "PlayStation 2",
  "builderAssemblyPath": "builder/helengine.ps2.builder.dll",
  "definitionFactoryType": "helengine.ps2.builder.Ps2PlatformDefinitionFactory"
}
```

- [ ] **Step 5: Run the focused PS2 tests and verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the PS2 manifest scaffolding**

```powershell
rtk -C C:\dev\helworks\helengine-ps2 git add managed/helengine.ps2/helengine.ps2.csproj managed/helengine.ps2/Plugin/Ps2PlatformPluginManifest.cs platform-plugin.json builder/Ps2PlatformDefinitionFactory.cs builder.tests/Ps2PlatformDefinitionFactoryTests.cs
rtk -C C:\dev\helworks\helengine-ps2 git commit -m "Publish metadata-only PS2 plugin manifest"
```

### Task 3: Move PS2 Material And Texture Cooked Contracts Out Of `helengine` With A PS2-Owned Serializer

**Files:**
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2MaterialAsset.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2TextureAsset.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2MaterialLightingMode.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2MaterialAlphaMode.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2RenderClass.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2TextureFormat.cs`
- Delete: `engine/helengine.core/assets/raw/ps2/Ps2TextureAlphaMode.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2MaterialAsset.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2TextureAsset.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2MaterialLightingMode.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2MaterialAlphaMode.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2RenderClass.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2TextureFormat.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2TextureAlphaMode.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Serialization\Ps2AssetBinaryValueKind.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Serialization\Ps2AssetBinarySerializer.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Serialization\Ps2AssetSerializer.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2MaterialCooker.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2RuntimeTextureCooker.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2CookedAssetPathRewriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing PS2 contract ownership tests**

```csharp
[Fact]
public void Ps2BuilderContracts_AreOwnedByPs2ManagedProject() {
    Type materialAssetType = typeof(helengine.ps2.Assets.Ps2MaterialAsset);
    Type textureAssetType = typeof(helengine.ps2.Assets.Ps2TextureAsset);

    Assert.Equal("helengine.ps2", materialAssetType.Assembly.GetName().Name);
    Assert.Equal("helengine.ps2", textureAssetType.Assembly.GetName().Name);
}
```

- [ ] **Step 2: Run the focused PS2 builder tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2BuilderContracts_AreOwnedByPs2ManagedProject|FullyQualifiedName~Ps2PlatformAssetBuilderTests"
```

Expected: FAIL because the contract types still come from `helengine.core`.

- [ ] **Step 3: Add the PS2-owned cooked asset serializer path**

```csharp
namespace helengine.ps2.Serialization {
    /// <summary>
    /// Provides HELE-compatible serialization helpers for PS2-owned cooked asset payloads.
    /// </summary>
    public static class Ps2AssetSerializer {
        /// <summary>
        /// Serializes one PS2-owned cooked asset into a new byte array.
        /// </summary>
        /// <param name="asset">PS2-owned cooked asset instance to serialize.</param>
        /// <returns>Serialized cooked payload bytes.</returns>
        public static byte[] SerializeToBytes(Asset asset) {
            using MemoryStream stream = new();
            Ps2AssetBinarySerializer.Serialize(stream, asset);
            return stream.ToArray();
        }
    }
}
```

- [ ] **Step 4: Move the PS2 contract types into `helengine.ps2` and retarget the PS2 builder**

```csharp
namespace helengine.ps2.Assets {
    /// <summary>
    /// Stores the PS2-owned cooked material payload written by the PS2 builder and consumed by the PS2 runtime.
    /// </summary>
    public class Ps2MaterialAsset : PlatformMaterialAsset {
        /// <summary>
        /// Relative runtime path to the cooked PS2 texture payload.
        /// </summary>
        public string TextureRelativePath;

        /// <summary>
        /// Selected PS2 lighting mode.
        /// </summary>
        public Ps2MaterialLightingMode LightingMode;
    }
}
```

- [ ] **Step 5: Update the PS2 builder imports and serialization calls to use only `helengine.ps2`**

```csharp
using helengine.ps2.Assets;
using helengine.ps2.Serialization;

public sealed class Ps2RuntimeTextureCooker {
    /// <summary>
    /// Converts one generic texture asset into a PS2-owned cooked texture payload.
    /// </summary>
    public Ps2TextureAsset Cook(TextureAsset sourceTexture, TextureAssetProcessorSettings settings) {
        return new Ps2TextureAsset {
            Format = Ps2TextureFormat.Rgba32,
            AlphaMode = Ps2TextureAlphaMode.Full,
            Width = sourceTexture.Width,
            Height = sourceTexture.Height
        };
    }
}
```

- [ ] **Step 6: Run the focused PS2 tests and verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~Ps2BuilderContracts_AreOwnedByPs2ManagedProject"
```

Expected: PASS.

- [ ] **Step 7: Commit the PS2 cooked contract and serializer move**

```powershell
rtk -C C:\dev\helworks\helengine-ps2 git add managed/helengine.ps2/Assets managed/helengine.ps2/Serialization builder builder.tests
rtk -C C:\dev\helworks\helengine-ps2 git commit -m "Move PS2 cooked contracts and serializer into helengine.ps2"
```

### Task 4: Remove PS2 Serializer Branches From Generic Engine Serialization And Retarget Codegen

**Files:**
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing generic serializer removal tests**

```csharp
[Fact]
public void GenericAssetSerializer_DoesNotPublishPs2SpecificValueKinds() {
    string source = File.ReadAllText("engine/helengine.core/assets/EditorAssetBinaryValueKind.cs");

    Assert.DoesNotContain("Ps2MaterialAsset", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Ps2TextureAsset", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused serialization tests and verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: FAIL because the generic serializers still contain PS2-specific branches and generated-core does not know about `helengine.ps2`.

- [ ] **Step 3: Remove the PS2-specific value kinds and branches**

```csharp
public enum EditorAssetBinaryValueKind : ushort {
    Asset = 0,
    MaterialAsset = 1,
    TextureAsset = 2,
    ModelAsset = 3,
    PlatformMaterialAsset = 4
}
```

```csharp
if (asset is PlatformMaterialAsset) {
    return EditorAssetBinaryValueKind.PlatformMaterialAsset;
}

throw new InvalidOperationException($"Unsupported generic asset payload type '{asset.GetType().FullName}'.");
```

- [ ] **Step 4: Update generic tests to assert the PS2 cases are gone**

```csharp
[Fact]
public void WindowsScenePackager_WhenPackagingGenericMaterials_DoesNotDependOnPs2CookedAssetTypes() {
    string source = File.ReadAllText("engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs");

    Assert.DoesNotContain("Ps2MaterialAsset", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Ps2TextureAsset", source, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Extend generated-core regeneration so external platform-managed runtime contract assemblies can be merged**

```csharp
if (string.Equals(platformDefinition.PlatformId, "ps2", StringComparison.Ordinal)) {
    MergeGeneratedSourceTree(ps2ManagedOutputRoot, generatedCoreOutputRoot);
    MergeGeneratedConversionReport(ps2ManagedOutputRoot, generatedCoreOutputRoot);
}
```

- [ ] **Step 6: Run the focused editor tests and verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: PASS.

- [ ] **Step 7: Commit the generic serializer and codegen retargeting**

```powershell
rtk git add engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
rtk git commit -m "Retarget PS2 cooked payload serialization and codegen"
```

### Task 5: Replace `ModelAsset.Ps2PackedMeshBytes` With A PS2-Owned Sidecar Model Payload

**Files:**
- Modify: `engine/helengine.core/assets/raw/ModelAsset.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
- Create: `C:\dev\helworks\helengine-ps2\managed\helengine.ps2\Assets\Ps2PackedModelAsset.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeModel.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2NativeBuildInputsTests.cs`

- [ ] **Step 1: Write the failing sidecar-payload tests**

```csharp
[Fact]
public void Ps2ModelCook_WritesDedicatedPackedModelSidecar() {
    Ps2PlatformAssetBuildResult result = BuildTestModelForPs2();

    Assert.Contains(result.PackagedOutputs, output => output.RelativePath.EndsWith(".ps2model", StringComparison.Ordinal));
}

[Fact]
public void ModelAsset_DoesNotExposePs2PackedMeshBytes() {
    FieldInfo field = typeof(ModelAsset).GetField("Ps2PackedMeshBytes");
    Assert.Null(field);
}
```

- [ ] **Step 2: Run the focused PS2 model tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~Ps2NativeBuildInputsTests"
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ModelAsset_DoesNotExposePs2PackedMeshBytes"
```

Expected: FAIL because PS2 still embeds packed mesh bytes on `ModelAsset`.

- [ ] **Step 3: Add the PS2-owned sidecar asset type and stop writing PS2 bytes into `ModelAsset`**

```csharp
namespace helengine.ps2.Assets {
    /// <summary>
    /// Stores one PS2-owned packed model payload produced during build staging.
    /// </summary>
    public class Ps2PackedModelAsset : Asset {
        /// <summary>
        /// Packed PS2-native model bytes consumed by the PS2 runtime.
        /// </summary>
        public byte[] PackedBytes;
    }
}
```

```csharp
Ps2PackedModelAsset packedModelAsset = new() {
    PackedBytes = packedMeshBytes
};

WritePackedModelSidecar(outputPath, packedModelAsset);
```

- [ ] **Step 4: Update the native PS2 runtime to load the sidecar instead of `ModelAsset.Ps2PackedMeshBytes`**

```cpp
::Ps2PackedModelAsset* packedModelAsset = LoadPs2PackedModelAsset(sidecarPath);
if (packedModelAsset == nullptr || packedModelAsset->PackedBytes == nullptr || packedModelAsset->PackedBytes->Length <= 0) {
    throw std::invalid_argument("PS2 packed model sidecar is required for runtime model loading.");
}

LoadFromPackedBytes(
    reinterpret_cast<const std::uint8_t*>(packedModelAsset->PackedBytes->Data),
    static_cast<std::size_t>(packedModelAsset->PackedBytes->Length));
```

- [ ] **Step 5: Run the focused main-repo and PS2 tests and verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ModelAsset_DoesNotExposePs2PackedMeshBytes"
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~Ps2NativeBuildInputsTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the PS2 packed model sidecar migration**

```powershell
rtk git add engine/helengine.core/assets/raw/ModelAsset.cs engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs
rtk git commit -m "Remove PS2 packed mesh bytes from generic model assets"
rtk -C C:\dev\helworks\helengine-ps2 git add managed/helengine.ps2/Assets/Ps2PackedModelAsset.cs builder/Ps2PlatformAssetBuilder.cs src/platform/ps2/rendering/Ps2RuntimeModel.cpp builder.tests/Ps2PlatformAssetBuilderTests.cs builder.tests/Ps2NativeBuildInputsTests.cs
rtk -C C:\dev\helworks\helengine-ps2 git commit -m "Load PS2 packed model sidecars"
```

### Task 6: Retarget The PS2 Runtime To PS2-Owned Payloads Only

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2CookedAssetPathRewriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2BootHostSourceTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2RenderManager3DSourceTests.cs`

- [ ] **Step 1: Write the failing runtime source tests**

```csharp
[Fact]
public void Ps2BootHost_UsesPs2OwnedManagedContractNamespace() {
    string source = File.ReadAllText(@"C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp");

    Assert.Contains("Ps2TextureAsset", source, StringComparison.Ordinal);
    Assert.DoesNotContain("helengine.core/assets/raw/ps2", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused PS2 runtime source tests and verify they fail if old ownership remains**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2BootHostSourceTests|FullyQualifiedName~Ps2RenderManager3DSourceTests"
```

Expected: FAIL until all generated/runtime paths reference only PS2-owned contracts.

- [ ] **Step 3: Repoint the PS2 runtime and rewriter to the PS2-owned payload contract**

```csharp
if (asset is helengine.ps2.Assets.Ps2MaterialAsset ps2MaterialAsset) {
    changed = RewritePs2MaterialAsset(ps2MaterialAsset, logicalToPhysicalPaths, rawMaterialPhysicalToCookedPhysicalPaths);
}
```

```cpp
::Ps2TextureAsset* textureAsset = he_cpp_try_cast<::Ps2TextureAsset>(asset);
if (textureAsset == nullptr) {
    throw std::invalid_argument("PS2 runtime expected one PS2-owned cooked texture payload.");
}
```

- [ ] **Step 4: Run the focused PS2 runtime source tests and verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2BootHostSourceTests|FullyQualifiedName~Ps2RenderManager3DSourceTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the PS2 runtime retargeting**

```powershell
rtk -C C:\dev\helworks\helengine-ps2 git add src/platform/ps2/Ps2BootHost.cpp src/platform/ps2/rendering/Ps2RenderManager3D.cpp src/platform/ps2/rendering/Ps2RuntimeMaterial.cpp builder/Ps2CookedAssetPathRewriter.cs builder.tests/Ps2BootHostSourceTests.cs builder.tests/Ps2RenderManager3DSourceTests.cs
rtk -C C:\dev\helworks\helengine-ps2 git commit -m "Retarget PS2 runtime to PS2-owned cooked payloads"
```

### Task 7: End-To-End Verification And Boundary Cleanup

**Files:**
- Modify: `docs/superpowers/plans/2026-05-22-ps2-platform-contract-extraction.md`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Modify: `docs/` notes only if runtime output paths or plugin-manifest instructions changed materially

- [ ] **Step 1: Run the smallest main-repo verification slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetBuilderLoaderTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: PASS.

- [ ] **Step 2: Run the smallest PS2 verification slice**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~Ps2BootHostSourceTests|FullyQualifiedName~Ps2RenderManager3DSourceTests|FullyQualifiedName~Ps2NativeBuildInputsTests"
```

Expected: PASS.

- [ ] **Step 3: Build a fresh PS2 demo disc with the external plugin path**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build ps2 --output 'C:\dev\helprojs\output\ps2-city-plugin-boundary' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: build completes and packages a fresh PS2 output with no `Ps2MaterialAsset` or `Ps2TextureAsset` ownership in the main repo.

- [ ] **Step 4: Grep the main repo for forbidden PS2 cooked contract leftovers**

Run:

```powershell
rtk rg -n -F "Ps2MaterialAsset" engine docs
rtk rg -n -F "Ps2TextureAsset" engine docs
rtk rg -n -F "Ps2PackedMeshBytes" engine docs
```

Expected:

- engine code results should be empty except for the new design/plan docs
- docs may still mention the old names in historical plans/specs, which is acceptable

- [ ] **Step 5: Update PS2 README ownership notes**

```markdown
PS2 cooked payload contracts now live in the `helengine.ps2` managed project inside this repository. The main `helengine` repository consumes only the metadata-only external platform manifest and does not define or deserialize PS2 cooked runtime asset payloads.
```

- [ ] **Step 6: Commit the verification/docs sweep**

```powershell
rtk -C C:\dev\helworks\helengine-ps2 git add README.md
rtk -C C:\dev\helworks\helengine-ps2 git commit -m "Document PS2-owned cooked payload boundary"
```

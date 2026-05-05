# PS2 Textured Opaque Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Teach the PS2 runtime renderer to draw opaque textured triangles using authored model UVs and cooked texture assets.

**Architecture:** Preserve model UVs in the PS2 runtime model, then let the PS2 3D renderer resolve a cooked texture path into a cached GS texture record. The renderer should keep the current gouraud-lit fallback for untextured or incomplete assets, while textured triangles reuse the same lighting path and only switch the primitive emission path when UVs and a texture are available.

**Tech Stack:** C++20, ps2dev/gsKit, HelEngine generated runtime assets, existing PS2 native Docker build.

---

### Task 1: Preserve UVs in the PS2 runtime model

**Files:**
- Modify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RuntimeModel.hpp`
- Modify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RuntimeModel.cpp`

- [ ] **Step 1: Inspect the generated runtime model shape**

```powershell
rtk proxy powershell.exe -NoProfile -Command "rg -n 'TexCoords|Indices16|Positions|Normals' 'C:\dev\helworks\helengine\tmp\helengine-core-cpp-regenerated\ModelAsset.hpp' 'C:\dev\helworks\helengine\tmp\helengine-core-cpp-regenerated\ModelAsset.cpp'"
```

- [ ] **Step 2: Add UV storage to `Ps2RuntimeModel`**

```cpp
// Ps2RuntimeModel.hpp
const std::vector<::float2>& GetTexCoords() const;

// Ps2RuntimeModel.cpp
if (modelAsset->TexCoords != nullptr && modelAsset->TexCoords->Length > 0) {
    TexCoords.reserve(static_cast<std::size_t>(modelAsset->TexCoords->Length));
    for (int32_t index = 0; index < modelAsset->TexCoords->Length; index++) {
        TexCoords.push_back(modelAsset->TexCoords->Data[index]);
    }
}
```

- [ ] **Step 3: Verify the generated model still round-trips UV data**

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~BinarySerializationTests --no-restore
```

- [ ] **Step 4: Commit the model-data change**

```powershell
git add src/platform/ps2/rendering/Ps2RuntimeModel.hpp src/platform/ps2/rendering/Ps2RuntimeModel.cpp
git commit -m "Preserve PS2 model UVs"
```

### Task 2: Render opaque textured triangles on PS2

**Files:**
- Modify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RenderManager3D.hpp`
- Modify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`

- [ ] **Step 1: Inspect the gsKit textured triangle signature in the build image**

```powershell
rtk proxy docker run --rm helengine-ps2-inline sh -lc 'sed -n "1,220p" /usr/local/ps2dev/gsKit/include/gsTexture.h | sed -n "/prim_triangle_texture_3d/,/prim_quad_texture_3d/p"'
```

- [ ] **Step 2: Add a GS texture cache and cooked-texture resolver**

```cpp
struct Ps2TextureRecord {
    GSTEXTURE Texture {};
    bool Uploaded;
    u32* Pixels;
};

const GSTEXTURE* ResolveTexture(const Ps2RuntimeMaterial& material);
Ps2TextureRecord* ResolveTextureRecord(const std::string& textureRelativePath);
```

- [ ] **Step 3: Emit textured Gouraud triangles when a texture and UVs are available**

```cpp
const GSTEXTURE* texture = ResolveTexture(*material);
if (texture != nullptr && !texCoords.empty()) {
    gsKit_prim_triangle_goraud_texture_3d(
        GsGlobal,
        const_cast<GSTEXTURE*>(texture),
        ProjectX(positionA), ProjectY(positionA), 1.0f, texCoords[indexA].X, texCoords[indexA].Y, colorA,
        ProjectX(positionB), ProjectY(positionB), 1.0f, texCoords[indexB].X, texCoords[indexB].Y, colorB,
        ProjectX(positionC), ProjectY(positionC), 1.0f, texCoords[indexC].X, texCoords[indexC].Y, colorC);
} else {
    gsKit_prim_triangle_gouraud_3d(...);
}
```

- [ ] **Step 4: Rebuild the PS2 native image**

```powershell
rtk proxy docker run --rm -v C:/tmp/helengine-ps2-worktrees/ps2-renderer-foundation-inline:/workspace -v C:/dev/helworks/helengine/.tmp/ps2-generated-core-worktree:/generated-core -w /workspace -e HELENGINE_CORE_CPP_ROOT=/generated-core helengine-ps2-inline sh -lc "make clean && make"
```

- [ ] **Step 5: Commit the renderer change**

```powershell
git add src/platform/ps2/rendering/Ps2RenderManager3D.hpp src/platform/ps2/rendering/Ps2RenderManager3D.cpp
git commit -m "Add PS2 textured opaque rendering"
```

### Task 3: Revalidate the whole PS2 slice

**Files:**
- Verify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RuntimeModel.cpp`
- Verify: `C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`

- [ ] **Step 1: Run the native PS2 build again after both code changes**

```powershell
rtk proxy docker run --rm -v C:/tmp/helengine-ps2-worktrees/ps2-renderer-foundation-inline:/workspace -v C:/dev/helworks/helengine/.tmp/ps2-generated-core-worktree:/generated-core -w /workspace -e HELENGINE_CORE_CPP_ROOT=/generated-core helengine-ps2-inline sh -lc "make clean && make"
```

- [ ] **Step 2: Verify the PS2 tree remains clean enough for reuse**

```powershell
git -C C:\dev\helworks\helengine status --short
```

Expected: no tracked edits in the HelEngine repo.

- [ ] **Step 3: Record the result in the branch commit**

```powershell
git -C C:\tmp\helengine-ps2-worktrees\ps2-renderer-foundation-inline rev-parse --short HEAD
```

Expected: a commit hash for the final PS2 renderer change set.

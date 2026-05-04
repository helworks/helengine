# Windows Forward Renderer Design

## Summary

HelEngine should build a Windows-first forward rendering architecture that supports modern desktop features now while keeping the core runtime contracts usable by simpler backends later.

The immediate target is a DirectX 11 / Shader Model 5 renderer with:

- multiple lights
- directional, point, and spot shadows
- normal-mapped physically based materials
- post-processing
- static batching
- dynamic batching
- instancing

The architecture must also stay ready for:

- a future deferred renderer on Windows
- simpler console renderers
- a reduced DirectX 8 or DirectX 9 era backend later

The key rule is that authored scene content should express engine-level rendering intent, while each backend declares how much of that intent it can realize.

## Goals

- Build a Windows forward renderer with a modern feature set.
- Keep the shared engine-side render contracts backend-neutral.
- Make future deferred rendering a renderer implementation change, not a scene or asset format rewrite.
- Support engine light objects for directional, point, and spot lights.
- Support shadow maps for all three light types in the Windows renderer.
- Support a compact physically based material model for Windows.
- Add explicit compatibility and downgrade rules for weaker backends.
- Expose important renderer controls through platform graphics settings and per-camera settings instead of burying them in backend code.

## Non-Goals

- Implement deferred rendering in this slice.
- Guarantee one-to-one visual parity with a future DirectX 9 or fixed-function backend.
- Build every post-process effect now.
- Solve every console material model now.
- Design a film-style offline renderer.

## Design Principles

## Windows First, Shared Contracts First

Windows DirectX 11 is the main rendering target.

The engine should not constrain the Windows renderer down to DirectX 9 limits from day one.

However, the shared contracts should avoid baking in assumptions that only a high-end shader backend can satisfy. Lights, cameras, material inputs, and render passes should be represented in engine data first, then mapped by the active backend.

## Forward Now, Deferred Later

The engine should not build one monolithic `DirectX11Renderer3D` that mixes scene traversal, visibility, lighting, shadow allocation, batching, post-processing, and presentation in one runtime object.

Instead, the renderer should be structured around a shared frame extraction and pass-planning model. The Windows implementation will consume that model as a forward renderer first. A later deferred path should be able to consume the same extracted frame data with different main lighting passes.

## Compatibility Is Explicit, Not Accidental

Weaker backends should not fail because they happen to compile fewer shaders or ignore a branch.

Each backend should declare:

- supported light counts and shadow types
- supported material features
- supported post-process features
- batching and instancing support
- downgrade behavior when unsupported features appear

If a feature can be degraded, the rule should be explicit. If a feature is required and cannot be degraded, the build should fail clearly.

## Shared Renderer Architecture

## 1. `RenderManager3D` Grows Into a Shared Backend Contract

`RenderManager3D` should evolve from a thin draw surface into the main backend interface for a shared render frame contract.

The shared side should define stable data for:

- camera submissions
- visible draw submissions
- light submissions
- shadow-caster submissions
- post-process requests
- batching eligibility metadata
- backend capability metadata

This makes later backends consume a stable frame description instead of scraping scene objects directly.

## 2. Frame Flow

The shared render flow should be:

1. scene extraction
2. frame planning
3. shadow passes
4. main geometry passes
5. post-processing
6. presentation

### Scene Extraction

The engine gathers:

- active cameras
- visible opaque drawables
- visible cutout drawables
- visible transparent drawables
- visible lights
- shadow casters
- batching and instancing candidates

This extracted frame data must stay renderer-neutral.

### Frame Planning

The backend chooses:

- which lights contribute to each camera
- which lights receive shadow resources
- how lights are prioritized when the backend budget is exceeded
- whether a depth prepass is used
- which drawables are statically batched, dynamically batched, or instanced
- which post-process passes are active

### Shadow Passes

Shadow-map rendering happens before the main geometry passes and produces reusable frame resources.

### Main Geometry Passes

The Windows path stays forward-rendered:

- optional depth prepass
- opaque forward pass
- cutout forward pass
- transparent forward pass

### Post-Processing

Post is an ordered chain of passes over renderer-managed intermediate targets, not one giant fixed effect.

### Presentation

The final resolved image is presented to the swap chain or render target surface.

## 3. Deferred-Ready Resource Boundaries

To stay ready for deferred later, the extracted frame data must not be shaped around forward-only assumptions.

That means:

- lights are represented independently from material draw calls
- materials expose surface inputs separately from the active lighting implementation
- shadow resources are frame resources, not private state hidden inside one draw loop
- camera outputs are modeled as target resources, not just direct swap-chain rendering

A later deferred renderer should be able to replace the main geometry stage with:

- G-buffer fill
- light accumulation
- transparent forward rendering
- post

without changing authored scene content.

## Lights

## Engine Light Objects

The engine should add explicit light objects or components for:

- directional lights
- point lights
- spot lights

Each light should carry engine-level properties such as:

- color
- intensity
- range where applicable
- cone angles for spot lights
- shadow enabled state
- shadow quality or resolution intent
- importance or priority
- culling masks or future layer filters

The scene should not express lights as backend-specific shader constants.

## Windows Light Model

The Windows renderer should support many visible lights in a scene, but use an explicit per-frame budget rather than pretending all visible lights affect all objects equally.

The backend should support:

- multiple non-shadowed lights
- several shadowed lights
- deterministic priority when the light budget is exceeded

The planner should sort by explicit importance first and by camera-relevant heuristics second.

## Shadow Types

The Windows path should support all three light families as shadow casters:

- directional: cascaded shadow maps
- spot: projected shadow maps
- point: cubemap shadows or another Windows-specific equivalent abstraction

The engine contract should describe the intent generically. The Windows backend is free to choose the exact point-light shadow implementation.

## Camera and Platform Settings

Renderer settings should be split across three layers.

## Platform Graphics Profile

Per-platform graphics settings should carry renderer-wide defaults and quality tiers, such as:

- backend target
- default HDR on or off
- MSAA policy
- shadow quality tier
- post-processing quality tier
- default depth prepass mode
- light budget tier
- shadow atlas sizing

These settings belong in the platform graphics configuration and build pipeline, not as hidden runtime constants.

## Per-Camera Render Settings

Each camera should be able to express rendering intent that may vary by platform. This should include settings such as:

- clear mode
- HDR participation
- exposure mode
- depth prepass mode
- shadow distance
- post-process stack selection
- transparency behavior
- optional debug or visualization modes later

The platform profile provides defaults. The camera expresses intent. The backend resolves both against its capabilities.

## Backend Capability Profile

Each renderer backend should publish a capability profile describing what it can realize.

Examples:

- maximum supported light counts
- supported shadow types
- supported material features
- HDR support
- normal-map support
- post-process support
- instancing support
- batching support

Backends with weaker capabilities should degrade through explicit compatibility rules.

## Materials

## Windows Material Model

The Windows renderer should support a compact physically based workflow with these inputs:

- albedo
- normal
- metallic
- roughness
- ambient occlusion
- emissive
- opacity or cutout state
- scalar multipliers where needed

This should stay disciplined rather than exploding into a huge feature matrix.

## PBR and Performance

Physically based shading is acceptable on the Windows target if the engine keeps it compact:

- one metal-rough workflow
- limited texture set
- controlled feature switches
- no requirement for extremely expensive layered BRDF behavior

This is different from trying to force the same exact shader onto older targets.

## Compatibility Material Model

Future DirectX 9 or similar backends should be allowed to cook the same authored material intent into a simpler runtime representation, such as:

- optional normal map
- reduced texture count
- approximated metallic and roughness behavior
- cheaper specular model
- reduced branching
- fewer samplers

The compatibility target is authored intent preservation, not byte-for-byte shader parity.

## Batching and Instancing

## Static Batching

Static batching should target non-moving opaque geometry whose mesh, material layout, and render state are compatible.

The batching system should merge or group geometry only when it produces stable wins without breaking culling and material behavior.

## Dynamic Batching

Dynamic batching should be limited to small transient geometry and simple repeated draws where CPU-side rebuild cost stays reasonable.

It should not become the universal answer for large dynamic scenes.

## Instancing

Instancing should be treated as a first-class batching path separate from dynamic batching.

It should be available for repeated mesh and material combinations where instance transforms and a small amount of per-instance data are enough.

## Transparent Geometry

Transparent objects should generally remain separate from aggressive batching, aside from compatible instancing and sort-friendly grouping.

The engine should prefer correctness and predictable sorting over forced batching wins.

## Post-Processing

The Windows renderer should support a small ordered post-process chain.

Baseline candidates:

- tone mapping
- bloom
- color grading
- vignette
- FXAA or another cheap antialiasing pass

Heavier depth-driven effects should be separate extensions rather than mandatory baseline requirements.

The chain should be data-driven enough that later backends can:

- disable unsupported passes
- substitute cheaper passes
- preserve pass ordering where possible

## Compatibility Rules for Simpler Backends

The engine should classify renderer features into three groups:

- required core contracts
- optional backend features
- explicit downgrade rules

Examples:

- point light exists, but point-light shadows may degrade to unshadowed point lights
- HDR-authored camera may degrade to LDR output with limited post-processing
- full metal-rough material may degrade to a cheaper specular response
- high light counts may degrade to a capped set of most important lights

Builds should fail only when the authored content demands a feature the target marks as non-degradable.

## DirectX 9 and Older API Readiness

The engine should stay ready for a future DirectX 9 or DirectX 8 style backend, but not let that tail drive the Windows design.

The practical rule is:

- Windows DX11 path gets the richer renderer
- legacy backends consume the same scene and material intent through a reduced capability profile
- no engine-level authoring rewrite is required just because a backend is simpler

DirectX 9 era constraints mean a future backend will likely need:

- fewer active lights
- fewer shadowed lights
- simpler BRDF behavior
- fewer texture fetches
- reduced post stack
- smaller runtime light and material parameter layouts

That is acceptable under this architecture.

## Build and Runtime Integration

The Windows builder and graphics settings pipeline should expose renderer controls that affect packaged runtime behavior.

Examples:

- default depth prepass mode
- shadow quality tier
- HDR default
- post-processing tier
- light budget tier

These settings should be staged into runtime-facing build data in the same way other platform settings are persisted and packaged today.

The player should not rely on editor-only settings files at runtime.

## Current Foundation Alignment

The first implementation slice should land these concrete foundations:

- shared render-frame extraction and backend capability contracts
- authored light components and per-camera render settings
- platform-staged renderer defaults for Windows
- DirectX 11 forward pass planning and light budgeting
- compact runtime material feature flags for Windows-forward PBR
- shadow-resource planning shells and post-process chain scaffolding

This keeps the renderer useful immediately while leaving the heavier execution work, richer batching behavior, and future deferred path on top of stable contracts.

## Testing and Validation

The implementation should add shared tests for:

- camera render settings serialization
- backend capability resolution
- light prioritization
- batching eligibility decisions
- compatibility downgrade selection

The Windows renderer should add tests for:

- pass planning
- shadow resource planning
- material feature binding
- post-process chain ordering
- instancing and batching planner behavior

Smoke scenes should cover:

- multiple light types together
- all three shadow-casting light families
- normal-mapped physically based materials
- repeated meshes for batching and instancing
- post-processing enabled and disabled

## Recommended Rollout

Implementation should proceed in this order:

1. shared render frame contracts and `RenderManager3D` expansion
2. engine light objects and camera render settings
3. Windows pass planner and resource model
4. forward opaque and transparent lighting path
5. shadow systems for directional, spot, and point lights
6. batching and instancing systems
7. post-processing chain
8. compatibility rules for reduced backends
9. deferred renderer follow-up later

This keeps the Windows slice useful quickly while protecting the future backend story.

## References

- Riemer archive overview: `https://github.com/simondarksidej/XNAGameStudio/wiki/RiemersArchiveOverview`
- Direct3D 9 HLSL overview: `https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-writing-shaders-9`
- Direct3D 9 pixel shader 2.0 limits: `https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-2-0`
- Direct3D 9 pixel shader differences: `https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-differences`
- Shader model 3 reference: `https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/shader-model-3`

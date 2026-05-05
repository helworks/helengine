# PS2 Renderer Architecture Design

## Summary

This document defines the end-to-end PlayStation 2 renderer architecture for HelEngine.

The PS2 target should keep the transpiled `helengine.core` runtime as the authoritative gameplay world while giving the PS2 player full ownership over rendering. The PS2 backend should not be forced through Windows-oriented rendering assumptions. Instead, it should compile PS2-native meshes, textures, materials, object policies, and scene metadata through the builder pipeline, then execute a custom DMA/VIF/VU1/GS renderer against a PS2-owned render cache that mirrors the live gameplay world.

The architecture must support real PS2 hardware as the target platform while still allowing ambitious expensive modes for tightly constrained scenes. Small showcase scenes should be able to push the highest-quality paths the engine can sustain on hardware. Larger scenes should be able to scale down explicitly through renderer-family limits, material schema limits, scene policy, and object policy.

## Goals

- Build an end-to-end PS2 rendering architecture instead of a thin runtime backend.
- Keep gameplay, physics, scripts, components, and scene ownership in the transpiled `helengine.core` runtime.
- Give the PS2 player full control over rendering representation and execution.
- Support deeply customized PS2-only material, mesh, texture, and object policies.
- Make renderer family selection an explicit platform option.
- Let the selected renderer family determine which PS2 material schemas and render features are valid.
- Support fully dynamic runtime scene changes as a first-class requirement.
- Preserve and exploit static-versus-dynamic classification through `Entity.Static`.
- Leave room for multiple renderer families later, including stylized families such as toon rendering.

## Non-Goals

- No attempt to preserve one universal material meaning across all platforms.
- No requirement that PS2 materials map directly to Windows shader concepts.
- No requirement that the first PS2 slice delivers final high-end lighting, final shadows, or final skinned-character execution.
- No requirement that the initial PS2 backend remain architecturally dependent on gsKit beyond bootstrap usefulness.
- No attempt to make all scenes run at the same quality tier.

## Current Foundation

The current HelEngine repository already contains shared rendering contracts that are useful for a PS2 backend:

- shared light components
- `CameraRenderSettings`
- shared render-frame extraction contracts
- shared render-plan concepts
- explicit per-platform material settings work

The current `helengine-ps2` repository is still a bootstrap host rather than a renderer architecture:

- `Ps2BootHost.cpp` owns a basic gsKit-based bring-up path
- `Ps2RenderManager3D` is still a stub
- `Ps2PlatformAssetBuilder.CookMaterial(...)` is not implemented

This design therefore treats the current PS2 code as a valid boot foundation, but not as the target renderer architecture.

## Core Principle

The engine should separate simulation ownership from rendering ownership.

The transpiled `helengine.core` runtime remains the authoritative gameplay world:

- entities
- components
- transforms
- scene loading
- physics
- scripts and update logic
- per-platform component settings

The PS2 player owns rendering completely:

- render asset formats
- render object policy
- render proxy caching
- renderer-family feature set
- pass planning
- DMA/VIF/VU1/GS execution

This same principle should apply to all players in the future. Each player may maintain its own renderer-owned render representation derived from the gameplay world. That allows HelEngine to support multiple renderer styles later, including radically different looks such as stylized toon pipelines, without making gameplay systems renderer-specific.

## Runtime Boundary

The PS2 runtime should contain two cooperating domains.

## Gameplay Domain

The gameplay domain is the transpiled `helengine.core` runtime. It owns the live scene and remains the source of truth for:

- entity hierarchy
- component state
- transform state
- camera and light components
- physics results
- runtime spawning and destruction
- per-platform component and material settings

## Render Domain

The render domain is a PS2-native subsystem. It does not own gameplay objects. It derives a PS2-specific rendering representation from the gameplay world and executes rendering from that representation.

The render domain should:

- load PS2-native cooked render artifacts
- build and maintain PS2 render proxies for live objects
- keep static and dynamic render paths distinct
- plan per-frame rendering based on the active renderer family
- execute custom PS2 hardware work

The rule is:

- shared world for simulation
- platform-owned world for rendering

## Major Subsystems

The PS2 renderer should be decomposed into five owned layers.

## 1. PS2 Authoring Layer

The authoring layer exposes PS2-native rendering intent through platform settings and builder-published schemas.

It should allow PS2-only concepts such as:

- unlit
- simple lit
- expensive showcase lit
- toon or stylized families later
- opaque
- alpha-test
- additive
- soft transparent
- static world chunk
- dynamic prop
- hero character
- billboard or effect mesh
- texture export policy
- CLUT policy
- VRAM residency hint
- shadow caster and receiver modes
- expensive-mode eligibility

This layer is not responsible for preserving Windows material semantics. It is responsible for expressing valid PS2 authoring intent.

## 2. PS2 Cook and Build Layer

The build layer compiles authored content into PS2-native runtime payloads.

It should:

- choose PS2-specific mesh representations
- quantize and pack vertex data
- export textures into PS2-target formats and palette layouts
- compile materials into exact PS2 render records
- compile object render policy into runtime-facing metadata
- prebucket static content where useful
- publish diagnostics that explain routing, downgrades, and invalid combinations

Most downgrade and specialization behavior belongs here rather than being improvised at runtime.

## 3. PS2 Runtime Sync Layer

This layer bridges the gameplay world and the render domain.

It should:

- create and destroy render proxies as entities appear and disappear
- watch component add and remove operations
- track transform, visibility, layer, mesh, and material changes
- distinguish static entities from dynamic entities
- rebuild only the affected PS2 render data when mutations occur

This is the key layer that keeps fully dynamic scenes compatible with a specialized PS2 renderer.

## 4. PS2 Render Orchestration Layer

This layer builds the frame plan for the active renderer family.

It should decide:

- visible sets
- pass order
- per-object lighting path
- per-frame shadow strategy
- high-end versus cheap path routing
- packet generation strategy
- texture residency work for the frame

This is the primary scalability layer.

## 5. PS2 Execution Layer

This is the hardware-facing backend.

It should own:

- GIF and DMA packet builders
- VIF upload builders
- VU1 microprogram management
- GS state encoding
- texture upload logic
- frame and shadow target management
- per-pass execution

This layer should consume already-specialized PS2 render data. It should not be responsible for editor concepts or generic asset interpretation.

## PS2 Render Data Model

The PS2 build should emit explicit PS2 render artifact families instead of trying to reuse generic runtime representations directly.

## 1. PS2 Mesh Artifacts

PS2 mesh artifacts should support multiple PS2-native geometry classes, such as:

- static world meshes
- dynamic rigid meshes
- skinned meshes
- billboard or effect meshes

Each artifact may vary by section and should support choices such as:

- strip or list organization
- quantized vertex layouts
- VU-friendly packing
- optional prebuilt upload blocks for static content

## 2. PS2 Texture Artifacts

PS2 texture artifacts should include PS2-specific export decisions, such as:

- pixel storage mode
- CLUT policy
- mip policy
- swizzle or layout policy
- permanent or streamed residency classification
- alternate encodes for expensive versus budget paths

Textures should be authored and built as PS2 assets, not treated as generic images copied forward.

## 3. PS2 Material Artifacts

PS2 material artifacts should be pure PS2 render records.

They should resolve exact runtime policy such as:

- render class
- lighting model
- texture usage
- alpha test, blend, and depth behavior
- shadow participation
- VU program family
- expensive-mode eligibility
- fallback chain

The PS2 target should not pretend that desktop-style freeform shader selection exists. On PS2, the material chooses one PS2 execution schema inside the active renderer family.

## 4. PS2 Render Object Artifacts

Mesh and material data are not enough. The builder also needs per-object PS2 render policy.

Object artifacts should capture:

- static or dynamic class
- world chunk, prop, character, foliage, effect, sky, or other render class
- culling behavior
- sort bucket
- shadow flags
- batching eligibility
- alternate renderer-family eligibility

This is where the engine gains deep per-object control over what each platform receives.

## Runtime Render Proxies

At runtime, the PS2 renderer should mirror gameplay objects into PS2-side render proxies that reference the cooked PS2 artifacts.

Each proxy should contain:

- resolved mesh artifact reference
- resolved material artifact reference
- resolved object policy
- current transform state
- current visibility and layer state
- static or dynamic classification
- renderer-family-specific execution metadata

The frame planner should operate on PS2 render proxies rather than raw gameplay components.

## Platform Definition and Builder Contract

The PS2 platform definition should become a real rendering control surface rather than a minimal package descriptor.

The editor should learn what the PS2 platform can author from the platform definition and the builder-published schemas. The builder should learn exactly what the scene expects the PS2 runtime to do from the same metadata.

The platform definition should grow controls in five groups.

## 1. Renderer Family and Quality Policy

The platform should be able to expose multiple PS2 renderer families later, not just one framebuffer backend.

Examples include:

- standard lit renderer
- tiny-scene showcase renderer
- stylized or toon renderer
- debug or validation renderer

Each family may own distinct:

- material schemas
- lighting paths
- shadow techniques
- texture constraints
- mesh constraints
- downgrade rules

## 2. Scene-Level PS2 Build Policy

Scenes should be able to declare PS2-facing build intent.

Examples:

- tiny showcase
- gameplay room
- outdoor zone
- effects-heavy scene
- static-world aggressiveness
- dynamic-light budget tier
- shadow allowance
- transparency budget
- texture residency strategy
- expensive hero-object allowance

This gives the builder permission to specialize scenes instead of compiling every PS2 scene identically.

## 3. Object-Level PS2 Render Policy

Per-platform component settings should allow per-object PS2 overrides such as:

- static world chunk versus dynamic actor
- hero object priority
- shadow caster and receiver policy
- forced unlit, simple, or expensive path
- forced cheap fallback
- culling hints
- alternate PS2 mesh selection

This is a direct consequence of HelEngine targeting many hardware classes and allowing deep per-platform customization.

## 4. Asset-Level PS2 Specialization

Models, textures, and materials should all be allowed to diverge strongly on PS2.

The builder should be able to choose:

- different PS2 mesh source or submesh selection
- different PS2 texture export recipe
- different PS2 material schema
- different LOD and packing strategy
- optional PS2-specific asset replacement

The build system should not assume that each platform merely transforms the same raw content in a shallow way.

## 5. Builder Diagnostics and Previewability

The platform cannot expose deep control unless authors receive deep feedback.

The PS2 builder should report:

- why an object was routed to a cheaper or richer path
- why a material is invalid for the selected renderer family
- when a scene exceeds the chosen PS2 policy budget
- which assets are using expensive hero paths
- what fallback chain will be used

## Renderer Family Selection

Renderer selection should be an explicit platform option.

The correct model is:

- platform
- renderer family
- material schema set

not:

- platform
- one giant universal material system

For PS2, the selected renderer family should determine:

- which material schemas are valid
- which object render classes are valid
- which lighting paths exist
- which shadow features exist
- which mesh classes are supported
- which texture policies are legal

The editor should filter PS2 authoring UI based on the active renderer family. The builder should validate PS2 materials and objects against the selected renderer family. Invalid combinations should fail clearly.

Examples of future renderer families might include:

- `ps2-standard-forward`
- `ps2-showcase-forward`
- `ps2-toon`

Each family can own distinct material schemas instead of pretending that every PS2 material option is valid everywhere.

## Runtime Sync and Static Versus Dynamic Flow

The PS2 runtime should not render directly from raw gameplay components every frame.

Instead, the runtime sync layer should maintain PS2 render proxies and react to mutations such as:

- entity creation
- entity destruction
- component add and remove
- transform changes
- material assignment changes
- mesh assignment changes
- visibility changes
- layer changes
- platform-specific render-setting changes

`Entity.Static` should be treated as a primary routing signal.

## Static Entities

Static entities should prefer long-lived PS2 render structures such as:

- prebucketed static render groups
- prebuilt DMA or VIF packet templates where safe
- stable texture residency assumptions
- more aggressive batching and preprocessing

## Dynamic Entities

Dynamic entities should use lighter-weight live render structures such as:

- fast transform updates
- narrower rebuild scope
- dynamic bucket membership
- runtime packet assembly where needed
- looser residency guarantees

The engine must support fully dynamic scenes, but it should not treat the entire world as dynamic by default. Static content should keep the benefits of specialization and reuse.

## Frame Planning and Scaling

The PS2 renderer should be planner-driven.

Each frame, the active renderer family should build a PS2 frame plan from the current PS2 render proxies. That plan should decide:

- visible proxy set
- pass order
- light treatment per object class
- whether shadows are active
- which objects use expensive versus cheap paths
- packet generation strategy
- texture residency work for the frame

This is the core scalability mechanism. The same scene may run at different effective quality levels depending on the selected renderer family, scene policy, and live runtime pressure.

## Pass Families

The PS2 renderer should model explicit pass families rather than one monolithic draw loop.

Examples:

- depth or mask-prep style passes where relevant
- opaque world pass
- opaque dynamic pass
- alpha-test pass
- translucent or effects pass
- shadow-generation or shadow-projection pass
- UI and overlay pass

Not every renderer family needs every pass.

## Hardware Execution Direction

The long-term PS2 backend should own:

- texture upload and VRAM allocation
- mesh and VIF upload assembly
- DMA and GIF packet assembly
- VU1 microprogram registry and dispatch
- GS state encoding
- per-pass execution

gsKit may remain useful during early bring-up, but it should not remain the architecture boundary for the final renderer.

## Recommended First Implementation Boundary

The first real PS2 renderer slice should establish the architecture rather than chasing final visual ambition immediately.

It should land:

- PS2 renderer family selection as a platform option
- PS2 material schemas filtered by renderer family
- PS2 mesh, texture, material, and object cooked artifact types
- PS2 render proxy system with static and dynamic routing
- planner-driven PS2 frame model
- one real custom PS2 opaque path
- one cheap lighting family, likely unlit plus simple lit
- builder diagnostics that explain routing and invalid combinations

It should explicitly not require:

- final high-end lighting model
- final shadow system
- final skinned-character execution path
- final toon renderer
- final VRAM streaming sophistication

Those belong in later slices built on top of stable contracts.

## Recommended Rollout

Implementation should proceed in this order:

1. expand PS2 platform definition and renderer-family metadata
2. define PS2-native material, mesh, texture, and object artifact contracts
3. implement builder-side PS2 cooking and diagnostics
4. implement PS2 runtime render proxies and static-dynamic sync
5. implement planner-driven PS2 opaque execution with unlit and simple-lit paths
6. add richer lighting and shadow slices
7. add alternate renderer families such as stylized or toon pipelines

This sequence keeps the foundation honest and avoids locking the engine into a fake shared rendering model that later blocks PS2-specific work.

## Testing and Validation Requirements

Implementation should include coverage for:

- renderer-family filtering in the platform definition and editor
- PS2 material-schema validation against renderer families
- PS2 artifact cooking for meshes, textures, materials, and object policies
- static versus dynamic render-proxy routing
- runtime sync behavior when live entities mutate
- frame-planning decisions for selected renderer families
- builder diagnostics for invalid and downgraded content

The PS2 project should also accumulate curated validation scenes that deliberately exercise:

- tiny showcase scenes
- dynamic gameplay scenes
- transparency-heavy scenes
- hero-object expensive modes
- forced cheap fallback scenes

## Recommendation

HelEngine should adopt a PS2 architecture where the transpiled gameplay runtime remains authoritative, but the PS2 player owns rendering fully through PS2-native authoring, PS2-native cooked artifacts, PS2-side render proxies, renderer-family-driven planning, and custom PS2 hardware execution.

This gives the engine what it actually needs:

- full control per player
- full PS2 specialization
- dynamic-scene support
- strong static-scene optimization
- room for multiple future renderer styles
- no requirement to force low-end console rendering through Windows-first assumptions

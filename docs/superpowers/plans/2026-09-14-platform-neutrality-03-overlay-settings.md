# Platform-configured performance overlay Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Replace the PS2 and 3DS checks inside FPSComponent with explicit runtime settings.

**Architecture:** Core owns neutral overlay settings; platform startup supplies values. Preserve authored FontScale separately from the effective platform multiplier.

**Tech Stack:** C#/.NET 9, xUnit, existing engine and platform builder contracts.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Create: engine/helengine.core/PerformanceOverlaySettings.cs.
- Modify: engine/helengine.core/CoreInitializationOptions.cs, Core.cs, components/2d/FPSComponent.cs.
- Modify: engine/helengine.editor.tests/FPSComponentTests.cs.
- Modify externally: C:/dev/helworks/helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs and C:/dev/helworks/helengine-3ds/builder/Nintendo3DsPlatformDefinitionFactory.cs; follow their startup-generation callers.
- Create externally: builder/PerformanceOverlayInitializationEmitter.cs in each of those two platform repositories if no existing startup emitter owns these settings. Reuse the existing owner when present instead of duplicating it.

### Task 1: Define explicit overlay settings

**Interface:** PerformanceOverlaySettings(float fontScaleMultiplier, bool textShadowEnabled) exposes immutable FontScaleMultiplier and TextShadowEnabled. Reject non-positive or non-finite multipliers. CoreInitializationOptions.PerformanceOverlay receives the settings; Core exposes the initialized value. Missing optional overlay settings preserve the generic current defaults (1f, true), not a missing renderer or required core configuration.

- [ ] Add validation tests and characterize current generic, ps2 and 3ds effective overlay output.
- [ ] Add regression assertions:
```csharp
var settings = new PerformanceOverlaySettings(0.5f, false);
Assert.Equal(0.5f, settings.FontScaleMultiplier);
Assert.False(settings.TextShadowEnabled);
Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceOverlaySettings(0f, true));
```
- [ ] Wire settings into Core initialization without changing serialized FPS fields. For detached components, keep the existing authored FontScale behavior.
- [ ] Run `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore -m:1 --filter FullyQualifiedName~FPSComponentTests`; commit `feat: configure runtime performance overlay policy`.

### Task 2: Migrate component behavior and platform initialization together

- [ ] Replace the platform-name branches with the following behavior:
```csharp
float effectiveScale = FontScale * settings.FontScaleMultiplier;
float2 shadowOffset = settings.TextShadowEnabled ? new float2(-1f, -1f) : new float2(0f, 0f);
byte4 shadowColor = settings.TextShadowEnabled ? new byte4(0, 0, 0, 255) : new byte4(0, 0, 0, 0);
```
Resolve settings from the owning initialized core; retain existing row layout and late-font assignment logic.
- [ ] Platform startup supplies (1f, false) for PS2 and (0.5f, true) for 3DS. Other runtimes use generic defaults unless they explicitly configure different values.
- [ ] Add cross-identity tests: a platform named ps2 with generic settings behaves generically; an arbitrary name with PS2 settings disables shadows. Serialize/reload and confirm FontScale remains authored.
- [ ] Update C# startup emission and source staging for generated C++; never edit generated output. Confirm constructors/properties are supported by the code generator.
- [ ] Run FPSComponentTests plus each modified platform builder's startup-generation tests. Smoke-test overlay layout on PS2 and 3DS with the existing demo fixture.
- [ ] Remove Nintendo3DsPerformanceOverlayScale and both platform checks. Commit engine and platform startup updates separately with matched version requirements.

## Completion

Platform identity cannot alter FPSComponent behavior by itself. Existing PS2 shadow suppression and 3DS scaling remain visible through explicitly installed settings.

# Core Animation Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable keyframe-based `AnimationClip` asset and a low-level `AnimationPlayerComponent` in `helengine.core` for transform animation playback.

**Architecture:** The implementation is split into two layers. First, `helengine.core` gains strongly typed animation asset data plus binary serialization support. Second, a compact `AnimationPlayerComponent` evaluates one active clip at a time into entity transforms using fixed transform channels and no reflection. Tests live in `helengine.editor.tests` because that is the current cross-core verification home for serializer and component behavior.

**Tech Stack:** C#/.NET 9, `helengine.core` asset serialization, `UpdateComponent`, xUnit, `rtk dotnet test`, `rtk dotnet build`

---

## File Structure

### New production files

- `engine/helengine.core/assets/raw/animation/AnimationClipAsset.cs`
  - top-level raw animation asset model containing duration and typed track collections
- `engine/helengine.core/assets/raw/animation/AnimationInterpolationMode.cs`
  - interpolation enum for keyframe evaluation
- `engine/helengine.core/assets/raw/animation/PositionKeyframeTrackAsset.cs`
  - absolute position keyframe track
- `engine/helengine.core/assets/raw/animation/PositionOffsetKeyframeTrackAsset.cs`
  - additive position keyframe track
- `engine/helengine.core/assets/raw/animation/ScaleKeyframeTrackAsset.cs`
  - absolute scale keyframe track
- `engine/helengine.core/assets/raw/animation/RotationKeyframeTrackAsset.cs`
  - absolute rotation keyframe track
- `engine/helengine.core/assets/raw/animation/PositionKeyframeAsset.cs`
  - plain keyframe data for `float3`
- `engine/helengine.core/assets/raw/animation/RotationKeyframeAsset.cs`
  - plain keyframe data for `float4`
- `engine/helengine.core/assets/raw/animation/AnimationClipRuntimeEvaluator.cs`
  - static typed interpolation/evaluation helpers for clip playback
- `engine/helengine.core/components/AnimationPlayerComponent.cs`
  - low-level playback component with `Play`, `Stop`, `Pause`, `Resume`, `Seek`
- `engine/helengine.core/components/AnimationPlayerUpdateComponent.cs`
  - update driver for animation playback, if a dedicated helper is cleaner than embedding update logic directly

### Modified production files

- `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  - add `AnimationClipAsset` binary read/write support
- `engine/helengine.core/assets/AssetSerializer.cs`
  - register animation clip serialization when required by the existing asset dispatch
- `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
  - add any new value kinds required for animation track serialization
- `engine/helengine.core/serialization/EditorBinaryRecordKind.cs`
  - add record kinds for animation clip/track/keyframe structures if the serializer uses explicit record ids
- `engine/helengine.core/components/UpdateComponent.cs`
  - only if needed to align update behavior for the new player/update helper with the existing component model

### New test files

- `engine/helengine.editor.tests/AnimationClipSerializationTests.cs`
  - focused serializer round-trip tests for animation clips and typed tracks
- `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs`
  - runtime playback tests for interpolation, offset behavior, looping, seeking, and stop/reset behavior

### Existing test files that may be modified instead of creating new ones

- `engine/helengine.editor.tests/BinarySerializationTests.cs`
  - only if the repo prefers keeping asset serializer tests in the existing large serializer suite instead of splitting

Preferred direction: create `AnimationClipSerializationTests.cs` and `AnimationPlayerComponentTests.cs` as focused files unless the existing serializer suite already has unavoidable shared helpers that make a split impractical.

---

### Task 1: Add Raw Animation Asset Types

**Files:**
- Create: `engine/helengine.core/assets/raw/animation/AnimationClipAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/AnimationInterpolationMode.cs`
- Create: `engine/helengine.core/assets/raw/animation/PositionKeyframeTrackAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/PositionOffsetKeyframeTrackAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/ScaleKeyframeTrackAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/RotationKeyframeTrackAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/PositionKeyframeAsset.cs`
- Create: `engine/helengine.core/assets/raw/animation/RotationKeyframeAsset.cs`
- Test: `engine/helengine.editor.tests/AnimationClipSerializationTests.cs`

- [ ] **Step 1: Write the failing serialization-shape test**

Add a new test class in `engine/helengine.editor.tests/AnimationClipSerializationTests.cs` with a first test that constructs an `AnimationClipAsset` containing:
- `Duration = 1.0f`
- one absolute position track with two keyframes
- one offset position track with two keyframes
- one absolute scale track with two keyframes
- one absolute rotation track with two keyframes

The first test should assert that the constructed clip exposes all typed track collections with the expected counts and values after round-tripping through the serializer later.

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipSerializationTests" -v minimal
```

Expected: FAIL because the animation asset types do not exist yet.

- [ ] **Step 3: Add the minimal raw animation asset types**

Implement the raw asset classes as plain data models only.

Required shape:

- `AnimationClipAsset`
  - clip name or identifier
  - `Duration`
  - `List<PositionKeyframeTrackAsset> PositionTracks`
  - `List<PositionOffsetKeyframeTrackAsset> PositionOffsetTracks`
  - `List<ScaleKeyframeTrackAsset> ScaleTracks`
  - `List<RotationKeyframeTrackAsset> RotationTracks`
- keyframe assets with:
  - `Time`
  - `Value`
  - `InterpolationMode`

Keep the models explicit and reflection-free. Add substantive XML comments to every class, property, and constructor.

- [ ] **Step 4: Re-run the focused test**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipSerializationTests" -v minimal
```

Expected: still FAIL, but now for missing serializer support rather than missing types.

- [ ] **Step 5: Commit the raw asset type scaffolding**

```bash
git add engine/helengine.core/assets/raw/animation engine/helengine.editor.tests/AnimationClipSerializationTests.cs
git commit -m "Add core animation clip asset models"
```

---

### Task 2: Add Binary Serialization Support For Animation Clips

**Files:**
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/AssetSerializer.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
- Modify: `engine/helengine.core/serialization/EditorBinaryRecordKind.cs`
- Test: `engine/helengine.editor.tests/AnimationClipSerializationTests.cs`

- [ ] **Step 1: Extend the failing test into a real round-trip**

In `AnimationClipSerializationTests`, add or refine tests so they verify:
- clip duration survives round-trip
- each typed track survives round-trip
- keyframe times survive round-trip
- interpolation mode survives round-trip
- `float3` and `float4` values survive round-trip

- [ ] **Step 2: Run the focused serializer test and verify it fails for the expected reason**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipSerializationTests" -v minimal
```

Expected: FAIL because `AnimationClipAsset` is not handled by the serializer.

- [ ] **Step 3: Add the minimal serializer implementation**

Update the serializer pipeline to support `AnimationClipAsset` and its typed track/keyframe payloads.

Implementation rules:
- keep the binary shape explicit
- do not introduce reflection-based field walking
- add only the record/value kinds needed by the animation asset
- reuse existing `float3`/`float4` binary helpers where available

- [ ] **Step 4: Re-run the serializer test**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipSerializationTests" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the serialization support**

```bash
git add engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.core/assets/AssetSerializer.cs engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.core/serialization/EditorBinaryRecordKind.cs engine/helengine.editor.tests/AnimationClipSerializationTests.cs
git commit -m "Add animation clip asset serialization"
```

---

### Task 3: Add Runtime Clip Evaluation Helpers

**Files:**
- Create: `engine/helengine.core/assets/raw/animation/AnimationClipRuntimeEvaluator.cs`
- Test: `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs`

- [ ] **Step 1: Write the failing evaluator tests**

Create `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs` with focused tests for pure evaluation behavior:
- linear interpolation between two position keyframes at `0.5f`
- step interpolation returning the previous keyframe value before the next key time
- rotation keyframe evaluation using direct `float4` interpolation rules selected for this first slice

These tests can start by calling a planned static helper if that is the simplest way to pin down deterministic math before component wiring.

- [ ] **Step 2: Run the focused evaluator tests and verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationPlayerComponentTests" -v minimal
```

Expected: FAIL because the evaluator helper does not exist yet.

- [ ] **Step 3: Implement the minimal runtime evaluator**

Add a focused helper that:
- finds the correct keyframe pair for a time value
- handles exact keyframe hits
- supports `Step` and `Linear`
- returns evaluated `float3` or `float4` values

Keep it allocation-free in the steady state. Avoid LINQ and avoid building temporary collections during evaluation.

- [ ] **Step 4: Re-run the evaluator tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationPlayerComponentTests" -v minimal
```

Expected: some tests PASS, component playback tests still FAIL because the runtime player does not exist yet.

- [ ] **Step 5: Commit the evaluator helper**

```bash
git add engine/helengine.core/assets/raw/animation/AnimationClipRuntimeEvaluator.cs engine/helengine.editor.tests/AnimationPlayerComponentTests.cs
git commit -m "Add animation clip runtime evaluator"
```

---

### Task 4: Add AnimationPlayerComponent Playback

**Files:**
- Create: `engine/helengine.core/components/AnimationPlayerComponent.cs`
- Create: `engine/helengine.core/components/AnimationPlayerUpdateComponent.cs`
- Test: `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs`

- [ ] **Step 1: Expand the failing runtime tests**

In `AnimationPlayerComponentTests`, add tests covering:
- `Play(clip, loop: false)` moving position over time
- offset position adding on top of an entity’s existing position
- `Pause()` preventing time advancement
- `Resume()` continuing time advancement
- `Stop()` clearing playback state
- `Seek(time)` immediately updating the evaluated transform
- looping wrapping playback time

Use a real `EditorEntity` plus the new component instead of mocking playback behavior.

- [ ] **Step 2: Run the playback tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationPlayerComponentTests" -v minimal
```

Expected: FAIL because the player component does not exist or does not yet advance transforms.

- [ ] **Step 3: Implement the minimal playback component**

Implementation requirements:
- one active clip at a time
- fixed internal transform channel state
- explicit methods:
  - `Play`
  - `Stop`
  - `Pause`
  - `Resume`
  - `Seek`
- deterministic update path
- no clip stack
- no blend tree
- no reflection

If a dedicated `AnimationPlayerUpdateComponent` keeps the runtime model cleaner and more consistent with existing update patterns, add it. Otherwise keep the update logic directly on the component if that is simpler and still focused.

- [ ] **Step 4: Re-run the playback tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationPlayerComponentTests" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the runtime player**

```bash
git add engine/helengine.core/components/AnimationPlayerComponent.cs engine/helengine.core/components/AnimationPlayerUpdateComponent.cs engine/helengine.editor.tests/AnimationPlayerComponentTests.cs
git commit -m "Add core animation player component"
```

---

### Task 5: Verify Integration And Keep Existing UI Work Green

**Files:**
- Verify: `engine/helengine.editor.tests/AnimationClipSerializationTests.cs`
- Verify: `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs`
- Verify: `engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs`
- Verify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Verify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Run the new animation-focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipSerializationTests|FullyQualifiedName~AnimationPlayerComponentTests" -v minimal
```

Expected: PASS

- [ ] **Step 2: Run the adjacent UI regression slice**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests|FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~TextBoxComponentKeyboardFocusTests" -v minimal
```

Expected: PASS

- [ ] **Step 3: Run the core editor build**

Run:

```bash
rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal
```

Expected: `0 errors, 0 warnings`

- [ ] **Step 4: Commit the verification-complete state**

```bash
git add engine/helengine.core engine/helengine.editor.tests
git commit -m "Complete core animation foundation"
```

---

## Notes For The Implementer

- Follow the repository rule of one class per file.
- Add substantive XML comments to every new class, property, method, and constructor.
- Keep runtime evaluation explicit and allocation-light.
- Do not add controller/FSM abstractions in this slice.
- Do not add skeletal animation support in this slice.
- Do not wire the build-dialog shake effect yet unless the task is explicitly expanded; this plan is only the foundation.

## Plan Review

I did not dispatch the plan-review subagent loop because you have not asked for delegated/subagent work in this session.

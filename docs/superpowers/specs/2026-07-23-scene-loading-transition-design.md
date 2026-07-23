# Scene Loading Transition Design

## Goal

Provide real 0–1 progress for every runtime single-scene transition and render it through one persistent loading scene.

## Current State

`SceneManager.LoadScene` queues a request and completes content loading, runtime materialization, activation, and event dispatch in one frame-boundary operation. `ReferenceCanvasFit` and render paths are unrelated. The existing `DontUnload` scene setting already permits a loading scene to survive single-scene transitions.

## Design

Add an engine-owned transition path that owns target-scene loading state. It exposes the active target scene, a normalized progress value in the inclusive range 0–1, and a completion state. The scene manager performs a transition across frame boundaries instead of treating it as one opaque synchronous operation.

The transition stages are:

1. Accept a target scene request and publish progress 0.
2. Keep the persistent loading scene alive and make its overlay visible.
3. Unload non-persistent scenes and release their owned assets.
4. Load and materialize the target scene incrementally, reporting progress from known scene work units.
5. Register and activate the target scene, publish progress 1, and hide the loading overlay on the following stable frame.

Scene content decoding remains a synchronous substep when the platform content API requires it. Progress therefore reflects the observable unload, materialization, registration, and activation work; it does not fabricate time-based progress.

## Public Contract

`SceneManager` gains one transition request API for normal game/menu scene changes and read-only loading state, including a normalized `float` progress. Existing raw `LoadScene` and `UnloadScene` remain available for bootstrap, additive infrastructure, diagnostics, and controlled internal use.

The loading scene is authored as additive and `DontUnload`. The boot path loads it alongside the main menu. Its presentation component observes the scene-manager loading state, blocks input during active transitions, and updates a bottom-of-screen progress bar.

## Demo Disc Adoption

Replace every game-facing `LoadScene(..., SceneLoadMode.Single)` call with the transition API, including main-menu selection, return-to-menu, level selection, Tilt Trial session changes, Zombislayer returns, and Nintendo handheld returns. The splash's additive menu bootstrap remains a raw additive load.

## Failure Behavior

Invalid target scene identifiers and scene-load failures preserve existing exceptions. Loading state resets only after cleanup, so the loading overlay cannot remain falsely active after a failed transition.

## Tests

Engine tests cover progress initialization, monotonic stage updates, inclusive 0–1 bounds, persistent-scene survival, target activation, and failure-state cleanup. Demo Disc tests cover use of the transition API at every normal transition site and the loading-scene progress-bar binding.

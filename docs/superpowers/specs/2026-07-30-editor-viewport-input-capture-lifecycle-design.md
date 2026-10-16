# Editor Viewport Input-Capture Lifecycle Design

## Problem

Viewport picking and camera navigation both ask `EditorInputCaptureService` whether the pointer is blocked. The service stores blockers in static process-wide state. `EditorSession` resets keyboard focus at startup but does not reset input capture at startup or during disposal. A blocker retained from a prior session can therefore prevent all interactions inside a new viewport.

## Chosen Design

Treat input capture as editor-session-owned state. Reset `EditorInputCaptureService` immediately before the session initializes its core and again during session disposal. This removes stale owners before a new session registers its current UI regions and guarantees teardown does not leave process-wide capture state behind.

## Regression Coverage

Add a focused source-level lifecycle test that requires `EditorSession` to reset the input-capture service in both its constructor and `Dispose` method. This protects the session boundary without creating a second heavyweight editor-session harness.

## Scope

The change does not alter viewport hit testing, camera controls, UI blocker geometry, or existing dialog behavior.

# Demo Disc Baked Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the runtime-generated demo-disc menu host with a baked, previewable scene hierarchy that is rebuilt on demand in the editor and still works at runtime without provider/build code.

**Architecture:** Introduce `DemoMenuBuildComponent` as the authored root component, a `DemoMenuSceneBuildService` in the editor to rebuild the generated subtree from `MenuDefinition`, and small serializable menu metadata components that drive runtime navigation against the baked hierarchy. Remove `MenuHostComponent` and its persistence/runtime registration once the baked path is live.

**Tech Stack:** `helengine.core`, `helengine.editor`, scene persistence descriptors, runtime component deserializers, demo-disc scene writer, xUnit.

---

## File Map

- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Create: `engine/helengine.core/components/2d/menu/DemoMenuBuildComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/DemoMenuPanelComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/DemoMenuItemComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/DemoMenuSelectedDescriptionComponent.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeDemoMenuBuildComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeDemoMenuPanelComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeDemoMenuItemComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeDemoMenuSelectedDescriptionComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Create: `engine/helengine.editor/serialization/scene/DemoMenuBuildComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/DemoMenuPanelComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/DemoMenuItemComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/DemoMenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/RoundedRectComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs`
- Create: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildLayout.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Create/Modify: focused menu persistence/runtime tests under `engine/helengine.editor.tests/menu/` and `.../serialization/scene/`

## Execution Order

- [ ] Write failing tests for baked scene writer output and scene-load/runtime baked menu behavior.
- [ ] Implement persistence descriptors and runtime deserializers for baked menu metadata plus rounded-rect visuals.
- [ ] Implement `DemoMenuSceneBuildService` and switch the writer to emit `DemoMenuBuildComponent` plus baked child hierarchy.
- [ ] Remove `MenuHostComponent` registration and migrate tests from host-based expectations to baked-scene expectations.
- [ ] Run focused verification for writer, scene-load, and runtime menu navigation.

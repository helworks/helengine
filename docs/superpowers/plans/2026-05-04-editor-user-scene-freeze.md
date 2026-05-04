# Editor User Scene Freeze Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Freeze user-authored scene updates and authored scene interactivity inside the editor while keeping editor-owned tools and UI responsive, without modifying `helengine.core`.

**Architecture:** Add one editor-only suppression service that scans user scene roots and removes user-owned `IUpdateable` and `IInteractable2D` registrations from the live `ObjectManager` before each editor update. Then wire `EditorSession.Update()` to invoke that service before `core.Update()`, and prove the behavior with direct service tests plus one editor-session integration test that checks ordering.

**Tech Stack:** C# / .NET 9, HelEngine editor/runtime libraries, xUnit, existing editor test harnesses in `engine/helengine.editor.tests`

---

## File Structure

### Editor suppression service

- Create: `engine/helengine.editor/managers/scene/EditorSceneRuntimeSuppressionService.cs`
  - Editor-only service that walks user-authored scene roots and removes user-scene `IUpdateable` and `IInteractable2D` registrations from the active object manager.

### Editor session integration

- Modify: `engine/helengine.editor/EditorSession.cs`
  - Add a suppression-service field and invoke it at the start of `Update()` so authored scene activity is frozen before `core.Update()` runs.

### Direct suppression tests

- Create: `engine/helengine.editor.tests/managers/scene/EditorSceneRuntimeSuppressionServiceTests.cs`
  - Validate that authored `FPSComponent` instances and authored UI interactables are removed, that descendant entities are traversed, and that editor-owned internal entities stay active.

### Editor update-order integration test

- Create: `engine/helengine.editor.tests/EditorSessionRuntimeSuppressionIntegrationTests.cs`
  - Validate that `EditorSession.Update()` suppresses user scene runtime activity before the main core update advances authored components.

## Task 1: Add Direct Suppression Tests

**Files:**
- Create: `engine/helengine.editor.tests/managers/scene/EditorSceneRuntimeSuppressionServiceTests.cs`

- [ ] **Step 1: Write the failing suppression tests**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies editor-side suppression removes user scene runtime activity without affecting editor infrastructure.
    /// </summary>
    public sealed class EditorSceneRuntimeSuppressionServiceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test core instance.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the lightweight core services required by suppression tests.
        /// </summary>
        public EditorSceneRuntimeSuppressionServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-scene-runtime-suppression-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
            Core.Instance.DefaultFontAsset = CreateFont();
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures authored FPS overlays are removed from the update list.
        /// </summary>
        [Fact]
        public void Suppress_WhenUserSceneContainsFpsComponent_RemovesItFromUpdateables() {
            EditorEntity sceneEntity = new EditorEntity {
                Name = "Scene Root",
                LayerMask = EditorLayerMasks.SceneObjects
            };
            FPSComponent fps = new FPSComponent();
            sceneEntity.AddComponent(fps);

            Assert.Contains(fps, Core.Instance.ObjectManager.Updateables);

            EditorSceneRuntimeSuppressionService service = new EditorSceneRuntimeSuppressionService();

            service.Suppress(Core.Instance.ObjectManager);

            Assert.DoesNotContain(fps, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Ensures authored UI interactables are removed even when they live on descendant helper entities.
        /// </summary>
        [Fact]
        public void Suppress_WhenUserSceneContainsDescendantButton_RemovesItsInteractableFromTheLiveList() {
            EditorEntity sceneRoot = new EditorEntity {
                Name = "Scene Root",
                LayerMask = EditorLayerMasks.SceneObjects
            };
            sceneRoot.InitChildren();

            Entity childEntity = new Entity();
            childEntity.InitComponents();
            childEntity.InitChildren();
            childEntity.LayerMask = EditorLayerMasks.SceneObjects;
            sceneRoot.AddChild(childEntity);

            ButtonComponent button = new ButtonComponent("Play", new int2(96, 32), CreateFont());
            childEntity.AddComponent(button);

            InteractableComponent interactable = Assert.Single(childEntity.Components.OfType<InteractableComponent>());
            Assert.Contains(interactable, Core.Instance.ObjectManager.Interactables);

            EditorSceneRuntimeSuppressionService service = new EditorSceneRuntimeSuppressionService();

            service.Suppress(Core.Instance.ObjectManager);

            Assert.DoesNotContain(interactable, Core.Instance.ObjectManager.Interactables);
        }

        /// <summary>
        /// Ensures internal editor entities keep their update registrations.
        /// </summary>
        [Fact]
        public void Suppress_WhenInternalEditorEntityOwnsAController_LeavesItRegistered() {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);
            EditorViewportCameraController controller = new EditorViewportCameraController(camera);
            cameraEntity.AddComponent(controller);

            Assert.Contains(controller, Core.Instance.ObjectManager.Updateables);

            EditorSceneRuntimeSuppressionService service = new EditorSceneRuntimeSuppressionService();

            service.Suppress(Core.Instance.ObjectManager);

            Assert.Contains(controller, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Ensures later-authored entities are also suppressed on the next reconcile pass.
        /// </summary>
        [Fact]
        public void Suppress_WhenNewUserSceneEntityAppearsAfterAnEarlierPass_RemovesItsRuntimeRegistrationsToo() {
            EditorSceneRuntimeSuppressionService service = new EditorSceneRuntimeSuppressionService();
            service.Suppress(Core.Instance.ObjectManager);

            EditorEntity sceneEntity = new EditorEntity {
                Name = "Late Scene Root",
                LayerMask = EditorLayerMasks.SceneObjects
            };
            FPSComponent fps = new FPSComponent();
            sceneEntity.AddComponent(fps);

            Assert.Contains(fps, Core.Instance.ObjectManager.Updateables);

            service.Suppress(Core.Instance.ObjectManager);

            Assert.DoesNotContain(fps, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Creates one deterministic font asset for UI and overlay tests.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSceneRuntimeSuppressionServiceTests -v minimal`

Expected: `FAIL` because `EditorSceneRuntimeSuppressionService` does not exist yet.

- [ ] **Step 3: Write the minimal suppression service**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Removes user-authored runtime activity from the live editor object graph while preserving editor infrastructure.
    /// </summary>
    public sealed class EditorSceneRuntimeSuppressionService {
        /// <summary>
        /// Removes user-scene updateables and interactables from the supplied object manager.
        /// </summary>
        /// <param name="objectManager">Live object manager owned by the editor core.</param>
        public void Suppress(ObjectManager objectManager) {
            if (objectManager == null) {
                throw new ArgumentNullException(nameof(objectManager));
            }

            List<EditorEntity> roots = CaptureUserSceneRoots(objectManager);
            for (int index = 0; index < roots.Count; index++) {
                SuppressEntityRecursive(roots[index], objectManager.Updateables, objectManager.Interactables);
            }
        }

        /// <summary>
        /// Captures the current root entities that belong to the authored scene.
        /// </summary>
        /// <param name="objectManager">Live object manager whose entities should be filtered.</param>
        /// <returns>User-authored scene roots.</returns>
        List<EditorEntity> CaptureUserSceneRoots(ObjectManager objectManager) {
            List<EditorEntity> roots = new List<EditorEntity>();
            for (int index = 0; index < objectManager.Entities.Count; index++) {
                if (objectManager.Entities[index] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!IsUserSceneRoot(editorEntity)) {
                    continue;
                }

                roots.Add(editorEntity);
            }

            return roots;
        }

        /// <summary>
        /// Returns true when the supplied entity is an authored scene root.
        /// </summary>
        /// <param name="editorEntity">Entity to classify.</param>
        /// <returns>True when the entity belongs to the user-authored scene.</returns>
        bool IsUserSceneRoot(EditorEntity editorEntity) {
            if (editorEntity == null) {
                return false;
            }
            if (editorEntity.InternalEntity) {
                return false;
            }
            if (editorEntity.Parent != null) {
                return false;
            }

            return editorEntity.LayerMask == EditorLayerMasks.SceneObjects;
        }

        /// <summary>
        /// Removes runtime registrations from one entity subtree.
        /// </summary>
        /// <param name="entity">Entity subtree to inspect.</param>
        /// <param name="updateables">Live update registration list.</param>
        /// <param name="interactables">Live interactable registration list.</param>
        void SuppressEntityRecursive(Entity entity, List<IUpdateable> updateables, List<IInteractable2D> interactables) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (updateables == null) {
                throw new ArgumentNullException(nameof(updateables));
            }
            if (interactables == null) {
                throw new ArgumentNullException(nameof(interactables));
            }

            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    Component component = entity.Components[index];
                    if (component is IUpdateable updateable) {
                        RemoveUpdateable(updateable, updateables);
                    }
                    if (component is IInteractable2D interactable) {
                        RemoveInteractable(interactable, interactables);
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                SuppressEntityRecursive(entity.Children[index], updateables, interactables);
            }
        }

        /// <summary>
        /// Removes all reference-equal instances of one updateable from the live update list.
        /// </summary>
        /// <param name="updateable">Updateable to remove.</param>
        /// <param name="updateables">Live update list.</param>
        static void RemoveUpdateable(IUpdateable updateable, List<IUpdateable> updateables) {
            for (int index = updateables.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(updateables[index], updateable)) {
                    updateables.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Removes all reference-equal instances of one interactable from the live interactable list.
        /// </summary>
        /// <param name="interactable">Interactable to remove.</param>
        /// <param name="interactables">Live interactable list.</param>
        static void RemoveInteractable(IInteractable2D interactable, List<IInteractable2D> interactables) {
            for (int index = interactables.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(interactables[index], interactable)) {
                    interactables.RemoveAt(index);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSceneRuntimeSuppressionServiceTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/scene/EditorSceneRuntimeSuppressionService.cs engine/helengine.editor.tests/managers/scene/EditorSceneRuntimeSuppressionServiceTests.cs
rtk git commit -m "feat: suppress user scene runtime activity in editor"
```

## Task 2: Wire Suppression Into `EditorSession.Update()`

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorSessionRuntimeSuppressionIntegrationTests.cs`

- [ ] **Step 1: Write the failing editor-session integration test**

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session suppresses user runtime behavior before the main core update executes.
    /// </summary>
    public sealed class EditorSessionRuntimeSuppressionIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the editor core under test.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the editor-core services required by the integration test.
        /// </summary>
        public EditorSessionRuntimeSuppressionIntegrationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-runtime-suppression-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures authored FPS overlays remain frozen when the editor session updates.
        /// </summary>
        [Fact]
        public void Update_WhenUserSceneContainsFpsComponent_DoesNotAdvanceUserRuntimeText() {
            EditorCore core = new EditorCore(new Project {
                Name = "Suppression",
                Path = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
            Core.Instance.DefaultFontAsset = CreateFont();

            EditorEntity sceneEntity = new EditorEntity {
                Name = "Scene Root",
                LayerMask = EditorLayerMasks.SceneObjects
            };
            FPSComponent fps = new FPSComponent {
                RefreshIntervalSeconds = 0d
            };
            sceneEntity.AddComponent(fps);

            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "core", core);
            SetPrivateField(session, "SceneRuntimeSuppressionService", new EditorSceneRuntimeSuppressionService());

            session.Update();

            Entity overlayHost = Assert.Single(sceneEntity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            Assert.Equal("Update FPS: --", updateText.Text);
            Assert.DoesNotContain(fps, core.ObjectManager.Updateables);
        }

        /// <summary>
        /// Sets one non-public field on the supplied instance for test setup.
        /// </summary>
        /// <param name="instance">Object whose field should be updated.</param>
        /// <param name="name">Exact field name.</param>
        /// <param name="value">Value assigned to the field.</param>
        static void SetPrivateField(object instance, string name, object value) {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(instance, value);
        }

        /// <summary>
        /// Creates one deterministic font asset for the FPS overlay hierarchy.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSessionRuntimeSuppressionIntegrationTests -v minimal`

Expected: `FAIL` because `EditorSession.Update()` still calls `core.Update()` directly, so the authored `FPSComponent` updates its visible text.

- [ ] **Step 3: Initialize and invoke the suppression service from `EditorSession.Update()`**

```csharp
/// <summary>
/// Freezes authored scene runtime activity while the editor is running.
/// </summary>
readonly EditorSceneRuntimeSuppressionService SceneRuntimeSuppressionService;
```

```csharp
SceneRuntimeSuppressionService = new EditorSceneRuntimeSuppressionService();
```

```csharp
/// <summary>
/// Executes the editor update loop for input and entities.
/// </summary>
public void Update() {
    SceneRuntimeSuppressionService.Suppress(core.ObjectManager);
    core.Update();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionRuntimeSuppressionIntegrationTests|FullyQualifiedName~EditorSceneRuntimeSuppressionServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionRuntimeSuppressionIntegrationTests.cs
rtk git commit -m "feat: freeze authored scene activity in editor updates"
```

## Task 3: Run Focused Regression Verification

**Files:**
- Verify: `engine/helengine.editor.tests/managers/scene/EditorSceneRuntimeSuppressionServiceTests.cs`
- Verify: `engine/helengine.editor.tests/EditorSessionRuntimeSuppressionIntegrationTests.cs`
- Verify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Verify: `engine/helengine.editor.tests/InputSystemTests.cs`

- [ ] **Step 1: Run the new suppression test suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSceneRuntimeSuppressionServiceTests|FullyQualifiedName~EditorSessionRuntimeSuppressionIntegrationTests" -v minimal`

Expected: `PASS`

- [ ] **Step 2: Run the nearby editor-session regression tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSessionSceneOpenTests -v minimal`

Expected: `PASS`

- [ ] **Step 3: Run the nearby input-routing regression tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~InputSystemTests -v minimal`

Expected: `PASS`

- [ ] **Step 4: Inspect the working tree**

Run: `rtk git status --short`

Expected: only the planned editor/test files are modified, plus any unrelated pre-existing user work that should be left untouched.

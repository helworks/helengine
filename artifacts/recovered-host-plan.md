# Host-Based Runtime And Editor API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build human-friendly `GameHost` and `EditorHost` startup APIs with small options objects, shared project-path/runtime bootstrap, and editor-scoped services so startup is obvious and editor state is no longer process-global.

**Architecture:** Keep the public Windows entry points in `helengine.core.windows` and `helengine.editor.windows`, but move the testable bootstrap contracts into `helengine.core` and `helengine.editor`. Introduce `ResolvedProjectPaths`, runtime/editor service collections, and `EditorSessionDependencies`, then refactor `EditorSession` to consume prepared services instead of initializing `EditorCore` or relying on static editor coordination classes.

**Tech Stack:** C#/.NET 9, WinForms, Hel engine core/editor/runtime/renderers, xUnit

---

## Scope Check

This remains one plan. Shared project-path resolution, runtime bootstrap, editor bootstrap, editor service scoping, and the WinForms app migration all depend on the same ownership model. Splitting them would leave a half-migrated editor that still bootstraps itself or still leaks state through static services.

## File Structure

### New Files

- `engine/helengine.core/hosting/ResolvedProjectPaths.cs`
- `engine/helengine.core/hosting/ProjectPathResolver.cs`
- `engine/helengine.core/managers/rendering/IRender2DProvider.cs`
- `engine/helengine.core/hosting/RuntimeWindowOptions.cs`
- `engine/helengine.core/hosting/GameHostOptions.cs`
- `engine/helengine.core/hosting/GameHostContext.cs`
- `engine/helengine.core/hosting/RuntimeServiceCollection.cs`
- `engine/helengine.core/hosting/RuntimeServices.cs`
- `engine/helengine.core/hosting/IGameHostShell.cs`
- `engine/helengine.core/hosting/GameHostBootstrap.cs`
- `engine/helengine.editor/hosting/EditorServices.cs`
- `engine/helengine.editor/hosting/EditorServiceCollection.cs`
- `engine/helengine.editor/hosting/EditorWindowOptions.cs`
- `engine/helengine.editor/hosting/EditorHostOptions.cs`
- `engine/helengine.editor/hosting/EditorHostContext.cs`
- `engine/helengine.editor/hosting/EditorRegistrationCollection.cs`
- `engine/helengine.editor/hosting/EditorHostShellResources.cs`
- `engine/helengine.editor/hosting/IEditorHostShell.cs`
- `engine/helengine.editor/hosting/EditorSessionDependencies.cs`
- `engine/helengine.editor/hosting/EditorHostRuntime.cs`
- `engine/helengine.editor/hosting/EditorHostBootstrap.cs`
- `engine/helengine.core.windows/hosting/GameHost.cs`
- `engine/helengine.core.windows/hosting/GameHostForm.cs`
- `engine/helengine.editor.windows/hosting/EditorHost.cs`
- `engine/helengine.editor.windows/hosting/EditorHostForm.cs`
- `engine/helengine.editor.tests/testing/TestGameHostShell.cs`
- `engine/helengine.editor.tests/testing/TestEditorHostShell.cs`
- `engine/helengine.editor.tests/hosting/ProjectPathResolverTests.cs`
- `engine/helengine.editor.tests/hosting/GameHostBootstrapTests.cs`
- `engine/helengine.editor.tests/hosting/EditorHostBootstrapTests.cs`
- `engine/helengine.editor.tests/EditorSelectionServiceTests.cs`

### Modified Files

- `engine/helengine.core.windows/helengine.core.windows.csproj`
- `engine/helengine.editor.windows/helengine.editor.windows.csproj`
- `engine/helengine.editor/EditorCore.cs`
- `engine/helengine.editor/EditorSelectionService.cs`
- `engine/helengine.editor/EditorAssetPickerService.cs`
- `engine/helengine.editor/EditorSceneMutationService.cs`
- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`
- `engine/helengine.editor/components/EditorViewportPicker.cs`
- `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoFollowComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformRotationGizmoFollowComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformScaleGizmoFollowComponent.cs`
- `engine/helengine.editor/managers/asset/EditorFileTemplateService.cs`
- `engine/helengine.editor/shaders/ShaderA…82539 chars truncated…
            Runtime.Runtime.InputManager.SetKeyboardActive(true);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
            Runtime.Runtime.InputManager.SetKeyboardActive(false);
        }

        protected override void OnClosed(EventArgs e) {
            Closed = true;
            Runtime.Session.Dispose();
            Runtime.Runtime.Core.Dispose();
            base.OnClosed(e);
        }

        EditorHostOptions CreateEffectiveOptions(EditorHostOptions options) {
            if (string.IsNullOrWhiteSpace(options.ProjectPath)) {
                throw new InvalidOperationException("ProjectPath must be provided.");
            }

            Action<EditorRegistrationCollection> registerEditorExtensions = registrations => {
                registrations.RegisterImporter(new TextureImporterRegistration("gdi", new GDITextureImporter(), new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" }));
                registrations.RegisterImporter(new TextImporterRegistration("text", new TextImporter(), new[] { ".txt" }));
                if (options.RegisterEditorExtensions != null) {
                    options.RegisterEditorExtensions(registrations);
                }
            };

            return new EditorHostOptions {
                ProjectPath = options.ProjectPath,
                Window = options.Window ?? new EditorWindowOptions(),
                RendererFactory = options.RendererFactory ?? (_ => new helengine.directx11.DirectX11Renderer3D()),
                Render2DFactory = options.Render2DFactory,
                InputFactory = options.InputFactory ?? (context => new InputManagerWindows(context.WindowHandle)),
                ConfigureRuntimeServices = options.ConfigureRuntimeServices,
                ConfigureEditorServices = options.ConfigureEditorServices,
                RegisterEditorExtensions = registerEditorExtensions
            };
        }
    }
}
```

Keep these existing `MainForm` behaviors in `EditorHostForm` instead of dropping them during the move:

```csharp
TitleBarWindowAdapter.Attach(Runtime.Session.TitleBar, this, ToggleMaximizeState);
UpdateMinimumWindowSize();
UpdateDockingCursor();
WindowResizeAdapter.ApplyResizeHitTest(this, ref m, WindowResizeAdapter.DefaultResizeBorderThickness);
```

Move these methods over from the old form and retarget them to `Runtime.Session` or `Runtime.Runtime.RenderManager3D` rather than rebuilding them from scratch:

```csharp
void InitializeWindowFrame()
bool UpdateMinimumWindowSize()
void UpdateDockingCursor()
void ToggleMaximizeState()
protected override void WndProc(ref Message m)
```

Slim the app project down so it only parses the path and hands control to the engine host:

```csharp
namespace helengine.editor.app {
    internal static class Program {
        [STAThread]
        static void Main(string[] args) {
            if (!TryGetProjectPath(args, out string projectPath)) {
                return;
            }

            EditorHost.Run(new EditorHostOptions {
                ProjectPath = projectPath
            });
        }
    }
}
```

Reduce `helengine.editor.app.csproj` to the editor-host dependency it really needs:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\engine\helengine.editor.windows\helengine.editor.windows.csproj" />
</ItemGroup>
```

Delete the old `MainForm` files after `EditorHostForm` is compiling.

- [ ] **Step 3: Build the editor app and then run the focused host/session tests**

Run: `dotnet build 'helengine.ui/helengine.editor.app/helengine.editor.app.csproj'`

Expected: PASS with the app compiling against `EditorHost` instead of owning bootstrap itself.

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorHostBootstrapTests|EditorSessionStartupSceneTests|EditorSessionSceneOpenTests|EditorSessionKeyboardFocusIntegrationTests"`

Expected: PASS with the host/session integration still green after the shell move.

- [ ] **Step 4: Run the full verification sweep**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS with the editor test suite green.

Run: `dotnet build 'helengine.ui/helengine.sln'`

Expected: PASS with the runtime host, editor host, and the app solution all compiling.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.windows/helengine.editor.windows.csproj engine/helengine.editor.windows/hosting/EditorHost.cs engine/helengine.editor.windows/hosting/EditorHostForm.cs helengine.ui/helengine.editor.app/Program.cs helengine.ui/helengine.editor.app/helengine.editor.app.csproj
git rm helengine.ui/helengine.editor.app/MainForm.cs helengine.ui/helengine.editor.app/MainForm.Designer.cs helengine.ui/helengine.editor.app/MainForm.resx
git commit -m "refactor: move editor startup behind windows host"
```


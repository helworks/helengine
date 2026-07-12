using System.Reflection;
using helengine;
using helengine.directx11;
using helengine.editor;
using helengine.editor.app;

namespace editor_repro {
    /// <summary>
    /// Boots the real WinForms editor host and invokes the private scene-open path against one target project and scene.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Absolute project path passed to the editor host.
        /// </summary>
        const string ProjectPath = @"C:\dev\helprojs\demodisc\project.heproj";

        /// <summary>
        /// Absolute scene path opened after the editor host finishes startup.
        /// </summary>
        const string ScenePath = @"C:\dev\helprojs\demodisc\assets\scenes\games\tilt_trial_level_01.helen";

        /// <summary>
        /// Delay before invoking the private scene-load path so the host can finish startup.
        /// </summary>
        static readonly TimeSpan OpenDelay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Log file written by the repro harness.
        /// </summary>
        static readonly string LogPath = Path.Combine(Path.GetTempPath(), "helengine-editor-repro.log");

        /// <summary>
        /// Starts the real editor host and drives one scene-open attempt.
        /// </summary>
        [STAThread]
        static int Main() {
            ApplicationConfiguration.Initialize();
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.ThreadException += HandleThreadException;

            try {
                File.WriteAllText(LogPath, string.Empty);
                Log("Harness starting.");
                MainForm form = new MainForm(ProjectPath);
                form.Shown += (_, __) => ScheduleSceneOpen(form);
                Application.Run(form);
                Log("Application.Run exited cleanly.");
                return 0;
            } catch (Exception ex) {
                Log("Harness main exception: " + ex);
                return 1;
            }
        }

        /// <summary>
        /// Schedules the private scene-open call after startup.
        /// </summary>
        /// <param name="form">Initialized editor host form.</param>
        static async void ScheduleSceneOpen(MainForm form) {
            if (form == null) {
                throw new ArgumentNullException(nameof(form));
            }

            try {
                Log("Form shown. Waiting before scene open.");
                await Task.Delay(OpenDelay);
                InvokeSceneLoad(form, ScenePath);
                Log("Scene load invoked.");
                LogSceneState(form);
                LogEntityState("TiltTrialCamera");
                LogCameraState();
                StartHeartbeat(form);
            } catch (Exception ex) {
                Log("ScheduleSceneOpen exception: " + ex);
                if (!form.IsDisposed) {
                    form.Close();
                }
            }
        }

        /// <summary>
        /// Invokes the editor session's private scene-open path through reflection.
        /// </summary>
        /// <param name="form">Editor host form that owns the live editor session.</param>
        /// <param name="scenePath">Absolute scene path to open.</param>
        static void InvokeSceneLoad(MainForm form, string scenePath) {
            if (form == null) {
                throw new ArgumentNullException(nameof(form));
            }
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            FieldInfo editorSessionField = typeof(MainForm).GetField("editorSession", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve MainForm.editorSession.");
            object editorSession = editorSessionField.GetValue(form)
                ?? throw new InvalidOperationException("MainForm.editorSession was null.");
            MethodInfo loadSceneMethod = editorSession.GetType().GetMethod("LoadSceneIntoSession", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve EditorSession.LoadSceneIntoSession.");
            Log("Invoking LoadSceneIntoSession for " + scenePath);
            loadSceneMethod.Invoke(editorSession, new object[] { scenePath });
        }

        /// <summary>
        /// Removes editor-only 2D viewport presentation components so crash isolation can distinguish editor 2D presentation from the rest of the renderer.
        /// </summary>
        static void DisableEditorViewport2DPresentation() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int removedWorldPreviewSyncComponentCount = 0;
            int removedDirect2DPresenterComponentCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                List<Component> components = entities[entityIndex].Components;
                for (int componentIndex = components.Count - 1; componentIndex >= 0; componentIndex--) {
                    if (components[componentIndex] is EditorWorldSpace2DPreviewSyncComponent worldPreviewSyncComponent) {
                        entities[entityIndex].RemoveComponent(worldPreviewSyncComponent);
                        removedWorldPreviewSyncComponentCount++;
                    } else if (components[componentIndex] is EditorViewportDirect2DScenePresenterComponent direct2DPresenterComponent) {
                        entities[entityIndex].RemoveComponent(direct2DPresenterComponent);
                        removedDirect2DPresenterComponentCount++;
                    }
                }
            }

            Log("Editor world-space 2D preview sync components removed=" + removedWorldPreviewSyncComponentCount);
            Log("Editor direct-2D scene presenter components removed=" + removedDirect2DPresenterComponentCount);
        }

        /// <summary>
        /// Removes authored scene cameras that would otherwise render directly into the editor backbuffer alongside the editor's own viewport cameras.
        /// </summary>
        static void DisableAuthoredRuntimeBackbufferCameras() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int removedCameraComponentCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is EditorEntity editorEntity && editorEntity.InternalEntity) {
                    continue;
                }

                List<Component> components = entities[entityIndex].Components;
                for (int componentIndex = components.Count - 1; componentIndex >= 0; componentIndex--) {
                    if (components[componentIndex] is not CameraComponent cameraComponent) {
                        continue;
                    }
                    if (cameraComponent.RenderTarget != null) {
                        continue;
                    }

                    entities[entityIndex].RemoveComponent(cameraComponent);
                    removedCameraComponentCount++;
                }
            }

            Log("Authored runtime backbuffer camera components removed=" + removedCameraComponentCount);
        }

        /// <summary>
        /// Disables one authored scene entity by name so crash isolation can remove specific content without changing the scene asset on disk.
        /// </summary>
        /// <param name="entityName">Authored entity name to disable.</param>
        static void DisableEntityByName(string entityName) {
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int disabledEntityCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, entityName, StringComparison.Ordinal)) {
                    continue;
                }

                editorEntity.Enabled = false;
                disabledEntityCount++;
            }

            Log("Disabled entities named '" + entityName + "' count=" + disabledEntityCount);
        }

        /// <summary>
        /// Disables editor-only camera-visual children attached to authored camera roots so crash isolation can distinguish the visual mesh render path from the authored camera root itself without disposing shared visual resources.
        /// </summary>
        static void DisableEditorCameraVisualChildren() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int disabledCameraVisualEntityCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, "Camera Visual", StringComparison.Ordinal)) {
                    continue;
                }

                bool hasCameraVisualComponent = false;
                for (int componentIndex = 0; componentIndex < editorEntity.Components.Count; componentIndex++) {
                    if (editorEntity.Components[componentIndex] is EditorCameraVisualComponent) {
                        hasCameraVisualComponent = true;
                        break;
                    }
                }

                if (!hasCameraVisualComponent || editorEntity.Parent == null) {
                    continue;
                }

                editorEntity.Enabled = false;
                disabledCameraVisualEntityCount++;
            }

            Log("Editor camera visual child entities disabled=" + disabledCameraVisualEntityCount);
        }

        /// <summary>
        /// Replaces editor camera-visual meshes with the point-light visual mesh so crash isolation can separate the camera-visual entity path from the camera-visual geometry data.
        /// </summary>
        static void ReplaceEditorCameraVisualModelsWithPointLightVisuals() {
            RuntimeModel replacementModel = EditorPointLightVisualResources.GetRuntimeModel();
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int replacedVisualCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, "Camera Visual", StringComparison.Ordinal)) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < editorEntity.Components.Count; componentIndex++) {
                    if (editorEntity.Components[componentIndex] is not EditorCameraVisualComponent cameraVisualComponent) {
                        continue;
                    }

                    cameraVisualComponent.Model = replacementModel;
                    replacedVisualCount++;
                }
            }

            Log("Editor camera visual models replaced with point-light visual model count=" + replacedVisualCount);
        }

        /// <summary>
        /// Detaches editor camera-visual children from authored camera roots and rehosts them on standalone internal entities so crash isolation can separate parent-camera transform behavior from the mesh render path.
        /// </summary>
        static void ReparentEditorCameraVisualChildrenToStandaloneEntities() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            int reparentedVisualCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, "Camera Visual", StringComparison.Ordinal)) {
                    continue;
                }
                if (editorEntity.Parent == null) {
                    continue;
                }

                float3 worldPosition = editorEntity.Position;
                float4 worldOrientation = editorEntity.Orientation;
                float3 worldScale = editorEntity.Scale;
                Entity previousParent = editorEntity.Parent;
                previousParent.RemoveChild(editorEntity);

                EditorEntity hostEntity = new EditorEntity {
                    InternalEntity = true,
                    LayerMask = editorEntity.LayerMask,
                    LocalPosition = worldPosition,
                    LocalOrientation = worldOrientation,
                    LocalScale = worldScale,
                    Name = "Detached Camera Visual"
                };

                hostEntity.AddChild(editorEntity);
                editorEntity.LocalPosition = float3.Zero;
                editorEntity.LocalOrientation = float4.Identity;
                editorEntity.LocalScale = float3.One;
                reparentedVisualCount++;
            }

            Log("Editor camera visual children reparented to standalone entities count=" + reparentedVisualCount);
        }

        /// <summary>
        /// Disables the original authored camera visual and spawns one brand-new standalone point-light visual at the same world transform so crash isolation can test whether a fresh scene-camera-visual draw is stable.
        /// </summary>
        static void SpawnStandalonePointLightVisualForTiltTrialCamera() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            EditorEntity sourceVisualEntity = null;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, "Camera Visual", StringComparison.Ordinal)) {
                    continue;
                }

                sourceVisualEntity = editorEntity;
                break;
            }

            if (sourceVisualEntity == null) {
                Log("SpawnStandalonePointLightVisualForTiltTrialCamera skipped because no source camera visual entity was found.");
                return;
            }

            EditorEntity visualEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalOrientation = float4.Identity,
                LocalScale = float3.One,
                Name = "Standalone Point Light Visual"
            };

            MeshComponent meshComponent = new MeshComponent {
                Model = EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId),
                Materials = new[] { EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId) }
            };
            visualEntity.AddComponent(meshComponent);
            Log("Spawned standalone point-light visual entity at source camera visual transform position=" + visualEntity.Position + " orientation=" + visualEntity.Orientation + " scale=" + visualEntity.Scale);
            LogMeshMaterialState("Standalone Point Light Visual");
        }

        /// <summary>
        /// Logs one runtime entity, its components, and its direct children so crash isolation can reason about authored camera hierarchy behavior.
        /// </summary>
        /// <param name="entityName">Name of the runtime entity to describe.</param>
        static void LogEntityState(string entityName) {
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, entityName, StringComparison.Ordinal)) {
                    continue;
                }

                Log("EntityState name=" + editorEntity.Name + " enabled=" + editorEntity.Enabled + " internal=" + editorEntity.InternalEntity + " layerMask=" + editorEntity.LayerMask + " childCount=" + editorEntity.Children.Count + " componentCount=" + editorEntity.Components.Count);
                Log("EntityTransform position=" + editorEntity.Position + " orientation=" + editorEntity.Orientation + " scale=" + editorEntity.Scale);
                for (int componentIndex = 0; componentIndex < editorEntity.Components.Count; componentIndex++) {
                    Component component = editorEntity.Components[componentIndex];
                    Log("EntityComponent[" + componentIndex + "] type=" + component.GetType().FullName);
                }

                for (int childIndex = 0; childIndex < editorEntity.Children.Count; childIndex++) {
                    Entity child = editorEntity.Children[childIndex];
                    string childName = child is EditorEntity childEditorEntity ? childEditorEntity.Name : child.GetType().FullName;
                    Log("EntityChild[" + childIndex + "] name=" + childName + " enabled=" + child.Enabled + " layerMask=" + child.LayerMask + " componentCount=" + child.Components.Count);
                    Log("EntityChildTransform[" + childIndex + "] position=" + child.Position + " orientation=" + child.Orientation + " scale=" + child.Scale);
                    for (int componentIndex = 0; componentIndex < child.Components.Count; componentIndex++) {
                        Component component = child.Components[componentIndex];
                        Log("EntityChildComponent[" + childIndex + "," + componentIndex + "] type=" + component.GetType().FullName);
                    }
                }

                return;
            }

            Log("EntityState missing name=" + entityName);
        }

        /// <summary>
        /// Logs the first mesh material bound to one named editor entity, including the standard texture slots used by editor visuals.
        /// </summary>
        /// <param name="entityName">Name of the runtime entity whose first mesh material should be described.</param>
        static void LogMeshMaterialState(string entityName) {
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                if (entities[entityIndex] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!string.Equals(editorEntity.Name, entityName, StringComparison.Ordinal)) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < editorEntity.Components.Count; componentIndex++) {
                    if (editorEntity.Components[componentIndex] is not MeshComponent meshComponent) {
                        continue;
                    }
                    if (meshComponent.Materials == null || meshComponent.Materials.Length == 0 || meshComponent.Materials[0] == null) {
                        Log("MeshMaterialState name=" + entityName + " has no first material.");
                        return;
                    }

                    ShaderRuntimeMaterial shaderMaterial = ShaderRuntimeMaterialAccess.Require(meshComponent.Materials[0]);
                    LogTextureBinding(shaderMaterial, StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
                    LogTextureBinding(shaderMaterial, StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName);
                    LogTextureBinding(shaderMaterial, StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName);
                    return;
                }

                Log("MeshMaterialState name=" + entityName + " has no MeshComponent.");
                return;
            }

            Log("MeshMaterialState missing name=" + entityName);
        }

        /// <summary>
        /// Logs one named shader texture binding for diagnostic inspection.
        /// </summary>
        /// <param name="material">Shader runtime material that owns the binding.</param>
        /// <param name="bindingName">Binding name to inspect.</param>
        static void LogTextureBinding(ShaderRuntimeMaterial material, string bindingName) {
            int bindingIndex = material.Layout.FindTextureBindingIndex(bindingName);
            if (bindingIndex < 0) {
                Log("MaterialBinding name=" + bindingName + " bindingIndex=missing");
                return;
            }

            MaterialLayoutBinding binding = material.Layout.TextureBindings[bindingIndex];
            RuntimeTexture runtimeTexture = material.Properties.GetTexture(bindingIndex);
            string textureType = runtimeTexture == null ? "null" : runtimeTexture.GetType().FullName;
            Log("MaterialBinding name=" + bindingName + " bindingIndex=" + bindingIndex + " slot=" + binding.Slot + " textureType=" + textureType);
        }

        /// <summary>
        /// Forces the managed DirectX11 renderer to skip shadow passes so native crash isolation can distinguish generic rendering from shadow-resource work.
        /// </summary>
        /// <param name="form">Editor host form that owns the live editor session.</param>
        static void DisableDirectX11ShadowPasses(MainForm form) {
            if (form == null) {
                throw new ArgumentNullException(nameof(form));
            }

            FieldInfo editorSessionField = typeof(MainForm).GetField("editorSession", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve MainForm.editorSession.");
            object editorSession = editorSessionField.GetValue(form)
                ?? throw new InvalidOperationException("MainForm.editorSession was null.");
            FieldInfo coreField = editorSession.GetType().GetField("core", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve EditorSession.core.");
            EditorCore core = (EditorCore)(coreField.GetValue(editorSession)
                ?? throw new InvalidOperationException("EditorSession.core was null."));
            DirectX11Renderer3D renderer = core.RenderManager3D as DirectX11Renderer3D
                ?? throw new InvalidOperationException("The repro harness expected a DirectX11 renderer.");
            FieldInfo renderPlanExecutorField = typeof(DirectX11Renderer3D).GetField("RenderPlanExecutorValue", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve DirectX11Renderer3D.RenderPlanExecutorValue.");
            renderPlanExecutorField.SetValue(renderer, new DirectX11RenderPlanExecutor(false, false));
            Log("DirectX11 shadow passes disabled for repro isolation.");
        }

        /// <summary>
        /// Logs the active editor-session scene path and current live user-scene roots after the private load method returns.
        /// </summary>
        /// <param name="form">Editor host form that owns the live editor session.</param>
        static void LogSceneState(MainForm form) {
            if (form == null) {
                throw new ArgumentNullException(nameof(form));
            }

            FieldInfo editorSessionField = typeof(MainForm).GetField("editorSession", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve MainForm.editorSession.");
            object editorSession = editorSessionField.GetValue(form)
                ?? throw new InvalidOperationException("MainForm.editorSession was null.");
            FieldInfo currentScenePathField = editorSession.GetType().GetField("CurrentScenePath", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not resolve EditorSession.CurrentScenePath.");
            string currentScenePath = (string)(currentScenePathField.GetValue(editorSession) ?? string.Empty);
            bool hasSceneManager = Core.Instance.SceneManager != null;
            string loadedSceneIds = hasSceneManager
                ? string.Join(", ", Core.Instance.SceneManager.GetLoadedSceneIds())
                : "<null>";

            List<string> userRootNames = new List<string>();
            List<Entity> liveEntities = Core.Instance.ObjectManager.Entities;
            for (int index = 0; index < liveEntities.Count; index++) {
                if (liveEntities[index] is not EditorEntity editorEntity) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }
                if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                    continue;
                }
                if (editorEntity.Parent != null) {
                    continue;
                }

                userRootNames.Add(editorEntity.Name ?? string.Empty);
            }

            string rootSummary = userRootNames.Count == 0 ? "<none>" : string.Join(", ", userRootNames);
            Log("CurrentScenePath=" + currentScenePath);
            Log("HasSceneManager=" + hasSceneManager);
            Log("LoadedSceneIds=" + loadedSceneIds);
            Log("UserSceneRootCount=" + userRootNames.Count);
            Log("UserSceneRoots=" + rootSummary);
        }

        /// <summary>
        /// Logs the current live camera stack so editor-only and authored camera interactions can be inspected without attaching a debugger.
        /// </summary>
        static void LogCameraState() {
            List<ICamera> cameras = Core.Instance.ObjectManager.Cameras;
            Log("CameraCount=" + cameras.Count);
            for (int cameraIndex = 0; cameraIndex < cameras.Count; cameraIndex++) {
                CameraComponent cameraComponent = cameras[cameraIndex] as CameraComponent
                    ?? throw new InvalidOperationException("Expected every repro camera to be a CameraComponent.");
                string parentName = cameraComponent.Parent is EditorEntity namedEditorEntity
                    ? namedEditorEntity.Name ?? "<unnamed>"
                    : cameraComponent.Parent?.GetType().Name ?? "<null>";
                bool isInternalEditorCamera = cameraComponent.Parent is EditorEntity editorEntity && editorEntity.InternalEntity;
                string renderTargetSummary = cameraComponent.RenderTarget == null
                    ? "backbuffer"
                    : cameraComponent.RenderTarget.GetType().Name + $"({cameraComponent.RenderTarget.Width}x{cameraComponent.RenderTarget.Height})";
                Log(
                    "Camera[" + cameraIndex + "] name=" + parentName +
                    " internal=" + isInternalEditorCamera +
                    " drawOrder=" + cameraComponent.CameraDrawOrder +
                    " layerMask=" + cameraComponent.LayerMask +
                    " target=" + renderTargetSummary);
            }
        }

        /// <summary>
        /// Writes one timestamped log line to the repro log.
        /// </summary>
        /// <param name="message">Message to append.</param>
        static void Log(string message) {
            string line = string.Concat(DateTime.UtcNow.ToString("O"), " | ", message, Environment.NewLine);
            File.AppendAllText(LogPath, line);
        }

        /// <summary>
        /// Writes periodic liveness markers after the scene opens so the crash time can be pinned down without attaching a debugger.
        /// </summary>
        /// <param name="form">Editor host form whose lifetime bounds the heartbeat loop.</param>
        static void StartHeartbeat(MainForm form) {
            if (form == null) {
                throw new ArgumentNullException(nameof(form));
            }

            _ = Task.Run(async () => {
                int heartbeatIndex = 0;
                while (!form.IsDisposed) {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    if (form.IsDisposed) {
                        break;
                    }

                    heartbeatIndex++;
                    Log("Heartbeat=" + heartbeatIndex);
                }
            });
        }

        /// <summary>
        /// Records one unhandled background exception.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Unhandled exception payload.</param>
        static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args) {
            Log("Unhandled exception: " + args.ExceptionObject);
        }

        /// <summary>
        /// Records one WinForms UI-thread exception.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Thread-exception payload.</param>
        static void HandleThreadException(object sender, ThreadExceptionEventArgs args) {
            Log("Thread exception: " + args.Exception);
        }
    }
}

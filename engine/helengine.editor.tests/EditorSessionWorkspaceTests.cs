using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies workspace panel instance creation and close behavior in editor sessions.
    /// </summary>
    public sealed class EditorSessionWorkspaceTests {
        /// <summary>
        /// Returns whether one ordered render queue contains the supplied drawable instance.
        /// </summary>
        /// <param name="renderQueue">Render queue to inspect.</param>
        /// <param name="drawable">Drawable instance to locate.</param>
        /// <returns>True when the queue contains the drawable.</returns>
        static bool QueueContainsDrawable(IRenderQueue3D renderQueue, IDrawable3D drawable) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            RenderQueueContainsVisitor visitor = new RenderQueueContainsVisitor(drawable);
            renderQueue.VisitOrdered(visitor);
            return visitor.Found;
        }

        /// <summary>
        /// Returns the first drawable found in one entity subtree.
        /// </summary>
        /// <param name="entity">Subtree root to inspect.</param>
        /// <returns>First drawable found in the subtree.</returns>
        static IDrawable3D FindFirstDrawableInSubtree(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is IDrawable3D drawable) {
                        return drawable;
                    }
                }
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    Entity child = entity.Children[childIndex];
                    if (child == null) {
                        continue;
                    }

                    IDrawable3D drawable = FindFirstDrawableInSubtree(child);
                    if (drawable != null) {
                        return drawable;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Visits one render queue and reports whether the target drawable was encountered.
        /// </summary>
        sealed class RenderQueueContainsVisitor : IRenderVisitor3D {
            /// <summary>
            /// Drawable instance the visitor should locate.
            /// </summary>
            readonly IDrawable3D TargetDrawable;

            /// <summary>
            /// Initializes one queue-contains visitor.
            /// </summary>
            /// <param name="targetDrawable">Drawable instance to locate.</param>
            public RenderQueueContainsVisitor(IDrawable3D targetDrawable) {
                TargetDrawable = targetDrawable ?? throw new ArgumentNullException(nameof(targetDrawable));
            }

            /// <summary>
            /// Gets whether the target drawable was encountered during traversal.
            /// </summary>
            public bool Found { get; private set; }

            /// <summary>
            /// Processes one drawable encountered during queue traversal.
            /// </summary>
            /// <param name="drawable">Drawable encountered during queue traversal.</param>
            public void Visit(IDrawable3D drawable) {
                if (ReferenceEquals(drawable, TargetDrawable)) {
                    Found = true;
                }
            }
        }

        /// <summary>
        /// Ensures workspace save and load round-trips one docked panel and one floating panel.
        /// </summary>
        [Fact]
        public void UiSaveAndLoad_WhenWorkspaceContainsDockedAndFloatingPanels_RestoresTrackedInstances() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowLogger);
            EditorWorkspacePanelInstance preview = harness.Session.GetPanelInstancesForTest("preview")[0];
            EditorWorkspacePanelInstance logger = harness.Session.GetPanelInstancesForTest("logger")[0];
            preview.Dockable.Position = new float3(48f, 64f, 0f);
            preview.Dockable.Size = new int2(333, 222);
            harness.Session.DockingManager.Layout.DockAsRoot(logger.Dockable);

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);
            preview.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);
            logger.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.Empty(harness.Session.GetPanelInstancesForTest("preview"));
            Assert.Empty(harness.Session.GetPanelInstancesForTest("logger"));

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

            EditorWorkspacePanelInstance restoredPreview = Assert.Single(harness.Session.GetPanelInstancesForTest("preview"));
            EditorWorkspacePanelInstance restoredLogger = Assert.Single(harness.Session.GetPanelInstancesForTest("logger"));
            Assert.False(restoredPreview.Dockable.IsDocked);
            Assert.Equal(new int2(333, 222), restoredPreview.Dockable.Size);
            Assert.Equal(48f, restoredPreview.Dockable.Position.X);
            Assert.Equal(64f, restoredPreview.Dockable.Position.Y);
            Assert.True(restoredLogger.Dockable.IsDocked);
        }

        /// <summary>
        /// Ensures opening Scene twice creates two independent scene hierarchy panel instances.
        /// </summary>
        [Fact]
        public void UiShow_WhenSceneHierarchyIsOpenedTwice_CreatesTwoIndependentSceneInstances() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowSceneHierarchy);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowSceneHierarchy);

            IReadOnlyList<DockableEntity> panels = harness.Session.GetPanelInstancesForTest("scene-hierarchy").Select(instance => instance.Dockable).ToArray();
            Assert.Equal(2, panels.Count);
            Assert.NotSame(panels[0], panels[1]);
        }

        /// <summary>
        /// Ensures opening Assets twice creates two independent asset browser panel instances.
        /// </summary>
        [Fact]
        public void UiShow_WhenAssetBrowserIsOpenedTwice_CreatesTwoIndependentAssetBrowserInstances() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowAssetBrowser);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowAssetBrowser);

            IReadOnlyList<DockableEntity> panels = harness.Session.GetPanelInstancesForTest("asset-browser").Select(instance => instance.Dockable).ToArray();
            Assert.Equal(2, panels.Count);
            Assert.NotSame(panels[0], panels[1]);
        }

        /// <summary>
        /// Ensures opening Preview twice creates two independent preview panel instances.
        /// </summary>
        [Fact]
        public void UiShow_WhenPreviewIsOpenedTwice_CreatesTwoIndependentPreviewInstances() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);

            IReadOnlyList<DockableEntity> previews = harness.Session.GetPanelInstancesForTest("preview").Select(instance => instance.Dockable).ToArray();
            Assert.Equal(2, previews.Count);
            Assert.NotSame(previews[0], previews[1]);
        }

        /// <summary>
        /// Ensures opening Viewport twice creates two tracked viewport panel instances.
        /// </summary>
        [Fact]
        public void UiShow_WhenViewportIsOpenedTwice_CreatesTwoIndependentViewportInstances() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);

            IReadOnlyList<DockableEntity> viewports = harness.Session.GetPanelInstancesForTest("viewport").Select(instance => instance.Dockable).ToArray();
            Assert.Equal(2, viewports.Count);
            Assert.NotSame(viewports[0], viewports[1]);
        }

        /// <summary>
        /// Ensures each duplicate viewport owns its own transform gizmo roots and closing one viewport removes only its own gizmos.
        /// </summary>
        [Fact]
        public void UiShow_WhenViewportIsOpenedTwice_CreatesIndependentGizmoRootsPerViewport() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);

            Assert.Equal(2, harness.CountEntitiesNamed("Transform Translation Gizmo"));
            Assert.Equal(2, harness.CountEntitiesNamed("Transform Rotation Gizmo"));
            Assert.Equal(2, harness.CountEntitiesNamed("Transform Scale Gizmo"));

            harness.Session.GetPanelInstancesForTest("viewport")[0].Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.Equal(1, harness.CountEntitiesNamed("Transform Translation Gizmo"));
            Assert.Equal(1, harness.CountEntitiesNamed("Transform Rotation Gizmo"));
            Assert.Equal(1, harness.CountEntitiesNamed("Transform Scale Gizmo"));
        }

        /// <summary>
        /// Ensures each viewport gizmo camera queue contains only the gizmo drawables owned by that viewport.
        /// </summary>
        [Fact]
        public void UiShow_WhenViewportIsOpenedTwice_KeepsEachGizmoCameraQueueScopedToItsOwnViewport() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");
            ViewportWorkspacePanelController firstController = harness.GetViewportControllerForTest(instances[0]);
            ViewportWorkspacePanelController secondController = harness.GetViewportControllerForTest(instances[1]);
            EditorEntity selectedEntity = new EditorEntity();

            try {
                EditorSelectionService.SetSelectedEntity(selectedEntity);
                Core.Instance.ObjectManager.Update();

                IDrawable3D firstDrawable = FindFirstDrawableInSubtree(firstController.ViewportState.TranslationGizmoRoot);
                IDrawable3D secondDrawable = FindFirstDrawableInSubtree(secondController.ViewportState.TranslationGizmoRoot);
                Assert.NotNull(firstDrawable);
                Assert.NotNull(secondDrawable);
                Assert.True(QueueContainsDrawable(firstController.ViewportState.GizmoCamera.RenderQueue3D, firstDrawable));
                Assert.False(QueueContainsDrawable(firstController.ViewportState.GizmoCamera.RenderQueue3D, secondDrawable));
                Assert.True(QueueContainsDrawable(secondController.ViewportState.GizmoCamera.RenderQueue3D, secondDrawable));
                Assert.False(QueueContainsDrawable(secondController.ViewportState.GizmoCamera.RenderQueue3D, firstDrawable));
            } finally {
                EditorSelectionService.ClearSelection();
                selectedEntity.Dispose();
            }
        }

        /// <summary>
        /// Ensures Add to scene places a spawned model at the orbit target of the last focused viewport.
        /// </summary>
        [Fact]
        public void AddToScene_WhenModelIsRequestedFromFocusedViewport_AddsEntityAtTheViewportOrbitTarget() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");
            EditorWorkspacePanelInstance focusedInstance = instances[1];
            ViewportWorkspacePanelController focusedController = harness.GetViewportControllerForTest(focusedInstance);
            float3 orbitTarget = new float3(12f, 34f, -56f);
            focusedController.ViewportState.CameraController.SetOrbitTarget(orbitTarget);

            InvokePrivate(focusedInstance.Dockable, "HandleViewportContentFocusedChanged", true);

            AssetBrowserEntry modelEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);

            InvokePrivate(harness.Session, "HandleAddToSceneRequested", modelEntry);

            EditorEntity createdEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            Assert.Equal("Cube", createdEntity.Name);
            Assert.Equal(orbitTarget, createdEntity.Position);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(createdEntity.Components, component => component is MeshComponent));
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(Assert.Single(meshComponent.Materials));
        }

        /// <summary>
        /// Ensures the session primary viewport accessors resolve through tracked viewport instances instead of older singleton fields.
        /// </summary>
        [Fact]
        public void ViewportAccessors_WhenTrackedViewportExists_ReturnTrackedViewportPanelAndSceneCamera() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);

            Assert.Same(controller.ViewportState.Viewport, harness.Session.MainViewport);
            Assert.Same(controller.ViewportState.SceneCamera, harness.Session.SceneCamera);
        }

        /// <summary>
        /// Ensures newly created workspace viewports start with an editor-scale far clip plane instead of the generic runtime camera default.
        /// </summary>
        [Fact]
        public void ViewportCreation_WhenWorkspaceViewportOpens_UsesExtendedSceneFarPlane() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);

            Assert.Equal(5000f, controller.ViewportState.SceneCamera.FarPlaneDistance);
            Assert.Equal(controller.ViewportState.SceneCamera.FarPlaneDistance, controller.ViewportState.GizmoCamera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures freshly created workspace viewports default to adaptive selection-size camera speed mode.
        /// </summary>
        [Fact]
        public void ViewportCreation_WhenWorkspaceViewportOpens_DefaultsToAutoSelectionSpeedMode() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));

            Assert.Equal(EditorViewportCameraSpeedMode.AutoFromSelection, harness.GetViewportCameraSpeedMode(instance));
            Assert.Equal(EditorViewportCameraController.DefaultMoveSpeed, harness.GetViewportManualCameraSpeed(instance));
        }

        /// <summary>
        /// Ensures one viewport focus request frames the selected authored viewport and expands the scene far plane when necessary.
        /// </summary>
        [Fact]
        public void ViewportFocus_WhenFocusSelectionIsRequested_FramesSelectedViewportAndExpandsFarPlane() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            controller.ViewportState.Viewport.Size = new int2(1280, 720);

            Entity selectedViewportEntity = new Entity();
            selectedViewportEntity.InitComponents();
            selectedViewportEntity.InitChildren();
            selectedViewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(40000, 20000)
            });
            EditorSelectionService.SetSelectedEntity(selectedViewportEntity);

            controller.ViewportState.Viewport.FocusSelectionRequested();

            Assert.Equal(new float3(20000f, -10000f, 0f), controller.ViewportState.CameraController.GetOrbitTarget());
            Assert.True(controller.ViewportState.SceneCamera.FarPlaneDistance > 5000f);
            Assert.Equal(controller.ViewportState.SceneCamera.FarPlaneDistance, controller.ViewportState.GizmoCamera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures closing the first tracked viewport retargets the session viewport accessors to the next surviving viewport instance.
        /// </summary>
        [Fact]
        public void ViewportAccessors_WhenFirstTrackedViewportCloses_RetargetToTheRemainingViewportInstance() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");
            EditorWorkspacePanelInstance first = instances[0];
            EditorWorkspacePanelInstance second = instances[1];
            ViewportWorkspacePanelController secondController = harness.GetViewportControllerForTest(second);

            first.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.Same(secondController.ViewportState.Viewport, harness.Session.MainViewport);
            Assert.Same(secondController.ViewportState.SceneCamera, harness.Session.SceneCamera);
        }

        /// <summary>
        /// Ensures closing the last tracked viewport clears session viewport accessors and reopening starts from a fresh viewport-local snap state.
        /// </summary>
        [Fact]
        public void ViewportLifecycle_WhenLastViewportClosesAndAnotherOpens_ClearsAccessorsAndResetsViewportSnapState() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance original = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            harness.SetViewportSnapValue(original, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1, 4.0);

            original.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.Null(harness.Session.MainViewport);
            Assert.Null(harness.Session.SceneCamera);
            Assert.Empty(harness.Session.GetPanelInstancesForTest("viewport"));

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);

            EditorWorkspacePanelInstance reopened = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            Assert.NotNull(harness.Session.MainViewport);
            Assert.NotNull(harness.Session.SceneCamera);
            Assert.Equal(0.25, harness.GetViewportSnapValue(reopened, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
        }

        /// <summary>
        /// Ensures viewport camera speed mode and manual override value round-trip through workspace save and load.
        /// </summary>
        [Fact]
        public void UiSaveAndLoad_WhenViewportUsesManualCameraSpeed_RestoresSpeedModeAndManualValue() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance viewportInstance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(viewportInstance);
            controller.ViewportState.Viewport.CameraSpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
            controller.ViewportState.Viewport.ManualCameraSpeedOverride = 6.5;

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);
            viewportInstance.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

            EditorWorkspacePanelInstance restoredViewport = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            Assert.Equal(EditorViewportCameraSpeedMode.ManualOverride, harness.GetViewportCameraSpeedMode(restoredViewport));
            Assert.Equal(6.5, harness.GetViewportManualCameraSpeed(restoredViewport));
        }

        /// <summary>
        /// Ensures tool-mode changes remain local to the viewport instance that receives them.
        /// </summary>
        [Fact]
        public void SetViewportToolMode_WhenSecondViewportChangesToolMode_DoesNotChangeFirstViewport() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");

            harness.SetViewportToolMode(instances[1], EditorViewportToolMode.Rotate);

            Assert.Equal(EditorViewportToolMode.Translate, harness.GetViewportToolMode(instances[0]));
            Assert.Equal(EditorViewportToolMode.Rotate, harness.GetViewportToolMode(instances[1]));
        }

        /// <summary>
        /// Ensures snap-value changes remain local to the viewport instance that receives them.
        /// </summary>
        [Fact]
        public void SetViewportSnapValue_WhenSecondViewportChangesSnap_DoesNotChangeFirstViewport() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");

            harness.SetViewportSnapValue(instances[1], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1, 22.5);

            Assert.Equal(5.0, harness.GetViewportSnapValue(instances[0], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(22.5, harness.GetViewportSnapValue(instances[1], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
        }

        /// <summary>
        /// Invokes one non-public instance method on a test target.
        /// </summary>
        /// <param name="instance">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object instance, string methodName, params object[] arguments) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(methodName)) {
                throw new ArgumentException("Method name must be provided.", nameof(methodName));
            }

            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            method.Invoke(instance, arguments);
        }

        /// <summary>
        /// Ensures closing one logger instance leaves its sibling logger instance open.
        /// </summary>
        [Fact]
        public void ClosePanel_WhenOneLoggerInstanceIsClosed_LeavesSiblingLoggerOpen() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowLogger);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowLogger);
            EditorWorkspacePanelInstance first = harness.Session.GetPanelInstancesForTest("logger")[0];
            EditorWorkspacePanelInstance second = harness.Session.GetPanelInstancesForTest("logger")[1];

            first.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            IReadOnlyList<EditorWorkspacePanelInstance> remaining = harness.Session.GetPanelInstancesForTest("logger");
            Assert.Single(remaining);
            Assert.Same(second, remaining[0]);
        }

        /// <summary>
        /// Ensures closing one viewport instance leaves its sibling viewport instance open.
        /// </summary>
        [Fact]
        public void ClosePanel_WhenOneViewportInstanceIsClosed_LeavesSiblingViewportOpen() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance first = harness.Session.GetPanelInstancesForTest("viewport")[0];
            EditorWorkspacePanelInstance second = harness.Session.GetPanelInstancesForTest("viewport")[1];

            first.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            IReadOnlyList<EditorWorkspacePanelInstance> remaining = harness.Session.GetPanelInstancesForTest("viewport");
            Assert.Single(remaining);
            Assert.Same(second, remaining[0]);
        }

        /// <summary>
        /// Ensures viewport-local camera, tool, clip-plane, canvas, and grid settings round-trip through one saved workspace slot.
        /// </summary>
        [Fact]
        public void UiSaveAndLoad_WhenWorkspaceContainsTwoViewports_RestoresViewportLocalState() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> original = harness.Session.GetPanelInstancesForTest("viewport");

            harness.SetViewportCameraPosition(original[0], new float3(1f, 2f, -3f));
            harness.SetViewportToolMode(original[1], EditorViewportToolMode.Scale);
            harness.SetViewportClipPlanes(original[1], 0.5f, 240f);
            harness.SetViewportPixelsPerWorldUnit(original[1], 144);
            harness.SetViewportGridVisible(original[1], false);
            harness.SetViewportSettingsOverlayOpen(original[1], true);
            harness.SetViewportSnapValue(original[0], EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1, 2.5);
            harness.SetViewportSnapValue(original[1], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2, 22.5);

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);

            harness.SetViewportCameraPosition(original[0], new float3(9f, 8f, 7f));
            harness.SetViewportToolMode(original[1], EditorViewportToolMode.Translate);
            harness.SetViewportClipPlanes(original[1], 2f, 80f);
            harness.SetViewportPixelsPerWorldUnit(original[1], 300);
            harness.SetViewportGridVisible(original[1], true);
            harness.SetViewportSettingsOverlayOpen(original[1], false);
            harness.SetViewportSnapValue(original[0], EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1, 0.25);
            harness.SetViewportSnapValue(original[1], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2, 15.0);

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

            IReadOnlyList<EditorWorkspacePanelInstance> restored = harness.Session.GetPanelInstancesForTest("viewport");
            Assert.Equal(2, restored.Count);
            Assert.Equal(new float3(1f, 2f, -3f), harness.GetViewportCameraPosition(restored[0]));
            Assert.Equal(EditorViewportToolMode.Scale, harness.GetViewportToolMode(restored[1]));
            Assert.Equal(0.5f, harness.GetViewportNearPlane(restored[1]));
            Assert.Equal(240f, harness.GetViewportFarPlane(restored[1]));
            Assert.Equal(144, harness.GetViewportPixelsPerWorldUnit(restored[1]));
            Assert.False(harness.IsViewportGridVisible(restored[1]));
            Assert.True(harness.IsViewportSettingsOverlayOpen(restored[1]));
            Assert.Equal(2.5, harness.GetViewportSnapValue(restored[0], EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(22.5, harness.GetViewportSnapValue(restored[1], EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
        }

        /// <summary>
        /// Ensures locked preview panels restore their persisted asset binding through one workspace save and load cycle.
        /// </summary>
        [Fact]
        public void UiSaveAndLoad_WhenPreviewPanelIsLockedToAsset_RestoresItsBinding() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
            EditorWorkspacePanelInstance previewInstance = Assert.Single(harness.Session.GetPanelInstancesForTest("preview"));
            PreviewPanel panel = Assert.IsType<PreviewPanel>(previewInstance.Dockable);
            string relativePath = harness.WriteSourceModel("Models/Locked.obj");
            panel.RestoreState(new PreviewPanelStateDocument {
                IsLocked = true,
                BindingKind = PreviewPanelBindingKind.Asset,
                AssetRelativePath = relativePath
            });

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);
            previewInstance.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

            PreviewPanel restored = Assert.IsType<PreviewPanel>(Assert.Single(harness.Session.GetPanelInstancesForTest("preview")).Dockable);
            PreviewPanelStateDocument state = restored.CaptureState();
            Assert.True(state.IsLocked);
            Assert.Equal(PreviewPanelBindingKind.Asset, state.BindingKind);
            Assert.Equal(relativePath, state.AssetRelativePath);
            Assert.IsType<ModelPreviewSource>(restored.ActivePreviewSource);
        }

        /// <summary>
        /// Ensures locked preview panels restore their persisted camera binding through one workspace save and load cycle.
        /// </summary>
        [Fact]
        public void UiSaveAndLoad_WhenPreviewPanelIsLockedToCamera_RestoresItsBinding() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
            EditorWorkspacePanelInstance previewInstance = Assert.Single(harness.Session.GetPanelInstancesForTest("preview"));
            PreviewPanel panel = Assert.IsType<PreviewPanel>(previewInstance.Dockable);
            EditorEntity cameraEntity = harness.CreatePreviewCameraEntity(17u);
            panel.RestoreState(new PreviewPanelStateDocument {
                IsLocked = true,
                BindingKind = PreviewPanelBindingKind.Camera,
                SceneEntityId = 17u
            });

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);
            previewInstance.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

            PreviewPanel restored = Assert.IsType<PreviewPanel>(Assert.Single(harness.Session.GetPanelInstancesForTest("preview")).Dockable);
            PreviewPanelStateDocument state = restored.CaptureState();
            Assert.True(state.IsLocked);
            Assert.Equal(PreviewPanelBindingKind.Camera, state.BindingKind);
            Assert.Equal(17u, state.SceneEntityId);
            Assert.IsType<CameraPreviewSource>(restored.ActivePreviewSource);
            Assert.NotNull(cameraEntity);
        }

        /// <summary>
        /// Ensures workspace restore skips unknown panel types and still restores known docked panels from the same slot.
        /// </summary>
        [Fact]
        public void RestoreWorkspaceSlot_WhenSlotContainsUnknownPanelType_SkipsItAndRestoresKnownPanels() {
            using EditorSessionHarness harness = EditorSessionHarness.Create();

            EditorWorkspaceSlotDocument slot = new EditorWorkspaceSlotDocument {
                SchemaVersion = 1,
                Panels = {
                    new EditorWorkspacePanelDocument {
                        InstanceId = "preview-known",
                        PanelTypeId = "preview",
                        IsDocked = true,
                        Title = "Preview",
                        State = new PreviewPanelStateDocument()
                    },
                    new EditorWorkspacePanelDocument {
                        InstanceId = "missing-1",
                        PanelTypeId = "missing-panel",
                        IsDocked = true,
                        Title = "Missing"
                    }
                },
                DockRoot = new EditorWorkspaceDockLeafNodeDocument {
                    InstanceIds = new List<string> { "missing-1", "preview-known" },
                    ActiveInstanceId = "missing-1"
                }
            };

            harness.RestoreWorkspaceSlot(slot);

            PreviewPanel restoredPreview = Assert.IsType<PreviewPanel>(Assert.Single(harness.Session.GetPanelInstancesForTest("preview")).Dockable);
            Assert.True(restoredPreview.IsDocked);
            Assert.Empty(harness.Session.GetPanelInstancesForTest("logger"));
        }

        /// <summary>
        /// Creates lightweight editor sessions suitable for workspace-instance tests.
        /// </summary>
        sealed class EditorSessionHarness : IDisposable {
            /// <summary>
            /// Editor session under test.
            /// </summary>
            public EditorSession Session { get; }

            /// <summary>
            /// Shared font used by dynamically created panels in the harness.
            /// </summary>
            readonly FontAsset Font;
            /// <summary>
            /// Shared icon set used by dynamically created viewport panels in the harness.
            /// </summary>
            readonly EditorViewportToolbarIconSet ViewportToolbarIcons;
            /// <summary>
            /// Shared content manager used by preview asset import services in the harness.
            /// </summary>
            readonly ContentManager ContentManager;
            /// <summary>
            /// Isolated project root used by workspace save and load tests.
            /// </summary>
            readonly string TempProjectRootPath;

            /// <summary>
            /// Creates one workspace test harness with the minimum editor dependencies.
            /// </summary>
            /// <returns>Initialized editor-session harness.</returns>
            public static EditorSessionHarness Create() {
                return new EditorSessionHarness();
            }

            /// <summary>
            /// Initializes the workspace test harness.
            /// </summary>
            EditorSessionHarness() {
                TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-workspace-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(TempProjectRootPath);
                Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
                EditorCore core = new EditorCore(new Project {
                    Name = "Workspace Tests",
                    Path = TempProjectRootPath
                });
                core.Initialize(
                    TestDirectX11RenderManager3D.Create(),
                    new TestRenderManager2D(),
                    null,
                    new PlatformInfo("test", "test-version"),
                    new CoreInitializationOptions {
                        ContentRootPath = TempProjectRootPath
                    });
                EditorKeyboardFocusService.Reset();
                GeneratedAssetProviderRegistry.ResetForTests();
                GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());

                Font = CreateFont();
                ViewportToolbarIcons = CreateViewportToolbarIcons();
                ContentManager = new ContentManager(TempProjectRootPath);
                EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(ContentManager);
                AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, ContentManager);
                assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
                assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
                assetImportManager.CurrentPlatformId = "windows";
                EditorProjectLocalSettingsService projectLocalSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new[] { "windows" });
                PreviewSourceResolver previewSourceResolver = new PreviewSourceResolver(assetImportManager, Core.Instance.RenderManager2D, Core.Instance.RenderManager3D);
                EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);
                EditorFileSystemFontResolver fileSystemFontResolver = new EditorFileSystemFontResolver(assetImportManager);
                Session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));

                SetPrivateField(Session, "dockingManager", new DockingManager());
                SetPrivateField(Session, "projectPath", TempProjectRootPath);
                SetPrivateField(Session, "uiFont", Font);
                SetPrivateField(Session, "SnapModifierFont", Font);
                SetPrivateField(Session, "ViewportToolbarIcons", ViewportToolbarIcons);
                SetPrivateField(Session, "CurrentUiMetrics", EditorUiMetrics.Default);
                SetPrivateField(Session, "SceneCreationService", new EditorSceneCreationService());
                SetPrivateField(Session, "PanelRegistry", new EditorWorkspacePanelRegistry());
                SetPrivateField(Session, "PanelInstances", new List<EditorWorkspacePanelInstance>());
                SetPrivateField(Session, "WorkspaceLayoutService", new EditorWorkspaceLayoutService(TempProjectRootPath));
                SetPrivateField(Session, "sceneCanvasProfileState", new EditorSceneCanvasProfileState());
                SetPrivateField(Session, "assetImportManager", assetImportManager);
                SetPrivateField(Session, "previewSourceResolver", previewSourceResolver);
                SetPrivateField(Session, "sceneAssetReferenceFactory", new SceneAssetReferenceFactory());
                SetPrivateField(Session, "sceneAssetReferenceResolver", new EditorSceneAssetReferenceResolver(ContentManager, TempProjectRootPath, fileSystemModelResolver, fileSystemFontResolver));
                SetPrivateField(Session, "ProjectSupportedPlatforms", new[] { "windows" });
                SetPrivateField(Session, "ProjectLocalSettingsService", projectLocalSettingsService);
                SetPrivateField(Session, "ActiveProjectPlatform", "windows");

                MethodInfo method = typeof(EditorSession).GetMethod("InitializePanelRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(Session, Array.Empty<object>());
            }

            /// <summary>
            /// Releases the lightweight panel instances created by the harness.
            /// </summary>
            public void Dispose() {
                IReadOnlyList<EditorWorkspacePanelInstance> instances = Session.GetPanelInstancesForTest("viewport")
                    .Concat(Session.GetPanelInstancesForTest("scene-hierarchy"))
                    .Concat(Session.GetPanelInstancesForTest("asset-browser"))
                    .Concat(Session.GetPanelInstancesForTest("properties"))
                    .Concat(Session.GetPanelInstancesForTest("preview"))
                    .Concat(Session.GetPanelInstancesForTest("logger"))
                    .ToArray();
                for (int index = 0; index < instances.Count; index++) {
                    EditorKeyboardFocusService.UnregisterGroup(instances[index].Dockable);
                    instances[index].Controller.Dispose();
                }

                EditorKeyboardFocusService.Reset();
                EditorSelectionService.ClearSelection();
                Core.Instance.Dispose();
                if (Directory.Exists(TempProjectRootPath)) {
                    Directory.Delete(TempProjectRootPath, true);
                }
                GeneratedAssetProviderRegistry.ResetForTests();
            }

            /// <summary>
            /// Writes one minimal model source file inside the harness project assets root.
            /// </summary>
            /// <param name="relativePath">Project-relative path assigned to the created asset.</param>
            /// <returns>Normalized relative path written to disk.</returns>
            public string WriteSourceModel(string relativePath) {
                if (string.IsNullOrWhiteSpace(relativePath)) {
                    throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
                }

                string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(TempProjectRootPath, "assets", normalizedRelativePath);
                string directoryPath = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directoryPath)) {
                    throw new InvalidOperationException("Model test asset directory could not be resolved.");
                }

                Directory.CreateDirectory(directoryPath);
                File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3, 4 });
                return relativePath.Replace('\\', '/');
            }

            /// <summary>
            /// Creates one camera entity with a stable scene id and registers it with the live object manager.
            /// </summary>
            /// <param name="entityId">Stable scene entity id assigned to the camera entity.</param>
            /// <returns>Registered camera entity.</returns>
            public EditorEntity CreatePreviewCameraEntity(uint entityId) {
                if (entityId == 0u) {
                    throw new ArgumentException("Entity id must be non-zero.", nameof(entityId));
                }

                EditorEntity cameraEntity = new EditorEntity();
                EntitySaveComponent saveComponent = FindComponent<EntitySaveComponent>(cameraEntity);
                if (saveComponent == null) {
                    throw new InvalidOperationException("Camera entity is missing the required save component.");
                }

                saveComponent.EntityId = entityId;
                cameraEntity.Position = new float3(2f, 3f, -5f);
                cameraEntity.AddComponent(new CameraComponent {
                    CameraDrawOrder = 4,
                    LayerMask = EditorLayerMasks.SceneObjects,
                    Viewport = new float4(0f, 0f, 160f, 90f)
                });
                return cameraEntity;
            }

            /// <summary>
            /// Finds the first component of the requested type on one entity.
            /// </summary>
            /// <typeparam name="T">Component type to locate.</typeparam>
            /// <param name="entity">Entity whose components should be searched.</param>
            /// <returns>Matching component instance when present; otherwise null.</returns>
            T FindComponent<T>(Entity entity) where T : Component {
                if (entity == null || entity.Components == null) {
                    return null;
                }

                for (int index = 0; index < entity.Components.Count; index++) {
                    if (entity.Components[index] is T component) {
                        return component;
                    }
                }

                return null;
            }

            /// <summary>
            /// Assigns one non-public instance field.
            /// </summary>
            /// <param name="target">Object that owns the field.</param>
            /// <param name="fieldName">Name of the field to assign.</param>
            /// <param name="value">Value assigned to the field.</param>
            void SetPrivateField(object target, string fieldName, object value) {
                FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) {
                    throw new InvalidOperationException("Expected private field was not found.");
                }

                field.SetValue(target, value);
            }

            /// <summary>
            /// Creates a small font asset suitable for logger and preview panel construction.
            /// </summary>
            /// <returns>Font asset with basic glyph metrics for workspace panel tests.</returns>
            FontAsset CreateFont() {
                Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                    ['L'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                    ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['+'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['-'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                    ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                    ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                    ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                    ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                    ['z'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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

            /// <summary>
            /// Updates the scene camera position for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="position">World position assigned to the viewport camera entity.</param>
            public void SetViewportCameraPosition(EditorWorkspacePanelInstance instance, float3 position) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                controller.ViewportState.SceneCameraEntity.Position = position;
            }

            /// <summary>
            /// Reads the scene camera position for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>World position of the viewport camera entity.</returns>
            public float3 GetViewportCameraPosition(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.SceneCameraEntity.Position;
            }

            /// <summary>
            /// Updates the active tool mode for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="toolMode">Tool mode assigned to the viewport.</param>
            public void SetViewportToolMode(EditorWorkspacePanelInstance instance, EditorViewportToolMode toolMode) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                controller.ViewportState.Viewport.ToolMode = toolMode;
            }

            /// <summary>
            /// Reads the active tool mode for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Active viewport tool mode.</returns>
            public EditorViewportToolMode GetViewportToolMode(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.Viewport.ToolMode;
            }

            /// <summary>
            /// Updates the near and far clip planes for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="nearPlane">Near clip plane distance to assign.</param>
            /// <param name="farPlane">Far clip plane distance to assign.</param>
            public void SetViewportClipPlanes(EditorWorkspacePanelInstance instance, float nearPlane, float farPlane) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                controller.ViewportState.SceneCamera.NearPlaneDistance = nearPlane;
                controller.ViewportState.SceneCamera.FarPlaneDistance = farPlane;
            }

            /// <summary>
            /// Reads the near clip plane for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Near clip plane distance.</returns>
            public float GetViewportNearPlane(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.SceneCamera.NearPlaneDistance;
            }

            /// <summary>
            /// Reads the far clip plane for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Far clip plane distance.</returns>
            public float GetViewportFarPlane(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.SceneCamera.FarPlaneDistance;
            }

            /// <summary>
            /// Reads the viewport-local camera speed mode for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Viewport-local camera speed mode.</returns>
            public byte GetViewportCameraSpeedMode(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.Viewport.CameraSpeedMode;
            }

            /// <summary>
            /// Reads the viewport-local manual camera speed override for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Viewport-local manual camera speed override value.</returns>
            public double GetViewportManualCameraSpeed(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.Viewport.ManualCameraSpeedOverride;
            }

            /// <summary>
            /// Updates the viewport-local canvas density for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="pixelsPerWorldUnit">Canvas density assigned to the viewport preview settings.</param>
            public void SetViewportPixelsPerWorldUnit(EditorWorkspacePanelInstance instance, int pixelsPerWorldUnit) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                controller.ViewportState.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit = pixelsPerWorldUnit;
            }

            /// <summary>
            /// Reads the viewport-local canvas density for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Canvas density in pixels per world unit.</returns>
            public int GetViewportPixelsPerWorldUnit(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit;
            }

            /// <summary>
            /// Updates viewport grid visibility for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="isVisible">True to include the grid layer; false to hide it.</param>
            public void SetViewportGridVisible(EditorWorkspacePanelInstance instance, bool isVisible) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                ushort layerMask = controller.ViewportState.SceneCamera.LayerMask;
                if (isVisible) {
                    controller.ViewportState.SceneCamera.LayerMask = (ushort)(layerMask | EditorLayerMasks.SceneGrid);
                    return;
                }

                controller.ViewportState.SceneCamera.LayerMask = (ushort)(layerMask & ~EditorLayerMasks.SceneGrid);
            }

            /// <summary>
            /// Returns whether the viewport grid layer is visible for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>True when the viewport grid layer is enabled.</returns>
            public bool IsViewportGridVisible(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return (controller.ViewportState.SceneCamera.LayerMask & EditorLayerMasks.SceneGrid) != 0;
            }

            /// <summary>
            /// Opens or closes the settings overlay for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="isOpen">True to open the settings overlay; false to close it.</param>
            public void SetViewportSettingsOverlayOpen(EditorWorkspacePanelInstance instance, bool isOpen) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                controller.ViewportState.Viewport.SetSettingsOverlayOpen(isOpen);
            }

            /// <summary>
            /// Returns whether the settings overlay is open for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>True when the viewport settings overlay is open.</returns>
            public bool IsViewportSettingsOverlayOpen(EditorWorkspacePanelInstance instance) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return controller.ViewportState.Viewport.IsSettingsOverlayVisible;
            }

            /// <summary>
            /// Updates one snap value for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to update.</param>
            /// <param name="toolMode">Tool mode whose snap value should be updated.</param>
            /// <param name="snapSlot">Snap slot to update.</param>
            /// <param name="value">Snap value assigned to the viewport-local snap settings.</param>
            public void SetViewportSnapValue(EditorWorkspacePanelInstance instance, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double value) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                TransformGizmoSnapSettingsService.SetSnapValue(controller.ViewportState.SceneCamera, toolMode, snapSlot, value);
            }

            /// <summary>
            /// Reads one snap value for one tracked viewport instance.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <param name="toolMode">Tool mode whose snap value should be read.</param>
            /// <param name="snapSlot">Snap slot to read.</param>
            /// <returns>Viewport-local snap value.</returns>
            public double GetViewportSnapValue(EditorWorkspacePanelInstance instance, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
                ViewportWorkspacePanelController controller = GetViewportController(instance);
                return TransformGizmoSnapSettingsService.GetSnapValue(controller.ViewportState.SceneCamera, toolMode, snapSlot);
            }

            /// <summary>
            /// Casts one tracked panel instance back to its viewport workspace controller.
            /// </summary>
            /// <param name="instance">Tracked viewport panel instance.</param>
            /// <returns>Viewport workspace controller for the supplied instance.</returns>
            ViewportWorkspacePanelController GetViewportController(EditorWorkspacePanelInstance instance) {
                if (instance == null) {
                    throw new ArgumentNullException(nameof(instance));
                }

                return Assert.IsType<ViewportWorkspacePanelController>(instance.Controller);
            }

            /// <summary>
            /// Exposes the tracked viewport controller for one instance so outer tests can assert session accessor routing.
            /// </summary>
            /// <param name="instance">Tracked viewport instance to inspect.</param>
            /// <returns>Viewport workspace controller for the supplied instance.</returns>
            public ViewportWorkspacePanelController GetViewportControllerForTest(EditorWorkspacePanelInstance instance) {
                return GetViewportController(instance);
            }

            /// <summary>
            /// Restores one workspace slot document through the session's private restore path.
            /// </summary>
            /// <param name="slot">Workspace slot document to restore.</param>
            public void RestoreWorkspaceSlot(EditorWorkspaceSlotDocument slot) {
                if (slot == null) {
                    throw new ArgumentNullException(nameof(slot));
                }

                MethodInfo method = typeof(EditorSession).GetMethod("RestoreWorkspaceSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) {
                    throw new InvalidOperationException("Expected private RestoreWorkspaceSlot method was not found.");
                }

                method.Invoke(Session, new object[] { slot });
            }

            /// <summary>
            /// Counts registered entities whose name matches the supplied value exactly.
            /// </summary>
            /// <param name="entityName">Exact entity name to count.</param>
            /// <returns>Number of matching entities in the object manager.</returns>
            public int CountEntitiesNamed(string entityName) {
                if (string.IsNullOrWhiteSpace(entityName)) {
                    throw new ArgumentException("Entity name must be provided.", nameof(entityName));
                }

                return Core.Instance.ObjectManager.Entities.Count(entity => entity is EditorEntity editorEntity && string.Equals(editorEntity.Name, entityName, StringComparison.Ordinal));
            }

            /// <summary>
            /// Creates one deterministic viewport toolbar icon set for workspace viewport tests.
            /// </summary>
            /// <returns>Toolbar icon set backed by reusable test runtime textures.</returns>
            EditorViewportToolbarIconSet CreateViewportToolbarIcons() {
                RuntimeTexture icon = CreateToolbarTexture();
                return new EditorViewportToolbarIconSet(
                    icon,
                    icon,
                    icon,
                    icon,
                    icon,
                    icon,
                    icon,
                    icon,
                    icon,
                    icon);
            }

            /// <summary>
            /// Creates one deterministic runtime texture used by viewport toolbar controls.
            /// </summary>
            /// <returns>Runtime texture used by the viewport toolbar icon set.</returns>
            RuntimeTexture CreateToolbarTexture() {
                return new TestRuntimeTexture {
                    Width = 16,
                    Height = 16
                };
            }
        }
    }
}

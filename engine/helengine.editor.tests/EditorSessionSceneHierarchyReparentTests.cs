using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene-hierarchy reparent workflow inside the editor session.
    /// </summary>
    public class EditorSessionSceneHierarchyReparentTests : IDisposable {
        /// <summary>
        /// Tolerance used when comparing world-space transform values after reparenting.
        /// </summary>
        const float TransformTolerance = 0.0001f;

        /// <summary>
        /// Temporary project root used by session reparent tests.
        /// </summary>
        readonly string TempProjectRootPath;
        /// <summary>
        /// Configurable input manager used to drive pointer-routing assertions.
        /// </summary>
        readonly TestInputManager Input;

        /// <summary>
        /// Initializes an isolated project root and core services for session reparent tests.
        /// </summary>
        public EditorSessionSceneHierarchyReparentTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-scene-reparent-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            Input = new TestInputManager();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Deletes temporary project state and clears editor-wide services after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();

            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a hierarchy reparent request shows the modal with the requested entity and includes invalid descendants for disabled display.
        /// </summary>
        [Fact]
        public void HandleSceneHierarchyReparentRequested_ShowsDialogWithVisibleHierarchyIncludingInvalidDescendants() {
            EditorSession session = CreateSessionForReparent();
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");
            EditorEntity grandchild = CreateSceneEntity("Grandchild");
            rootA.AddChild(child);
            child.AddChild(grandchild);

            InvokePrivate(session, "HandleSceneHierarchyReparentRequested", child);

            ReparentEntityDialog dialog = GetPrivateField<ReparentEntityDialog>(session, "reparentEntityDialog");

            Assert.True(dialog.IsVisible);
            Assert.Same(child, dialog.TargetEntity);
            Assert.Equal(4, dialog.AvailableParentEntities.Count);
            Assert.Same(rootA, dialog.SelectedParentEntity);
            Assert.Contains(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, rootA));
            Assert.Contains(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, rootB));
            Assert.Contains(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, child));
            Assert.Contains(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, grandchild));
        }

        /// <summary>
        /// Ensures the reparent dialog greys out invalid targets and ignores their row clicks while still allowing valid parent selection.
        /// </summary>
        [Fact]
        public void ReparentEntityDialog_WhenRowsAreClicked_IgnoresInvalidTargetsAndSelectsValidParents() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");
            EditorEntity grandchild = CreateSceneEntity("Grandchild");
            rootA.AddChild(child);
            child.AddChild(grandchild);

            dialog.Show(child, new Entity[] { rootA, rootB, child, grandchild });
            dialog.UpdateLayout(640, 480);

            SceneHierarchyRow invalidRow = FindDialogRow(dialog, grandchild);
            SceneHierarchyRow validRow = FindDialogRow(dialog, rootB);

            Assert.Equal(ThemeManager.Colors.AccentQuaternary, invalidRow.Label.Color);
            Assert.Same(rootA, dialog.SelectedParentEntity);

            ClickRowBody(invalidRow);

            Assert.Same(rootA, dialog.SelectedParentEntity);

            ClickRowBody(validRow);

            Assert.Same(rootB, dialog.SelectedParentEntity);
        }

        /// <summary>
        /// Ensures pointer input routed through the runtime input manager can activate one valid reparent row.
        /// </summary>
        [Fact]
        public void ReparentEntityDialog_WhenVisible_RoutesPointerClicksToSelectableRows() {
            CreateUiCamera(640, 480);

            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");
            rootA.AddChild(child);

            dialog.Show(child, new Entity[] { rootA, rootB, child });
            dialog.UpdateLayout(640, 480);

            SceneHierarchyRow validRow = FindDialogRow(dialog, rootB);

            ClickRowBodyThroughInput(validRow);

            Assert.Same(rootB, dialog.SelectedParentEntity);
        }

        /// <summary>
        /// Ensures the reparent hierarchy picker can select one row even when the dialog appears under a stationary cursor.
        /// </summary>
        [Fact]
        public void ReparentEntityDialog_WhenShownUnderStationaryPointer_ClicksSelectableRows() {
            CreateUiCamera(640, 480);

            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");
            rootA.AddChild(child);

            dialog.Show(child, new Entity[] { rootA, rootB, child });
            dialog.UpdateLayout(640, 480);

            SceneHierarchyRow validRow = FindDialogRow(dialog, rootB);
            int2 pointer = GetRowBodyPointer(validRow);
            MouseState releasedState = new MouseState(pointer.X, pointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            MouseState pressedState = new MouseState(pointer.X, pointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            dialog.Hide();

            AdvanceInput(releasedState);

            dialog.Show(child, new Entity[] { rootA, rootB, child });
            dialog.UpdateLayout(640, 480);

            AdvanceInput(pressedState);
            AdvanceInput(releasedState);

            Assert.Same(rootB, dialog.SelectedParentEntity);
        }

        /// <summary>
        /// Ensures confirming a reparent request moves the entity, keeps it selected, hides the dialog, and marks the scene dirty.
        /// </summary>
        [Fact]
        public void HandleReparentEntityDialogConfirmed_ReparentsEntityAndMarksSceneDirty() {
            EditorSession session = CreateSessionForReparent();
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");
            rootA.AddChild(child);

            ReparentEntityDialog dialog = GetPrivateField<ReparentEntityDialog>(session, "reparentEntityDialog");
            dialog.Show(child, new Entity[] { null, rootB });
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            Action handleSceneMutated = () => InvokePrivate(session, "HandleSceneMutated");

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;

                InvokePrivate(session, "HandleReparentEntityDialogConfirmed", new ReparentEntityDialogSelection(child, rootB));

                Assert.Same(rootB, child.Parent);
                Assert.DoesNotContain(child, rootA.Children);
                Assert.Contains(child, rootB.Children);
                Assert.Same(child, EditorSelectionService.SelectedEntity);
                Assert.False(dialog.IsVisible);
                Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
            }
        }

        /// <summary>
        /// Ensures confirming a reparent request preserves the entity's current world position and visible size.
        /// </summary>
        [Fact]
        public void HandleReparentEntityDialogConfirmed_PreservesWorldPositionAndScale() {
            EditorSession session = CreateSessionForReparent();
            EditorEntity rootA = CreateSceneEntity("Root A");
            EditorEntity rootB = CreateSceneEntity("Root B");
            EditorEntity child = CreateSceneEntity("Child");

            rootA.LocalPosition = new float3(10f, 0f, 0f);
            rootA.LocalScale = new float3(1f, 2f, 3f);

            rootB.LocalPosition = new float3(30f, 0f, 0f);
            rootB.LocalScale = new float3(5f, 6f, 7f);
            rootB.LocalOrientation = CreateYawOrientation((float)(Math.PI / 2.0));

            child.LocalPosition = new float3(4f, 0f, 0f);
            child.LocalScale = new float3(2f, 3f, 4f);

            rootA.AddChild(child);

            float3 originalWorldPosition = child.Position;
            float3 originalWorldScale = child.Scale;

            ReparentEntityDialog dialog = GetPrivateField<ReparentEntityDialog>(session, "reparentEntityDialog");
            dialog.Show(child, new Entity[] { null, rootB });

            InvokePrivate(session, "HandleReparentEntityDialogConfirmed", new ReparentEntityDialogSelection(child, rootB));

            AssertFloat3Equal(originalWorldPosition, child.Position);
            AssertFloat3Equal(originalWorldScale, child.Scale);
        }

        /// <summary>
        /// Creates a partially initialized editor session containing only the collaborators used by reparent handlers.
        /// </summary>
        /// <returns>Editor session instance configured for reparent tests.</returns>
        EditorSession CreateSessionForReparent() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SceneHierarchyPanel sceneHierarchyPanel = new SceneHierarchyPanel(CreateFont());
            ReparentEntityDialog reparentEntityDialog = new ReparentEntityDialog(CreateFont());
            EditorEntityReparentService reparentService = new EditorEntityReparentService();

            SetPrivateField(session, "sceneHierarchyPanel", sceneHierarchyPanel);
            SetPrivateField(session, "reparentEntityDialog", reparentEntityDialog);
            SetPrivateField(session, "ReparentService", reparentService);

            return session;
        }

        /// <summary>
        /// Creates one user-authored scene entity for reparent tests.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <returns>Configured scene entity.</returns>
        EditorEntity CreateSceneEntity(string name) {
            return new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects
            };
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Reads one non-public instance field value without casting it to a specific runtime type.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Raw field value.</returns>
        object GetPrivateFieldValue(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field.GetValue(target);
        }

        /// <summary>
        /// Finds the visible dialog row representing the requested entity.
        /// </summary>
        /// <param name="dialog">Dialog containing the picker rows.</param>
        /// <param name="entity">Entity represented by the desired row.</param>
        /// <returns>Matching visible row.</returns>
        SceneHierarchyRow FindDialogRow(ReparentEntityDialog dialog, Entity entity) {
            object hierarchyView = GetPrivateFieldValue(dialog, "ParentHierarchyView");
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(hierarchyView, "rows");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (!row.Entity.Enabled) {
                    continue;
                }
                if (ReferenceEquals(row.NodeEntity, entity)) {
                    return row;
                }
            }

            string entityLabel = entity is EditorEntity editorEntity ? editorEntity.Name : entity.GetType().Name;
            throw new InvalidOperationException($"Expected to find a visible reparent-dialog row for '{entityLabel}'.");
        }

        /// <summary>
        /// Clicks the non-arrow body region of one dialog row.
        /// </summary>
        /// <param name="row">Row to activate.</param>
        void ClickRowBody(SceneHierarchyRow row) {
            int2 point = new int2(Math.Max(row.ArrowHitLeft + row.ArrowHitWidth + 8, 32), SceneHierarchyPanel.RowHeight / 2);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Release);
        }

        /// <summary>
        /// Clicks the non-arrow body region of one dialog row through the runtime input-routing path.
        /// </summary>
        /// <param name="row">Row to activate.</param>
        void ClickRowBodyThroughInput(SceneHierarchyRow row) {
            int2 pointer = GetRowBodyPointer(row);
            int pointerX = pointer.X;
            int pointerY = pointer.Y;
            MouseState releasedState = new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            MouseState pressedState = new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            AdvanceInput(releasedState);
            AdvanceInput(pressedState);
            AdvanceInput(releasedState);
        }

        /// <summary>
        /// Returns one screen-space pointer position centered in the clickable body region of the provided row.
        /// </summary>
        /// <param name="row">Row whose body position should be resolved.</param>
        /// <returns>Screen-space pointer position inside the row body.</returns>
        int2 GetRowBodyPointer(SceneHierarchyRow row) {
            int bodyOffsetX = Math.Max(row.ArrowHitLeft + row.ArrowHitWidth + 8, 32);
            int pointerX = (int)Math.Round(row.Entity.Position.X) + bodyOffsetX;
            int pointerY = (int)Math.Round(row.Entity.Position.Y) + (SceneHierarchyPanel.RowHeight / 2);
            return new int2(pointerX, pointerY);
        }

        /// <summary>
        /// Advances the test input state by one core frame.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }

        /// <summary>
        /// Creates the UI camera used to route modal hit testing.
        /// </summary>
        /// <param name="width">Viewport width.</param>
        /// <param name="height">Viewport height.</param>
        void CreateUiCamera(int width, int height) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.EditorUi,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy hierarchy and modal layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['B'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
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
        /// Verifies two <see cref="float3"/> values are equal within the configured transform tolerance.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        void AssertFloat3Equal(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, TransformTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, TransformTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, TransformTolerance);
        }

        /// <summary>
        /// Creates one yaw-only orientation for transform-focused tests.
        /// </summary>
        /// <param name="yawRadians">Yaw angle in radians.</param>
        /// <returns>Quaternion representing the requested yaw rotation.</returns>
        float4 CreateYawOrientation(float yawRadians) {
            float4.CreateFromYawPitchRoll(yawRadians, 0f, 0f, out float4 orientation);
            return orientation;
        }
    }
}

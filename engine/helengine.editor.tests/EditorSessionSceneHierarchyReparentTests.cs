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
        /// Temporary project root used by session reparent tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and core services for session reparent tests.
        /// </summary>
        public EditorSessionSceneHierarchyReparentTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-scene-reparent-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
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
        /// Ensures a hierarchy reparent request shows the modal with the requested entity and excludes invalid descendants.
        /// </summary>
        [Fact]
        public void HandleSceneHierarchyReparentRequested_ShowsDialogWithValidParentChoices() {
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
            Assert.Equal(2, dialog.AvailableParentEntities.Count);
            Assert.Null(dialog.AvailableParentEntities[0]);
            Assert.Same(rootA, dialog.SelectedParentEntity);
            Assert.Contains(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, rootB));
            Assert.DoesNotContain(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, child));
            Assert.DoesNotContain(dialog.AvailableParentEntities, candidate => ReferenceEquals(candidate, grandchild));
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
    }
}

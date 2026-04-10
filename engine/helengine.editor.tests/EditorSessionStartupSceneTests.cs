using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session bootstrap leaves new scenes empty.
    /// </summary>
    public class EditorSessionStartupSceneTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the core instance created for each test.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required to exercise editor-session scene bootstrap behavior.
        /// </summary>
        public EditorSessionStartupSceneTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-startup-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the editor startup scene builder does not create any user scene entities.
        /// </summary>
        [Fact]
        public void BuildStartScene_WhenCalled_LeavesSceneEmpty() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            ObjectManager objectManager = Core.Instance.ObjectManager;

            MethodInfo method = typeof(EditorSession).GetMethod("BuildStartScene", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            Assert.NotNull(method);

            method.Invoke(session, Array.Empty<object>());

            Assert.Empty(GetUserSceneEntities(objectManager));
        }

        /// <summary>
        /// Collects registered user-authored scene entities currently tracked by the object manager.
        /// </summary>
        /// <param name="objectManager">Object manager whose entities should be filtered.</param>
        /// <returns>List of non-internal scene entities.</returns>
        IReadOnlyList<EditorEntity> GetUserSceneEntities(ObjectManager objectManager) {
            if (objectManager == null) {
                throw new ArgumentNullException(nameof(objectManager));
            }

            List<EditorEntity> entities = new List<EditorEntity>();
            for (int i = 0; i < objectManager.Entities.Count; i++) {
                if (objectManager.Entities[i] is EditorEntity editorEntity &&
                    !editorEntity.InternalEntity &&
                    editorEntity.LayerMask == EditorLayerMasks.SceneObjects) {
                    entities.Add(editorEntity);
                }
            }

            return entities;
        }
    }
}

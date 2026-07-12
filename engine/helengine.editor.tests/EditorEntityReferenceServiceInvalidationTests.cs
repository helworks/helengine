using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies static editor entity-reference services invalidate disposed runtime entities before later callers can use stale references.
    /// </summary>
    public sealed class EditorEntityReferenceServiceInvalidationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one deterministic runtime core for editor entity-reference invalidation tests.
        /// </summary>
        public EditorEntityReferenceServiceInvalidationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-entity-reference-service-invalidation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears static editor state and deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.Reset();
            EditorGizmoHoverService.ClearHoveredHandle();

            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures reading the selected entity after it was disposed clears the stale selection instead of exposing one dead entity reference.
        /// </summary>
        [Fact]
        public void SelectedEntity_WhenStoredEntityWasDisposed_ReturnsNull() {
            Entity entity = CreateInitializedEntity();
            EditorSelectionService.SetSelectedEntity(entity);

            entity.Dispose();

            Assert.Null(EditorSelectionService.SelectedEntity);
        }

        /// <summary>
        /// Ensures reading the hovered handle after it was disposed clears the stale hover state instead of exposing one dead entity reference.
        /// </summary>
        [Fact]
        public void HoveredHandleEntity_WhenStoredEntityWasDisposed_ReturnsNull() {
            Entity entity = CreateInitializedEntity();
            EditorGizmoHoverService.SetHoveredHandle(entity);

            entity.Dispose();

            Assert.Null(EditorGizmoHoverService.HoveredHandleEntity);
            Assert.Null(EditorGizmoHoverService.HoveredHandleCamera);
        }

        /// <summary>
        /// Creates one initialized runtime entity that can participate in disposal flows.
        /// </summary>
        /// <returns>Initialized entity.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}

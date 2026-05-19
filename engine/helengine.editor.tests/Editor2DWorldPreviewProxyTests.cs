using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-owned 2D world-preview proxy mappings between authored scene entities and internal preview entities.
    /// </summary>
    public sealed class Editor2DWorldPreviewProxyTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by preview-proxy registry tests.
        /// </summary>
        public Editor2DWorldPreviewProxyTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            EditorWorldSpace2DPreviewRegistry.Clear();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures one registered preview proxy resolves back to the authored source entity.
        /// </summary>
        [Fact]
        public void RegisterPreviewProxy_WhenSourceEntityIsMapped_ResolvesPreviewBackToSourceEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };

            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity));
            Assert.Same(previewEntity, EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
        }

        /// <summary>
        /// Ensures removing one source entity clears both the source-to-preview and preview-to-source mappings.
        /// </summary>
        [Fact]
        public void RemovePreviewProxy_WhenSourceEntityIsRemoved_ClearsBothDirections() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            EditorWorldSpace2DPreviewRegistry.RemoveBySourceEntity(sourceEntity);

            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity));
        }
    }
}

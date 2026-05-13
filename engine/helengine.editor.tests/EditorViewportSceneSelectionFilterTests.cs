using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport scene selection excludes internal editor infrastructure.
    /// </summary>
    public class EditorViewportSceneSelectionFilterTests {
        /// <summary>
        /// Ensures user-authored scene drawables remain selectable.
        /// </summary>
        [Fact]
        public void ShouldIncludeDrawableForSelection_WhenDrawableBelongsToUserSceneEntity_ReturnsTrue() {
            MeshComponent meshComponent = CreateMeshComponent(false);

            bool result = EditorViewportSceneSelectionFilter.ShouldIncludeDrawableForSelection(meshComponent);

            Assert.True(result);
        }

        /// <summary>
        /// Ensures internal editor scene drawables are excluded from scene selection.
        /// </summary>
        [Fact]
        public void ShouldIncludeDrawableForSelection_WhenDrawableBelongsToInternalEntity_ReturnsFalse() {
            MeshComponent meshComponent = CreateMeshComponent(true);

            bool result = EditorViewportSceneSelectionFilter.ShouldIncludeDrawableForSelection(meshComponent);

            Assert.False(result);
        }

        /// <summary>
        /// Ensures hidden drawable children still participate in viewport selection when their owning entity is user-authored.
        /// </summary>
        [Fact]
        public void ShouldIncludeDrawableForSelection_WhenDrawableBelongsToInternalChildOfSelectableEntity_ReturnsTrue() {
            MeshComponent meshComponent = CreateMeshComponentWithInternalChild();

            bool result = EditorViewportSceneSelectionFilter.ShouldIncludeDrawableForSelection(meshComponent);

            Assert.True(result);
        }

        /// <summary>
        /// Ensures viewport selection resolves one hidden drawable child back to its selectable owning entity.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntity_WhenEntityIsInternalChildOfSelectableEntity_ReturnsSelectableParent() {
            InitializeCore();

            EditorEntity parentEntity = new EditorEntity {
                InternalEntity = false,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EditorEntity childEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneCameraVisuals
            };
            parentEntity.AddChild(childEntity);

            Entity result = EditorViewportSceneSelectionFilter.ResolveSelectableEntity(childEntity);

            Assert.Same(parentEntity, result);
        }

        /// <summary>
        /// Creates one mesh component attached to an editor entity with the desired internal flag.
        /// </summary>
        /// <param name="internalEntity">True when the owning entity should be marked internal.</param>
        /// <returns>Mesh component attached to the configured entity.</returns>
        MeshComponent CreateMeshComponent(bool internalEntity) {
            InitializeCore();

            EditorEntity entity = new EditorEntity {
                InternalEntity = internalEntity,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            var meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial()
            };
            entity.AddComponent(meshComponent);
            return meshComponent;
        }

        /// <summary>
        /// Creates one mesh component attached to an internal child entity whose parent remains selectable.
        /// </summary>
        /// <returns>Mesh component attached to the configured child entity.</returns>
        MeshComponent CreateMeshComponentWithInternalChild() {
            InitializeCore();

            EditorEntity parentEntity = new EditorEntity {
                InternalEntity = false,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EditorEntity childEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneCameraVisuals
            };
            var meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial()
            };

            parentEntity.AddChild(childEntity);
            childEntity.AddComponent(meshComponent);
            return meshComponent;
        }

        /// <summary>
        /// Initializes the global core instance used by the current test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}

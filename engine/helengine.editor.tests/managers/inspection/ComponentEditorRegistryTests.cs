using helengine.editor.tests.testing;

namespace helengine.editor.tests.inspection {
    /// <summary>
    /// Verifies the central component editor registry and the built-in box collider scene selection editor.
    /// </summary>
    public sealed class ComponentEditorRegistryTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by entity-backed selection editor tests.
        /// </summary>
        public ComponentEditorRegistryTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }
        /// <summary>
        /// Ensures the registry ships one scene selection editor that supports authored box colliders.
        /// </summary>
        [Fact]
        public void Registry_WithDefaults_SupportsBoxColliderSceneSelection() {
            BoxCollider3DComponent boxCollider = new BoxCollider3DComponent();

            bool supported = false;
            for (int index = 0; index < ComponentEditorRegistry.SceneSelectionEditors.Count; index++) {
                if (ComponentEditorRegistry.SceneSelectionEditors[index].Supports(boxCollider)) {
                    supported = true;
                    break;
                }
            }

            Assert.True(supported);
        }

        /// <summary>
        /// Ensures the registry ships the previously hardcoded properties-panel custom property editor providers.
        /// </summary>
        [Fact]
        public void Registry_WithDefaults_ContainsBuiltInPropertyEditorProviders() {
            Assert.Contains(ComponentEditorRegistry.PropertyEditorProviders, provider => provider is CameraClearSettingsPropertyEditorProvider);
            Assert.Contains(ComponentEditorRegistry.PropertyEditorProviders, provider => provider is SceneMapPropertyEditorProvider);
        }

        /// <summary>
        /// Ensures externally registered scene selection editors become visible to the editor loop.
        /// </summary>
        [Fact]
        public void RegisterSceneSelectionEditor_WithCustomEditor_AppearsInRegistry() {
            RecordingSceneSelectionEditor editor = new RecordingSceneSelectionEditor();

            ComponentEditorRegistry.RegisterSceneSelectionEditor(editor);

            Assert.Contains(ComponentEditorRegistry.SceneSelectionEditors, registered => ReferenceEquals(registered, editor));
        }

        /// <summary>
        /// Ensures the box collider selection editor applies the effective world-space size, matching the physics size-times-scale convention.
        /// </summary>
        [Fact]
        public void BoxColliderEditor_UpdateSelectionVisual_AppliesSizeTimesEntityScale() {
            BoxCollider3DSceneSelectionEditor editor = new BoxCollider3DSceneSelectionEditor();
            Entity selectedEntity = new Entity {
                LocalPosition = new float3(1f, 2f, 3f),
                LocalScale = new float3(7f, 1f, 9f),
                LocalOrientation = float4.Identity
            };
            BoxCollider3DComponent boxCollider = new BoxCollider3DComponent {
                Size = new float3(1f, 0.5f, 2f)
            };
            EditorEntity visualEntity = new EditorEntity {
                InternalEntity = true
            };

            editor.UpdateSelectionVisual(visualEntity, selectedEntity, boxCollider);

            Assert.Equal(new float3(1f, 2f, 3f), visualEntity.LocalPosition);
            Assert.Equal(new float3(7f, 0.5f, 18f), visualEntity.LocalScale);
        }

        /// <summary>
        /// Minimal scene selection editor used to observe registry registration.
        /// </summary>
        sealed class RecordingSceneSelectionEditor : IComponentSceneSelectionEditor {
            public bool Supports(Component component) {
                return false;
            }

            public EditorEntity CreateSelectionVisual(RenderManager3D render3D, Entity selectedEntity, Component component) {
                throw new NotSupportedException();
            }

            public void UpdateSelectionVisual(EditorEntity visualEntity, Entity selectedEntity, Component component) {
            }
        }
    }
}

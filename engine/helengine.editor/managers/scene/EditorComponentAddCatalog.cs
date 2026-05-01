namespace helengine.editor {
    /// <summary>
    /// Provides the curated set of components that can be added from the properties panel.
    /// </summary>
    public static class EditorComponentAddCatalog {
        /// <summary>
        /// Available addable component descriptors in the order shown to the user.
        /// </summary>
        static readonly EditorComponentAddDescriptor[] AddableComponents = new[] {
            new EditorComponentAddDescriptor("Anchor", typeof(AnchorComponent), false, AddAnchor),
            new EditorComponentAddDescriptor("Camera", typeof(CameraComponent), true, AddCamera),
            new EditorComponentAddDescriptor("Line Renderer", typeof(LineRendererComponent), false, AddLineRenderer),
            new EditorComponentAddDescriptor("Mesh", typeof(MeshComponent), false, AddMesh),
            new EditorComponentAddDescriptor("Rotate", typeof(RotateComponent), false, AddRotate),
            new EditorComponentAddDescriptor("Rounded Rect", typeof(RoundedRectComponent), false, AddRoundedRect),
            new EditorComponentAddDescriptor("Sprite", typeof(SpriteComponent), false, AddSprite),
            new EditorComponentAddDescriptor("Text", typeof(TextComponent), false, AddText)
        };

        /// <summary>
        /// Returns the component options that can be added to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the new component.</param>
        /// <returns>Filtered list of component descriptors.</returns>
        public static IReadOnlyList<EditorComponentAddDescriptor> GetAvailableComponents(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<EditorComponentAddDescriptor> results = new List<EditorComponentAddDescriptor>(AddableComponents.Length);
            for (int i = 0; i < AddableComponents.Length; i++) {
                EditorComponentAddDescriptor descriptor = AddableComponents[i];
                if (descriptor == null) {
                    continue;
                }

                if (descriptor.SingleInstance && HasExactComponent(entity, descriptor.ComponentType)) {
                    continue;
                }

                results.Add(descriptor);
            }

            return results;
        }

        /// <summary>
        /// Adds one anchor component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddAnchor(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new AnchorComponent());
        }

        /// <summary>
        /// Adds one camera component to the supplied entity and applies the editor-specific suppression and visual chrome.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddCamera(Entity entity) {
            EditorEntity editorEntity = RequireEditorEntity(entity);
            CameraComponent cameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 0,
                Viewport = new float4(0f, 0f, 1f, 1f),
                ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0)
            };
            editorEntity.AddComponent(cameraComponent);
            EditorSceneCameraSuppressionService.AttachAndSuppress(editorEntity);
            EditorCameraVisualAttachmentService.Attach(editorEntity);
        }

        /// <summary>
        /// Adds one line-renderer component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddLineRenderer(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new LineRendererComponent());
        }

        /// <summary>
        /// Adds one mesh component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddMesh(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new MeshComponent());
        }

        /// <summary>
        /// Adds one rotate component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddRotate(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new RotateComponent());
        }

        /// <summary>
        /// Adds one rounded rectangle component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddRoundedRect(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new RoundedRectComponent());
        }

        /// <summary>
        /// Adds one sprite component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddSprite(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new SpriteComponent());
        }

        /// <summary>
        /// Adds one text component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component.</param>
        static void AddText(Entity entity) {
            RequireEditorEntity(entity).AddComponent(new TextComponent());
        }

        /// <summary>
        /// Resolves the editor entity used by the add actions.
        /// </summary>
        /// <param name="entity">Entity targeted by the add action.</param>
        /// <returns>The supplied entity as an editor entity.</returns>
        static EditorEntity RequireEditorEntity(Entity entity) {
            if (entity is not EditorEntity editorEntity) {
                throw new InvalidOperationException("Addable components can only be attached to editor entities.");
            }

            return editorEntity;
        }

        /// <summary>
        /// Determines whether one entity already owns a component of the exact requested type.
        /// </summary>
        /// <param name="entity">Entity whose component list should be searched.</param>
        /// <param name="componentType">Concrete component type to locate.</param>
        /// <returns>True when the entity already owns the exact component type.</returns>
        static bool HasExactComponent(Entity entity, Type componentType) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (entity.Components == null) {
                return false;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                Component component = entity.Components[i];
                if (component != null && component.GetType() == componentType) {
                    return true;
                }
            }

            return false;
        }
    }
}

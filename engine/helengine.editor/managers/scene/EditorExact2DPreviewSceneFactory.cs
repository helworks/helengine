namespace helengine.editor {
    /// <summary>
    /// Creates the hidden editor entities and components used by exact 2D preview capture scenes.
    /// </summary>
    public static class EditorExact2DPreviewSceneFactory {
        /// <summary>
        /// Creates the hidden preview camera entity used by one exact 2D preview capture service.
        /// </summary>
        /// <returns>Configured hidden preview camera entity.</returns>
        public static EditorEntity CreatePreviewCameraEntity() {
            return new EditorEntity {
                Name = "Exact 2D Preview Camera",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneModelPreview
            };
        }

        /// <summary>
        /// Creates the hidden preview camera component used to render one exact 2D preview target.
        /// </summary>
        /// <param name="previewSize">Current preview target size.</param>
        /// <returns>Configured preview camera component.</returns>
        public static CameraComponent CreatePreviewCameraComponent(int2 previewSize) {
            return new CameraComponent {
                CameraDrawOrder = 0,
                LayerMask = EditorLayerMasks.SceneModelPreview,
                ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1f, false, 0),
                Viewport = new float4(0f, 0f, Math.Max(1, previewSize.X), Math.Max(1, previewSize.Y))
            };
        }

        /// <summary>
        /// Creates the hidden preview content entity used to host one cloned 2D component.
        /// </summary>
        /// <returns>Configured hidden preview content entity.</returns>
        public static EditorEntity CreatePreviewContentEntity() {
            return new EditorEntity {
                Name = "Exact 2D Preview Content",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneModelPreview
            };
        }

        /// <summary>
        /// Creates one cloned text component used by an exact 2D preview capture scene.
        /// </summary>
        /// <returns>Fresh hidden preview text component.</returns>
        public static TextComponent CreatePreviewTextComponent() {
            return new TextComponent {
                LayerMask = (byte)EditorLayerMasks.SceneModelPreview
            };
        }

        /// <summary>
        /// Creates one cloned rounded-rectangle component used by an exact 2D preview capture scene.
        /// </summary>
        /// <returns>Fresh hidden preview rounded-rectangle component.</returns>
        public static RoundedRectComponent CreatePreviewRoundedRectComponent() {
            return new RoundedRectComponent {
                LayerMask = (byte)EditorLayerMasks.SceneModelPreview
            };
        }
    }
}

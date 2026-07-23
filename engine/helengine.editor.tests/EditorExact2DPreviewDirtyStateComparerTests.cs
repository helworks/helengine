using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor-only exact 2D preview dirty-state comparer only marks previews dirty when texture-affecting data changes.
    /// </summary>
    public sealed class EditorExact2DPreviewDirtyStateComparerTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by exact 2D preview dirty-state tests.
        /// </summary>
        public EditorExact2DPreviewDirtyStateComparerTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures moving the source entity does not mark the text preview dirty because transform changes are handled by the world-space proxy only.
        /// </summary>
        [Fact]
        public void IsTextPreviewDirty_WhenOnlyTransformChanges_ReturnsFalse() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(10f, 20f, 30f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview",
                WrapText = true,
                Rotation = 15f,
                FontScale = 2f
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewRenderState previousState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);
            sourceEntity.LocalPosition = new float3(300f, 400f, 500f);
            sourceEntity.LocalOrientation = float4.Identity;
            sourceEntity.LocalScale = new float3(2f, 3f, 4f);
            EditorExact2DPreviewRenderState nextState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);

            Assert.False(EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(previousState, nextState));
        }

        /// <summary>
        /// Ensures changing visible text data marks the preview dirty.
        /// </summary>
        [Fact]
        public void IsTextPreviewDirty_WhenVisibleTextDataChanges_ReturnsTrue() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview",
                WrapText = false,
                Rotation = 0f,
                FontScale = 1f,
                Color = new byte4(255, 255, 255, 255)
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewRenderState previousState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);
            sourceComponent.Text = "Preview Changed";
            EditorExact2DPreviewRenderState nextState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);

            Assert.True(EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(previousState, nextState));
        }

        /// <summary>
        /// Ensures changing authored text alignment marks the preview dirty because glyph placement changes inside the preview texture.
        /// </summary>
        [Fact]
        public void IsTextPreviewDirty_WhenAlignmentChanges_ReturnsTrue() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(200, 32),
                Text = "Preview",
                WrapText = false,
                Rotation = 0f,
                FontScale = 1f,
                Color = new byte4(255, 255, 255, 255)
            };
            sourceEntity.AddComponent(sourceComponent);
            System.Reflection.PropertyInfo alignmentProperty = typeof(TextComponent).GetProperty("Alignment");
            Assert.NotNull(alignmentProperty);
            alignmentProperty.SetValue(sourceComponent, Enum.Parse(alignmentProperty.PropertyType, "Left"));

            EditorExact2DPreviewRenderState previousState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);
            alignmentProperty.SetValue(sourceComponent, Enum.Parse(alignmentProperty.PropertyType, "Right"));
            EditorExact2DPreviewRenderState nextState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);

            Assert.True(EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(previousState, nextState));
        }

        /// <summary>
        /// Ensures changing any authored text effect marks the exact preview dirty.
        /// </summary>
        [Fact]
        public void IsTextPreviewDirty_WhenTextEffectsChange_ReturnsTrue() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(200, 32),
                Text = "Preview",
                OutlineScale = 1f,
                OutlineColor = new byte4(0, 0, 0, 255),
                ShadowOffset = new float2(2f, 3f),
                ShadowColor = new byte4(10, 20, 30, 200)
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewRenderState previousState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);
            sourceComponent.OutlineScale = 2f;
            EditorExact2DPreviewRenderState nextState = EditorExact2DPreviewDirtyStateComparer.CaptureTextState(sourceEntity, sourceComponent);

            Assert.True(EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(previousState, nextState));
        }

        /// <summary>
        /// Ensures changing visible rounded-rectangle shape data marks the preview dirty.
        /// </summary>
        [Fact]
        public void IsRoundedRectPreviewDirty_WhenVisibleShapeDataChanges_ReturnsTrue() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            RoundedRectComponent sourceComponent = new RoundedRectComponent {
                Size = new int2(160, 48),
                Radius = 12f,
                BorderThickness = 2f,
                FillColor = new byte4(10, 20, 30, 255),
                BorderColor = new byte4(100, 110, 120, 255)
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewRenderState previousState = EditorExact2DPreviewDirtyStateComparer.CaptureRoundedRectState(sourceEntity, sourceComponent);
            sourceComponent.FillColor = new byte4(200, 10, 10, 255);
            EditorExact2DPreviewRenderState nextState = EditorExact2DPreviewDirtyStateComparer.CaptureRoundedRectState(sourceEntity, sourceComponent);

            Assert.True(EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(previousState, nextState));
        }
    }
}

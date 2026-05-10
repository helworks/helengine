using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies preview panel lifecycle behavior.
    /// </summary>
    public class PreviewPanelTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the panel tests.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Deterministic input backend used to feed wheel and pointer state into the preview panel.
        /// </summary>
        readonly TestInputBackend Input;

        /// <summary>
        /// Initializes the core services required by the preview panel tests.
        /// </summary>
        public PreviewPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-preview-panel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            Input = new TestInputBackend();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures replacing the preview source disposes the previous source.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenNewSourceIsAssigned_DisposesThePreviousSource() {
            PreviewPanel panel = new PreviewPanel(CreateFont());
            TestPreviewSource first = new TestPreviewSource(new TestRuntimeTexture {
                Width = 32,
                Height = 32
            });
            TestPreviewSource second = new TestPreviewSource(new TestRuntimeTexture {
                Width = 64,
                Height = 64
            });

            panel.SetPreviewSource(first);
            panel.SetPreviewSource(second);

            Assert.True(first.IsDisposed);
            Assert.Same(second, panel.ActivePreviewSource);
        }

        /// <summary>
        /// Ensures scaled dock metrics move the preview content below the scaled title bar while letting the preview use the full panel body width.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WithScaledMetrics_UsesScaledTitleBarOffsetAndFullBodyWidth() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            PreviewPanel panel = new PreviewPanel(CreateFont(), metrics) {
                Size = new int2(300, 240)
            };
            TestPreviewSource source = new TestPreviewSource(new TestRuntimeTexture {
                Width = 100,
                Height = 50
            });

            panel.SetPreviewSource(source);

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            EditorEntity textureHost = GetPrivateField<EditorEntity>(panel, "textureHost");
            SpriteComponent textureSprite = GetPrivateField<SpriteComponent>(panel, "textureSprite");

            Assert.Equal(30f, contentRoot.Position.Y);
            Assert.Equal(0f, textureHost.Position.X);
            Assert.Equal(60f, textureHost.Position.Y);
            Assert.Equal(new int2(300, 150), textureSprite.Size);
        }

        /// <summary>
        /// Ensures texture previews show their resolution label beneath the image.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenTexturePreviewIsAssigned_ShowsTheResolutionLabel() {
            PreviewPanel panel = new PreviewPanel(CreateFont());
            TexturePreviewSource source = new TexturePreviewSource(new TestRuntimeTexture {
                Width = 120,
                Height = 80
            });

            panel.SetPreviewSource(source);

            EditorEntity resolutionLabelHost = GetPrivateField<EditorEntity>(panel, "resolutionLabelHost");
            TextComponent resolutionLabelText = GetPrivateField<TextComponent>(panel, "resolutionLabelText");

            Assert.True(resolutionLabelHost.Enabled);
            Assert.Equal("120 x 80", resolutionLabelText.Text);
        }

        /// <summary>
        /// Ensures non-texture previews hide the resolution label instead of leaving stale text visible.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenNonTexturePreviewIsAssigned_HidesTheResolutionLabel() {
            PreviewPanel panel = new PreviewPanel(CreateFont());

            panel.SetPreviewSource(new TexturePreviewSource(new TestRuntimeTexture {
                Width = 120,
                Height = 80
            }));
            panel.SetPreviewSource(new TestPreviewSource(new TestRuntimeTexture {
                Width = 120,
                Height = 80
            }));

            EditorEntity resolutionLabelHost = GetPrivateField<EditorEntity>(panel, "resolutionLabelHost");
            TextComponent resolutionLabelText = GetPrivateField<TextComponent>(panel, "resolutionLabelText");

            Assert.False(resolutionLabelHost.Enabled);
            Assert.Equal(string.Empty, resolutionLabelText.Text);
        }

        /// <summary>
        /// Ensures wheel scrolling zooms a texture preview around the cursor position.
        /// </summary>
        [Fact]
        public void UpdatePreviewSource_WhenWheelScrollsOverTexturePreview_ZoomsAroundTheCursor() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            panel.SetPreviewSource(new TexturePreviewSource(new TestRuntimeTexture {
                Width = 100,
                Height = 50
            }));

            EditorEntity textureHost = GetPrivateField<EditorEntity>(panel, "textureHost");
            SpriteComponent textureSprite = GetPrivateField<SpriteComponent>(panel, "textureSprite");
            float3 initialPosition = textureHost.Position;
            int2 initialSize = textureSprite.Size;

            int pointerX = (int)Math.Round(initialPosition.X) + 100;
            int pointerY = (int)Math.Round(initialPosition.Y) + 50;

            CompleteInputFrame(new MouseState(
                pointerX,
                pointerY,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            AdvanceInputFrame(new MouseState(
                pointerX,
                pointerY,
                120,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));

            panel.UpdatePreviewSource();
            Input.Update();

            Assert.Equal(new int2(458, 229), textureSprite.Size);
            double widthScale = textureSprite.Size.X / (double)initialSize.X;
            double heightScale = textureSprite.Size.Y / (double)initialSize.Y;
            double expectedOffsetX = (pointerX - initialPosition.X) * (widthScale - 1d);
            double expectedOffsetY = (pointerY - initialPosition.Y) * (heightScale - 1d);

            Assert.Equal(initialPosition.X - expectedOffsetX, textureHost.Position.X, 3);
            Assert.Equal(initialPosition.Y - expectedOffsetY, textureHost.Position.Y, 3);
        }

        /// <summary>
        /// Ensures middle mouse dragging pans the visible texture preview.
        /// </summary>
        [Fact]
        public void UpdatePreviewSource_WhenMiddleMouseDragsTexturePreview_PansTheTexture() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            panel.SetPreviewSource(new TexturePreviewSource(new TestRuntimeTexture {
                Width = 100,
                Height = 50
            }));

            EditorEntity textureHost = GetPrivateField<EditorEntity>(panel, "textureHost");
            float3 initialPosition = textureHost.LocalPosition;

            CompleteInputFrame(new MouseState(
                (int)Math.Round(initialPosition.X) + 100,
                (int)Math.Round(initialPosition.Y) + 50,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            AdvanceInputFrame(new MouseState(
                (int)Math.Round(initialPosition.X) + 120,
                (int)Math.Round(initialPosition.Y) + 70,
                0,
                ButtonState.Released,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));

            panel.UpdatePreviewSource();
            Input.Update();

            Assert.Equal(initialPosition.X + 20f, textureHost.LocalPosition.X);
            Assert.Equal(initialPosition.Y + 20f, textureHost.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures wheel and left-drag input is forwarded to interactive preview sources.
        /// </summary>
        [Fact]
        public void UpdatePreviewSource_WhenInteractivePreviewIsAssigned_ForwardsWheelAndDragInput() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            TestInteractivePreviewSource source = new TestInteractivePreviewSource(new TestRuntimeTexture {
                Width = 64,
                Height = 64
            });
            panel.SetPreviewSource(source);

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            int pointerX = (int)Math.Round(contentRoot.Position.X) + 100;
            int pointerY = (int)Math.Round(contentRoot.Position.Y) + 80;

            CompleteInputFrame(new MouseState(
                pointerX,
                pointerY,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            AdvanceInputFrame(new MouseState(
                pointerX + 12,
                pointerY + 8,
                120,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));

            panel.UpdatePreviewSource();
            Input.Update();

            Assert.Equal(1, source.UpdateCount);
            Assert.Equal(1, source.WheelCount);
            Assert.Equal(1, source.DragCount);
        }

        /// <summary>
        /// Ensures middle mouse dragging is forwarded to interactive preview sources.
        /// </summary>
        [Fact]
        public void UpdatePreviewSource_WhenInteractivePreviewReceivesMiddleMouseDrag_ForwardsTheDrag() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            TestInteractivePreviewSource source = new TestInteractivePreviewSource(new TestRuntimeTexture {
                Width = 64,
                Height = 64
            });
            panel.SetPreviewSource(source);

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            int pointerX = (int)Math.Round(contentRoot.Position.X) + 100;
            int pointerY = (int)Math.Round(contentRoot.Position.Y) + 80;

            CompleteInputFrame(new MouseState(
                pointerX,
                pointerY,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            AdvanceInputFrame(new MouseState(
                pointerX + 12,
                pointerY + 8,
                0,
                ButtonState.Released,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));

            panel.UpdatePreviewSource();
            Input.Update();

            Assert.Equal(1, source.UpdateCount);
            Assert.Equal(1, source.DragCount);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dockable layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
        /// Captures one full input frame so the next frame reports wheel deltas correctly.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the frame.</param>
        void CompleteInputFrame(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
            Input.Update();
        }

        /// <summary>
        /// Captures the next input frame without finalizing it, which keeps the wheel delta available for preview updates.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the frame.</param>
        void AdvanceInputFrame(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }
    }
}

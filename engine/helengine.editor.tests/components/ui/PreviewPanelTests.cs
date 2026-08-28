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
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

        /// <summary>
        /// Initializes the core services required by the preview panel tests.
        /// </summary>
        public PreviewPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-preview-panel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();
            Input = new TestInputBackend();

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new helengine.directx11.DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new helengine.vulkan.VulkanShaderBackend());

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            EditorInputCaptureService.Reset();
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
        /// Ensures scaled dock metrics move the preview content below the scaled title bar while letting generic previews fill the panel body.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WithScaledMetrics_UsesScaledTitleBarOffsetAndFullBodySize() {
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
            Assert.Equal(30f, textureHost.Position.Y);
        Assert.Equal(new int2(300, 240), textureSprite.Size);
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
        /// Ensures the compact model toolbar is shown only while the panel hosts a model preview source.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenModelPreviewIsAssigned_ShowsGridToolbarOnlyForTheModel() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource modelSource = CreateModelPreviewSource();

            panel.SetPreviewSource(modelSource);

            EditorEntity modelToolbarRoot = GetPrivateField<EditorEntity>(panel, "modelToolbarRoot");
            Assert.True(modelToolbarRoot.Enabled);

            panel.SetPreviewSource(new TexturePreviewSource(new TestRuntimeTexture {
                Width = 64,
                Height = 64
            }));

            Assert.False(modelToolbarRoot.Enabled);
        }

        /// <summary>
        /// Ensures the toolbar grid button changes the active model preview and preserves its state for later model previews.
        /// </summary>
        [Fact]
        public void GridToolbarButton_WhenActivated_PersistsThePanelGridPreferenceAcrossModelPreviews() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource firstSource = CreateModelPreviewSource();
            ModelPreviewSource secondSource = CreateModelPreviewSource();
            panel.SetPreviewSource(firstSource);
            InteractableComponent gridButtonInteractable = GetPrivateField<InteractableComponent>(panel, "gridButtonInteractable");

            gridButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Hover);
            gridButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            gridButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);

            Assert.False(firstSource.IsGridVisible);

            panel.SetPreviewSource(secondSource);

            Assert.False(secondSource.IsGridVisible);
            panel.ClearPreview();
        }

        /// <summary>
        /// Ensures the bounds toolbar button cycles through box, sphere, and no overlay for the active model preview.
        /// </summary>
        [Fact]
        public void BoundsToolbarButton_WhenActivated_CyclesThroughBoxSphereAndNone() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource source = CreateModelPreviewSource();
            panel.SetPreviewSource(source);
            InteractableComponent boundsButtonInteractable = GetPrivateField<InteractableComponent>(panel, "boundsButtonInteractable");

            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Hover);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
            Assert.Equal(ModelPreviewBoundsDisplayMode.Box, source.BoundsDisplayMode);

            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
            Assert.Equal(ModelPreviewBoundsDisplayMode.Sphere, source.BoundsDisplayMode);

            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
            Assert.Equal(ModelPreviewBoundsDisplayMode.None, source.BoundsDisplayMode);
            panel.ClearPreview();
        }

        /// <summary>
        /// Ensures the bounds display preference is re-applied when this panel is assigned a later model preview source.
        /// </summary>
        [Fact]
        public void BoundsToolbarButton_WhenModelPreviewChanges_PersistsThePanelBoundsPreference() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource firstSource = CreateModelPreviewSource();
            ModelPreviewSource secondSource = CreateModelPreviewSource();
            panel.SetPreviewSource(firstSource);
            InteractableComponent boundsButtonInteractable = GetPrivateField<InteractableComponent>(panel, "boundsButtonInteractable");

            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Hover);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
            boundsButtonInteractable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
            Assert.Equal(ModelPreviewBoundsDisplayMode.Sphere, firstSource.BoundsDisplayMode);

            panel.SetPreviewSource(secondSource);

            Assert.Equal(ModelPreviewBoundsDisplayMode.Sphere, secondSource.BoundsDisplayMode);
            panel.ClearPreview();
        }

        /// <summary>
        /// Ensures box mode configures bounds-dimension labels from the panel font shared with editor viewport gizmos.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenBoundsBoxIsActivated_ConfiguresDimensionLabelsWithThePanelFont() {
            FontAsset font = CreateFont();
            PreviewPanel panel = new PreviewPanel(font) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource source = CreateModelPreviewSource();

            panel.SetPreviewSource(source);
            panel.CycleModelBoundsDisplayMode();

            EditorEntity[] labels = GetPrivateField<EditorEntity[]>(source, "boundsDimensionLabelEntities");
            Assert.Equal(3, labels.Length);
            Assert.All(labels, label => Assert.True(label.Enabled));
            panel.ClearPreview();
        }

        /// <summary>
        /// Ensures pointer drags beginning on the model toolbar do not orbit the model beneath it.
        /// </summary>
        [Fact]
        public void UpdatePreviewSource_WhenPointerDragsOverModelToolbar_DoesNotOrbitTheModel() {
            PreviewPanel panel = new PreviewPanel(CreateFont()) {
                Size = new int2(416, 312)
            };
            ModelPreviewSource source = CreateModelPreviewSource();
            panel.SetPreviewSource(source);
            EditorEntity gridButtonRoot = GetPrivateField<EditorEntity>(panel, "gridButtonRoot");
            float4 initialOrientation = source.PreviewCamera.Parent.Orientation;
            int pointerX = (int)Math.Round(gridButtonRoot.Position.X) + 4;
            int pointerY = (int)Math.Round(gridButtonRoot.Position.Y) + 4;

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
                pointerY,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));

            panel.UpdatePreviewSource();
            Input.Update();

            Assert.Equal(initialOrientation, source.PreviewCamera.Parent.Orientation);
            panel.ClearPreview();
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
            Assert.Equal(0, source.MiddleDragCount);
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
            Assert.Equal(0, source.DragCount);
            Assert.Equal(1, source.MiddleDragCount);
        }

        /// <summary>
        /// Ensures the preview panel registers its updater with the engine loop so interactive sources receive input during normal editor frames.
        /// </summary>
        [Fact]
        public void PreviewPanelUpdater_WhenInteractivePreviewIsAssigned_ForwardsInputDuringEngineUpdate() {
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

            Core.Instance.ObjectManager.Update();
            Input.Update();

            Assert.Equal(1, source.UpdateCount);
            Assert.Equal(1, source.WheelCount);
            Assert.Equal(1, source.DragCount);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dockable layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['B'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 4f, 4f), 0f, 4f, 0f, 0f),
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
        /// Creates one model preview source with deterministic bounds for toolbar interaction tests.
        /// </summary>
        /// <returns>Configured interactive model preview source.</returns>
        ModelPreviewSource CreateModelPreviewSource() {
            ModelAsset modelAsset = new ModelAsset {
                Positions = new[] {
                    new float3(-1f, -1f, -1f),
                    new float3(1f, -1f, -1f),
                    new float3(1f, 1f, -1f),
                    new float3(-1f, 1f, -1f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f),
                    new float2(0f, 1f)
                },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 6,
                        MaterialSlotName = "Default"
                    }
                },
                Indices16 = new ushort[] { 0, 1, 2, 0, 2, 3 },
                BoundsMin = new float3(-1f, -1f, -1f),
                BoundsMax = new float3(1f, 1f, 1f)
            };
            RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            return new ModelPreviewSource(runtimeModel, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.RendererResources);
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


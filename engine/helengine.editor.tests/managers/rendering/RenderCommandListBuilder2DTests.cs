using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.rendering {
    /// <summary>
    /// Verifies the resolved 2D command-list builder flattens the current render queue into backend-ready commands.
    /// </summary>
    public sealed class RenderCommandListBuilder2DTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate the shared core instance for the builder tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the command-list builder tests.
        /// </summary>
        public RenderCommandListBuilder2DTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-render-command-list-builder-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases temporary test directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a sprite becomes one textured-quad command with preserved texture, bounds, source rectangle, and tint.
        /// </summary>
        [Fact]
        public void Build_WhenQueueContainsSprite_EmitsOneTexturedQuad() {
            Entity entity = CreateEntity(new float3(15f, 25f, 0f), true);
            SpriteComponent sprite = new SpriteComponent {
                Texture = new TestRuntimeTexture {
                    Width = 64,
                    Height = 32
                },
                Size = new int2(70, 35),
                SourceRect = new float4(0.2f, 0.3f, 0.4f, 0.5f),
                Color = new byte4(1, 2, 3, 4)
            };
            entity.AddComponent(sprite);

            RenderList2D queue = new RenderList2D(1);
            queue.Add(sprite);

            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commandList = builder.Build(queue);

            Assert.Equal(1, commandList.Count);
            Assert.Equal(RenderCommand2DType.TexturedQuad, commandList.GetCommandType(0));

            int payloadIndex = commandList.GetTexturedQuadPayloadIndex(0);
            Assert.Same(sprite.Texture, commandList.GetTexturedQuadTexture(payloadIndex));
            Assert.Equal(new float4(15f, 25f, 70f, 35f), commandList.GetTexturedQuadBounds(payloadIndex));
            Assert.Equal(sprite.SourceRect, commandList.GetTexturedQuadSourceRect(payloadIndex));
            Assert.Equal(sprite.Color, commandList.GetTexturedQuadColor(payloadIndex));
        }

        /// <summary>
        /// Ensures wrapped text resolves upstream and emits one glyph command per rendered glyph.
        /// </summary>
        [Fact]
        public void Build_WhenQueueContainsWrappedText_EmitsWrappedGlyphQuads() {
            Entity entity = CreateEntity(new float3(10f, 20f, 0f), true);
            TextComponent text = new TextComponent {
                Font = CreateFont(),
                Text = "A A",
                WrapText = true,
                Size = new int2(8, 40),
                Color = new byte4(9, 8, 7, 6)
            };
            entity.AddComponent(text);

            RenderList2D queue = new RenderList2D(1);
            queue.Add(text);

            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commandList = builder.Build(queue);

            Assert.Equal(2, commandList.Count);
            Assert.Equal(RenderCommand2DType.GlyphQuad, commandList.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.GlyphQuad, commandList.GetCommandType(1));

            int firstPayloadIndex = commandList.GetGlyphQuadPayloadIndex(0);
            int secondPayloadIndex = commandList.GetGlyphQuadPayloadIndex(1);
            Assert.Same(text.Font.Texture, commandList.GetGlyphQuadTexture(firstPayloadIndex));
            Assert.Same(text.Font.Texture, commandList.GetGlyphQuadTexture(secondPayloadIndex));
            Assert.Equal(text.Color, commandList.GetGlyphQuadColor(firstPayloadIndex));
            Assert.Equal(text.Color, commandList.GetGlyphQuadColor(secondPayloadIndex));
            Assert.True(commandList.GetGlyphQuadBounds(secondPayloadIndex).Y > commandList.GetGlyphQuadBounds(firstPayloadIndex).Y);
        }

        /// <summary>
        /// Ensures text glyph bounds and advances honor one authored font scale.
        /// </summary>
        [Fact]
        public void Build_WhenQueueContainsScaledText_EmitsScaledGlyphQuads() {
            Entity entity = CreateEntity(new float3(10f, 20f, 0f), true);
            TextComponent text = new TextComponent {
                Font = CreateFont(),
                Text = "AA",
                FontScale = 0.5f,
                Color = new byte4(9, 8, 7, 6)
            };
            entity.AddComponent(text);

            RenderList2D queue = new RenderList2D(1);
            queue.Add(text);

            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commandList = builder.Build(queue);

            Assert.Equal(2, commandList.Count);

            int firstPayloadIndex = commandList.GetGlyphQuadPayloadIndex(0);
            int secondPayloadIndex = commandList.GetGlyphQuadPayloadIndex(1);
            Assert.Equal(new float4(10f, 20.5f, 2.5f, 2.5f), commandList.GetGlyphQuadBounds(firstPayloadIndex));
            Assert.Equal(new float4(13f, 20.5f, 2.5f, 2.5f), commandList.GetGlyphQuadBounds(secondPayloadIndex));
        }

        /// <summary>
        /// Ensures a rounded rectangle becomes one rounded-rectangle command with preserved shape parameters.
        /// </summary>
        [Fact]
        public void Build_WhenQueueContainsRoundedRect_EmitsOneRoundedRect() {
            Entity entity = CreateEntity(new float3(5f, 6f, 0f), true);
            RoundedRectComponent shape = new RoundedRectComponent {
                Size = new int2(80, 45),
                Radius = 12f,
                BorderThickness = 3f,
                Corners = RoundedRectCorners.TopLeft | RoundedRectCorners.BottomLeft,
                FillColor = new byte4(10, 20, 30, 40),
                BorderColor = new byte4(50, 60, 70, 80)
            };
            entity.AddComponent(shape);

            RenderList2D queue = new RenderList2D(1);
            queue.Add(shape);

            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commandList = builder.Build(queue);

            Assert.Equal(1, commandList.Count);
            Assert.Equal(RenderCommand2DType.RoundedRect, commandList.GetCommandType(0));

            int payloadIndex = commandList.GetRoundedRectPayloadIndex(0);
            Assert.Equal(new float4(5f, 6f, 80f, 45f), commandList.GetRoundedRectBounds(payloadIndex));
            Assert.Equal(12f, commandList.GetRoundedRectRadius(payloadIndex));
            Assert.Equal(3f, commandList.GetRoundedRectBorderThickness(payloadIndex));
            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.BottomLeft, commandList.GetRoundedRectCorners(payloadIndex));
            Assert.Equal(new byte4(10, 20, 30, 40), commandList.GetRoundedRectFillColor(payloadIndex));
            Assert.Equal(new byte4(50, 60, 70, 80), commandList.GetRoundedRectBorderColor(payloadIndex));
        }

        /// <summary>
        /// Ensures disabled parents are skipped even when their drawables remain in the render queue.
        /// </summary>
        [Fact]
        public void Build_WhenParentIsDisabled_SkipsDrawable() {
            Entity entity = CreateEntity(new float3(1f, 2f, 0f), false);
            SpriteComponent sprite = new SpriteComponent {
                Texture = new TestRuntimeTexture {
                    Width = 16,
                    Height = 16
                },
                Size = new int2(16, 16)
            };
            entity.AddComponent(sprite);

            RenderList2D queue = new RenderList2D(1);
            queue.Add(sprite);

            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commandList = builder.Build(queue);

            Assert.Equal(0, commandList.Count);
        }

        /// <summary>
        /// Creates one initialized entity configured for the supplied position and enabled state.
        /// </summary>
        /// <param name="position">World position assigned to the entity.</param>
        /// <param name="enabled">True to keep the entity enabled; otherwise false.</param>
        /// <returns>Initialized entity ready to accept components.</returns>
        static Entity CreateEntity(float3 position, bool enabled) {
            Entity entity = new Entity();
            entity.Position = position;
            entity.Enabled = enabled;
            entity.InitComponents();
            return entity;
        }

        /// <summary>
        /// Creates one small font asset with deterministic glyph metrics for the builder tests.
        /// </summary>
        /// <returns>Font asset used by the text-builder tests.</returns>
        static FontAsset CreateFont() {
            TestRuntimeTexture texture = new TestRuntimeTexture {
                Width = 100,
                Height = 50
            };
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0.1f, 0.2f, 0.05f, 0.1f), 1f, 6f, 0f, 0f)
            };
            return new FontAsset(new FontInfo("Test", 10, 3f), texture, characters, 10f, 100, 50);
        }
    }
}

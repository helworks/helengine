using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.rendering {
    /// <summary>
    /// Verifies clip push and pop commands are emitted in stable order around queued 2D drawables.
    /// </summary>
    public sealed class RenderCommandClipBuilder2DTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate the shared core instance for clip command builder tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the clip command builder tests.
        /// </summary>
        public RenderCommandClipBuilder2DTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-render-command-clip-builder-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
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
        /// Ensures entering and leaving one clipped container emits one push before the drawable and one pop after it.
        /// </summary>
        [Fact]
        public void Build_WhenDrawableIsInsideOneClipOwner_EmitsPushDrawablePopSequence() {
            RenderList2D renderList = new RenderList2D(4);
            EditorEntity clipHost = new EditorEntity {
                Position = new float3(40f, 60f, 0f)
            };
            ClipRectComponent clip = new ClipRectComponent {
                Size = new int2(200, 80)
            };
            clipHost.AddComponent(clip);

            SpriteComponent sprite = new SpriteComponent {
                Texture = new TestRuntimeTexture {
                    Width = 16,
                    Height = 16
                },
                Size = new int2(16, 16),
                RenderOrder2D = 10
            };
            clipHost.AddComponent(sprite);
            renderList.Add(sprite);

            RenderCommandList2D commands = new RenderCommandListBuilder2D().Build(renderList);

            Assert.Equal(3, commands.Count);
            Assert.Equal(RenderCommand2DType.ClipPush, commands.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.TexturedQuad, commands.GetCommandType(1));
            Assert.Equal(RenderCommand2DType.ClipPop, commands.GetCommandType(2));
            Assert.Equal(new float4(40f, 60f, 200f, 80f), commands.GetClipPushRect(commands.GetClipPushPayloadIndex(0)));
        }

        /// <summary>
        /// Ensures nested clip owners produce intersected push rectangles in traversal order.
        /// </summary>
        [Fact]
        public void Build_WhenNestedClipOwnersOverlap_EmitsIntersectedNestedClipRect() {
            RenderList2D renderList = new RenderList2D(4);
            EditorEntity outer = new EditorEntity {
                Position = new float3(10f, 10f, 0f)
            };
            ClipRectComponent outerClip = new ClipRectComponent {
                Size = new int2(100, 100)
            };
            outer.AddComponent(outerClip);

            EditorEntity inner = new EditorEntity {
                Position = new float3(60f, 20f, 0f)
            };
            outer.AddChild(inner);
            ClipRectComponent innerClip = new ClipRectComponent {
                Size = new int2(100, 40)
            };
            inner.AddComponent(innerClip);

            TextComponent text = new TextComponent {
                Font = CreateFont(),
                Text = "A",
                Color = new byte4(20, 30, 40, 50),
                RenderOrder2D = 20
            };
            inner.AddComponent(text);
            renderList.Add(text);

            RenderCommandList2D commands = new RenderCommandListBuilder2D().Build(renderList);

            Assert.Equal(RenderCommand2DType.ClipPush, commands.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.ClipPush, commands.GetCommandType(1));
            int nestedPayloadIndex = commands.GetClipPushPayloadIndex(1);
            Assert.Equal(new float4(70f, 30f, 40f, 40f), commands.GetClipPushRect(nestedPayloadIndex));
        }

        /// <summary>
        /// Creates one small font asset with deterministic glyph metrics for the clip builder tests.
        /// </summary>
        /// <returns>Font asset used by the text-based clip tests.</returns>
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


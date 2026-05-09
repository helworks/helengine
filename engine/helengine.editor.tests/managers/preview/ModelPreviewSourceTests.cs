using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies model preview behavior for bounds framing and pointer interaction.
    /// </summary>
    public class ModelPreviewSourceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the preview tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core services required by the model preview tests.
        /// </summary>
        public ModelPreviewSourceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-model-preview-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures resizing the preview updates the render target and camera viewport.
        /// </summary>
        [Fact]
        public void Resize_WhenContentSizeChanges_ResizesTheRenderTargetAndViewport() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);

            source.Resize(new int2(640, 360));

            Assert.Equal(640, source.RenderTarget.Width);
            Assert.Equal(360, source.RenderTarget.Height);
            Assert.Equal(new float4(0f, 0f, 640f, 360f), source.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Ensures wheel input moves the camera closer to the model bounds center.
        /// </summary>
        [Fact]
        public void HandleMouseWheel_WhenZoomingIn_MovesTheCameraCloser() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);
            source.Resize(new int2(640, 360));
            float3 initialPosition = source.PreviewCamera.Parent.Position;
            double initialDistance = GetDistance(initialPosition, float3.Zero);

            source.HandleMouseWheel(120);
            source.Update();

            float3 zoomedPosition = source.PreviewCamera.Parent.Position;
            double zoomedDistance = GetDistance(zoomedPosition, float3.Zero);

            Assert.True(zoomedDistance < initialDistance);
        }

        /// <summary>
        /// Ensures left-drag input orbits the camera around the model bounds center.
        /// </summary>
        [Fact]
        public void HandleMouseDrag_WhenOrbiting_ChangesTheCameraOrientationWithoutChangingDistance() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);
            source.Resize(new int2(640, 360));
            float3 initialPosition = source.PreviewCamera.Parent.Position;
            float4 initialOrientation = source.PreviewCamera.Parent.Orientation;
            double initialDistance = GetDistance(initialPosition, float3.Zero);

            source.HandleMouseDrag(new int2(24, -12));
            source.Update();

            float3 orbitPosition = source.PreviewCamera.Parent.Position;
            float4 orbitOrientation = source.PreviewCamera.Parent.Orientation;
            double orbitDistance = GetDistance(orbitPosition, float3.Zero);

            Assert.NotEqual(initialOrientation, orbitOrientation);
            Assert.NotEqual(initialPosition, orbitPosition);
            Assert.True(Math.Abs(orbitDistance - initialDistance) < 0.0001d);
        }

        /// <summary>
        /// Ensures the preview model is isolated from the main viewport camera by using a dedicated preview layer.
        /// </summary>
        [Fact]
        public void Constructor_WhenPreviewSourceIsCreated_KeepsTheModelOutOfTheMainViewportQueue() {
            EditorEntity mainCameraEntity = new EditorEntity();
            CameraComponent mainCamera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 0,
                Viewport = new float4(0f, 0f, 640f, 360f)
            };
            mainCameraEntity.AddComponent(mainCamera);

            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");

            Assert.False(QueueContainsDrawable(mainCamera.RenderQueue3D, previewMesh));
            Assert.True(QueueContainsDrawable(source.PreviewCamera.RenderQueue3D, previewMesh));

            source.Dispose();
            mainCameraEntity.Dispose();
        }

        /// <summary>
        /// Builds one simple runtime model with known cached bounds for preview framing tests.
        /// </summary>
        /// <returns>Runtime model with deterministic bounds.</returns>
        RuntimeModel CreateRuntimeModel() {
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

            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
        }

        /// <summary>
        /// Measures the distance from one point to the origin.
        /// </summary>
        /// <param name="position">Position to measure.</param>
        /// <param name="target">Target point.</param>
        /// <returns>Euclidean distance between both points.</returns>
        double GetDistance(float3 position, float3 target) {
            double dx = position.X - target.X;
            double dy = position.Y - target.Y;
            double dz = position.Z - target.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Determines whether one render queue contains the requested drawable.
        /// </summary>
        /// <param name="renderQueue">Render queue to inspect.</param>
        /// <param name="drawable">Drawable expected to be present.</param>
        /// <returns>True when the drawable was visited by the render queue.</returns>
        bool QueueContainsDrawable(IRenderQueue3D renderQueue, IDrawable3D drawable) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            RenderQueueContainsVisitor visitor = new RenderQueueContainsVisitor(drawable);
            renderQueue.VisitOrdered(visitor);
            return visitor.Found;
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

        /// <summary>
        /// Visitor that detects whether a specific drawable is present in one render queue.
        /// </summary>
        sealed class RenderQueueContainsVisitor : IRenderVisitor3D {
            /// <summary>
            /// Drawable that the visitor searches for.
            /// </summary>
            readonly IDrawable3D targetDrawable;

            /// <summary>
            /// Initializes a new render-queue presence visitor.
            /// </summary>
            /// <param name="targetDrawable">Drawable expected to appear in the queue.</param>
            public RenderQueueContainsVisitor(IDrawable3D targetDrawable) {
                this.targetDrawable = targetDrawable;
            }

            /// <summary>
            /// Gets a value indicating whether the target drawable was encountered.
            /// </summary>
            public bool Found { get; private set; }

            /// <summary>
            /// Visits one drawable and records whether it matches the target.
            /// </summary>
            /// <param name="drawable">Drawable encountered during queue traversal.</param>
            public void Visit(IDrawable3D drawable) {
                if (ReferenceEquals(drawable, targetDrawable)) {
                    Found = true;
                }
            }
        }
    }
}

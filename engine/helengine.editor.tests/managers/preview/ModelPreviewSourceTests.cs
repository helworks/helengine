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
    }
}

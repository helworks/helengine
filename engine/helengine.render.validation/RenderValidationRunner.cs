using System.Drawing;
using helengine.directx11;
using helengine.vulkan;

namespace helengine.render.validation {
    /// <summary>
    /// Executes backend render validations and captures output images.
    /// </summary>
    public class RenderValidationRunner {
        /// <summary>
        /// Cornflower blue red channel used for clear color comparisons.
        /// </summary>
        const int CornflowerBlueR = 100;
        /// <summary>
        /// Cornflower blue green channel used for clear color comparisons.
        /// </summary>
        const int CornflowerBlueG = 149;
        /// <summary>
        /// Cornflower blue blue channel used for clear color comparisons.
        /// </summary>
        const int CornflowerBlueB = 237;
        /// <summary>
        /// Accepted channel delta when checking for cornflower blue fallback.
        /// </summary>
        const int CornflowerTolerance = 24;
        /// <summary>
        /// Width of the overlay validation sprites in logical pixels.
        /// </summary>
        const int OverlaySpriteWidth = 48;
        /// <summary>
        /// Height of the overlay validation sprites in logical pixels.
        /// </summary>
        const int OverlaySpriteHeight = 48;
        /// <summary>
        /// Margin from the client edge used to place overlay validation sprites.
        /// </summary>
        const int OverlaySpriteMargin = 24;
        /// <summary>
        /// Minimum number of red overlay pixels expected in the captured image.
        /// </summary>
        const int MinimumRedOverlayPixelCount = 900;
        /// <summary>
        /// Minimum number of yellow overlay pixels expected in the captured image.
        /// </summary>
        const int MinimumYellowOverlayPixelCount = 900;

        /// <summary>
        /// Options controlling the validation run.
        /// </summary>
        readonly RenderValidationOptions Options;

        /// <summary>
        /// Initializes a new validation runner.
        /// </summary>
        /// <param name="options">Run options.</param>
        public RenderValidationRunner(RenderValidationOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            Options = options;
        }

        /// <summary>
        /// Runs validation for all selected backends.
        /// </summary>
        /// <returns>Validation results in execution order.</returns>
        public IReadOnlyList<RenderValidationResult> Run() {
            RenderBackend[] backends = Options.ResolveBackends();
            var results = new List<RenderValidationResult>(backends.Length);

            for (int i = 0; i < backends.Length; i++) {
                RenderValidationResult result = RunBackend(backends[i]);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Runs a single backend validation and captures an output image.
        /// </summary>
        /// <param name="backend">Backend to validate.</param>
        /// <returns>Validation result for the backend.</returns>
        RenderValidationResult RunBackend(RenderBackend backend) {
            string fileName = backend == RenderBackend.DirectX11 ? "dx11.png" : "vulkan.png";
            string outputPath = Path.Combine(Options.OutputDirectory, fileName);
            RenderManager3D renderer3D = null;
            RenderManager2D renderer2D = null;
            Core core = null;
            RenderValidationWindow window = null;

            try {
                window = new RenderValidationWindow(Options.FrameWidth, Options.FrameHeight, backend);
                window.Show();
                window.BringToFront();
                Application.DoEvents();

                CreateRenderManagers(backend, out renderer3D, out renderer2D);

                core = new Core(new CoreInitializationOptions());
                var inputManager = new InputManagerWindows(window.Handle);
                core.Initialize(renderer3D, renderer2D, inputManager, new CoreInitializationOptions());
                renderer3D.AddWindow(window.Handle, Options.FrameWidth, Options.FrameHeight);

                CreateScene(renderer3D, renderer2D, Options.FrameWidth, Options.FrameHeight, backend);
                RenderFrames(core, Options.FrameCount);

                RenderImageCapture.CaptureClientArea(window, outputPath);
                Color centerPixel = RenderImageCapture.ReadCenterPixel(outputPath);
                int redOverlayPixelCount = CountRedOverlayPixels(outputPath);
                int yellowOverlayPixelCount = CountYellowOverlayPixels(outputPath);
                bool passed = ValidateCenterPixel(centerPixel) &&
                    redOverlayPixelCount >= MinimumRedOverlayPixelCount &&
                    yellowOverlayPixelCount >= MinimumYellowOverlayPixelCount;

                string status = passed
                    ? $"Center={RenderImageCapture.FormatColor(centerPixel)} redPixels={redOverlayPixelCount} yellowPixels={yellowOverlayPixelCount} matched expected mesh and overlay output."
                    : $"Center={RenderImageCapture.FormatColor(centerPixel)} redPixels={redOverlayPixelCount} yellowPixels={yellowOverlayPixelCount} did not match expected mesh and overlay output.";
                return new RenderValidationResult(backend, outputPath, centerPixel, passed, status);
            } catch (Exception ex) {
                return new RenderValidationResult(backend, outputPath, Color.Empty, false, ex.ToString());
            } finally {
                if (core != null) {
                    core.Dispose();
                }

                if (window != null) {
                    window.Close();
                    window.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates render managers for a concrete backend.
        /// </summary>
        /// <param name="backend">Backend to instantiate.</param>
        /// <param name="renderer3D">Created 3D renderer.</param>
        /// <param name="renderer2D">Created 2D renderer.</param>
        void CreateRenderManagers(RenderBackend backend, out RenderManager3D renderer3D, out RenderManager2D renderer2D) {
            if (backend == RenderBackend.DirectX11) {
                var renderer = new DirectX11Renderer3D();
                renderer3D = renderer;
                renderer2D = renderer.Render2D;
                return;
            }

            if (backend == RenderBackend.Vulkan) {
                var renderer = new VulkanRenderer3D();
                renderer3D = renderer;
                renderer2D = renderer.Render2D;
                return;
            }

            throw new InvalidOperationException("Unsupported backend.");
        }

        /// <summary>
        /// Builds a simple camera and cube mesh scene used for validation.
        /// </summary>
        /// <param name="renderer3D">Active 3D renderer.</param>
        /// <param name="renderer2D">Active 2D renderer.</param>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        /// <param name="backend">Backend used for shader compilation target selection.</param>
        void CreateScene(RenderManager3D renderer3D, RenderManager2D renderer2D, int width, int height, RenderBackend backend) {
            if (renderer3D == null) {
                throw new ArgumentNullException(nameof(renderer3D));
            }

            if (renderer2D == null) {
                throw new ArgumentNullException(nameof(renderer2D));
            }

            if (width <= 0 || height <= 0) {
                throw new InvalidOperationException("Scene dimensions must be greater than zero.");
            }

            var cameraEntity = new Entity {
                LayerMask = 0b00000001,
                Position = new float3(0f, 0f, 5f),
                Orientation = float4.Identity
            };
            cameraEntity.InitComponents();
            var camera = new CameraComponent {
                LayerMask = 0b00000001,
                Viewport = new float4(0f, 0f, width, height),
                ClearSettings = new CameraClearSettings(
                    true,
                    new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f),
                    true,
                    1f,
                    false,
                    0)
            };
            cameraEntity.AddComponent(camera);

            var cubeEntity = new Entity {
                LayerMask = 0b00000001,
                Position = float3.Zero,
                Scale = float3.One
            };
            cubeEntity.InitComponents();
            var mesh = new MeshComponent();
            cubeEntity.AddComponent(mesh);

            ModelAsset cubeModelAsset = ModelUtils.GenerateCubeMesh(float3.Zero, new float3(1.25f, 1.25f, 1.25f));
            mesh.Model = renderer3D.BuildModelFromRaw(cubeModelAsset);

            ShaderAsset shaderAsset = RenderShaderFactory.BuildShaderAsset(backend);
            MaterialAsset materialAsset = RenderShaderFactory.BuildMaterialAsset();
            mesh.Material = renderer3D.BuildMaterialFromRaw(materialAsset, shaderAsset);

            CreateOverlaySprites(renderer2D);
        }

        /// <summary>
        /// Renders a number of frames to warm up pipelines before image capture.
        /// </summary>
        /// <param name="core">Core instance driving updates and draws.</param>
        /// <param name="frameCount">Number of frames to render.</param>
        void RenderFrames(Core core, int frameCount) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }

            for (int i = 0; i < frameCount; i++) {
                Application.DoEvents();
                core.Update();
                core.Draw();
                Thread.Sleep(16);
            }
        }

        /// <summary>
        /// Validates that the center pixel reflects mesh shading rather than clear color fallback.
        /// </summary>
        /// <param name="pixel">Center pixel color.</param>
        /// <returns>True when the sampled pixel is accepted.</returns>
        bool ValidateCenterPixel(Color pixel) {
            bool greenDominant = pixel.G >= 140 && pixel.G > pixel.R + 40 && pixel.G > pixel.B + 40;
            bool appearsCornflower = IsNearCornflowerBlue(pixel);
            return greenDominant && !appearsCornflower;
        }

        /// <summary>
        /// Creates two overlay sprites used to validate multi-quad 2D rendering.
        /// </summary>
        /// <param name="renderer2D">Renderer used to create runtime textures for this backend.</param>
        void CreateOverlaySprites(RenderManager2D renderer2D) {
            var whitePixelTextureAsset = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };
            RuntimeTexture pixelTexture = renderer2D.BuildTextureFromRaw(whitePixelTextureAsset);
            int secondSpriteX = OverlaySpriteMargin + OverlaySpriteWidth + OverlaySpriteMargin;
            CreateOverlaySprite(
                OverlaySpriteMargin,
                OverlaySpriteMargin,
                new byte4(255, 32, 32, 255),
                10,
                pixelTexture);
            CreateOverlaySprite(
                secondSpriteX,
                OverlaySpriteMargin,
                new byte4(255, 240, 32, 255),
                11,
                pixelTexture);
        }

        /// <summary>
        /// Creates a single overlay sprite entity.
        /// </summary>
        /// <param name="x">Left position in pixels.</param>
        /// <param name="y">Top position in pixels.</param>
        /// <param name="color">Sprite tint color.</param>
        /// <param name="renderOrder">Render order used for deterministic layering.</param>
        /// <param name="texture">Texture used by the sprite.</param>
        void CreateOverlaySprite(int x, int y, byte4 color, byte renderOrder, RuntimeTexture texture) {
            var overlayEntity = new Entity {
                LayerMask = 0b00000001,
                Position = new float3(x, y, 0f)
            };
            overlayEntity.InitComponents();

            var sprite = new SpriteComponent {
                Texture = texture,
                Color = color,
                Size = new int2(OverlaySpriteWidth, OverlaySpriteHeight),
                RenderOrder2D = renderOrder
            };
            overlayEntity.AddComponent(sprite);
        }

        /// <summary>
        /// Counts red-dominant overlay pixels in the captured image.
        /// </summary>
        /// <param name="imagePath">Path to the captured image.</param>
        /// <returns>Number of red-dominant pixels.</returns>
        int CountRedOverlayPixels(string imagePath) {
            return CountMatchingPixels(imagePath, IsRedOverlayPixel);
        }

        /// <summary>
        /// Counts yellow-dominant overlay pixels in the captured image.
        /// </summary>
        /// <param name="imagePath">Path to the captured image.</param>
        /// <returns>Number of yellow-dominant pixels.</returns>
        int CountYellowOverlayPixels(string imagePath) {
            return CountMatchingPixels(imagePath, IsYellowOverlayPixel);
        }

        /// <summary>
        /// Counts pixels in an image that satisfy the supplied predicate.
        /// </summary>
        /// <param name="imagePath">Path to the captured image.</param>
        /// <param name="predicate">Predicate used to evaluate each pixel.</param>
        /// <returns>Matching pixel count.</returns>
        int CountMatchingPixels(string imagePath, Func<Color, bool> predicate) {
            if (string.IsNullOrWhiteSpace(imagePath)) {
                throw new ArgumentException("Image path must be provided.", nameof(imagePath));
            }

            if (!File.Exists(imagePath)) {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            if (predicate == null) {
                throw new ArgumentNullException(nameof(predicate));
            }

            int count = 0;
            using (var bitmap = new Bitmap(imagePath)) {
                for (int y = 0; y < bitmap.Height; y++) {
                    for (int x = 0; x < bitmap.Width; x++) {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (predicate(pixel)) {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Determines whether a pixel matches the red overlay sprite profile.
        /// </summary>
        /// <param name="pixel">Pixel to evaluate.</param>
        /// <returns>True when the pixel matches expected red overlay output.</returns>
        bool IsRedOverlayPixel(Color pixel) {
            bool redDominant = pixel.R >= 180 && pixel.R > pixel.G + 60 && pixel.R > pixel.B + 60;
            return redDominant && !IsNearCornflowerBlue(pixel);
        }

        /// <summary>
        /// Determines whether a pixel matches the yellow overlay sprite profile.
        /// </summary>
        /// <param name="pixel">Pixel to evaluate.</param>
        /// <returns>True when the pixel matches expected yellow overlay output.</returns>
        bool IsYellowOverlayPixel(Color pixel) {
            bool redHigh = pixel.R >= 180;
            bool greenHigh = pixel.G >= 170;
            bool blueLow = pixel.B <= 110;
            return redHigh && greenHigh && blueLow && !IsNearCornflowerBlue(pixel);
        }

        /// <summary>
        /// Checks whether a color is close to cornflower blue clear color.
        /// </summary>
        /// <param name="pixel">Color to evaluate.</param>
        /// <returns>True when the color is near cornflower blue.</returns>
        bool IsNearCornflowerBlue(Color pixel) {
            bool redNear = Math.Abs(pixel.R - CornflowerBlueR) <= CornflowerTolerance;
            bool greenNear = Math.Abs(pixel.G - CornflowerBlueG) <= CornflowerTolerance;
            bool blueNear = Math.Abs(pixel.B - CornflowerBlueB) <= CornflowerTolerance;
            return redNear && greenNear && blueNear;
        }
    }
}

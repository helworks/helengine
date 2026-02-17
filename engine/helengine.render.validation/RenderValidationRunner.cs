using System.Drawing;
using helengine.directx11;
using helengine.editor;
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
        /// Layer mask used by validation cameras and meshes.
        /// </summary>
        const ushort ValidationSceneLayerMask = 0b00000001;
        /// <summary>
        /// Baseline camera distance used for the regular mesh validation scene.
        /// </summary>
        const float ValidationSceneCameraDistance = 5f;
        /// <summary>
        /// Minimum gizmo-colored pixel count required for each captured gizmo image.
        /// </summary>
        const int MinimumGizmoPixelCount = 120;
        /// <summary>
        /// Channel delta threshold used when classifying gizmo axis colors in captures.
        /// </summary>
        const int GizmoColorDominanceThreshold = 18;
        /// <summary>
        /// Minimum dominant channel value used when classifying gizmo axis colors.
        /// </summary>
        const int GizmoColorMinimumChannel = 40;
        /// <summary>
        /// Minimum color saturation used to reject clear-color background pixels.
        /// </summary>
        const double GizmoColorMinimumSaturation = 0.68;
        /// <summary>
        /// Maximum allowed variation in measured gizmo diagonal size between captures.
        /// </summary>
        const double GizmoDiagonalPixelTolerance = 6.0;
        /// <summary>
        /// Camera distances used when validating constant on-screen gizmo size.
        /// </summary>
        static readonly double[] GizmoValidationDistances = new double[] { 3.0, 6.0, 12.0, 18.0 };

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
            bool selectionSet = false;

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

                CameraComponent validationSceneCamera = CreateScene(renderer3D, renderer2D, Options.FrameWidth, Options.FrameHeight, backend);
                RenderFrames(core, Options.FrameCount);

                RenderImageCapture.CaptureClientArea(window, outputPath);
                Color centerPixel = RenderImageCapture.ReadCenterPixel(outputPath);
                int redOverlayPixelCount = CountRedOverlayPixels(outputPath);
                int yellowOverlayPixelCount = CountYellowOverlayPixels(outputPath);
                bool basePass = ValidateCenterPixel(centerPixel) &&
                    redOverlayPixelCount >= MinimumRedOverlayPixelCount &&
                    yellowOverlayPixelCount >= MinimumYellowOverlayPixelCount;

                if (validationSceneCamera == null || validationSceneCamera.Parent == null) {
                    throw new InvalidOperationException("Validation scene camera must be attached before gizmo validation.");
                }

                validationSceneCamera.Parent.Enabled = false;

                CameraComponent gizmoCamera = CreateGizmoValidationScene(renderer3D, backend, Options.FrameWidth, Options.FrameHeight);
                selectionSet = true;
                bool gizmoScalePass;
                string gizmoScaleMessage;
                ValidateGizmoScaleStability(core, window, outputPath, gizmoCamera, out gizmoScalePass, out gizmoScaleMessage);

                bool passed = basePass && gizmoScalePass;
                string status = passed
                    ? $"Center={RenderImageCapture.FormatColor(centerPixel)} redPixels={redOverlayPixelCount} yellowPixels={yellowOverlayPixelCount} matched expected mesh and overlay output. {gizmoScaleMessage}"
                    : $"Center={RenderImageCapture.FormatColor(centerPixel)} redPixels={redOverlayPixelCount} yellowPixels={yellowOverlayPixelCount} did not match expected mesh and overlay output. {gizmoScaleMessage}";
                return new RenderValidationResult(backend, outputPath, centerPixel, passed, status);
            } catch (Exception ex) {
                return new RenderValidationResult(backend, outputPath, Color.Empty, false, ex.ToString());
            } finally {
                if (selectionSet) {
                    EditorSelectionService.ClearSelection();
                }

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
        /// <returns>Camera used to render the baseline validation scene.</returns>
        CameraComponent CreateScene(RenderManager3D renderer3D, RenderManager2D renderer2D, int width, int height, RenderBackend backend) {
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
                LayerMask = ValidationSceneLayerMask,
                Position = new float3(0f, 0f, ValidationSceneCameraDistance),
                Orientation = float4.Identity
            };
            cameraEntity.InitComponents();
            var camera = new CameraComponent {
                LayerMask = ValidationSceneLayerMask,
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
                LayerMask = ValidationSceneLayerMask,
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
            return camera;
        }

        /// <summary>
        /// Creates a transform gizmo validation scene with a dedicated gizmo camera.
        /// </summary>
        /// <param name="renderer3D">Active 3D renderer.</param>
        /// <param name="backend">Backend used for shader compilation target selection.</param>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        /// <returns>Camera component used to render transform gizmo captures.</returns>
        CameraComponent CreateGizmoValidationScene(RenderManager3D renderer3D, RenderBackend backend, int width, int height) {
            if (renderer3D == null) {
                throw new ArgumentNullException(nameof(renderer3D));
            }

            if (width <= 0 || height <= 0) {
                throw new InvalidOperationException("Gizmo validation dimensions must be greater than zero.");
            }

            var selectedEntity = new Entity {
                LayerMask = ValidationSceneLayerMask,
                Position = float3.Zero,
                Orientation = float4.Identity,
                Scale = float3.One
            };
            selectedEntity.InitComponents();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            var gizmoCameraEntity = new Entity {
                LayerMask = EditorLayerMasks.SceneGizmo,
                Position = new float3(0f, 0f, ValidationSceneCameraDistance),
                Orientation = float4.Identity
            };
            gizmoCameraEntity.InitComponents();
            var gizmoCamera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneGizmo,
                CameraDrawOrder = 200,
                Viewport = new float4(0f, 0f, width, height),
                ClearSettings = new CameraClearSettings(
                    true,
                    new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f),
                    true,
                    1f,
                    false,
                    0)
            };
            gizmoCameraEntity.AddComponent(gizmoCamera);

            RuntimeMaterial gizmoMaterial = BuildTransformGizmoMaterial(renderer3D, backend);
            TransformTranslationGizmoFactory.Create(renderer3D, gizmoCamera, gizmoMaterial);
            return gizmoCamera;
        }

        /// <summary>
        /// Builds the runtime material used by transform gizmo meshes during validation.
        /// </summary>
        /// <param name="renderer3D">Renderer that owns runtime material resources.</param>
        /// <param name="backend">Backend used for shader compilation target selection.</param>
        /// <returns>Runtime gizmo material instance.</returns>
        RuntimeMaterial BuildTransformGizmoMaterial(RenderManager3D renderer3D, RenderBackend backend) {
            if (renderer3D == null) {
                throw new ArgumentNullException(nameof(renderer3D));
            }

            ShaderAsset shaderAsset = RenderShaderFactory.BuildTransformGizmoShaderAsset(backend);
            MaterialAsset materialAsset = RenderShaderFactory.BuildTransformGizmoMaterialAsset();
            return renderer3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Validates that gizmo captures maintain stable on-screen size across camera movement.
        /// </summary>
        /// <param name="core">Core instance driving updates and draws.</param>
        /// <param name="window">Window whose client area is captured.</param>
        /// <param name="baseOutputPath">Base capture path used to derive gizmo output file names.</param>
        /// <param name="gizmoCamera">Camera used to render transform gizmo captures.</param>
        /// <param name="passed">True when measured gizmo size variation is within tolerance.</param>
        /// <param name="statusMessage">Status summary describing measured gizmo diagonals.</param>
        void ValidateGizmoScaleStability(
            Core core,
            RenderValidationWindow window,
            string baseOutputPath,
            CameraComponent gizmoCamera,
            out bool passed,
            out string statusMessage) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }

            if (window == null) {
                throw new ArgumentNullException(nameof(window));
            }

            if (string.IsNullOrWhiteSpace(baseOutputPath)) {
                throw new ArgumentException("Base output path must be provided.", nameof(baseOutputPath));
            }

            if (gizmoCamera == null) {
                throw new ArgumentNullException(nameof(gizmoCamera));
            }

            if (gizmoCamera.Parent == null) {
                throw new InvalidOperationException("Gizmo camera must be attached to an entity.");
            }

            double[] diagonals = new double[GizmoValidationDistances.Length];
            int[] pixelCounts = new int[GizmoValidationDistances.Length];
            for (int i = 0; i < GizmoValidationDistances.Length; i++) {
                MoveGizmoCameraToDistance(gizmoCamera.Parent, GizmoValidationDistances[i]);
                RenderFrames(core, Options.FrameCount);

                string capturePath = BuildGizmoCapturePath(baseOutputPath, i);
                RenderImageCapture.CaptureClientArea(window, capturePath);

                int minX;
                int maxX;
                int minY;
                int maxY;
                MeasureGizmoFootprint(capturePath, out pixelCounts[i], out minX, out maxX, out minY, out maxY);
                if (pixelCounts[i] < MinimumGizmoPixelCount) {
                    passed = false;
                    statusMessage = $"GizmoScale=FAIL distance={GizmoValidationDistances[i]:0.###} produced too few gizmo pixels ({pixelCounts[i]}).";
                    return;
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                diagonals[i] = ComputeDistance(0.0, 0.0, width, height);
            }

            double minimumDiagonal = diagonals[0];
            double maximumDiagonal = diagonals[0];
            for (int i = 1; i < diagonals.Length; i++) {
                if (diagonals[i] < minimumDiagonal) {
                    minimumDiagonal = diagonals[i];
                }

                if (diagonals[i] > maximumDiagonal) {
                    maximumDiagonal = diagonals[i];
                }
            }

            double delta = maximumDiagonal - minimumDiagonal;
            passed = delta <= GizmoDiagonalPixelTolerance;
            string summary = BuildGizmoMeasurementSummary(diagonals, pixelCounts);
            statusMessage = passed
                ? $"GizmoScale=PASS delta={delta:0.###}px tolerance={GizmoDiagonalPixelTolerance:0.###}px {summary}"
                : $"GizmoScale=FAIL delta={delta:0.###}px tolerance={GizmoDiagonalPixelTolerance:0.###}px {summary}";
        }

        /// <summary>
        /// Moves the gizmo validation camera to a specified Z-axis distance from the origin.
        /// </summary>
        /// <param name="cameraEntity">Camera entity to reposition.</param>
        /// <param name="distance">Positive distance from the origin along +Z.</param>
        void MoveGizmoCameraToDistance(Entity cameraEntity, double distance) {
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }

            if (distance <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(distance), "Camera distance must be greater than zero.");
            }

            cameraEntity.Position = new float3(0f, 0f, (float)distance);
            cameraEntity.Orientation = float4.Identity;
        }

        /// <summary>
        /// Builds the output file path for a gizmo capture index.
        /// </summary>
        /// <param name="baseOutputPath">Base validation output path.</param>
        /// <param name="index">Zero-based gizmo capture index.</param>
        /// <returns>Output path for the indexed gizmo capture.</returns>
        string BuildGizmoCapturePath(string baseOutputPath, int index) {
            if (string.IsNullOrWhiteSpace(baseOutputPath)) {
                throw new ArgumentException("Base output path must be provided.", nameof(baseOutputPath));
            }

            if (index < 0) {
                throw new ArgumentOutOfRangeException(nameof(index), "Capture index must be non-negative.");
            }

            string directory = Path.GetDirectoryName(baseOutputPath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Base output path must include a directory.");
            }

            string baseFileName = Path.GetFileNameWithoutExtension(baseOutputPath);
            if (string.IsNullOrWhiteSpace(baseFileName)) {
                throw new InvalidOperationException("Base output file name must be provided.");
            }

            string extension = Path.GetExtension(baseOutputPath);
            if (string.IsNullOrWhiteSpace(extension)) {
                extension = ".png";
            }

            string fileName = $"{baseFileName}.gizmo-{index + 1}{extension}";
            return Path.Combine(directory, fileName);
        }

        /// <summary>
        /// Measures gizmo footprint bounds in a captured image.
        /// </summary>
        /// <param name="imagePath">Path to captured gizmo image.</param>
        /// <param name="pixelCount">Number of pixels classified as gizmo pixels.</param>
        /// <param name="minX">Minimum x coordinate of gizmo pixels.</param>
        /// <param name="maxX">Maximum x coordinate of gizmo pixels.</param>
        /// <param name="minY">Minimum y coordinate of gizmo pixels.</param>
        /// <param name="maxY">Maximum y coordinate of gizmo pixels.</param>
        void MeasureGizmoFootprint(
            string imagePath,
            out int pixelCount,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY) {
            if (string.IsNullOrWhiteSpace(imagePath)) {
                throw new ArgumentException("Image path must be provided.", nameof(imagePath));
            }

            if (!File.Exists(imagePath)) {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            pixelCount = 0;
            minX = 0;
            maxX = 0;
            minY = 0;
            maxY = 0;
            using (var bitmap = new Bitmap(imagePath)) {
                int width = bitmap.Width;
                int height = bitmap.Height;
                if (width <= 0 || height <= 0) {
                    throw new InvalidOperationException("Captured gizmo image must have positive dimensions.");
                }

                int totalPixels = width * height;
                bool[] gizmoMask = new bool[totalPixels];
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        int index = (y * width) + x;
                        gizmoMask[index] = IsGizmoPixel(bitmap.GetPixel(x, y));
                    }
                }

                bool[] visited = new bool[totalPixels];
                int[] queue = new int[totalPixels];
                int bestPixelCount = 0;
                int bestMinX = 0;
                int bestMaxX = 0;
                int bestMinY = 0;
                int bestMaxY = 0;

                for (int startIndex = 0; startIndex < totalPixels; startIndex++) {
                    if (!gizmoMask[startIndex] || visited[startIndex]) {
                        continue;
                    }

                    int queueStart = 0;
                    int queueEnd = 0;
                    queue[queueEnd] = startIndex;
                    queueEnd++;
                    visited[startIndex] = true;

                    int componentPixelCount = 0;
                    int componentMinX = int.MaxValue;
                    int componentMaxX = int.MinValue;
                    int componentMinY = int.MaxValue;
                    int componentMaxY = int.MinValue;
                    bool touchesImageBoundary = false;

                    while (queueStart < queueEnd) {
                        int currentIndex = queue[queueStart];
                        queueStart++;

                        int y = currentIndex / width;
                        int x = currentIndex - (y * width);
                        componentPixelCount++;
                        if (x < componentMinX) {
                            componentMinX = x;
                        }
                        if (x > componentMaxX) {
                            componentMaxX = x;
                        }
                        if (y < componentMinY) {
                            componentMinY = y;
                        }
                        if (y > componentMaxY) {
                            componentMaxY = y;
                        }

                        if (x == 0 || y == 0 || x == width - 1 || y == height - 1) {
                            touchesImageBoundary = true;
                        }

                        int leftIndex = currentIndex - 1;
                        if (x > 0 && gizmoMask[leftIndex] && !visited[leftIndex]) {
                            visited[leftIndex] = true;
                            queue[queueEnd] = leftIndex;
                            queueEnd++;
                        }

                        int rightIndex = currentIndex + 1;
                        if (x < width - 1 && gizmoMask[rightIndex] && !visited[rightIndex]) {
                            visited[rightIndex] = true;
                            queue[queueEnd] = rightIndex;
                            queueEnd++;
                        }

                        int upIndex = currentIndex - width;
                        if (y > 0 && gizmoMask[upIndex] && !visited[upIndex]) {
                            visited[upIndex] = true;
                            queue[queueEnd] = upIndex;
                            queueEnd++;
                        }

                        int downIndex = currentIndex + width;
                        if (y < height - 1 && gizmoMask[downIndex] && !visited[downIndex]) {
                            visited[downIndex] = true;
                            queue[queueEnd] = downIndex;
                            queueEnd++;
                        }
                    }

                    if (touchesImageBoundary) {
                        continue;
                    }

                    if (componentPixelCount > bestPixelCount) {
                        bestPixelCount = componentPixelCount;
                        bestMinX = componentMinX;
                        bestMaxX = componentMaxX;
                        bestMinY = componentMinY;
                        bestMaxY = componentMaxY;
                    }
                }

                if (bestPixelCount > 0) {
                    pixelCount = bestPixelCount;
                    minX = bestMinX;
                    maxX = bestMaxX;
                    minY = bestMinY;
                    maxY = bestMaxY;
                }
            }
        }

        /// <summary>
        /// Determines whether a pixel belongs to the transform gizmo color palette.
        /// </summary>
        /// <param name="pixel">Pixel to evaluate.</param>
        /// <returns>True when the pixel is classified as a gizmo pixel.</returns>
        bool IsGizmoPixel(Color pixel) {
            if (IsNearCornflowerBlue(pixel)) {
                return false;
            }

            int maxChannel = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
            if (maxChannel < GizmoColorMinimumChannel) {
                return false;
            }

            int minChannel = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
            double saturation = (maxChannel - minChannel) / (double)maxChannel;
            if (saturation < GizmoColorMinimumSaturation) {
                return false;
            }

            bool redDominant = pixel.R > pixel.G + GizmoColorDominanceThreshold &&
                pixel.R > pixel.B + GizmoColorDominanceThreshold;
            bool greenDominant = pixel.G > pixel.R + GizmoColorDominanceThreshold &&
                pixel.G > pixel.B + GizmoColorDominanceThreshold;
            bool blueDominant = pixel.B > pixel.R + GizmoColorDominanceThreshold &&
                pixel.B > pixel.G + GizmoColorDominanceThreshold;
            return redDominant || greenDominant || blueDominant;
        }

        /// <summary>
        /// Builds a compact summary of measured gizmo diagonals and pixel counts.
        /// </summary>
        /// <param name="diagonals">Measured gizmo diagonal values in pixels.</param>
        /// <param name="pixelCounts">Measured gizmo pixel counts.</param>
        /// <returns>Formatted summary string.</returns>
        string BuildGizmoMeasurementSummary(double[] diagonals, int[] pixelCounts) {
            if (diagonals == null) {
                throw new ArgumentNullException(nameof(diagonals));
            }

            if (pixelCounts == null) {
                throw new ArgumentNullException(nameof(pixelCounts));
            }

            if (diagonals.Length != pixelCounts.Length || diagonals.Length != GizmoValidationDistances.Length) {
                throw new InvalidOperationException("Gizmo measurement arrays must match validation distance count.");
            }

            string summary = "samples=";
            for (int i = 0; i < diagonals.Length; i++) {
                if (i > 0) {
                    summary = string.Concat(summary, " | ");
                }

                summary = string.Concat(
                    summary,
                    "d=",
                    GizmoValidationDistances[i].ToString("0.###"),
                    ":diag=",
                    diagonals[i].ToString("0.###"),
                    ":px=",
                    pixelCounts[i].ToString());
            }

            return summary;
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
                LayerMask = ValidationSceneLayerMask,
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

        /// <summary>
        /// Computes Euclidean distance between two 2D points.
        /// </summary>
        /// <param name="x0">First point X coordinate.</param>
        /// <param name="y0">First point Y coordinate.</param>
        /// <param name="x1">Second point X coordinate.</param>
        /// <param name="y1">Second point Y coordinate.</param>
        /// <returns>Distance between the two points.</returns>
        double ComputeDistance(double x0, double y0, double x1, double y1) {
            double deltaX = x1 - x0;
            double deltaY = y1 - y0;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }
    }
}

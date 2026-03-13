using helengine.editor;
using helengine.editor.windows;
using helengine.directx11;
using helengine.vulkan;
using System;
using System.IO;

namespace helengine.editor.app {
    /// <summary>
    /// Main editor host form for Helengine, wiring up rendering and dockable UI.
    /// </summary>
    public partial class MainForm : Form {
        /// <summary>
        /// Environment variable that selects the rendering backend (vulkan or directx11).
        /// </summary>
        const string RendererBackendEnvironmentVariable = "HELENGINE_RENDER_BACKEND";
        /// <summary>
        /// Environment variable that supplies the helshader tool path.
        /// </summary>
        const string ShaderToolEnvironmentVariable = "HELENGINE_SHADER_TOOL";
        /// <summary>
        /// File path used to persist editor loop exceptions.
        /// </summary>
        static readonly string LoopErrorLogPath = Path.Combine(Path.GetTempPath(), "helengine.editor.loop-errors.log");
        /// <summary>
        /// Background thread that drives the editor update loop.
        /// </summary>
        Thread thread;
        /// <summary>
        /// Tracks whether the form has been closed to stop the loop.
        /// </summary>
        bool closed;
        /// <summary>
        /// Tracks whether initialization has completed to guard resize logic.
        /// </summary>
        bool initialized;
        /// <summary>
        /// Stores the project path used to locate project assets.
        /// </summary>
        string projectPath = string.Empty;
        /// <summary>
        /// Tracks whether a loop exception has been recorded to avoid log spam.
        /// </summary>
        bool loopExceptionRecorded;

        /// <summary>
        /// Editor session that owns core editor state and panels.
        /// </summary>
        EditorSession editorSession;
        /// <summary>
        /// Renderer driving the editor render loop.
        /// </summary>
        RenderManager3D renderer3D;

        /// <summary>
        /// Initializes the main editor form for a specific project path.
        /// </summary>
        /// <param name="projectPath">Path to the project to open.</param>
        public MainForm(string projectPath) {
            InitializeWindowFrame();

            this.projectPath = projectPath;

            string title = Text;
            if (!string.IsNullOrWhiteSpace(this.projectPath)) {
                title = $"helengine - {Path.GetFileName(this.projectPath)}";
            }

            InitializeEditor(title);
        }

        /// <summary>
        /// Initializes the form shell and window chrome settings.
        /// </summary>
        void InitializeWindowFrame() {
            InitializeComponent();
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
        }

        /// <summary>
        /// Updates the form title text and keeps the title bar in sync.
        /// </summary>
        /// <param name="title">Title text to display.</param>
        private void SetWindowTitle(string title) {
            Text = title;
            editorSession.SetTitle(title);
        }

        /// <summary>
        /// Sets up rendering, input, cameras, UI chrome, and the initial layout.
        /// </summary>
        /// <param name="titleText">Initial window title text.</param>
        private void InitializeEditor(string titleText) {
            EditorCore core = new EditorCore(null);
            string projectRootPath = ResolveProjectRootPath(projectPath);
            string projectAssetsRootPath = ResolveAssetsRootPath(projectRootPath);

            string rendererBackend = Environment.GetEnvironmentVariable(RendererBackendEnvironmentVariable, EnvironmentVariableTarget.Process);
            bool useVulkan = false;
            if (!string.IsNullOrWhiteSpace(rendererBackend)) {
                rendererBackend = rendererBackend.Trim();
                if (string.Equals(rendererBackend, "vulkan", StringComparison.OrdinalIgnoreCase)) {
                    useVulkan = true;
                } else if (!string.Equals(rendererBackend, "directx11", StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Unsupported renderer backend '{rendererBackend}'. Use 'vulkan' or 'directx11'.");
                }
            }

            useVulkan = false;

            RenderManager2D renderer2D;
            if (useVulkan) {
                VulkanRenderer3D vulkanRenderer = new VulkanRenderer3D();
                renderer3D = vulkanRenderer;
                renderer2D = vulkanRenderer.Render2D;
            } else {
                DirectX11Renderer3D directX11Renderer = new DirectX11Renderer3D();
                renderer3D = directX11Renderer;
                renderer2D = directX11Renderer.Render2D;
            }
            InputManager inputManager = new InputManagerWindows(this.Handle);
            CoreInitializationOptions initOptions = new CoreInitializationOptions {
                ContentRootPath = projectAssetsRootPath
            };
            core.Initialize(renderer3D, renderer2D, inputManager, initOptions);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            renderer3D.AddWindow(this.Handle, renderWidth, renderHeight);

            FontAsset uiFont = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));
            FontAsset snapModifierFont = GDIFontProcessor.ImportFont(new Font("Consolas", 15, FontStyle.Bold, GraphicsUnit.Pixel));
            ContentManager contentManager = core.ContentManager;
            EditorViewportToolbarIconSet toolbarIcons = EditorToolbarIconLoader.LoadDefaultToolbarIcons(contentManager, AppContext.BaseDirectory);
            IReadOnlyList<IAssetImporterRegistration> importers = BuildImporters();
            editorSession = new EditorSession(
                core,
                projectPath,
                uiFont,
                snapModifierFont,
                titleText,
                renderer3D,
                renderer2D,
                inputManager,
                renderWidth,
                renderHeight,
                toolbarIcons,
                importers);

            TitleBarWindowAdapter.Attach(editorSession.TitleBar, this);
            SetWindowTitle(titleText);

            UpdateMinimumWindowSize();
            renderWidth = Math.Max(1, ClientSize.Width);
            renderHeight = Math.Max(1, ClientSize.Height);
            editorSession.UpdateLayout(renderWidth, renderHeight);

            thread = new Thread(RunEditorLoop);
            thread.Start();

            initialized = true;
        }

        /// <summary>
        /// Drives the editor update and draw loop on a worker thread.
        /// </summary>
        private void RunEditorLoop() {
            TimeSpan span = TimeSpan.FromMilliseconds(1000 / 120.0);
            for (; ; ) {
                Thread.Sleep(span);
                if (closed) {
                    break;
                }

                try {
                    Invoke(() => {
                        if (UpdateMinimumWindowSize()) {
                            return;
                        }

                        int renderWidth = Math.Max(1, ClientSize.Width);
                        int renderHeight = Math.Max(1, ClientSize.Height);
                        editorSession.UpdateFrame(renderWidth, renderHeight);
                        UpdateDockingCursor();
                    });
                } catch (Exception ex) {
                    RecordLoopException(ex);
                }
            }
        }

        /// <summary>
        /// Persists a render-loop exception so runtime failures are visible outside the debugger.
        /// </summary>
        /// <param name="exception">Exception to record.</param>
        void RecordLoopException(Exception exception) {
            if (exception == null) {
                throw new ArgumentNullException(nameof(exception));
            }

            if (loopExceptionRecorded) {
                return;
            }

            loopExceptionRecorded = true;

            try {
                string message = string.Concat(
                    DateTime.UtcNow.ToString("O"),
                    " | ",
                    exception.ToString(),
                    Environment.NewLine,
                    Environment.NewLine);
                File.AppendAllText(LoopErrorLogPath, message);
            } catch {
            }
        }

        /// <summary>
        /// Builds the default asset importer registrations for the editor.
        /// </summary>
        /// <returns>Importer registrations used for asset import settings.</returns>
        IReadOnlyList<IAssetImporterRegistration> BuildImporters() {
            string[] textureExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" };
            string[] textExtensions = new[] { ".txt" };
            var registrations = new IAssetImporterRegistration[] {
                new TextureImporterRegistration("gdi", new GDITextureImporter(), textureExtensions),
                new TextImporterRegistration("text", new TextImporter(), textExtensions)
            };
            return registrations;
        }

        /// <summary>
        /// Resolves the absolute project root path from the configured project input.
        /// </summary>
        /// <param name="inputProjectPath">Project directory path or project file path.</param>
        /// <returns>Absolute project root path.</returns>
        string ResolveProjectRootPath(string inputProjectPath) {
            if (string.IsNullOrWhiteSpace(inputProjectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            if (Directory.Exists(inputProjectPath)) {
                return Path.GetFullPath(inputProjectPath);
            }

            if (File.Exists(inputProjectPath)) {
                string directory = Path.GetDirectoryName(inputProjectPath);
                if (string.IsNullOrWhiteSpace(directory)) {
                    throw new InvalidOperationException("Project file path does not include a directory.");
                }

                return Path.GetFullPath(directory);
            }

            throw new DirectoryNotFoundException($"Project path '{inputProjectPath}' does not exist.");
        }

        /// <summary>
        /// Resolves the absolute assets root path for the current project.
        /// </summary>
        /// <param name="inputProjectRootPath">Absolute project root path.</param>
        /// <returns>Absolute assets root path.</returns>
        string ResolveAssetsRootPath(string inputProjectRootPath) {
            if (string.IsNullOrWhiteSpace(inputProjectRootPath)) {
                throw new InvalidOperationException("Project root path is required to locate assets.");
            }

            return Path.GetFullPath(Path.Combine(inputProjectRootPath, "assets"));
        }

        /// <summary>
        /// Stops the editor loop and disposes engine resources when the window closes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            editorSession.Dispose();
        }

        /// <summary>
        /// Handles activation to allow future input focus handling hooks.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            editorSession.SetKeyboardActive(true);
        }

        /// <summary>
        /// Handles window deactivation to support future focus-aware behaviors.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            editorSession.SetKeyboardActive(false);
        }

        /// <summary>
        /// Resizes render targets and UI layout when the window size changes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (!initialized) {
                return;
            }

            renderer3D.OnWindowResize(Handle, ClientSize.Width, ClientSize.Height);
            if (UpdateMinimumWindowSize()) {
                return;
            }
            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            editorSession.UpdateLayout(renderWidth, renderHeight);
        }

        /// <summary>
        /// Applies the minimum window size needed to fit docked panels and the title bar.
        /// </summary>
        /// <returns>True when the window size was adjusted.</returns>
        bool UpdateMinimumWindowSize() {
            int2 minWindow = editorSession.MinimumWindowSize;
            int minWidth = Math.Max(1, minWindow.X);
            int minHeight = Math.Max(1, minWindow.Y);
            MinimumSize = new Size(minWidth, minHeight);

            int targetWidth = Math.Max(Width, minWidth);
            int targetHeight = Math.Max(Height, minHeight);
            if (targetWidth != Width || targetHeight != Height) {
                Size = new Size(targetWidth, targetHeight);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the cursor based on docking state and resize hit testing.
        /// </summary>
        void UpdateDockingCursor() {
            int2 pointer = editorSession.PointerPosition;

            switch (editorSession.DockingCursorState) {
                case DockingCursorState.VerticalSplit:
                    Cursor = Cursors.VSplit;
                    break;
                case DockingCursorState.HorizontalSplit:
                    Cursor = Cursors.HSplit;
                    break;
                default:
                    if (WindowResizeAdapter.TryGetResizeCursor(this, new Point(pointer.X, pointer.Y), WindowResizeAdapter.DefaultResizeBorderThickness, out var resizeCursor)) {
                        Cursor = resizeCursor;
                    } else {
                        Cursor = Cursors.Default;
                    }
                    break;
            }
        }

        /// <summary>
        /// Enables borderless window resizing by returning the appropriate hit test results.
        /// </summary>
        /// <param name="m">Windows message payload.</param>
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            WindowResizeAdapter.ApplyResizeHitTest(this, ref m, WindowResizeAdapter.DefaultResizeBorderThickness);
        }
    }
}

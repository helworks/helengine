using helengine.editor;
using helengine.editor.windows;
using helengine.directx11;
using helengine.projectfile;
using helengine.vulkan;
using System;
using System.IO;

namespace helengine.editor.app {
    /// <summary>
    /// Main editor host form for Helengine, wiring up rendering and dockable UI.
    /// </summary>
    public partial class MainForm : Form, IResizeBorderState, ITitleBarDragRestoreState, IWindowForegroundState {
        /// <summary>
        /// Environment variable that selects the rendering backend (vulkan or directx11).
        /// </summary>
        const string RendererBackendEnvironmentVariable = "HELENGINE_RENDER_BACKEND";
        /// <summary>
        /// Windows message sent after a move or resize loop completes.
        /// </summary>
        const int WmExitSizeMove = 0x0232;
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
        /// Tracks whether the next close attempt should be allowed after a session-driven exit request.
        /// </summary>
        bool allowSessionDrivenClose;
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
        /// Tracks the current custom maximize state for the borderless editor host.
        /// </summary>
        readonly BorderlessWindowStateController WindowStateController = new BorderlessWindowStateController(new WindowsWindowArrangementFeatureState());

        /// <summary>
        /// Editor session that owns core editor state and panels.
        /// </summary>
        EditorSession editorSession;
        /// <summary>
        /// Renderer driving the editor render loop.
        /// </summary>
        RenderManager3D renderer3D;
        /// <summary>
        /// Controller that resolves persisted editor UI scale settings against the current monitor DPI.
        /// </summary>
        EditorUiScaleController uiScaleController;

        /// <summary>
        /// Gets a value indicating whether border-resize behavior remains enabled for the current window state.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsResizeBorderEnabled => WindowStateController.IsResizeBorderEnabled;

        /// <summary>
        /// Gets whether foreground-only window affordances should be active for this host.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsWindowForegroundActive { get; private set; } = true;

        /// <summary>
        /// Initializes the main editor form for a specific project path.
        /// </summary>
        /// <param name="projectPath">Path to the project to open.</param>
        public MainForm(string projectPath) {
            InitializeWindowFrame();

            this.projectPath = projectPath;
            InitializeEditor();
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
        /// Updates the form title text so the native window mirrors the editor session title.
        /// </summary>
        /// <param name="title">Title text to display.</param>
        private void SetWindowTitle(string title) {
            Text = title;
        }

        /// <summary>
        /// Sets up rendering, input, cameras, UI chrome, and the initial layout.
        /// </summary>
        private void InitializeEditor() {
            EditorCore core = new EditorCore(null);
            string projectRootPath = ResolveProjectRootPath(projectPath);
            string projectAssetsRootPath = ResolveAssetsRootPath(projectRootPath);
            uiScaleController = new EditorUiScaleController(new EditorPreferencesService(ResolveEditorPreferencesRootPath()));
            EditorUiScaleSettings initialUiScaleSettings = uiScaleController.Load();
            EditorUiMetrics initialUiMetrics = uiScaleController.ResolveMetrics(DeviceDpi);

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
            IInputBackend inputBackend = new InputBackendWindows(this.Handle);
            CoreInitializationOptions initOptions = new CoreInitializationOptions {
                ContentRootPath = projectAssetsRootPath
            };
            core.Initialize(renderer3D, renderer2D, inputBackend, initOptions);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            renderer3D.AddWindow(this.Handle, renderWidth, renderHeight);

            FontAsset uiFont = CreateUiFont(initialUiMetrics);
            FontAsset snapModifierFont = CreateSnapModifierFont(initialUiMetrics);
            ContentManager contentManager = core.ContentManager;
            EditorViewportToolbarIconSet toolbarIcons = EditorToolbarIconLoader.LoadDefaultToolbarIcons(contentManager, AppContext.BaseDirectory);
            RuntimeTexture titleBarIcon = EditorToolbarIconLoader.LoadTitleBarIcon(contentManager, AppContext.BaseDirectory);
            IReadOnlyList<IAssetImporterRegistration> importers = EditorHostImporterFactory.CreateDefault();
            editorSession = new EditorSession(
                core,
                projectPath,
                initialUiScaleSettings,
                initialUiMetrics,
                uiFont,
                snapModifierFont,
                renderer3D,
                renderer2D,
                inputBackend,
                renderWidth,
                renderHeight,
                toolbarIcons,
                titleBarIcon,
                importers,
                FolderDialog.OpenFolderDialog);

            editorSession.TitleChanged += SetWindowTitle;
            editorSession.CloseRequested += HandleEditorSessionCloseRequested;
            editorSession.UiScaleSettingsChanged += HandleUiScaleSettingsChanged;
            TitleBarWindowAdapter.Attach(editorSession.TitleBar, this, () => ToggleMaximizeState());
            SetWindowTitle(editorSession.WindowTitle);

            UpdateMinimumWindowSize();
            renderWidth = Math.Max(1, ClientSize.Width);
            renderHeight = Math.Max(1, ClientSize.Height);
            editorSession.UpdateLayout(renderWidth, renderHeight);

            thread = new Thread(RunEditorLoop);
            thread.Start();

            initialized = true;
        }

        /// <summary>
        /// Restores the custom maximized state so a native title-bar drag can continue from the current cursor position.
        /// </summary>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        public void PrepareForTitleBarDrag(Point cursorScreenPosition) {
            WindowStateController.PrepareForTitleBarDrag(this, cursorScreenPosition);
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
        /// Resolves the absolute project root path from the configured project input.
        /// </summary>
        /// <param name="inputProjectPath">Project directory path or project file path.</param>
        /// <returns>Absolute project root path.</returns>
        string ResolveProjectRootPath(string inputProjectPath) {
            if (string.IsNullOrWhiteSpace(inputProjectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            ProjectFilePathResolver resolver = new ProjectFilePathResolver();
            string canonicalProjectFilePath = resolver.Resolve(inputProjectPath);
            string directory = Path.GetDirectoryName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Project file path does not include a directory.");
            }

            return Path.GetFullPath(directory);
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
        /// Resolves the editor-global preferences root directory used to persist host-independent editor settings.
        /// </summary>
        /// <returns>Absolute preferences root directory path.</returns>
        string ResolveEditorPreferencesRootPath() {
            string applicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(applicationDataRoot)) {
                throw new InvalidOperationException("Application data root path is required to store editor preferences.");
            }

            return Path.Combine(applicationDataRoot, "helengine", "editor");
        }

        /// <summary>
        /// Creates the editor UI font for the supplied scaled editor metrics.
        /// </summary>
        /// <param name="metrics">Scaled editor UI metrics resolved for the current host DPI state.</param>
        /// <returns>Font asset used for editor UI chrome and panel text.</returns>
        FontAsset CreateUiFont(EditorUiMetrics metrics) {
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            return GDIFontProcessor.ImportFont(new Font("Consolas", metrics.UiFontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel));
        }

        /// <summary>
        /// Creates the viewport snap-modifier font for the supplied scaled editor metrics.
        /// </summary>
        /// <param name="metrics">Scaled editor UI metrics resolved for the current host DPI state.</param>
        /// <returns>Font asset used for viewport snap-modifier labels.</returns>
        FontAsset CreateSnapModifierFont(EditorUiMetrics metrics) {
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            return GDIFontProcessor.ImportFont(new Font("Consolas", metrics.SnapModifierFontPixelSize, FontStyle.Bold, GraphicsUnit.Pixel));
        }

        /// <summary>
        /// Stops the editor loop and disposes engine resources when the window closes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            editorSession.CloseRequested -= HandleEditorSessionCloseRequested;
            editorSession.UiScaleSettingsChanged -= HandleUiScaleSettingsChanged;
            editorSession.Dispose();
        }

        /// <summary>
        /// Reapplies the current editor UI scale when the host monitor DPI changes and the editor is following monitor DPI automatically.
        /// </summary>
        /// <param name="e">DPI-change event data supplied by WinForms.</param>
        protected override void OnDpiChanged(DpiChangedEventArgs e) {
            base.OnDpiChanged(e);
            if (!initialized || uiScaleController == null || editorSession == null) {
                return;
            }

            if (uiScaleController.ShouldReapplyForMonitorDpiChange()) {
                ReapplyCurrentUiScale();
            }
        }

        /// <summary>
        /// Intercepts close attempts so dirty scenes can show the unsaved-changes prompt before exit.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if (allowSessionDrivenClose) {
                allowSessionDrivenClose = false;
                base.OnFormClosing(e);
                return;
            }

            if (editorSession != null && editorSession.RequestClose()) {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        /// <summary>
        /// Handles activation to allow future input focus handling hooks.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            IsWindowForegroundActive = true;
            editorSession.SetKeyboardActive(true);
        }

        /// <summary>
        /// Handles window deactivation to support future focus-aware behaviors.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            IsWindowForegroundActive = false;
            Cursor = Cursors.Default;
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
        /// Toggles between maximized and normal window states using working area bounds.
        /// </summary>
        void ToggleMaximizeState() {
            WindowStateController.ToggleMaximize(this);
        }

        /// <summary>
        /// Handles the session request to close the host window after pending unsaved changes are resolved.
        /// </summary>
        void HandleEditorSessionCloseRequested() {
            allowSessionDrivenClose = true;
            Close();
        }

        /// <summary>
        /// Persists one newly confirmed editor UI scale selection and reapplies the effective UI scale live.
        /// </summary>
        /// <param name="settings">Validated editor UI scale settings confirmed by the user.</param>
        void HandleUiScaleSettingsChanged(EditorUiScaleSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            uiScaleController.ApplyUserSelection(settings);
            ReapplyCurrentUiScale();
        }

        /// <summary>
        /// Reloads persisted editor UI scale settings, rebuilds scaled fonts, and reapplies the current metrics to the active session.
        /// </summary>
        void ReapplyCurrentUiScale() {
            EditorUiScaleSettings settings = uiScaleController.Load();
            EditorUiMetrics metrics = uiScaleController.ResolveMetrics(DeviceDpi);
            FontAsset uiFont = CreateUiFont(metrics);
            FontAsset snapModifierFont = CreateSnapModifierFont(metrics);
            editorSession.ApplyUiScale(settings, metrics, uiFont, snapModifierFont);
            UpdateMinimumWindowSize();
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
            if (!IsWindowForegroundActive) {
                Cursor = Cursors.Default;
                return;
            }

            int2 pointer = editorSession.PointerPosition;

            switch (editorSession.DockingCursorState) {
                case DockingCursorState.VerticalSplit:
                    Cursor = EditorHostCursorResolver.Resolve(
                        editorSession.DockingCursorState,
                        editorSession.HoverCursor,
                        false,
                        Cursors.Default);
                    break;
                case DockingCursorState.HorizontalSplit:
                    Cursor = EditorHostCursorResolver.Resolve(
                        editorSession.DockingCursorState,
                        editorSession.HoverCursor,
                        false,
                        Cursors.Default);
                    break;
                default:
                    if (WindowResizeAdapter.TryGetResizeCursor(this, new Point(pointer.X, pointer.Y), WindowResizeAdapter.DefaultResizeBorderThickness, out var resizeCursor)) {
                        Cursor = EditorHostCursorResolver.Resolve(
                            editorSession.DockingCursorState,
                            editorSession.HoverCursor,
                            true,
                            resizeCursor);
                    } else {
                        Cursor = EditorHostCursorResolver.Resolve(
                            editorSession.DockingCursorState,
                            editorSession.HoverCursor,
                            false,
                            Cursors.Default);
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

            if (m.Msg == WmExitSizeMove) {
                WindowStateController.CompleteTitleBarDrag(this, Cursor.Position);
            }

            WindowResizeAdapter.ApplyResizeHitTest(this, ref m, WindowResizeAdapter.DefaultResizeBorderThickness);
        }
    }
}


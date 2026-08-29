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
        /// File path used to persist all editor logger output for diagnostics outside the in-app logger panel.
        /// </summary>
        static readonly string SessionLogPath = Path.Combine(Path.GetTempPath(), "helengine.editor.log");
        /// <summary>
        /// Logger subscription that mirrors logger-panel entries into the session log file.
        /// </summary>
        Action<LogEntry> sessionLogListener;
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
            AttachSessionLogListener();
            InitializeEditor();
        }

        /// <summary>
        /// Mirrors all logger output into the session log file so diagnostics survive outside the in-app logger panel.
        /// </summary>
        void AttachSessionLogListener() {
            sessionLogListener = entry => {
                try {
                    File.AppendAllText(
                        SessionLogPath,
                        string.Concat(DateTime.UtcNow.ToString("O"), " | ", entry.Level, " | ", entry.Message, Environment.NewLine));
                } catch {
                }
            };
            Logger.MessageLogged += sessionLogListener;
            sessionLogListener(new LogEntry(LogLevel.Info, $"Editor session started for project '{projectPath}'.", 0d));
        }

        /// <summary>
        /// Adds the native sizing frame while allowing the editor to render all window chrome itself.
        /// </summary>
        protected override CreateParams CreateParams {
            get {
                CreateParams createParams = base.CreateParams;
                createParams.Style = WindowResizeAdapter.GetResizableWindowStyle(createParams.Style);
                return createParams;
            }
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
            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(projectRootPath);
            string projectAssetsRootPath = ResolveAssetsRootPath(projectRootPath);
            uiScaleController = new EditorUiScaleController(new EditorPreferencesService(ResolveEditorPreferencesRootPath()));
            EditorPreferencesSettings initialEditorPreferences = uiScaleController.LoadPreferences();
            ApplyEditorTheme(initialEditorPreferences.ThemeId);
            EditorUiScaleSettings initialUiScaleSettings = initialEditorPreferences.UiScale;
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
                ContentStreamSource = new HostFileSystemContentStreamSource(projectAssetsRootPath),
                ScenePathResolver = bootstrap.SceneCatalogService
            };
            PlatformInfo platformInfo = ResolveEditorPlatformInfo(projectPath);
            core.Initialize(renderer3D, renderer2D, inputBackend, platformInfo, initOptions);
            core.SetTextClipboardService(new SystemTextClipboardService());
            BepuRuntimeComponentRegistration.Register(core);
            BepuPhysicsWorld3D physicsWorld = BepuRuntimeComponentRegistration.CreateRuntimeWorld(core);
            BepuRuntimeComponentRegistration.AttachRuntimeWorld(core, physicsWorld);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            renderer3D.AddWindow(this.Handle, renderWidth, renderHeight);

            FontAsset uiFont = CreateUiFont(initialUiMetrics, renderer2D);
            FontAsset snapModifierFont = CreateSnapModifierFont(initialUiMetrics, renderer2D);
            ContentManager contentManager = core.ContentManager;
            EditorViewportToolbarIconSet toolbarIcons = EditorToolbarIconLoader.LoadDefaultToolbarIcons(contentManager, AppContext.BaseDirectory, renderer2D);
            RuntimeTexture titleBarIcon = EditorToolbarIconLoader.LoadTitleBarIcon(contentManager, AppContext.BaseDirectory, renderer2D);
            IReadOnlyList<IAssetImporterRegistration> importers = EditorHostImporterFactory.CreateDefault(renderer2D);
            ShaderBackendRegistry shaderBackendRegistry = CreateShaderBackendRegistry(bootstrap.PlatformCatalogService);
            editorSession = new EditorSession(
                core,
                projectPath,
                initialEditorPreferences,
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
                FolderDialog.OpenFolderDialog,
                shaderBackendRegistry,
                bootstrap.AvailablePlatformProviderResolver);

            editorSession.TitleChanged += SetWindowTitle;
            editorSession.CloseRequested += HandleEditorSessionCloseRequested;
            editorSession.PreferencesChanged += HandleEditorPreferencesChanged;
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
        /// Creates the shader backend registry required by the WinForms editor host.
        /// </summary>
        /// <param name="platformCatalogService">Dynamic platform catalog that can contribute additional shader backends from loaded platform builders.</param>
        /// <returns>Registry populated with the desktop shader backends supported by the editor app host.</returns>
        static ShaderBackendRegistry CreateShaderBackendRegistry(EditorPlatformCatalogService platformCatalogService) {
            if (platformCatalogService == null) {
                throw new ArgumentNullException(nameof(platformCatalogService));
            }

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            platformCatalogService.RegisterShaderBackends(shaderBackendRegistry);
            return shaderBackendRegistry;
        }

        /// <summary>
        /// Restores the custom maximized state so a native title-bar drag can continue from the current cursor position.
        /// </summary>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        public void PrepareForTitleBarDrag(Point cursorScreenPosition) {
            WindowStateController.PrepareForTitleBarDrag(this, cursorScreenPosition);
        }

        /// <summary>
        /// Requests a specific system timer resolution so short sleeps become accurate.
        /// </summary>
        /// <param name="uMilliseconds">Requested timer resolution in milliseconds.</param>
        /// <returns>Zero when the request succeeded.</returns>
        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        static extern uint TimeBeginPeriod(uint uMilliseconds);

        /// <summary>
        /// Releases a previously requested system timer resolution.
        /// </summary>
        /// <param name="uMilliseconds">Previously requested timer resolution in milliseconds.</param>
        /// <returns>Zero when the release succeeded.</returns>
        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        static extern uint TimeEndPeriod(uint uMilliseconds);

        /// <summary>
        /// Native display-mode payload used to query the active refresh rate.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        struct NativeDisplayMode {
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
        }

        /// <summary>
        /// Queries the active display mode for one display device.
        /// </summary>
        /// <param name="lpszDeviceName">Display device name, or null for the primary display.</param>
        /// <param name="iModeNum">Mode index; -1 requests the current mode.</param>
        /// <param name="lpDevMode">Receives the display mode.</param>
        /// <returns>True when the mode was retrieved.</returns>
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref NativeDisplayMode lpDevMode);

        /// <summary>
        /// Resolves the refresh rate of the display currently hosting this window, falling back to 120 Hz.
        /// </summary>
        /// <returns>Target editor frame rate in frames per second.</returns>
        double ResolveDisplayRefreshRate() {
            try {
                string deviceName = Screen.FromControl(this).DeviceName;
                NativeDisplayMode displayMode = new NativeDisplayMode {
                    dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<NativeDisplayMode>()
                };
                const int currentSettingsModeIndex = -1;
                if (EnumDisplaySettings(deviceName, currentSettingsModeIndex, ref displayMode)
                    && displayMode.dmDisplayFrequency >= 30
                    && displayMode.dmDisplayFrequency <= 1000) {
                    return displayMode.dmDisplayFrequency;
                }
            } catch {
            }

            return 120.0;
        }

        /// <summary>
        /// Drives the editor update and draw loop on a worker thread. Frame pacing subtracts the frame's own cost from
        /// the sleep and runs under a 1 ms system timer resolution; the previous fixed sleep was rounded up to the
        /// default 15.6 ms Windows timer granularity, capping the editor below 64 FPS regardless of hardware.
        /// </summary>
        private void RunEditorLoop() {
            double targetFrameSeconds = 1.0 / 120.0;
            try {
                targetFrameSeconds = 1.0 / (double)Invoke(() => ResolveDisplayRefreshRate());
            } catch {
            }

            sessionLogListener(new LogEntry(LogLevel.Info, $"Editor loop target: {1.0 / targetFrameSeconds:0.#} FPS ({targetFrameSeconds * 1000.0:0.##} ms budget).", 0d));

            TimeBeginPeriod(1);
            try {
                System.Diagnostics.Stopwatch loopStopwatch = System.Diagnostics.Stopwatch.StartNew();
                double telemetryWindowStartSeconds = 0.0;
                double telemetryFrameCostSumSeconds = 0.0;
                double telemetryFrameCostMaxSeconds = 0.0;
                double telemetrySleepSumSeconds = 0.0;
                int telemetryFrameCount = 0;
                const double telemetryWindowSeconds = 5.0;
                for (; ; ) {
                    if (closed) {
                        break;
                    }

                    double frameStartSeconds = loopStopwatch.Elapsed.TotalSeconds;
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

                    double frameCostSeconds = loopStopwatch.Elapsed.TotalSeconds - frameStartSeconds;
                    double remainingSeconds = targetFrameSeconds - frameCostSeconds;
                    // Always sleep at least 1 ms: the UI thread retrieves posted invoke callbacks ahead of input
                    // messages, so a loop that never sleeps starves mouse and scroll input entirely once a frame
                    // costs more than the frame budget.
                    int sleepMilliseconds = Math.Max(1, (int)(remainingSeconds * 1000.0));
                    double sleepStartSeconds = loopStopwatch.Elapsed.TotalSeconds;
                    Thread.Sleep(sleepMilliseconds);

                    telemetryFrameCostSumSeconds += frameCostSeconds;
                    telemetryFrameCostMaxSeconds = Math.Max(telemetryFrameCostMaxSeconds, frameCostSeconds);
                    telemetrySleepSumSeconds += loopStopwatch.Elapsed.TotalSeconds - sleepStartSeconds;
                    telemetryFrameCount++;
                    double telemetryElapsedSeconds = loopStopwatch.Elapsed.TotalSeconds - telemetryWindowStartSeconds;
                    if (telemetryElapsedSeconds >= telemetryWindowSeconds && telemetryFrameCount > 0) {
                        sessionLogListener(new LogEntry(
                            LogLevel.Info,
                            $"Editor loop: {telemetryFrameCount / telemetryElapsedSeconds:0.#} FPS, " +
                            $"avg frame {telemetryFrameCostSumSeconds * 1000.0 / telemetryFrameCount:0.##} ms, " +
                            $"max frame {telemetryFrameCostMaxSeconds * 1000.0:0.##} ms, " +
                            $"avg sleep {telemetrySleepSumSeconds * 1000.0 / telemetryFrameCount:0.##} ms.",
                            0d));
                        telemetryWindowStartSeconds = loopStopwatch.Elapsed.TotalSeconds;
                        telemetryFrameCostSumSeconds = 0.0;
                        telemetryFrameCostMaxSeconds = 0.0;
                        telemetrySleepSumSeconds = 0.0;
                        telemetryFrameCount = 0;
                    }
                }
            } finally {
                TimeEndPeriod(1);
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
        /// Resolves the runtime platform metadata injected into the editor-owned core instance.
        /// </summary>
        /// <param name="inputProjectPath">Project directory path or project file path.</param>
        /// <returns>Stable editor platform metadata built from the project's required engine version.</returns>
        PlatformInfo ResolveEditorPlatformInfo(string inputProjectPath) {
            string requiredEngineVersion = ResolveRequiredEngineVersion(inputProjectPath);
            return new PlatformInfo("editor", requiredEngineVersion);
        }

        /// <summary>
        /// Resolves the exact engine version required by the project opened in this host.
        /// </summary>
        /// <param name="inputProjectPath">Project directory path or project file path.</param>
        /// <returns>Exact required engine version declared by the canonical project document.</returns>
        string ResolveRequiredEngineVersion(string inputProjectPath) {
            if (string.IsNullOrWhiteSpace(inputProjectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            ProjectFilePathResolver resolver = new ProjectFilePathResolver();
            string canonicalProjectFilePath = resolver.Resolve(inputProjectPath);
            ProjectFileReader reader = new ProjectFileReader();
            ProjectFileReadResult readResult = reader.ReadAsync(canonicalProjectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded) {
                throw new InvalidOperationException(readResult.Errors[0].Message);
            }
            if (string.IsNullOrWhiteSpace(readResult.Document.RequiredEngineVersion)) {
                throw new InvalidOperationException("Project file must declare a required engine version.");
            }

            return readResult.Document.RequiredEngineVersion;
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
        FontAsset CreateUiFont(EditorUiMetrics metrics, RenderManager2D renderManager2D) {
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            return GDIFontProcessor.ImportFont(new Font("Consolas", metrics.UiFontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel), renderManager2D);
        }

        /// <summary>
        /// Creates the viewport snap-modifier font for the supplied scaled editor metrics.
        /// </summary>
        /// <param name="metrics">Scaled editor UI metrics resolved for the current host DPI state.</param>
        /// <returns>Font asset used for viewport snap-modifier labels.</returns>
        FontAsset CreateSnapModifierFont(EditorUiMetrics metrics, RenderManager2D renderManager2D) {
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            return GDIFontProcessor.ImportFont(new Font("Consolas", metrics.SnapModifierFontPixelSize, FontStyle.Bold, GraphicsUnit.Pixel), renderManager2D);
        }

        /// <summary>
        /// Stops the editor loop and disposes engine resources when the window closes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            if (sessionLogListener != null) {
                Logger.MessageLogged -= sessionLogListener;
                sessionLogListener = null;
            }
            editorSession.CloseRequested -= HandleEditorSessionCloseRequested;
            editorSession.PreferencesChanged -= HandleEditorPreferencesChanged;
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
        /// Resolves one persisted editor theme identifier and applies the matching palette to the live runtime theme manager.
        /// </summary>
        /// <param name="themeId">Stable persisted editor theme identifier.</param>
        void ApplyEditorTheme(string themeId) {
            EditorThemeDefinition theme = EditorThemeCatalog.FindById(themeId);
            if (theme == null) {
                throw new InvalidOperationException($"Unknown editor theme '{themeId}'.");
            }

            ThemeManager.SetTheme(theme.PaletteFactory());
        }

        /// <summary>
        /// Persists one newly confirmed editor-global preferences selection and reapplies the effective UI scale live.
        /// </summary>
        /// <param name="settings">Validated editor-global preferences confirmed by the user.</param>
        void HandleEditorPreferencesChanged(EditorPreferencesSettings settings) {
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
            FontAsset uiFont = CreateUiFont(metrics, editorSession.Core.RenderManager2D);
            FontAsset snapModifierFont = CreateSnapModifierFont(metrics, editorSession.Core.RenderManager2D);
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
            if (WindowResizeAdapter.ApplyBorderlessClientFrame(ref m)) {
                return;
            }

            if (WindowResizeAdapter.ApplyResizeHitTest(this, ref m, WindowResizeAdapter.DefaultResizeBorderThickness)) {
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WmExitSizeMove) {
                WindowStateController.CompleteTitleBarDrag(this, Cursor.Position);
            }
        }
    }
}


namespace helengine {
    /// <summary>
    /// Abstract base for 3D rendering backends.
    /// </summary>
    public abstract class RenderManager3D : IDisposable {
        /// <summary>
        /// Stores each registered host window's client size independently of the primary window.
        /// </summary>
        readonly Dictionary<IntPtr, int2> WindowSizes = new Dictionary<IntPtr, int2>();

        /// <summary>
        /// Records primary registration, including hosts with a zero-valued virtual handle.
        /// </summary>
        bool HasMainWindow;

        /// <summary>Retains each active handle's last usable client dimensions across minimization.</summary>
        readonly Dictionary<IntPtr, int2> WindowLayoutSizes = new Dictionary<IntPtr, int2>();

        /// <summary>Stores host-reported display density without changing rendering or pointer coordinates.</summary>
        readonly Dictionary<IntPtr, int> WindowDensityDpi = new Dictionary<IntPtr, int>();

        /// <summary>Retains density for named layout views, including temporarily closed windows.</summary>
        readonly Dictionary<string, int> NamedWindowDensityDpi = new Dictionary<string, int>();

        /// <summary>Retains named layout dimensions for the scene lifetime, including views that have closed.</summary>
        readonly Dictionary<string, int2> NamedWindowLayoutSizes = new Dictionary<string, int2>();

        /// <summary>Associates registered handles with their stable host view tags.</summary>
        readonly Dictionary<IntPtr, string> WindowTags = new Dictionary<IntPtr, string>();

        /// <summary>
        /// Core that owns this renderer. Renderers must use this explicit owner
        /// when traversing scene state so a later Core construction cannot
        /// redirect an existing session's draw path.
        /// </summary>
        public Core OwnerCore { get; internal set; }

        /// <summary>
        /// Gets the primary layout reference size, retaining its last usable dimensions while that window is minimized.
        /// </summary>
        public int2 MainWindowSize { get; private set; }

        /// <summary>
        /// Gets the first registered handle, whose size is the shared scene's layout reference.
        /// </summary>
        public IntPtr MainWindowHandle { get; private set; }

        /// <summary>
        /// Gets the window whose client coordinates are used by the current input frame.
        /// </summary>
        public IntPtr InputWindowHandle { get; private set; }

        /// <summary>Gets the registered view currently receiving draw commands; it starts at the primary view.</summary>
        public IntPtr RenderWindowHandle { get; private set; }

        /// <summary>
        /// Gets the input window's size, or the primary size before any host window is registered.
        /// </summary>
        public int2 InputWindowSize => HasMainWindow ? GetWindowSize(InputWindowHandle) : MainWindowSize;

        /// <summary>
        /// Gets the number of windows currently registered with this renderer.
        /// </summary>
        public int WindowCount => WindowSizes.Count;

        /// <summary>
        /// Event raised when a window is resized.
        /// </summary>
        public event Action<IntPtr, int, int> WindowResized;

        /// <summary>Signals a named view whose usable layout dimensions were registered or changed.</summary>
        public event Action<string> WindowLayoutChanged;

        /// <summary>
        /// Adds a window to the renderer and tracks its size.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        public virtual void AddWindow(IntPtr handle, int width, int height) {
            if (width < 0 || height < 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Window client dimensions cannot be negative.");
            }
            if (WindowSizes.ContainsKey(handle)) {
                throw new InvalidOperationException("The window is already registered with this renderer.");
            }
            WindowSizes.Add(handle, new int2(width, height));
            WindowLayoutSizes.Add(handle, new int2(width, height));
            WindowTags.Add(handle, HasMainWindow ? string.Empty : "main");
            if (!HasMainWindow) {
                MainWindowHandle = handle;
                InputWindowHandle = handle;
                RenderWindowHandle = handle;
                MainWindowSize = new int2(width, height);
                HasMainWindow = true;
                NamedWindowLayoutSizes["main"] = MainWindowSize;
            }
        }

        /// <summary>
        /// Retrieves a registered window's client size; unknown handles are configuration errors.
        /// </summary>
        /// <param name="handle">Registered host window handle.</param>
        /// <returns>The client width and height in pixels.</returns>
        public int2 GetWindowSize(IntPtr handle) {
            if (!WindowSizes.ContainsKey(handle)) {
                throw new ArgumentException("The window is not registered with this renderer.", nameof(handle));
            }
            return WindowSizes[handle];
        }

        /// <summary>Reports display density to viewport layout; physical render dimensions and input pixels remain independent.</summary>
        /// <param name="handle">Registered host window whose display density changed.</param>
        /// <param name="densityDpi">Positive host-reported dots per inch.</param>
        public void SetWindowDensityDpi(IntPtr handle, int densityDpi) {
            GetWindowSize(handle);
            if (densityDpi <= 0) {
                throw new ArgumentOutOfRangeException(nameof(densityDpi), "Display density must be positive.");
            }
            if (WindowDensityDpi.ContainsKey(handle) && WindowDensityDpi[handle] == densityDpi) {
                return;
            }
            WindowDensityDpi[handle] = densityDpi;
            string tag = WindowTags[handle];
            if (tag.Length > 0) {
                NamedWindowDensityDpi[tag.ToLowerInvariant()] = densityDpi;
                WindowLayoutChanged?.Invoke(tag);
            }
        }

        /// <summary>Returns the density reported for a named view; density scaling requires the host to supply this measurement.</summary>
        /// <param name="tag">Registered layout view, normally main or a camera's output tag.</param>
        /// <returns>Reported dots per inch.</returns>
        public int GetWindowDensityDpi(string tag) {
            RenderWindowTagRules.Validate(tag, false);
            string key = tag.ToLowerInvariant();
            if (!NamedWindowDensityDpi.ContainsKey(key)) {
                throw new InvalidOperationException("Display density has not been reported for layout view: " + tag);
            }
            return NamedWindowDensityDpi[key];
        }

        /// <summary>Assigns a unique host tag to a registered window without changing its size or layout ownership.</summary>
        /// <param name="handle">Previously registered host window.</param>
        /// <param name="tag">Nonempty host view identifier, compared without case.</param>
        public void SetWindowTag(IntPtr handle, string tag) {
            GetWindowSize(handle);
            RenderWindowTagRules.Validate(tag, false);
            if (handle == MainWindowHandle && !string.Equals(tag, "main", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("The primary output window retains the main tag.", nameof(tag));
            }
            foreach (KeyValuePair<IntPtr, string> entry in WindowTags) {
                if (entry.Key != handle && string.Equals(entry.Value, tag, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("The output window tag is already registered.");
                }
            }
            WindowTags[handle] = tag;
            NamedWindowLayoutSizes[tag.ToLowerInvariant()] = WindowLayoutSizes[handle];
            if (WindowDensityDpi.ContainsKey(handle)) {
                NamedWindowDensityDpi[tag.ToLowerInvariant()] = WindowDensityDpi[handle];
            } else {
                NamedWindowDensityDpi.Remove(tag.ToLowerInvariant());
            }
            WindowLayoutChanged?.Invoke(tag);
        }

        /// <summary>Returns the last usable dimensions of a declared named view, retaining them after minimize or closure.</summary>
        /// <param name="tag">A host view registered at least once in this renderer's lifetime.</param>
        /// <returns>The authoritative layout dimensions recorded for this view.</returns>
        public int2 GetWindowLayoutSize(string tag) {
            RenderWindowTagRules.Validate(tag, false);
            string key = tag.ToLowerInvariant();
            if (!NamedWindowLayoutSizes.ContainsKey(key)) {
                throw new InvalidOperationException("The layout output window has not been registered: " + tag);
            }
            return NamedWindowLayoutSizes[key];
        }

        /// <summary>Resolves a camera's layout reference without depending on the currently drawn or focused window.</summary>
        /// <param name="camera">The authored camera defining the UI output scope.</param>
        /// <returns>Named view dimensions, or the primary layout dimensions for shared cameras.</returns>
        public int2 GetCameraLayoutSize(ICamera camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            return camera.OutputWindowTag.Length == 0 ? MainWindowSize : GetWindowLayoutSize(camera.OutputWindowTag);
        }

        /// <summary>Selects a registered presentation view while leaving input and the primary layout reference unchanged.</summary>
        /// <param name="handle">Registered window receiving the next draw pass.</param>
        public void SelectRenderWindow(IntPtr handle) {
            GetWindowSize(handle);
            RenderWindowHandle = handle;
        }

        /// <summary>Matches a camera's authored target against a registered view; an empty camera target keeps legacy shared rendering.</summary>
        /// <param name="camera">Scene-owned camera whose target should be evaluated.</param>
        /// <param name="handle">Registered presentation or input window.</param>
        /// <returns>Whether this view displays the camera.</returns>
        public bool IsCameraVisibleInWindow(ICamera camera, IntPtr handle) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            GetWindowSize(handle);
            string target = camera.OutputWindowTag;
            return target.Length == 0 || string.Equals(target, WindowTags[handle], StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Selects the pointer coordinate space without changing the primary layout size.
        /// </summary>
        /// <param name="handle">Registered window that owns the current input frame.</param>
        public void SelectInputWindow(IntPtr handle) {
            GetWindowSize(handle);
            InputWindowHandle = handle;
        }

        /// <summary>
        /// Unregisters a secondary window and returns its input ownership to the primary window.
        /// The primary window owns host lifetime and is released by disposing the renderer.
        /// </summary>
        /// <param name="handle">Registered secondary window to remove.</param>
        public void RemoveWindow(IntPtr handle) {
            GetWindowSize(handle);
            if (handle == MainWindowHandle) {
                throw new InvalidOperationException("The primary window cannot be removed while the renderer is running.");
            }
            WindowSizes.Remove(handle);
            WindowLayoutSizes.Remove(handle);
            WindowDensityDpi.Remove(handle);
            WindowTags.Remove(handle);
            if (RenderWindowHandle == handle) {
                RenderWindowHandle = MainWindowHandle;
            }
            if (InputWindowHandle == handle) {
                InputWindowHandle = MainWindowHandle;
            }
        }

        /// <summary>
        /// Builds a runtime model from raw asset data.
        /// </summary>
        /// <param name="data">Raw model data.</param>
        /// <returns>Runtime model instance.</returns>
        public abstract RuntimeModel BuildModelFromRaw([NativeNoEscape] ModelAsset data);

        /// <summary>
        /// Builds a runtime model from one platform-owned cooked model payload.
        /// </summary>
        /// <param name="cookedAssetPath">Runtime asset path of the cooked model payload.</param>
        /// <param name="contentStreamSource">Stream source that owns cooked asset reads for the active runtime.</param>
        /// <returns>Runtime model instance.</returns>
        public virtual RuntimeModel BuildModelFromCooked(string cookedAssetPath, IContentStreamSource contentStreamSource) {
            if (string.IsNullOrWhiteSpace(cookedAssetPath)) {
                throw new ArgumentException("Cooked model asset path must be provided.", nameof(cookedAssetPath));
            }
            if (contentStreamSource == null) {
                throw new ArgumentNullException(nameof(contentStreamSource));
            }

            throw new NotSupportedException("This renderer does not support platform-owned cooked model creation.");
        }

        /// <summary>
        /// Creates a render target that can be assigned to a camera.
        /// </summary>
        /// <param name="width">Width of the render target in pixels.</param>
        /// <param name="height">Height of the render target in pixels.</param>
        /// <returns>Render target instance.</returns>
        public virtual RenderTarget CreateRenderTarget(int width, int height) {
            throw new NotSupportedException("This renderer does not support render target creation.");
        }

        /// <summary>
        /// Builds a runtime material from one raw material asset using the active renderer's asset-loading strategy.
        /// </summary>
        /// <param name="assetContentManager">Content manager that can load companion assets needed by the renderer.</param>
        /// <param name="materialAssetPath">Runtime asset path to the serialized material asset.</param>
        /// <returns>Runtime material instance.</returns>
        [NativeOwnedReturn]
        public virtual RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string materialAssetPath) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            throw new NotSupportedException("This renderer does not support material creation.");
        }

        /// <summary>
        /// Releases one runtime model previously created by this renderer and assumes ownership for final destruction timing.
        /// </summary>
        /// <param name="model">Runtime model that should release any renderer-owned resources and be disposed when safe.</param>
        public virtual void ReleaseModel(RuntimeModel model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            NativeOwnership.DisposeAndDelete(model);
        }

        /// <summary>
        /// Releases one runtime material previously created by this renderer and assumes ownership for final destruction timing.
        /// </summary>
        /// <param name="material">Runtime material that should release any renderer-owned resources and be disposed when safe.</param>
        public virtual void ReleaseMaterial(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            NativeOwnership.DisposeAndDelete(material);
        }

        /// <summary>
        /// Flushes any renderer-owned runtime asset releases that were deferred until the renderer reached a safe point.
        /// </summary>
        public virtual void FlushReleasedAssets() {
        }

#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
        /// <summary>
        /// Builds a runtime material from one builder-owned cooked material payload.
        /// </summary>
        /// <param name="materialAsset">Builder-owned cooked material payload.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromCooked(PlatformMaterialAsset materialAsset) {
            throw new NotSupportedException("This renderer does not support platform-owned cooked material creation.");
        }

        /// <summary>
        /// Builds a runtime material directly from one platform-owned cooked material payload path.
        /// </summary>
        /// <param name="cookedAssetPath">Runtime asset path of the cooked material payload.</param>
        /// <param name="contentStreamSource">Stream source that owns cooked asset reads for the active runtime.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromCooked(string cookedAssetPath, IContentStreamSource contentStreamSource) {
            if (string.IsNullOrWhiteSpace(cookedAssetPath)) {
                throw new ArgumentException("Cooked material asset path must be provided.", nameof(cookedAssetPath));
            }
            if (contentStreamSource == null) {
                throw new ArgumentNullException(nameof(contentStreamSource));
            }

            throw new NotSupportedException("This renderer does not support opaque platform-owned cooked material creation.");
        }
#endif

        /// <summary>
        /// Gets the backend capability profile published by this renderer.
        /// </summary>
        /// <returns>Renderer capability profile used by shared extraction and planning systems.</returns>
        public virtual RendererBackendCapabilityProfile GetCapabilityProfile() {
            return new RendererBackendCapabilityProfile(true, false, false, false, 0, 0);
        }

        /// <summary>
        /// Gets the draw-call count recorded by the most recent completed draw.
        /// </summary>
        public virtual int LastDrawCallCount => 0;

        /// <summary>
        /// Performs per-frame update for 3D rendering systems.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Executes the 3D render pass.
        /// </summary>
        public virtual void Draw() { }

        /// <summary>
        /// Releases resources owned by the render manager.
        /// </summary>
        public virtual void Dispose() {
            WindowSizes.Clear();
            WindowLayoutSizes.Clear();
            WindowDensityDpi.Clear();
            NamedWindowDensityDpi.Clear();
            NamedWindowLayoutSizes.Clear();
            WindowTags.Clear();
            RenderWindowHandle = (IntPtr)0;
            HasMainWindow = false;
            MainWindowSize = new int2(0, 0);
            MainWindowHandle = (IntPtr)0;
            InputWindowHandle = (IntPtr)0;
        }

        /// <summary>
        /// Triggers window resize handling; should be called by the host when resizing.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        public virtual void OnWindowResize(IntPtr handle, int newWidth, int newHeight) {
            GetWindowSize(handle);
            if (newWidth < 0 || newHeight < 0) {
                throw new ArgumentOutOfRangeException(nameof(newWidth), "Window client dimensions cannot be negative.");
            }
            WindowSizes[handle] = new int2(newWidth, newHeight);
            if (handle == MainWindowHandle && newWidth > 0 && newHeight > 0) {
                MainWindowSize = new int2(newWidth, newHeight);
            }

            if (newWidth > 0 && newHeight > 0) {
                WindowLayoutSizes[handle] = new int2(newWidth, newHeight);
                string tag = WindowTags[handle];
                if (tag.Length > 0) {
                    NamedWindowLayoutSizes[tag.ToLowerInvariant()] = WindowLayoutSizes[handle];
                    WindowLayoutChanged?.Invoke(tag);
                }
            }
            WindowResized?.Invoke(handle, newWidth, newHeight);
        }
    }
}

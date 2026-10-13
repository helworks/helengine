using System.Diagnostics;
using helengine.baseplatform.Player;
using helengine.directx11;
using helengine.editor;

namespace helengine.player {
    /// <summary>
    /// Runs one Play session: loads the platform adapter and script modules, boots a desktop core over the platform-cooked
    /// content, loads the requested scene and drives the frame loop until the player window is closed.
    /// </summary>
    public static class PlayerHost {
        /// <summary>Platform name the player reports to the core.</summary>
        public const string PlatformName = "player";

        /// <summary>Platform version the player reports to the core.</summary>
        public const string PlatformVersion = "1";

        /// <summary>Polling interval, in milliseconds, used while waiting for a debugger to attach before startup.</summary>
        public const int DebuggerPollMilliseconds = 100;

        /// <summary>
        /// Runs the player until its window is closed.
        /// </summary>
        /// <param name="options">Validated launch options from the editor.</param>
        public static void Run(PlayerLaunchOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.WaitForDebugger) {
                Console.WriteLine("[player] waiting for debugger, pid " + Environment.ProcessId);
                while (!Debugger.IsAttached) {
                    Thread.Sleep(DebuggerPollMilliseconds);
                }
            }

            IPlatformPlayerAdapter adapter = PlayerPlatformAdapterLoader.Load(options.AdapterAssemblyPath, options.AdapterTypeName);
            adapter.Initialize(options.ContentRootPath);
            PlayerScriptModuleLoader.LoadAll(options.ScriptModulesRootPath);

            EditorBuiltInShaderAssetLibrary shaders = null;
            PlayerRenderManager3D renderer3D = null;
            PlayerWindow window = null;
            RuntimeSceneCatalog sceneCatalog = null;
            Core core = null;
            try {
                ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
                shaderBackendRegistry.Register(new DirectX11ShaderBackend());
                shaders = new EditorBuiltInShaderAssetLibrary(shaderBackendRegistry);

                renderer3D = new PlayerRenderManager3D(adapter, shaders);
                window = new PlayerWindow(options.SceneId);
                window.Show();
                Application.DoEvents();
                InputBackendWindows input = new InputBackendWindows(window.Handle);

                IContentStreamSource source = new AliasedContentStreamSource(
                    new HostFileSystemContentStreamSource(options.ContentRootPath),
                    adapter.BuildContentAliases());
                sceneCatalog = new RuntimeSceneCatalog(new[] {
                    new RuntimeSceneCatalogEntry(options.SceneId, options.SceneCookedRelativePath)
                });
                CoreInitializationOptions coreOptions = new CoreInitializationOptions {
                    ContentStreamSource = source,
                    AssetContentManagerConfigurator = new PlayerAdapterContentManagerConfigurator(adapter),
                    SceneCatalog = sceneCatalog
                };
                core = new Core(coreOptions);
                core.Initialize(renderer3D, renderer3D.Render2D, input, new PlatformInfo(PlatformName, PlatformVersion), coreOptions);

                renderer3D.AddWindow(window.Handle, PlayerWindow.ClientWidth, PlayerWindow.ClientHeight);
                core.SceneManager.LoadScene(options.SceneId, SceneLoadMode.Single);

                RunFrameLoop(core, window);
            } finally {
                if (core != null) {
                    core.Dispose();
                } else if (renderer3D != null) {
                    renderer3D.Dispose();
                }
                if (sceneCatalog != null) {
                    sceneCatalog.Dispose();
                }
                if (shaders != null) {
                    shaders.Dispose();
                }
                if (window != null) {
                    window.Dispose();
                }
            }
        }

        /// <summary>
        /// Pumps window messages and advances the core one frame at a time, using the measured wall-clock delta,
        /// until the player window is closed.
        /// </summary>
        /// <param name="core">Initialized core with the scene loaded.</param>
        /// <param name="window">Player window whose closing ends the loop.</param>
        static void RunFrameLoop(Core core, PlayerWindow window) {
            Stopwatch frameStopwatch = Stopwatch.StartNew();
            double previousElapsedSeconds = 0d;
            bool isFirstFrame = true;
            Application.DoEvents();
            while (!window.IsClosed) {
                double currentElapsedSeconds = frameStopwatch.Elapsed.TotalSeconds;
                double frameDeltaSeconds = isFirstFrame
                    ? core.InitializationOptions.DefaultUpdateDeltaSeconds
                    : currentElapsedSeconds - previousElapsedSeconds;
                previousElapsedSeconds = currentElapsedSeconds;
                isFirstFrame = false;

                core.Update(frameDeltaSeconds);
                core.Draw();
                Application.DoEvents();
            }
        }
    }
}

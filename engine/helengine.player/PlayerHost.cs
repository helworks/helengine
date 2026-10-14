using System.Diagnostics;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using helengine.directx11;
using helengine.editor;

namespace helengine.player {
    /// <summary>
    /// Runs one Play session: loads the platform adapter and script modules, boots a desktop core over the platform-cooked
    /// content, loads the requested scene and drives the frame loop until the player window is closed.
    /// </summary>
    public static class PlayerHost {
        /// <summary>Polling interval, in milliseconds, used while waiting for a debugger to attach before startup.</summary>
        public const int DebuggerPollMilliseconds = 100;

        /// <summary>
        /// Runs the player until its window is closed. Engine log entries written while it runs are forwarded to the
        /// player's console streams, which the editor reads into its console.
        /// </summary>
        /// <param name="options">Validated launch options from the editor.</param>
        /// <param name="startupStopwatch">
        /// Stopwatch started at the top of <see cref="Program.Main"/>; the first-frame marker reports its elapsed time.
        /// </param>
        public static void Run(PlayerLaunchOptions options, Stopwatch startupStopwatch) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            } else if (startupStopwatch == null) {
                throw new ArgumentNullException(nameof(startupStopwatch));
            }

            PlayerConsoleLogForwarder logForwarder = new PlayerConsoleLogForwarder(Console.Out, Console.Error);
            EditorBuiltInShaderAssetLibrary shaders = null;
            PlayerRenderManager3D renderer3D = null;
            PlayerWindow window = null;
            RuntimeSceneCatalog sceneCatalog = null;
            Core core = null;
            PlatformSettingsConfiguration runtimeSettings = null;
            try {
                PlayerPlaySettings playSettings = options.PlaySettings;
                if (playSettings.WaitForDebugger) {
                    Console.WriteLine("[player] waiting for debugger, pid " + Environment.ProcessId);
                    while (!Debugger.IsAttached) {
                        Thread.Sleep(DebuggerPollMilliseconds);
                    }
                }

                IPlatformPlayerAdapter adapter = PlayerPlatformAdapterLoader.Load(options.AdapterAssemblyPath, options.AdapterTypeName);
                if (options.RuntimeSettingsPath.Length > 0) { runtimeSettings = PlatformSettingsSerializer.ReadBytes(File.ReadAllBytes(options.RuntimeSettingsPath)); }
                adapter.Initialize(options.ContentRootPath);
                PlayerRenderSize renderSize = adapter.ApplyPlaySettings(playSettings);
                PlayerScriptModuleLoader.LoadAll(options.ScriptModulesRootPath);

                ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
                shaderBackendRegistry.Register(new DirectX11ShaderBackend());
                shaders = new EditorBuiltInShaderAssetLibrary(shaderBackendRegistry);

                renderer3D = new PlayerRenderManager3D(adapter, shaders);
                renderer3D.ShadowsEnabled = playSettings.ShadowsEnabled;
                // Vsync only prevents tearing; the frame limiter holds the loop to the platform's rate. An unlimited Play
                // turns vsync off too, so it really runs unpaced.
                renderer3D.PresentSyncInterval = playSettings.FrameRateUnlimited ? 0 : 1;
                window = new PlayerWindow(options.PlatformName, options.SceneId, renderSize.Width, renderSize.Height);
                PlayerScreenArea workArea = new PlayerMonitorLocator(new WinFormsPlayerMonitorQuery()).ResolveWorkArea(playSettings.WindowMonitor, playSettings.EditorProcessId);
                PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(
                    renderSize,
                    playSettings.WindowScale,
                    workArea,
                    window.Width - window.ClientSize.Width,
                    window.Height - window.ClientSize.Height);
                window.ClientSize = new Size(layout.ClientWidth, layout.ClientHeight);
                window.Location = new Point(layout.Left, layout.Top);
                Console.WriteLine("[player] render " + renderSize.Width + "x" + renderSize.Height + " at " + layout.Multiplier + "x");
                if (layout.ExceedsWorkArea) {
                    Console.WriteLine("[player] window-scale " + layout.Multiplier + "x is larger than the monitor work area " + workArea.Width + "x" + workArea.Height + "; the window opens at the work-area origin");
                }
                window.Show();
                Application.DoEvents();
                new PlayerWindowActivator(new Win32PlayerForegroundWindow(), Console.Out).Activate(window.Handle);
                InputBackendWindows desktopInput = new InputBackendWindows(window.Handle);
                PlayerPlatformInputBackend input = CreatePlatformInput(desktopInput, options.InputDevices);

                IContentStreamSource source = new AliasedContentStreamSource(
                    new HostFileSystemContentStreamSource(options.ContentRootPath),
                    adapter.BuildContentAliases());
                sceneCatalog = new RuntimeSceneCatalog(new[] {
                    new RuntimeSceneCatalogEntry(options.SceneId, options.SceneCookedRelativePath)
                });
                CoreInitializationOptions coreOptions = new CoreInitializationOptions {
                    ContentStreamSource = source,
                    AssetContentManagerConfigurator = new PlayerAdapterContentManagerConfigurator(adapter),
                    PackagedAssetPathPolicy = new ContentRootPackagedAssetPathPolicy(),
                    SceneCatalog = sceneCatalog,
                    PlatformSettingsConfiguration = runtimeSettings,
                    PlatformSettingsBackend = adapter as IPlatformSettingsBackend
                };
                core = new Core(coreOptions);
                core.Initialize(renderer3D, renderer3D.Render2D, input, options.CreateRuntimePlatformInfo(), coreOptions);

                renderer3D.AddWindow(window.Handle, layout.ClientWidth, layout.ClientHeight);
                core.SceneManager.LoadScene(options.SceneId, SceneLoadMode.Single);

                RunFrameLoop(core, window, playSettings, startupStopwatch);
            } finally {
                if (core != null) {
                    core.Dispose();
                } else if (renderer3D != null) {
                    renderer3D.Dispose();
                }
                if (sceneCatalog != null) {
                    sceneCatalog.Dispose();
                }
                runtimeSettings?.Dispose();
                if (shaders != null) {
                    shaders.Dispose();
                }
                if (window != null) {
                    window.Dispose();
                }
                logForwarder.Dispose();
            }
        }

        /// <summary>
        /// Wraps the desktop input backend so the game sees only the platform's declared devices, and writes the active
        /// devices, the desktop controller availability and the keyboard pad layout to stdout, where the editor shows
        /// them as <c>[player]</c> lines (the editor adds that prefix).
        /// </summary>
        /// <param name="desktopInput">Desktop backend bound to the player window.</param>
        /// <param name="devices">Devices the selected platform declares.</param>
        /// <returns>The filtering backend handed to the core.</returns>
        static PlayerPlatformInputBackend CreatePlatformInput(InputBackendWindows desktopInput, PlayerInputDevices devices) {
            XInputPlayerGamepadReader controller = new XInputPlayerGamepadReader();
            PlayerKeyboardGamepadMap keyboardGamepad = PlayerKeyboardGamepadMap.CreateDefault();
            Console.WriteLine("input devices: " + PlayerInputDevicesText.Format(devices));
            if ((devices & PlayerInputDevices.Gamepad) != PlayerInputDevices.None) {
                Console.WriteLine(controller.IsAvailable
                    ? "gamepad: first connected XInput controller merged with the keyboard pad"
                    : "gamepad: " + XInputPlayerGamepadReader.XInputLibraryName + " not available, keyboard pad only");
                IReadOnlyList<string> bindings = keyboardGamepad.Describe();
                for (int i = 0; i < bindings.Count; i++) {
                    Console.WriteLine("keyboard pad: " + bindings[i]);
                }
            }

            return new PlayerPlatformInputBackend(desktopInput, desktopInput, devices, controller, keyboardGamepad);
        }

        /// <summary>
        /// Pumps window messages and advances the core one frame at a time, using the measured wall-clock delta, until
        /// the player window is closed. The <c>frame-rate</c> Play setting picks the frame limiter: a paced Play holds each
        /// frame to the target frame rate, so the measured delta handed to <c>Core.Update</c> is the real time between
        /// frames at that rate; an unlimited Play never waits. Right after the first frame is presented, the
        /// machine-readable <see cref="EditorPlayFirstFrameMarker"/> line is written to stdout with the milliseconds
        /// elapsed on <paramref name="startupStopwatch"/>, so the editor can finish its Play startup summary.
        /// </summary>
        /// <param name="core">Initialized core with the scene loaded.</param>
        /// <param name="window">Player window whose closing ends the loop.</param>
        /// <param name="playSettings">Resolved Play settings; their frame rate selects the frame limiter.</param>
        /// <param name="startupStopwatch">Stopwatch started at the top of <see cref="Program.Main"/>.</param>
        static void RunFrameLoop(Core core, PlayerWindow window, PlayerPlaySettings playSettings, Stopwatch startupStopwatch) {
            Console.WriteLine("[player] frame loop target: " + (playSettings.FrameRateUnlimited ? "unlimited" : playSettings.TargetFrameRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " FPS"));
            using StopwatchPlayerFrameClock clock = new StopwatchPlayerFrameClock();
            IPlayerFrameLimiter limiter = PlayerFrameLimiterFactory.Create(playSettings, clock);
            double previousElapsedSeconds = 0d;
            bool isFirstFrame = true;
            Application.DoEvents();
            limiter.Start();
            while (!window.IsClosed) {
                double currentElapsedSeconds = clock.ElapsedSeconds;
                double frameDeltaSeconds = isFirstFrame
                    ? core.InitializationOptions.DefaultUpdateDeltaSeconds
                    : currentElapsedSeconds - previousElapsedSeconds;
                previousElapsedSeconds = currentElapsedSeconds;

                core.Update(frameDeltaSeconds);
                core.Draw();
                if (isFirstFrame) {
                    isFirstFrame = false;
                    Console.WriteLine(EditorPlayFirstFrameMarker.Format(startupStopwatch.ElapsedMilliseconds));
                }
                Application.DoEvents();
                limiter.WaitForNextFrame();
            }
        }
    }
}

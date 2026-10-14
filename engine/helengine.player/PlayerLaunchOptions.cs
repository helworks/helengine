using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Holds the validated command line the editor passes to the Play player:
    /// <c>--content-root &lt;dir&gt; --scene-id &lt;id&gt; --platform-name &lt;display name&gt; --scene-path &lt;cooked/relative.hasset&gt;
    /// --adapter-assembly &lt;dll&gt; --adapter-type &lt;full type name&gt; --script-modules &lt;dir&gt;
    /// --input-devices &lt;comma list&gt; --runtime-platform-name &lt;name&gt;
    /// --runtime-platform-version &lt;version&gt; --play-settings &lt;file&gt; [--runtime-settings &lt;file&gt;]</c>.
    /// Every value option is required; unknown, repeated or value-less options are rejected so a
    /// mismatch between the editor and the player fails loudly instead of being ignored.
    /// </summary>
    public sealed class PlayerLaunchOptions {
        /// <summary>Option naming the directory that holds the cooked platform content.</summary>
        public const string ContentRootOption = "--content-root";

        /// <summary>Option naming the stable id of the scene to load first.</summary>
        public const string SceneIdOption = "--scene-id";

        /// <summary>
        /// Option naming the display name of the platform being played (for example <c>PlayStation 2</c>), shown in the
        /// player window title. The value may contain spaces; it is passed as one argument.
        /// </summary>
        public const string PlatformNameOption = "--platform-name";

        /// <summary>Option naming the cooked scene path relative to the content root.</summary>
        public const string ScenePathOption = "--scene-path";

        /// <summary>Option naming the platform builder assembly that contains the player adapter.</summary>
        public const string AdapterAssemblyOption = "--adapter-assembly";

        /// <summary>Option naming the full type name of the player adapter inside the adapter assembly.</summary>
        public const string AdapterTypeOption = "--adapter-type";

        /// <summary>Option naming the root folder that holds one sub-folder per compiled script module.</summary>
        public const string ScriptModulesOption = "--script-modules";

        /// <summary>
        /// Option naming the input devices the platform's hardware offers, as the lowercase comma list of
        /// <see cref="PlayerInputDevicesText"/> (for example <c>gamepad</c>).
        /// </summary>
        public const string InputDevicesOption = "--input-devices";

        /// <summary>
        /// Option naming the platform name the player reports to the core, the same name the platform's native runtime
        /// reports (for example <c>ps2</c>), so game code that branches on <c>PlatformInfo.Name</c> behaves as on the console.
        /// </summary>
        public const string RuntimePlatformNameOption = "--runtime-platform-name";

        /// <summary>
        /// Option naming the platform version the player reports to the core, the same version the platform's native
        /// runtime reports (for example <c>1.0.0</c>).
        /// </summary>
        public const string RuntimePlatformVersionOption = "--runtime-platform-version";

        /// <summary>Optional portable catalog containing user-authored presets for the selected platform.</summary>
        public const string RuntimeSettingsOption = "--runtime-settings";

        /// <summary>Option naming the resolved Play settings file the editor wrote for this Play.</summary>
        public const string PlaySettingsOption = "--play-settings";

        /// <summary>
        /// Creates one options instance from already validated values; use <see cref="Parse"/> to build one from a command line.
        /// </summary>
        /// <param name="contentRootPath">Directory that holds the cooked platform content.</param>
        /// <param name="sceneId">Stable id of the scene to load first.</param>
        /// <param name="platformName">Display name of the platform being played.</param>
        /// <param name="sceneCookedRelativePath">Cooked scene path relative to the content root.</param>
        /// <param name="adapterAssemblyPath">Path to the platform builder assembly containing the adapter.</param>
        /// <param name="adapterTypeName">Full type name of the player adapter.</param>
        /// <param name="scriptModulesRootPath">Root folder holding compiled script modules.</param>
        /// <param name="inputDevices">Non-empty set of input devices the player forwards to the game.</param>
        /// <param name="runtimePlatformName">Platform name the player reports to the core.</param>
        /// <param name="runtimePlatformVersion">Platform version the player reports to the core.</param>
        /// <param name="playSettings">Resolved Play settings read from the <c>--play-settings</c> file.</param>
        /// <param name="runtimeSettingsPath">Portable runtime settings catalog path, or empty when none was supplied.</param>
        PlayerLaunchOptions(
            string contentRootPath,
            string sceneId,
            string platformName,
            string sceneCookedRelativePath,
            string adapterAssemblyPath,
            string adapterTypeName,
            string scriptModulesRootPath,
            PlayerInputDevices inputDevices,
            string runtimePlatformName,
            string runtimePlatformVersion,
            PlayerPlaySettings playSettings,
            string runtimeSettingsPath) {
            ContentRootPath = contentRootPath;
            SceneId = sceneId;
            PlatformName = platformName;
            SceneCookedRelativePath = sceneCookedRelativePath;
            AdapterAssemblyPath = adapterAssemblyPath;
            AdapterTypeName = adapterTypeName;
            ScriptModulesRootPath = scriptModulesRootPath;
            InputDevices = inputDevices;
            RuntimePlatformName = runtimePlatformName;
            RuntimePlatformVersion = runtimePlatformVersion;
            PlaySettings = playSettings;
            RuntimeSettingsPath = runtimeSettingsPath;
        }

        /// <summary>Gets the directory that holds the cooked platform content the player reads.</summary>
        public string ContentRootPath { get; }

        /// <summary>Gets the stable id of the scene the player loads at startup.</summary>
        public string SceneId { get; }

        /// <summary>Gets the display name of the platform being played, shown in the player window title.</summary>
        public string PlatformName { get; }

        /// <summary>Gets the cooked scene path, relative to <see cref="ContentRootPath"/>.</summary>
        public string SceneCookedRelativePath { get; }

        /// <summary>Gets the path to the platform builder assembly that contains the player adapter.</summary>
        public string AdapterAssemblyPath { get; }

        /// <summary>Gets the full type name of the player adapter inside <see cref="AdapterAssemblyPath"/>.</summary>
        public string AdapterTypeName { get; }

        /// <summary>Gets the root folder laid out as <c>&lt;root&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</summary>
        public string ScriptModulesRootPath { get; }

        /// <summary>
        /// Gets the input devices the selected platform's hardware offers; every other desktop device is reported idle to
        /// the game.
        /// </summary>
        public PlayerInputDevices InputDevices { get; }

        /// <summary>
        /// Gets the platform name the player reports to the core, matching the name the selected platform's native runtime
        /// reports.
        /// </summary>
        public string RuntimePlatformName { get; }

        /// <summary>
        /// Gets the platform version the player reports to the core, matching the version the selected platform's native
        /// runtime reports.
        /// </summary>
        public string RuntimePlatformVersion { get; }

        /// <summary>Gets the resolved Play settings of this Play (window, debugger wait, shadows, pacing, platform values).</summary>
        public PlayerPlaySettings PlaySettings { get; }

        /// <summary>Gets the optional portable catalog path supplied by the editor.</summary>
        public string RuntimeSettingsPath { get; }

        /// <summary>
        /// Creates the platform identity the player hands to the core: the runtime platform name and version the editor
        /// took from the selected platform's Play capability, so the core sees the console's identity.
        /// </summary>
        /// <returns>Platform identity reported to the core.</returns>
        public PlatformInfo CreateRuntimePlatformInfo() {
            return new PlatformInfo(RuntimePlatformName, RuntimePlatformVersion);
        }

        /// <summary>
        /// Parses the player command line. Each value option consumes the argument that follows it.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <returns>Validated launch options.</returns>
        /// <exception cref="ArgumentException">An option is unknown, repeated, missing its value, or a required option is absent.</exception>
        /// <exception cref="FileNotFoundException">The <c>--play-settings</c> file does not exist.</exception>
        /// <exception cref="InvalidOperationException">The <c>--play-settings</c> file is not a valid current-format Play settings file.</exception>
        public static PlayerLaunchOptions Parse(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            string contentRootPath = null;
            string sceneId = null;
            string platformName = null;
            string sceneCookedRelativePath = null;
            string adapterAssemblyPath = null;
            string adapterTypeName = null;
            string scriptModulesRootPath = null;
            string inputDevicesText = null;
            string runtimePlatformName = null;
            string runtimePlatformVersion = null;
            string runtimeSettingsPath = null;
            string playSettingsPath = null;

            for (int i = 0; i < args.Length; i++) {
                string option = args[i];
                if (string.Equals(option, ContentRootOption, StringComparison.Ordinal)) {
                    contentRootPath = ReadValue(args, i, contentRootPath);
                } else if (string.Equals(option, SceneIdOption, StringComparison.Ordinal)) {
                    sceneId = ReadValue(args, i, sceneId);
                } else if (string.Equals(option, PlatformNameOption, StringComparison.Ordinal)) {
                    platformName = ReadValue(args, i, platformName);
                } else if (string.Equals(option, ScenePathOption, StringComparison.Ordinal)) {
                    sceneCookedRelativePath = ReadValue(args, i, sceneCookedRelativePath);
                } else if (string.Equals(option, AdapterAssemblyOption, StringComparison.Ordinal)) {
                    adapterAssemblyPath = ReadValue(args, i, adapterAssemblyPath);
                } else if (string.Equals(option, AdapterTypeOption, StringComparison.Ordinal)) {
                    adapterTypeName = ReadValue(args, i, adapterTypeName);
                } else if (string.Equals(option, ScriptModulesOption, StringComparison.Ordinal)) {
                    scriptModulesRootPath = ReadValue(args, i, scriptModulesRootPath);
                } else if (string.Equals(option, InputDevicesOption, StringComparison.Ordinal)) {
                    inputDevicesText = ReadValue(args, i, inputDevicesText);
                } else if (string.Equals(option, RuntimePlatformNameOption, StringComparison.Ordinal)) {
                    runtimePlatformName = ReadValue(args, i, runtimePlatformName);
                } else if (string.Equals(option, RuntimePlatformVersionOption, StringComparison.Ordinal)) {
                    runtimePlatformVersion = ReadValue(args, i, runtimePlatformVersion);
                } else if (string.Equals(option, RuntimeSettingsOption, StringComparison.Ordinal)) {
                    runtimeSettingsPath = ReadValue(args, i, runtimeSettingsPath);
                } else if (string.Equals(option, PlaySettingsOption, StringComparison.Ordinal)) {
                    playSettingsPath = ReadValue(args, i, playSettingsPath);
                } else {
                    throw new ArgumentException($"Unknown player option '{option}'.", nameof(args));
                }

                i++;
            }

            RequireOption(ContentRootOption, contentRootPath);
            RequireOption(SceneIdOption, sceneId);
            RequireOption(PlatformNameOption, platformName);
            RequireOption(ScenePathOption, sceneCookedRelativePath);
            RequireOption(AdapterAssemblyOption, adapterAssemblyPath);
            RequireOption(AdapterTypeOption, adapterTypeName);
            RequireOption(ScriptModulesOption, scriptModulesRootPath);
            RequireOption(InputDevicesOption, inputDevicesText);
            RequireOption(RuntimePlatformNameOption, runtimePlatformName);
            RequireOption(RuntimePlatformVersionOption, runtimePlatformVersion);
            RequireOption(PlaySettingsOption, playSettingsPath);
            PlayerInputDevices inputDevices = PlayerInputDevicesText.Parse(inputDevicesText, InputDevicesOption);

            return new PlayerLaunchOptions(
                contentRootPath,
                sceneId,
                platformName,
                sceneCookedRelativePath,
                adapterAssemblyPath,
                adapterTypeName,
                scriptModulesRootPath,
                inputDevices,
                runtimePlatformName,
                runtimePlatformVersion,
                PlayerPlaySettingsFile.Read(playSettingsPath),
                runtimeSettingsPath ?? string.Empty);
        }

        /// <summary>
        /// Reads the value that follows the value option at <paramref name="optionIndex"/>.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <param name="optionIndex">Index of the option whose value is read.</param>
        /// <param name="currentValue">Value already parsed for this option, or null when it has not been seen yet.</param>
        /// <returns>The non-empty option value.</returns>
        /// <exception cref="ArgumentException">The option is repeated, or its value is missing, empty, or another option.</exception>
        static string ReadValue(string[] args, int optionIndex, string currentValue) {
            string option = args[optionIndex];
            if (currentValue != null) {
                throw new ArgumentException($"Option '{option}' was specified more than once.", nameof(args));
            }

            int valueIndex = optionIndex + 1;
            if (valueIndex >= args.Length) {
                throw new ArgumentException($"Option '{option}' requires a value.", nameof(args));
            }

            string value = args[valueIndex];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)) {
                throw new ArgumentException($"Option '{option}' requires a value but received '{value}'.", nameof(args));
            }

            return value;
        }

        /// <summary>
        /// Ensures one required value option was supplied.
        /// </summary>
        /// <param name="option">Option name reported when the value is missing.</param>
        /// <param name="value">Parsed value, or null when the option was absent.</param>
        /// <exception cref="ArgumentException">The option was absent.</exception>
        static void RequireOption(string option, string value) {
            if (value == null) {
                throw new ArgumentException($"Required player option '{option}' is missing.", option);
            }
        }
    }
}

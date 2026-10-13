namespace helengine.player {
    /// <summary>
    /// Holds the validated command line the editor passes to the Play player:
    /// <c>--content-root &lt;dir&gt; --scene-id &lt;id&gt; --scene-path &lt;cooked/relative.hasset&gt;
    /// --adapter-assembly &lt;dll&gt; --adapter-type &lt;full type name&gt; --script-modules &lt;dir&gt;
    /// --target-fps &lt;frames per second&gt; [--wait-for-debugger]</c>.
    /// Every value option is required; unknown, repeated or value-less options are rejected so a
    /// mismatch between the editor and the player fails loudly instead of being ignored.
    /// </summary>
    public sealed class PlayerLaunchOptions {
        /// <summary>Option naming the directory that holds the cooked platform content.</summary>
        public const string ContentRootOption = "--content-root";

        /// <summary>Option naming the stable id of the scene to load first.</summary>
        public const string SceneIdOption = "--scene-id";

        /// <summary>Option naming the cooked scene path relative to the content root.</summary>
        public const string ScenePathOption = "--scene-path";

        /// <summary>Option naming the platform builder assembly that contains the player adapter.</summary>
        public const string AdapterAssemblyOption = "--adapter-assembly";

        /// <summary>Option naming the full type name of the player adapter inside the adapter assembly.</summary>
        public const string AdapterTypeOption = "--adapter-type";

        /// <summary>Option naming the root folder that holds one sub-folder per compiled script module.</summary>
        public const string ScriptModulesOption = "--script-modules";

        /// <summary>Option naming the frames per second the player paces its frame loop to, written with the invariant culture.</summary>
        public const string TargetFpsOption = "--target-fps";

        /// <summary>Switch that makes the player block at startup until a debugger attaches.</summary>
        public const string WaitForDebuggerOption = "--wait-for-debugger";

        /// <summary>
        /// Creates one options instance from already validated values; use <see cref="Parse"/> to build one from a command line.
        /// </summary>
        /// <param name="contentRootPath">Directory that holds the cooked platform content.</param>
        /// <param name="sceneId">Stable id of the scene to load first.</param>
        /// <param name="sceneCookedRelativePath">Cooked scene path relative to the content root.</param>
        /// <param name="adapterAssemblyPath">Path to the platform builder assembly containing the adapter.</param>
        /// <param name="adapterTypeName">Full type name of the player adapter.</param>
        /// <param name="scriptModulesRootPath">Root folder holding compiled script modules.</param>
        /// <param name="targetFrameRate">Positive frames per second the player paces its frame loop to.</param>
        /// <param name="waitForDebugger">Whether the player waits for a debugger before starting.</param>
        PlayerLaunchOptions(
            string contentRootPath,
            string sceneId,
            string sceneCookedRelativePath,
            string adapterAssemblyPath,
            string adapterTypeName,
            string scriptModulesRootPath,
            double targetFrameRate,
            bool waitForDebugger) {
            ContentRootPath = contentRootPath;
            SceneId = sceneId;
            SceneCookedRelativePath = sceneCookedRelativePath;
            AdapterAssemblyPath = adapterAssemblyPath;
            AdapterTypeName = adapterTypeName;
            ScriptModulesRootPath = scriptModulesRootPath;
            TargetFrameRate = targetFrameRate;
            WaitForDebugger = waitForDebugger;
        }

        /// <summary>Gets the directory that holds the cooked platform content the player reads.</summary>
        public string ContentRootPath { get; }

        /// <summary>Gets the stable id of the scene the player loads at startup.</summary>
        public string SceneId { get; }

        /// <summary>Gets the cooked scene path, relative to <see cref="ContentRootPath"/>.</summary>
        public string SceneCookedRelativePath { get; }

        /// <summary>Gets the path to the platform builder assembly that contains the player adapter.</summary>
        public string AdapterAssemblyPath { get; }

        /// <summary>Gets the full type name of the player adapter inside <see cref="AdapterAssemblyPath"/>.</summary>
        public string AdapterTypeName { get; }

        /// <summary>Gets the root folder laid out as <c>&lt;root&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</summary>
        public string ScriptModulesRootPath { get; }

        /// <summary>Gets the frames per second the player paces its frame loop to (the selected platform's frame rate).</summary>
        public double TargetFrameRate { get; }

        /// <summary>Gets whether the player blocks at startup until a debugger is attached.</summary>
        public bool WaitForDebugger { get; }

        /// <summary>
        /// Parses the player command line. Each value option consumes the argument that follows it.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <returns>Validated launch options.</returns>
        /// <exception cref="ArgumentException">An option is unknown, repeated, missing its value, or a required option is absent.</exception>
        public static PlayerLaunchOptions Parse(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            string contentRootPath = null;
            string sceneId = null;
            string sceneCookedRelativePath = null;
            string adapterAssemblyPath = null;
            string adapterTypeName = null;
            string scriptModulesRootPath = null;
            string targetFrameRateText = null;
            bool waitForDebugger = false;

            for (int i = 0; i < args.Length; i++) {
                string option = args[i];
                if (string.Equals(option, WaitForDebuggerOption, StringComparison.Ordinal)) {
                    if (waitForDebugger) {
                        throw new ArgumentException($"Option '{option}' was specified more than once.", nameof(args));
                    }
                    waitForDebugger = true;
                    continue;
                }

                if (string.Equals(option, ContentRootOption, StringComparison.Ordinal)) {
                    contentRootPath = ReadValue(args, i, contentRootPath);
                } else if (string.Equals(option, SceneIdOption, StringComparison.Ordinal)) {
                    sceneId = ReadValue(args, i, sceneId);
                } else if (string.Equals(option, ScenePathOption, StringComparison.Ordinal)) {
                    sceneCookedRelativePath = ReadValue(args, i, sceneCookedRelativePath);
                } else if (string.Equals(option, AdapterAssemblyOption, StringComparison.Ordinal)) {
                    adapterAssemblyPath = ReadValue(args, i, adapterAssemblyPath);
                } else if (string.Equals(option, AdapterTypeOption, StringComparison.Ordinal)) {
                    adapterTypeName = ReadValue(args, i, adapterTypeName);
                } else if (string.Equals(option, ScriptModulesOption, StringComparison.Ordinal)) {
                    scriptModulesRootPath = ReadValue(args, i, scriptModulesRootPath);
                } else if (string.Equals(option, TargetFpsOption, StringComparison.Ordinal)) {
                    targetFrameRateText = ReadValue(args, i, targetFrameRateText);
                } else {
                    throw new ArgumentException($"Unknown player option '{option}'.", nameof(args));
                }

                i++;
            }

            RequireOption(ContentRootOption, contentRootPath);
            RequireOption(SceneIdOption, sceneId);
            RequireOption(ScenePathOption, sceneCookedRelativePath);
            RequireOption(AdapterAssemblyOption, adapterAssemblyPath);
            RequireOption(AdapterTypeOption, adapterTypeName);
            RequireOption(ScriptModulesOption, scriptModulesRootPath);
            RequireOption(TargetFpsOption, targetFrameRateText);
            double targetFrameRate = ParseTargetFrameRate(targetFrameRateText);

            return new PlayerLaunchOptions(
                contentRootPath,
                sceneId,
                sceneCookedRelativePath,
                adapterAssemblyPath,
                adapterTypeName,
                scriptModulesRootPath,
                targetFrameRate,
                waitForDebugger);
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
        /// Parses the <c>--target-fps</c> value as an invariant-culture number that must be positive and finite.
        /// </summary>
        /// <param name="text">Raw option value.</param>
        /// <returns>Frames per second the player paces its frame loop to.</returns>
        /// <exception cref="ArgumentException">The value is not an invariant number, or is not positive and finite.</exception>
        static double ParseTargetFrameRate(string text) {
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double targetFrameRate)) {
                throw new ArgumentException($"Option '{TargetFpsOption}' requires an invariant-culture number but received '{text}'.", TargetFpsOption);
            } else if (!(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentException($"Option '{TargetFpsOption}' requires a positive, finite frame rate but received '{text}'.", TargetFpsOption);
            }

            return targetFrameRate;
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

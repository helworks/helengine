namespace helengine.baseplatform.Definitions {

    /// <summary>
    /// Describes whether one platform supports editor Play through the desktop C# player and how the player finds
    /// the platform adapter and the cooked content.
    /// </summary>
    public sealed class PlatformPlayerCapability {
        /// <summary>
        /// Initializes one platform Play capability description.
        /// </summary>
        /// <param name="supportsPlay">Whether the platform supports editor Play.</param>
        /// <param name="adapterTypeName">Assembly-qualified or full type name of the platform's <c>IPlatformPlayerAdapter</c> implementation.</param>
        /// <param name="contentRootRelativePath">Cooked content root, relative to the builder working root (PS2: <c>ps2-staging</c>).</param>
        /// <param name="targetFrameRate">
        /// Frames per second the desktop player paces its frame loop to, matching the frame rate the platform's native
        /// host runs at (PS2: 60). Must be positive and finite when Play is supported.
        /// </param>
        /// <param name="inputDevices">
        /// Input devices the platform's real hardware offers (PS2: <see cref="PlayerInputDevices.Gamepad"/> only). The player
        /// forwards only these devices to the game. Must name at least one known device when Play is supported.
        /// </param>
        /// <param name="runtimePlatformName">
        /// Platform name the platform's native runtime reports to the core through <c>PlatformInfo.Name</c> (PS2: <c>ps2</c>).
        /// The desktop player reports the same name, so game code that branches on the platform behaves as on the console.
        /// Required when Play is supported.
        /// </param>
        /// <param name="runtimePlatformVersion">
        /// Platform version the platform's native runtime reports to the core through <c>PlatformInfo.Version</c>
        /// (PS2: <c>1.0.0</c>). The desktop player reports the same version. Required when Play is supported.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when Play is supported but the adapter type name, content root, runtime platform name or runtime platform
        /// version is blank, or no input device is declared.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when Play is supported but the target frame rate is not a positive, finite number, or the device set contains
        /// bits that name no known device.
        /// </exception>
        public PlatformPlayerCapability(bool supportsPlay, string adapterTypeName, string contentRootRelativePath, double targetFrameRate, PlayerInputDevices inputDevices, string runtimePlatformName, string runtimePlatformVersion) {
            if (supportsPlay && string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("A platform that supports Play must name its player adapter type.", nameof(adapterTypeName));
            } else if (supportsPlay && string.IsNullOrWhiteSpace(contentRootRelativePath)) {
                throw new ArgumentException("A platform that supports Play must name its cooked content root.", nameof(contentRootRelativePath));
            } else if (supportsPlay && !(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(
                    nameof(targetFrameRate),
                    targetFrameRate,
                    "A platform that supports Play must declare a positive, finite player target frame rate.");
            } else if (supportsPlay && inputDevices == PlayerInputDevices.None) {
                throw new ArgumentException("A platform that supports Play must declare at least one player input device.", nameof(inputDevices));
            } else if (supportsPlay && (inputDevices & ~PlayerInputDevicesText.AllDevices) != PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(
                    nameof(inputDevices),
                    inputDevices,
                    "A platform's player input devices contain bits that name no known device.");
            } else if (supportsPlay && string.IsNullOrWhiteSpace(runtimePlatformName)) {
                throw new ArgumentException("A platform that supports Play must name the platform its native runtime reports to the core.", nameof(runtimePlatformName));
            } else if (supportsPlay && string.IsNullOrWhiteSpace(runtimePlatformVersion)) {
                throw new ArgumentException("A platform that supports Play must name the platform version its native runtime reports to the core.", nameof(runtimePlatformVersion));
            }

            SupportsPlay = supportsPlay;
            AdapterTypeName = adapterTypeName;
            ContentRootRelativePath = contentRootRelativePath;
            TargetFrameRate = targetFrameRate;
            InputDevices = inputDevices;
            RuntimePlatformName = runtimePlatformName;
            RuntimePlatformVersion = runtimePlatformVersion;
        }

        /// <summary>
        /// Gets whether the platform supports editor Play.
        /// </summary>
        public bool SupportsPlay { get; }

        /// <summary>
        /// Gets the type name of the platform's player adapter; <c>none</c> when Play is unsupported.
        /// </summary>
        public string AdapterTypeName { get; }

        /// <summary>
        /// Gets the cooked content root relative to the builder working root; <c>none</c> when Play is unsupported.
        /// </summary>
        public string ContentRootRelativePath { get; }

        /// <summary>
        /// Gets the frames per second the desktop player paces its frame loop to, so Play runs at the platform's own
        /// frame rate instead of the editor monitor's refresh rate; <c>0</c> when Play is unsupported.
        /// </summary>
        public double TargetFrameRate { get; }

        /// <summary>
        /// Gets the input devices the platform's real hardware offers. The desktop player reports every other device as
        /// idle, so a game cannot react to desktop input its platform does not have; <see cref="PlayerInputDevices.None"/>
        /// when Play is unsupported.
        /// </summary>
        public PlayerInputDevices InputDevices { get; }

        /// <summary>
        /// Gets the platform name the platform's native runtime reports to the core; the desktop player reports the same
        /// name so game code sees the console's identity during Play. <c>none</c> when Play is unsupported.
        /// </summary>
        public string RuntimePlatformName { get; }

        /// <summary>
        /// Gets the platform version the platform's native runtime reports to the core; the desktop player reports the
        /// same version. <c>none</c> when Play is unsupported.
        /// </summary>
        public string RuntimePlatformVersion { get; }

        /// <summary>
        /// Creates the capability for platforms that do not opt in to editor Play. The adapter and content root are
        /// <c>none</c>, the target frame rate is <c>0</c>, no input device is declared and the runtime platform name and version are <c>none</c>, because no player is ever
        /// launched for such a platform.
        /// </summary>
        /// <returns>Unsupported Play capability.</returns>
        public static PlatformPlayerCapability CreateUnsupported() {
            return new PlatformPlayerCapability(false, "none", "none", 0d, PlayerInputDevices.None, "none", "none");
        }
    }
}

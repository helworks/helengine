namespace helengine.baseplatform.Definitions {

    /// <summary>
    /// Converts a <see cref="PlayerInputDevices"/> set to and from the lowercase comma list the editor passes to the
    /// Play player (for example <c>gamepad</c> or <c>keyboard,mouse</c>). Both sides use this one type so the names
    /// cannot drift apart.
    /// </summary>
    public static class PlayerInputDevicesText {
        /// <summary>Separator between device names in the text form.</summary>
        public const char Separator = ',';

        /// <summary>Text name of <see cref="PlayerInputDevices.Keyboard"/>.</summary>
        public const string KeyboardName = "keyboard";

        /// <summary>Text name of <see cref="PlayerInputDevices.Mouse"/>.</summary>
        public const string MouseName = "mouse";

        /// <summary>Text name of <see cref="PlayerInputDevices.Pointer"/>.</summary>
        public const string PointerName = "pointer";

        /// <summary>Text name of <see cref="PlayerInputDevices.Gamepad"/>.</summary>
        public const string GamepadName = "gamepad";

        /// <summary>Every device flag the text form can carry, in the order <see cref="Format"/> writes them.</summary>
        public const PlayerInputDevices AllDevices = PlayerInputDevices.Keyboard | PlayerInputDevices.Mouse | PlayerInputDevices.Pointer | PlayerInputDevices.Gamepad;

        /// <summary>
        /// Writes a device set as lowercase names in declaration order (keyboard, mouse, pointer, gamepad), separated by
        /// commas without spaces.
        /// </summary>
        /// <param name="devices">Non-empty set of known devices.</param>
        /// <returns>The comma list, for example <c>gamepad</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The set is empty or contains bits that name no known device.</exception>
        public static string Format(PlayerInputDevices devices) {
            if (devices == PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(nameof(devices), devices, "An input device set must contain at least one device to have a text form.");
            } else if ((devices & ~AllDevices) != PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(nameof(devices), devices, "The input device set contains bits that name no known device.");
            }

            List<string> names = new List<string>(4);
            if ((devices & PlayerInputDevices.Keyboard) != PlayerInputDevices.None) {
                names.Add(KeyboardName);
            }
            if ((devices & PlayerInputDevices.Mouse) != PlayerInputDevices.None) {
                names.Add(MouseName);
            }
            if ((devices & PlayerInputDevices.Pointer) != PlayerInputDevices.None) {
                names.Add(PointerName);
            }
            if ((devices & PlayerInputDevices.Gamepad) != PlayerInputDevices.None) {
                names.Add(GamepadName);
            }

            return string.Join(Separator, names);
        }

        /// <summary>
        /// Reads a comma list written by <see cref="Format"/>. Names must be lowercase, non-empty and not repeated.
        /// </summary>
        /// <param name="text">Comma list to read.</param>
        /// <param name="sourceName">Name of the option or setting the text came from; it is named in every error.</param>
        /// <returns>The non-empty device set.</returns>
        /// <exception cref="ArgumentException">The list is empty, contains an empty entry, a repeated device, or an unknown name.</exception>
        public static PlayerInputDevices Parse(string text, string sourceName) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            } else if (string.IsNullOrWhiteSpace(sourceName)) {
                throw new ArgumentException("The source of an input device list must be named.", nameof(sourceName));
            }

            string[] names = text.Split(Separator);
            PlayerInputDevices devices = PlayerInputDevices.None;
            for (int i = 0; i < names.Length; i++) {
                PlayerInputDevices device = ParseName(names[i], text, sourceName);
                if ((devices & device) != PlayerInputDevices.None) {
                    throw new ArgumentException($"'{sourceName}' names input device '{names[i]}' more than once in '{text}'.", sourceName);
                }

                devices |= device;
            }

            return devices;
        }

        /// <summary>
        /// Reads one lowercase device name.
        /// </summary>
        /// <param name="name">One entry of the comma list.</param>
        /// <param name="text">Whole comma list, quoted in errors.</param>
        /// <param name="sourceName">Name of the option or setting the text came from.</param>
        /// <returns>The single device flag the name stands for.</returns>
        /// <exception cref="ArgumentException">The entry is empty or names no known device.</exception>
        static PlayerInputDevices ParseName(string name, string text, string sourceName) {
            if (name.Length == 0) {
                throw new ArgumentException($"'{sourceName}' contains an empty input device entry in '{text}'.", sourceName);
            } else if (string.Equals(name, KeyboardName, StringComparison.Ordinal)) {
                return PlayerInputDevices.Keyboard;
            } else if (string.Equals(name, MouseName, StringComparison.Ordinal)) {
                return PlayerInputDevices.Mouse;
            } else if (string.Equals(name, PointerName, StringComparison.Ordinal)) {
                return PlayerInputDevices.Pointer;
            } else if (string.Equals(name, GamepadName, StringComparison.Ordinal)) {
                return PlayerInputDevices.Gamepad;
            }

            throw new ArgumentException(
                $"'{sourceName}' names unknown input device '{name}'; expected a comma list of {KeyboardName}, {MouseName}, {PointerName}, {GamepadName}.",
                sourceName);
        }
    }
}

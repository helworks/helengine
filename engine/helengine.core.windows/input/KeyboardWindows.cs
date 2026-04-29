// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

using System.Runtime.InteropServices;

namespace helengine {
    public class KeyboardWindows : Keyboard {
        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        private readonly byte[] definedKeyCodes;

        private readonly byte[] _keyState = new byte[256];
        private readonly List<Keys> _keys = new List<Keys>(10);

        /// <summary>
        /// Tracks whether the keyboard input device should capture state.
        /// </summary>
        private bool _isActive;

        private Predicate<Keys> isKeyReleasedPredicate;

        public KeyboardWindows() {
            isKeyReleasedPredicate = key => IsKeyReleased((byte)key);

            var definedKeys = Enum.GetValues(typeof(Keys));
            var keyCodes = new List<byte>(Math.Min(definedKeys.Length, 255));
            foreach (var key in definedKeys) {
                var keyCode = (int)key;
                if ((keyCode >= 1) && (keyCode <= 255))
                    keyCodes.Add((byte)keyCode);
            }
            definedKeyCodes = keyCodes.ToArray();
        }

        private bool IsKeyReleased(byte keyCode) {
            return ((_keyState[keyCode] & 0x80) == 0);
        }

        public override KeyboardState GetState() {
            if (_isActive && GetKeyboardState(_keyState)) {
                _keys.RemoveAll(isKeyReleasedPredicate);

                foreach (var keyCode in definedKeyCodes) {
                    if (IsKeyReleased(keyCode)) {
                        continue;
                    }

                    var key = (Keys)keyCode;
                    if (!_keys.Contains(key)) {
                        _keys.Add(key);
                    }
                }
            }

            return new KeyboardState(_keys, Console.CapsLock, Console.NumberLock);
        }

        /// <summary>
        /// Enables or disables keyboard capture for the Windows backend.
        /// </summary>
        /// <param name="isActive">True to capture key state; false to ignore input.</param>
        public override void SetActive(bool isActive) {
            _isActive = isActive;
            if (!_isActive) {
                _keys.Clear();
            }
        }
    }
}

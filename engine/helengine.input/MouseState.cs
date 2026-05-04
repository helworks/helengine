// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a mouse state with cursor position and button press information.
    /// </summary>
    public struct MouseState {
        private const byte LeftButtonFlag = 1;
        private const byte RightButtonFlag = 2;
        private const byte MiddleButtonFlag = 4;
        private const byte XButton1Flag = 8;
        private const byte XButton2Flag = 16;

        private int _x;
        private int _y;
        private int _scrollWheelValue;
        private int _horizontalScrollWheelValue;
        private byte _buttons;

        public MouseState(
            int x,
            int y,
            int scrollWheel,
            ButtonState leftButton,
            ButtonState middleButton,
            ButtonState rightButton,
            ButtonState xButton1,
            ButtonState xButton2) {
            _x = x;
            _y = y;
            _scrollWheelValue = scrollWheel;
            _buttons = (byte)(
                (leftButton == ButtonState.Pressed ? LeftButtonFlag : 0) |
                (rightButton == ButtonState.Pressed ? RightButtonFlag : 0) |
                (middleButton == ButtonState.Pressed ? MiddleButtonFlag : 0) |
                (xButton1 == ButtonState.Pressed ? XButton1Flag : 0) |
                (xButton2 == ButtonState.Pressed ? XButton2Flag : 0)
            );
            _horizontalScrollWheelValue = 0;
        }

        public MouseState(
            int x,
            int y,
            int scrollWheel,
            ButtonState leftButton,
            ButtonState middleButton,
            ButtonState rightButton,
            ButtonState xButton1,
            ButtonState xButton2,
            int horizontalScrollWheel) {
            _x = x;
            _y = y;
            _scrollWheelValue = scrollWheel;
            _buttons = (byte)(
                (leftButton == ButtonState.Pressed ? LeftButtonFlag : 0) |
                (rightButton == ButtonState.Pressed ? RightButtonFlag : 0) |
                (middleButton == ButtonState.Pressed ? MiddleButtonFlag : 0) |
                (xButton1 == ButtonState.Pressed ? XButton1Flag : 0) |
                (xButton2 == ButtonState.Pressed ? XButton2Flag : 0)
            );
            _horizontalScrollWheelValue = horizontalScrollWheel;
        }

        public static bool operator ==(MouseState left, MouseState right) {
            return left._x == right._x &&
                   left._y == right._y &&
                   left._buttons == right._buttons &&
                   left._scrollWheelValue == right._scrollWheelValue &&
                   left._horizontalScrollWheelValue == right._horizontalScrollWheelValue;
        }

        public static bool operator !=(MouseState left, MouseState right) {
            return !(left == right);
        }

        public override bool Equals(object obj) {
            if (obj is MouseState)
                return this == (MouseState)obj;
            return false;
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = _x;
                hashCode = (hashCode * 397) ^ _y;
                hashCode = (hashCode * 397) ^ _scrollWheelValue;
                hashCode = (hashCode * 397) ^ _horizontalScrollWheelValue;
                hashCode = (hashCode * 397) ^ (int)_buttons;
                return hashCode;
            }
        }

        public override string ToString() {
            string buttons;
            if (_buttons == 0)
                buttons = "None";
            else {
                buttons = string.Empty;
                if ((_buttons & LeftButtonFlag) == LeftButtonFlag) {
                    if (buttons.Length > 0)
                        buttons += " Left";
                    else
                        buttons += "Left";
                }
                if ((_buttons & RightButtonFlag) == RightButtonFlag) {
                    if (buttons.Length > 0)
                        buttons += " Right";
                    else
                        buttons += "Right";
                }
                if ((_buttons & MiddleButtonFlag) == MiddleButtonFlag) {
                    if (buttons.Length > 0)
                        buttons += " Middle";
                    else
                        buttons += "Middle";
                }
                if ((_buttons & XButton1Flag) == XButton1Flag) {
                    if (buttons.Length > 0)
                        buttons += " XButton1";
                    else
                        buttons += "XButton1";
                }
                if ((_buttons & XButton2Flag) == XButton2Flag) {
                    if (buttons.Length > 0)
                        buttons += " XButton2";
                    else
                        buttons += "XButton2";
                }
            }

            return  "[MouseState X=" + _x +
                    ", Y=" + _y +
                    ", Buttons=" + buttons +
                    ", Wheel=" + _scrollWheelValue +
                    ", HWheel=" + _horizontalScrollWheelValue +
                    "]";
        }

        public int X {
            get { return _x; }
            set { _x = value; }
        }

        public int Y {
            get { return _y; }
            set { _y = value; }
        }

        public int2 Position {
            get { return new int2(_x, _y); }
        }

        public ButtonState LeftButton {
            get {
                return ((_buttons & LeftButtonFlag) > 0) ? ButtonState.Pressed : ButtonState.Released;
            }
            set {
                if (value == ButtonState.Pressed) {
                    _buttons = (byte)(_buttons | LeftButtonFlag);
                } else {
                    _buttons = (byte)(_buttons & (~LeftButtonFlag));
                }
            }
        }

        public ButtonState MiddleButton {
            get {
                return ((_buttons & MiddleButtonFlag) > 0) ? ButtonState.Pressed : ButtonState.Released;
            }
            set {
                if (value == ButtonState.Pressed) {
                    _buttons = (byte)(_buttons | MiddleButtonFlag);
                } else {
                    _buttons = (byte)(_buttons & (~MiddleButtonFlag));
                }
            }
        }

        public ButtonState RightButton {
            get {
                return ((_buttons & RightButtonFlag) > 0) ? ButtonState.Pressed : ButtonState.Released;
            }
            set {
                if (value == ButtonState.Pressed) {
                    _buttons = (byte)(_buttons | RightButtonFlag);
                } else {
                    _buttons = (byte)(_buttons & (~RightButtonFlag));
                }
            }
        }

        public int ScrollWheelValue {
            get { return _scrollWheelValue; }
            set { _scrollWheelValue = value; }
        }

        public int HorizontalScrollWheelValue {
            get { return _horizontalScrollWheelValue; }
            set { _horizontalScrollWheelValue = value; }
        }

        public ButtonState XButton1 {
            get {
                return ((_buttons & XButton1Flag) > 0) ? ButtonState.Pressed : ButtonState.Released;
            }
            set {
                if (value == ButtonState.Pressed) {
                    _buttons = (byte)(_buttons | XButton1Flag);
                } else {
                    _buttons = (byte)(_buttons & (~XButton1Flag));
                }
            }
        }

        public ButtonState XButton2 {
            get {
                return ((_buttons & XButton2Flag) > 0) ? ButtonState.Pressed : ButtonState.Released;
            }
            set {
                if (value == ButtonState.Pressed) {
                    _buttons = (byte)(_buttons | XButton2Flag);
                } else {
                    _buttons = (byte)(_buttons & (~XButton2Flag));
                }
            }
        }
    }
}

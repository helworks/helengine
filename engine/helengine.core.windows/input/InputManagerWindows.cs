namespace helengine {
    public class InputManagerWindows : InputManager {
        public InputManagerWindows(IntPtr window) {
            Keyboard = new KeyboardWindows();
            Mouse = new MouseWindows(window);
        }
    }
}

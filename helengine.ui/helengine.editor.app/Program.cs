namespace helengine.editor.app {
    internal static class Program {
        [STAThread]
        static void Main() {
            ApplicationConfiguration.Initialize();
            Application.Run(new ProjectView());
        }
    }
}
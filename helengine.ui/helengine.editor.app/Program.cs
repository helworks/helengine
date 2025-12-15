namespace helengine.editor.app {
    internal static class Program {
        [STAThread]
        static void Main(string[] args) {
            if (!TryGetProjectPath(args, out var projectPath)) {
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(projectPath));
        }

        static bool TryGetProjectPath(string[] args, out string projectPath) {
            projectPath = string.Empty;
            var candidate = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith("-", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(candidate)) {
                return false;
            }

            try {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath) || Directory.Exists(fullPath)) {
                    projectPath = fullPath;
                    return true;
                }
            } catch {
            }

            return false;
        }
    }
}

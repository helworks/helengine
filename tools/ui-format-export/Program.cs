using Microsoft.Build.Locator;

namespace helengine.tools.uiformat {
    /// <summary>Bootstraps the SDK before loading converter types that depend on MSBuild.</summary>
    public static class Program {
        /// <summary>Runs only an explicitly requested format export; errors propagate as nonzero process failure.</summary>
        public static int Main(string[] args) {
            if (args.Length != 2) { Console.Error.WriteLine("Usage: ui-format-export <helengine.ui.format.csproj> <output-directory>"); return 1; }
            if (!MSBuildLocator.IsRegistered) { MSBuildLocator.RegisterDefaults(); }
            return UiFormatExporter.Run(args[0], args[1]);
        }
    }
}

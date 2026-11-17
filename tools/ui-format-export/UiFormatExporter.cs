using cs2.core;
using cs2.ts;
using cs2.ts.util;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace helengine.tools.uiformat {
    /// <summary>Exports the portable C# module with the engine's pinned TypeScript converter without automatic dependency installation.</summary>
    public static class UiFormatExporter {
        /// <summary>Requires prepared runtime metadata and formatter before constructing the converter.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Run(string projectPath, string outputPath) {
            string project = Path.GetFullPath(projectPath); string output = Path.GetFullPath(outputPath);
            if (!File.Exists(project) || Path.GetFileName(project) != "helengine.ui.format.csproj") { throw new ArgumentException("Expected the real UI format project."); }
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any() && !File.Exists(Path.Combine(output, ".helengine-ui-format-export"))) { throw new InvalidOperationException("Output directory is not an existing UI format export."); }
            string runtime = Path.Combine(Path.GetDirectoryName(typeof(TypeScriptCodeConverter).Assembly.Location), ".net.ts");
            ValidateMetadata(runtime);
            if (!TypeScriptRuntimeMetadata.HasRequiredDependencies(runtime)) { throw new InvalidOperationException("Prepare the local TypeScript runtime dependencies before exporting; automatic installation is disabled by this driver."); }
            string formatterRuntime = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".net.ts");
            if (!Directory.Exists(formatterRuntime)) { throw new DirectoryNotFoundException("Prepared formatter runtime missing: " + formatterRuntime); }
            string formatter = TypeScriptCodeFormatter.ResolveLanguageServerExecutable(formatterRuntime);
            if (string.IsNullOrWhiteSpace(formatter)) { throw new InvalidOperationException("TypeScript formatter unavailable. Prepare it explicitly or set CS2_TS_TSGO; automatic installation is disabled by this driver."); }
            Environment.SetEnvironmentVariable(TypeScriptCodeFormatter.ExecutableEnvironmentVariable, formatter);
            TypeScriptConversionOptions options = new TypeScriptConversionOptions {
                Reflection = new ReflectionOptions { EnableReflection = false },
                RuntimeImportPath = "./runtime", EmitStrictTsConfig = true
            };
            TypeScriptCodeConverter converter = new TypeScriptCodeConverter(new ConversionRules(), TypeScriptEnvironment.Web, options) { RequireCleanCompilation = true };
            converter.AddCsproj(project);
            Directory.CreateDirectory(output);
            converter.WriteFile(output, "helengine-ui-format.ts");
            File.WriteAllText(Path.Combine(output, ".helengine-ui-format-export"), "helengine.ui.format\n");
            Console.WriteLine("UI TypeScript sources exported; TypeScript compilation and conformance are separate checks.");
            return 0;
        }
        /// <summary>Prevents the converter constructor from entering its automatic npm/metadata regeneration path.</summary>
        static void ValidateMetadata(string runtime) {
            if (!Directory.Exists(runtime)) { throw new DirectoryNotFoundException("Prepared TypeScript runtime missing: " + runtime); }
            string src = string.Concat(Path.DirectorySeparatorChar, "src", Path.DirectorySeparatorChar);
            string[] files = Directory.EnumerateFiles(runtime, "*.ts", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("node_modules", StringComparison.OrdinalIgnoreCase) == -1 && path.IndexOf(src, StringComparison.OrdinalIgnoreCase) == -1).ToArray();
            if (files.Length == 0) { throw new InvalidOperationException("TypeScript runtime contains no metadata inputs."); }
            foreach (string file in files) { if (!File.Exists(Path.ChangeExtension(file, ".json"))) { throw new InvalidOperationException("Prepare TypeScript runtime metadata explicitly before exporting; missing: " + Path.ChangeExtension(file, ".json")); } }
        }
    }
}

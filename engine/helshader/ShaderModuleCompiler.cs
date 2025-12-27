using helengine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace helshader {
    /// <summary>
    /// Compiles generated shader module source files into assemblies.
    /// </summary>
    public class ShaderModuleCompiler {
        /// <summary>
        /// Cached metadata references for compilation.
        /// </summary>
        readonly List<MetadataReference> references;

        /// <summary>
        /// Initializes a new shader module compiler.
        /// </summary>
        public ShaderModuleCompiler() {
            references = BuildReferences();
        }

        /// <summary>
        /// Compiles a shader module source file.
        /// </summary>
        /// <param name="request">Compilation request.</param>
        /// <returns>Compilation result.</returns>
        public ShaderModuleCompilationResult Compile(ShaderModuleCompilationRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (!File.Exists(request.SourcePath)) {
                throw new FileNotFoundException("Shader module source file was not found.", request.SourcePath);
            }

            string outputDirectory = Path.GetDirectoryName(request.OutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(outputDirectory);

            string source = File.ReadAllText(request.SourcePath);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(request.OutputPath),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (FileStream outputStream = File.Open(request.OutputPath, FileMode.Create, FileAccess.Write)) {
                EmitResult result = compilation.Emit(outputStream);
                string[] diagnostics = BuildDiagnostics(result.Diagnostics);
                return new ShaderModuleCompilationResult(result.Success, request.OutputPath, diagnostics);
            }
        }

        /// <summary>
        /// Builds the list of metadata references for compilation.
        /// </summary>
        /// <returns>Metadata reference list.</returns>
        List<MetadataReference> BuildReferences() {
            List<MetadataReference> list = new List<MetadataReference>();
            object data = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            string trustedAssemblies = data == null ? string.Empty : data.ToString();
            if (!string.IsNullOrWhiteSpace(trustedAssemblies)) {
                string[] paths = trustedAssemblies.Split(Path.PathSeparator);
                for (int i = 0; i < paths.Length; i++) {
                    string path = paths[i];
                    if (string.IsNullOrWhiteSpace(path)) {
                        continue;
                    }

                    list.Add(MetadataReference.CreateFromFile(path));
                }
            }

            string corePath = typeof(ShaderModuleDefinition).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(corePath)) {
                list.Add(MetadataReference.CreateFromFile(corePath));
            }

            return list;
        }

        /// <summary>
        /// Builds diagnostic messages from compiler output.
        /// </summary>
        /// <param name="diagnostics">Diagnostic collection.</param>
        /// <returns>Diagnostic strings.</returns>
        string[] BuildDiagnostics(IEnumerable<Diagnostic> diagnostics) {
            if (diagnostics == null) {
                return Array.Empty<string>();
            }

            List<string> messages = new List<string>();
            foreach (Diagnostic diagnostic in diagnostics) {
                messages.Add(diagnostic.ToString());
            }

            return messages.ToArray();
        }
    }
}

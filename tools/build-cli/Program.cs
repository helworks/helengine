using helengine;
using helengine.editor;

namespace helengine.buildcli {
    /// <summary>Runs the shared authoring/build pipeline without a desktop UI or graphics device.</summary>
    internal static class Program {
        /// <summary>Parses portable host options and forwards the existing build/editor-command argument contract.</summary>
        /// <param name="args">Host options followed by existing editor CLI arguments.</param>
        /// <returns>Zero only when the requested command completed successfully.</returns>
        static int Main(string[] args) {
            try {
                if (args.Length == 0 || args.Contains("--help")) {
                    Console.WriteLine("HelEngine portable build CLI");
                    Console.WriteLine("--default-font-source <font.ttf> --project <project.heproj> --build <platform> --output <directory> [--build-profile <profile>]");
                    Console.WriteLine("--default-font-source supplies the explicit face used for built-in editor font references; source assets keep their own font settings.");
                    return args.Length == 0 ? 1 : 0;
                }
                string fontSource = null;
                List<string> forwarded = new List<string>();
                for (int index = 0; index < args.Length; index++) {
                    if (args[index] == "--default-font-source") {
                        if (fontSource != null || ++index >= args.Length) {
                            throw new ArgumentException("Supply --default-font-source exactly once with a source face path.");
                        }
                        fontSource = Path.GetFullPath(args[index]);
                    } else {
                        forwarded.Add(args[index]);
                    }
                }
                if (string.IsNullOrWhiteSpace(fontSource) || !File.Exists(fontSource)) {
                    throw new ArgumentException("--default-font-source must name an existing TrueType or OpenType face.");
                }
                IReadOnlyList<IAssetImporterRegistration> importers = PortableImporterFactory.CreateDefault();
                using FileStream face = File.OpenRead(fontSource);
                using FontAsset defaultFont = new PortableFontImporter().ImportFont(face, new FontAssetProcessorSettings { PixelSize = 12 });
                string[] commandArgs = forwarded.ToArray();
                EditorBuildExecutionResult result;
                if (EditorCliArgumentParser.IsBuildModeRequested(commandArgs)) {
                    if (!EditorCliArgumentParser.TryParseBuildOptions(commandArgs, out EditorCliBuildOptions options, out string error)) {
                        throw new ArgumentException(error);
                    }
                    result = new EditorCliBuildRunner(importers, defaultFont).Run(options);
                } else if (EditorCliArgumentParser.IsEditorCommandModeRequested(commandArgs)) {
                    if (!EditorCliArgumentParser.TryParseEditorCommandOptions(commandArgs, out EditorCliCommandOptions options, out string error)) {
                        throw new ArgumentException(error);
                    }
                    result = new EditorCliCommandRunner(defaultFont, new EditorProjectAssetAuthoringServiceFactory(importers)).Run(options);
                } else {
                    throw new ArgumentException("Specify --build or --editor-command; this executable has no interactive editor mode.");
                }
                if (result.Succeeded) {
                    Console.WriteLine(result.Message);
                    return 0;
                }
                Console.Error.WriteLine(result.Message);
                return 1;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }
    }
}

using System.Reflection;
using System.Text.Json;

namespace helengine.editor.windows.importerprobe {
    /// <summary>
    /// Runs isolated importer registration and import operations so tests can observe backend assembly load timing from a fresh process.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Executes the importer probe and writes one JSON result to standard output.
        /// </summary>
        /// <param name="args">Unused command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        static int Main(string[] args) {
            try {
                ImporterProbeResult result = RunProbe();
                Console.WriteLine(JsonSerializer.Serialize(result));
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }

        /// <summary>
        /// Executes importer registration and import operations while recording backend assembly load state transitions.
        /// </summary>
        /// <returns>Probe result containing the observed load-state timeline.</returns>
        static ImporterProbeResult RunProbe() {
            ImporterProbeResult result = new ImporterProbeResult {
                GdiLoadedBeforeRegistration = IsAssemblyLoaded("helengine.editor.windows.gdiimporter"),
                PfimLoadedBeforeRegistration = IsAssemblyLoaded("helengine.editor.windows.pfimimporter"),
                MagickLoadedBeforeRegistration = IsAssemblyLoaded("helengine.editor.windows.magickimporter"),
                AssimpLoadedBeforeRegistration = IsAssemblyLoaded("helengine.editor.assimp")
            };

            IReadOnlyList<IAssetImporterRegistration> registrations = CreateDefaultImporters();

            result.GdiLoadedAfterRegistration = IsAssemblyLoaded("helengine.editor.windows.gdiimporter");
            result.PfimLoadedAfterRegistration = IsAssemblyLoaded("helengine.editor.windows.pfimimporter");
            result.MagickLoadedAfterRegistration = IsAssemblyLoaded("helengine.editor.windows.magickimporter");
            result.AssimpLoadedAfterRegistration = IsAssemblyLoaded("helengine.editor.assimp");

            ImportTexture(GetRequiredTextureRegistration(registrations, "gdi"), CreateSinglePixelPngFile());
            result.GdiLoadedAfterImport = IsAssemblyLoaded("helengine.editor.windows.gdiimporter");

            ImportTexture(GetRequiredTextureRegistration(registrations, "pfim"), CreateSinglePixelTga32File());
            result.PfimLoadedAfterImport = IsAssemblyLoaded("helengine.editor.windows.pfimimporter");

            ImportTexture(GetRequiredTextureRegistration(registrations, "magick"), CreateSinglePixelOpaquePngFile());
            result.MagickLoadedAfterImport = IsAssemblyLoaded("helengine.editor.windows.magickimporter");

            ImportModel(GetRequiredModelRegistration(registrations, "assimp"));
            result.AssimpLoadedAfterImport = IsAssemblyLoaded("helengine.editor.assimp");

            return result;
        }

        /// <summary>
        /// Creates the default host importer registrations through the editor application assembly.
        /// </summary>
        /// <returns>Default importer registrations used by the editor host.</returns>
        static IReadOnlyList<IAssetImporterRegistration> CreateDefaultImporters() {
            Assembly appAssembly = Assembly.Load("helengine.editor.app");
            Type factoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", true) ?? throw new InvalidOperationException("EditorHostImporterFactory was not found.");
            MethodInfo createDefaultMethod = factoryType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("CreateDefault was not found.");
            object registrationsObject = createDefaultMethod.Invoke(null, Array.Empty<object>()) ?? throw new InvalidOperationException("CreateDefault returned null.");
            if (registrationsObject is IReadOnlyList<IAssetImporterRegistration> registrations) {
                return registrations;
            }

            throw new InvalidOperationException("CreateDefault did not return an importer registration list.");
        }

        /// <summary>
        /// Retrieves one required texture importer registration by identifier.
        /// </summary>
        /// <param name="registrations">Available importer registrations.</param>
        /// <param name="importerId">Identifier of the texture importer registration to retrieve.</param>
        /// <returns>Resolved texture importer registration.</returns>
        static TextureImporterRegistration GetRequiredTextureRegistration(IReadOnlyList<IAssetImporterRegistration> registrations, string importerId) {
            if (registrations == null) {
                throw new ArgumentNullException(nameof(registrations));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            for (int index = 0; index < registrations.Count; index++) {
                if (registrations[index] is TextureImporterRegistration registration &&
                    string.Equals(registration.ImporterId, importerId, StringComparison.OrdinalIgnoreCase)) {
                    return registration;
                }
            }

            throw new InvalidOperationException($"Texture importer '{importerId}' was not found.");
        }

        /// <summary>
        /// Retrieves one required model importer registration by identifier.
        /// </summary>
        /// <param name="registrations">Available importer registrations.</param>
        /// <param name="importerId">Identifier of the model importer registration to retrieve.</param>
        /// <returns>Resolved model importer registration.</returns>
        static ModelImporterRegistration GetRequiredModelRegistration(IReadOnlyList<IAssetImporterRegistration> registrations, string importerId) {
            if (registrations == null) {
                throw new ArgumentNullException(nameof(registrations));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            for (int index = 0; index < registrations.Count; index++) {
                if (registrations[index] is ModelImporterRegistration registration &&
                    string.Equals(registration.ImporterId, importerId, StringComparison.OrdinalIgnoreCase)) {
                    return registration;
                }
            }

            throw new InvalidOperationException($"Model importer '{importerId}' was not found.");
        }

        /// <summary>
        /// Imports one texture through the supplied registration.
        /// </summary>
        /// <param name="registration">Texture importer registration to execute.</param>
        /// <param name="sourceBytes">Encoded source image bytes.</param>
        static void ImportTexture(TextureImporterRegistration registration, byte[] sourceBytes) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (sourceBytes == null) {
                throw new ArgumentNullException(nameof(sourceBytes));
            }

            using MemoryStream stream = new MemoryStream(sourceBytes);
            registration.Importer.ImportTexture(stream);
        }

        /// <summary>
        /// Imports one minimal OBJ model through the supplied registration.
        /// </summary>
        /// <param name="registration">Model importer registration to execute.</param>
        static void ImportModel(ModelImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            string workspacePath = Path.Combine(Path.GetTempPath(), "helengine-importer-probe", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspacePath);
            try {
                string modelPath = Path.Combine(workspacePath, "triangle.obj");
                File.WriteAllText(modelPath, BuildSingleTriangleObj());
                using FileStream stream = File.OpenRead(modelPath);
                registration.Importer.ImportModel(stream);
            } finally {
                if (Directory.Exists(workspacePath)) {
                    Directory.Delete(workspacePath, true);
                }
            }
        }

        /// <summary>
        /// Checks whether one assembly is currently loaded in the probe process.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name to search for.</param>
        /// <returns>True when the assembly is loaded.</returns>
        static bool IsAssemblyLoaded(string assemblyName) {
            if (string.IsNullOrWhiteSpace(assemblyName)) {
                throw new ArgumentException("Assembly name must be provided.", nameof(assemblyName));
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++) {
                if (string.Equals(assemblies[index].GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a 1x1 PNG file whose single pixel is encoded as RGBA 9,8,7,6.
        /// </summary>
        /// <returns>Encoded PNG file bytes.</returns>
        static byte[] CreateSinglePixelPngFile() {
            return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY+DkYGcDAABVAB+CD+SxAAAAAElFTkSuQmCC");
        }

        /// <summary>
        /// Creates a 1x1 32-bit uncompressed TGA file whose pixel bytes are stored as BGRA.
        /// </summary>
        /// <returns>Encoded TGA file bytes.</returns>
        static byte[] CreateSinglePixelTga32File() {
            return new byte[] {
                0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 32, 40,
                1, 2, 3, 4
            };
        }

        /// <summary>
        /// Creates a 1x1 opaque PNG file whose single pixel is encoded as RGBA 9,8,7,255.
        /// </summary>
        /// <returns>Encoded PNG file bytes.</returns>
        static byte[] CreateSinglePixelOpaquePngFile() {
            return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY+DkYP8PAAFOARgGWpOHAAAAAElFTkSuQmCC");
        }

        /// <summary>
        /// Builds one minimal OBJ file containing a single textured triangle.
        /// </summary>
        /// <returns>OBJ file text.</returns>
        static string BuildSingleTriangleObj() {
            return string.Join(
                Environment.NewLine,
                new[] {
                    "o Triangle",
                    "v 0 0 0",
                    "v 1 0 0",
                    "v 0 1 0",
                    "vt 0 0",
                    "vt 1 0",
                    "vt 0 1",
                    "vn 0 0 1",
                    "f 1/1/1 2/2/1 3/3/1",
                    string.Empty
                });
        }
    }
}

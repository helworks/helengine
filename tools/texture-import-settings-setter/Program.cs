using helengine.editor;
using helengine.editor.windows;

namespace helengine.tools.textureimportsettingssetter {
    /// <summary>
    /// Updates one texture asset's per-platform import settings and refreshes its cached import output.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Entry point for the texture import settings setter tool.
        /// </summary>
        /// <param name="args">Command-line arguments: project root, texture source path, then one or more repeating platform-id/max-resolution pairs.</param>
        /// <returns>Exit code indicating success or failure.</returns>
        static int Main(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }
            if (args.Length < 4 || ((args.Length - 2) % 2) != 0) {
                Console.Error.WriteLine("Usage: <project-root> <texture-source-path> <platform-id> <max-resolution> [<platform-id> <max-resolution> ...]");
                return 1;
            }

            string projectRootPath = Path.GetFullPath(args[0]);
            string textureSourcePath = Path.GetFullPath(args[1]);
            Dictionary<string, int> maxResolutionsByPlatformId = ParsePlatformResolutionArguments(args);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
            AssetImportManager importManager = new AssetImportManager(projectRootPath, contentManager);
            TextureImporterRegistration registration = new TextureImporterRegistration("gdi", new GDITextureImporter(), new[] { ".png" });
            registration.Register(importManager);
            importManager.CurrentPlatformId = "windows";

            AssetImportSettings settings = importManager.LoadOrCreateImportSettings(textureSourcePath);
            if (settings == null) {
                throw new InvalidOperationException("Texture import settings could not be created.");
            }

            foreach (KeyValuePair<string, int> entry in maxResolutionsByPlatformId) {
                EnsureTexturePlatformSettings(settings, entry.Key).MaxResolution = entry.Value;
            }

            importManager.SaveImportSettings(textureSourcePath, settings);
            TextureAsset textureAsset = importManager.ImportTexture(textureSourcePath);
            if (textureAsset == null) {
                throw new InvalidOperationException("Texture import did not return a texture asset.");
            }

            Console.WriteLine(textureAsset.Id ?? string.Empty);
            return 0;
        }

        /// <summary>
        /// Parses one max-resolution command-line argument.
        /// </summary>
        /// <param name="value">Raw argument value.</param>
        /// <param name="platformId">Platform id used for diagnostics.</param>
        /// <returns>Parsed positive max-resolution value.</returns>
        static int ParseResolutionArgument(string value, string platformId) {
            if (!int.TryParse(value, out int parsedValue) || parsedValue < 1) {
                throw new InvalidOperationException($"Platform '{platformId}' requires a positive integer max resolution.");
            }

            return parsedValue;
        }

        /// <summary>
        /// Parses the repeating platform-id/max-resolution command-line arguments into one lookup keyed by platform id.
        /// </summary>
        /// <param name="args">Full command-line argument array supplied to the process entry point.</param>
        /// <returns>Per-platform max-resolution values keyed by platform id.</returns>
        static Dictionary<string, int> ParsePlatformResolutionArguments(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            Dictionary<string, int> maxResolutionsByPlatformId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 2; index < args.Length; index += 2) {
                string platformId = args[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Platform id arguments must not be empty.");
                }

                maxResolutionsByPlatformId[platformId] = ParseResolutionArgument(args[index + 1], platformId);
            }

            return maxResolutionsByPlatformId;
        }

        /// <summary>
        /// Ensures one platform entry exposes a texture processor settings object.
        /// </summary>
        /// <param name="settings">Import settings to update.</param>
        /// <param name="platformId">Platform id whose texture settings should be created or returned.</param>
        /// <returns>Texture processor settings for the requested platform.</returns>
        static TextureAssetProcessorSettings EnsureTexturePlatformSettings(AssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (settings.Processor == null) {
                settings.Processor = new AssetProcessorSettings();
            }
            if (settings.Processor.Platforms == null) {
                settings.Processor.Platforms = new Dictionary<string, AssetPlatformProcessorSettings>(StringComparer.OrdinalIgnoreCase);
            }
            if (!settings.Processor.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings) || platformSettings == null) {
                platformSettings = new AssetPlatformProcessorSettings();
                settings.Processor.Platforms[platformId] = platformSettings;
            }
            if (platformSettings.Texture == null) {
                platformSettings.Texture = new TextureAssetProcessorSettings();
            }

            return platformSettings.Texture;
        }
    }
}

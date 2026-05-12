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
        /// <param name="args">Command-line arguments: project root, texture source path, windows max, ps2 max, psp max.</param>
        /// <returns>Exit code indicating success or failure.</returns>
        static int Main(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }
            if (args.Length != 5) {
                Console.Error.WriteLine("Usage: <project-root> <texture-source-path> <windows-max> <ps2-max> <psp-max>");
                return 1;
            }

            string projectRootPath = Path.GetFullPath(args[0]);
            string textureSourcePath = Path.GetFullPath(args[1]);
            int windowsMaxResolution = ParseResolutionArgument(args[2], "windows");
            int ps2MaxResolution = ParseResolutionArgument(args[3], "ps2");
            int pspMaxResolution = ParseResolutionArgument(args[4], "psp");

            ContentManager contentManager = new ContentManager(Path.Combine(projectRootPath, "assets"));
            AssetImportManager importManager = new AssetImportManager(projectRootPath, contentManager);
            TextureImporterRegistration registration = new TextureImporterRegistration("gdi", new GDITextureImporter(), new[] { ".png" });
            registration.Register(importManager);
            importManager.CurrentPlatformId = "windows";

            AssetImportSettings settings = importManager.LoadOrCreateImportSettings(textureSourcePath);
            if (settings == null) {
                throw new InvalidOperationException("Texture import settings could not be created.");
            }

            EnsureTexturePlatformSettings(settings, "windows").MaxResolution = windowsMaxResolution;
            EnsureTexturePlatformSettings(settings, "ps2").MaxResolution = ps2MaxResolution;
            EnsureTexturePlatformSettings(settings, "psp").MaxResolution = pspMaxResolution;

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

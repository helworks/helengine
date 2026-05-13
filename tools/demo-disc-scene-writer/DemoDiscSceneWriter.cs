using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.editor;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Writes the city-project assets and configuration required by the first-pass demo-disc menu.
    /// </summary>
    public sealed class DemoDiscSceneWriter {
        /// <summary>
        /// Authored scene path used by the generated menu scene asset.
        /// </summary>
        const string MenuScenePath = "Scenes/DemoDiscMainMenu.helen";

        /// <summary>
        /// Project-relative folder that owns the generated demo-disc menu source files.
        /// </summary>
        const string MenuCodeFolderPath = "assets/codebase/menu";

        /// <summary>
        /// Root gameplay module id used when no authored folder-scoped module owns the generated menu code.
        /// </summary>
        const string DefaultMenuModuleId = "gameplay";

        /// <summary>
        /// Curated playable scene paths packaged by the demo-disc build.
        /// </summary>
        static readonly string[] CuratedScenePaths = new[] {
            MenuScenePath,
            "scenes/rendering/cube_test.helen",
            "scenes/rendering/colored_cube_grid.helen",
            "scenes/rendering/textured_cube_grid.helen"
        };

        /// <summary>
        /// Curated playable scene ids packaged by the demo-disc build.
        /// </summary>
        static readonly string[] CuratedSceneIds = BuildCuratedSceneIds();

        /// <summary>
        /// Font writer used to copy authored source fonts into the generated project.
        /// </summary>
        readonly DemoDiscFontWriter FontWriter;

        /// <summary>
        /// Shared baked-scene build service used by both writer generation and future editor rebuilds.
        /// </summary>
        readonly DemoMenuSceneBuildService SceneBuildService;

        /// <summary>
        /// Initializes the scene writer with the supplied font writer.
        /// </summary>
        /// <param name="fontWriter">Font writer used by the generator.</param>
        public DemoDiscSceneWriter(DemoDiscFontWriter fontWriter) {
            FontWriter = fontWriter ?? throw new ArgumentNullException(nameof(fontWriter));
            SceneBuildService = new DemoMenuSceneBuildService();
        }

        /// <summary>
        /// Writes all menu assets and city configuration updates into the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">City project root path.</param>
        public void WriteAll(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            string userSettingsRootPath = Path.Combine(fullProjectRootPath, "user_settings");
            if (!Directory.Exists(assetsRootPath)) {
                throw new InvalidOperationException($"Assets root was not found: {assetsRootPath}");
            }
            if (!Directory.Exists(userSettingsRootPath)) {
                throw new InvalidOperationException($"User settings root was not found: {userSettingsRootPath}");
            }

            string owningModuleId = ResolveOwningModuleId(fullProjectRootPath);
            string providerTypeName = BuildProviderTypeName(owningModuleId);
            WriteMenuSourceFiles(assetsRootPath);
            WriteMenuFonts(assetsRootPath);
            WriteMenuSceneAsset(assetsRootPath, providerTypeName);
            UpdateBuildConfig(Path.Combine(userSettingsRootPath, "build_config.json"));
        }

        /// <summary>
        /// Writes the curated city-side menu source files.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the city project.</param>
        void WriteMenuSourceFiles(string assetsRootPath) {
            string menuRootPath = Path.Combine(assetsRootPath, "codebase", "menu");
            Directory.CreateDirectory(menuRootPath);
            File.WriteAllText(Path.Combine(menuRootPath, "DemoDiscSceneCatalog.cs"), BuildSceneCatalogSource());
            File.WriteAllText(Path.Combine(menuRootPath, "DemoDiscMenuTheme.cs"), BuildMenuThemeSource());
            File.WriteAllText(Path.Combine(menuRootPath, "DemoDiscMenuDefinitionProvider.cs"), BuildMenuDefinitionProviderSource());
            File.WriteAllText(Path.Combine(menuRootPath, "PlatformInfoTextComponent.cs"), BuildPlatformInfoTextComponentSource());
        }

        /// <summary>
        /// Writes the authored title and body source fonts consumed by the generated menu definition.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the city project.</param>
        void WriteMenuFonts(string assetsRootPath) {
            string fontsRootPath = Path.Combine(assetsRootPath, "Fonts");
            Directory.CreateDirectory(fontsRootPath);
            FontWriter.WriteFont(Path.Combine(fontsRootPath, "DemoDiscTitle.ttf"), "georgiab.ttf");
            FontWriter.WriteFont(Path.Combine(fontsRootPath, "DemoDiscBody.ttf"), "trebuc.ttf");
        }

        /// <summary>
        /// Writes the generated demo-disc menu scene asset.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the city project.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type stored in the menu-host component.</param>
        void WriteMenuSceneAsset(string assetsRootPath, string providerTypeName) {
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            string scenePath = Path.Combine(assetsRootPath, MenuScenePath.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Menu scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            MenuDefinition definition = BuildMenuDefinition();
            SceneAsset sceneAsset = SceneBuildService.BuildSceneAsset(SceneIdUtility.FromPath(MenuScenePath), providerTypeName, definition);

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            helengine.files.EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Updates the city build configuration so the menu scene starts first and all curated scenes are packaged.
        /// </summary>
        /// <param name="buildConfigPath">Build-config document path.</param>
        void UpdateBuildConfig(string buildConfigPath) {
            JsonNode rootNode = JsonNode.Parse(File.ReadAllText(buildConfigPath)) ?? throw new InvalidOperationException("Build config JSON could not be parsed.");
            JsonArray platforms = rootNode["platforms"]?.AsArray() ?? throw new InvalidOperationException("Build config is missing the platforms array.");
            JsonObject windowsPlatform = FindWindowsPlatform(platforms);

            JsonArray selectedSceneIds = new JsonArray();
            JsonArray sceneOrders = new JsonArray();
            for (int sceneIndex = 0; sceneIndex < CuratedSceneIds.Length; sceneIndex++) {
                string sceneId = CuratedSceneIds[sceneIndex];
                selectedSceneIds.Add(sceneId);
                sceneOrders.Add(new JsonObject {
                    ["sceneId"] = sceneId,
                    ["orderNumber"] = sceneIndex + 1
                });
            }

            windowsPlatform["selectedSceneIds"] = selectedSceneIds;
            windowsPlatform["sceneOrders"] = sceneOrders;
            rootNode["queueItems"] = new JsonArray();

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions {
                WriteIndented = true
            };
            File.WriteAllText(buildConfigPath, rootNode.ToJsonString(serializerOptions));
        }

        /// <summary>
        /// Finds the Windows platform entry inside the build-config platforms array.
        /// </summary>
        /// <param name="platforms">Platforms array from the build config.</param>
        /// <returns>Windows platform entry.</returns>
        JsonObject FindWindowsPlatform(JsonArray platforms) {
            for (int index = 0; index < platforms.Count; index++) {
                JsonObject platform = platforms[index]?.AsObject();
                if (platform == null) {
                    continue;
                }

                string platformId = platform["platformId"]?.GetValue<string>() ?? string.Empty;
                if (string.Equals(platformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                    return platform;
                }
            }

            throw new InvalidOperationException("Build config does not contain a windows platform entry.");
        }

        /// <summary>
        /// Builds the assembly-qualified menu provider type name that matches the owning script module.
        /// </summary>
        /// <param name="owningModuleId">Authored module id that owns the generated menu code.</param>
        /// <returns>Assembly-qualified provider type name persisted into the generated menu scene.</returns>
        string BuildProviderTypeName(string owningModuleId) {
            if (string.IsNullOrWhiteSpace(owningModuleId)) {
                throw new ArgumentException("Owning module id must be provided.", nameof(owningModuleId));
            }

            return $"city.menu.DemoDiscMenuDefinitionProvider, {owningModuleId}";
        }

        /// <summary>
        /// Resolves the authored module id that owns the generated menu source files.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Authored module id that owns the generated menu code.</returns>
        string ResolveOwningModuleId(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(projectRootPath).Load();
            string normalizedMenuFolderPath = NormalizeRelativePath(MenuCodeFolderPath);
            string owningModuleId = DefaultMenuModuleId;
            int owningModuleDepth = 0;
            for (int index = 0; index < manifestDocument.Modules.Length; index++) {
                EditorCodeModuleManifestEntry module = manifestDocument.Modules[index];
                if (!OwnsFolderPath(module.FolderPath, normalizedMenuFolderPath)) {
                    continue;
                }

                int moduleDepth = GetFolderDepth(module.FolderPath);
                if (moduleDepth < owningModuleDepth) {
                    continue;
                }

                owningModuleId = module.ModuleId;
                owningModuleDepth = moduleDepth;
            }

            return owningModuleId;
        }

        /// <summary>
        /// Returns whether one authored code-module folder owns the supplied descendant folder path.
        /// </summary>
        /// <param name="moduleFolderPath">Project-relative authored code-module folder path.</param>
        /// <param name="candidateFolderPath">Project-relative folder path to inspect.</param>
        /// <returns>True when the module folder owns the candidate folder path.</returns>
        static bool OwnsFolderPath(string moduleFolderPath, string candidateFolderPath) {
            if (string.IsNullOrWhiteSpace(moduleFolderPath) || string.IsNullOrWhiteSpace(candidateFolderPath)) {
                return false;
            }
            if (string.Equals(moduleFolderPath, candidateFolderPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string moduleFolderPrefix = moduleFolderPath.TrimEnd('/') + "/";
            return candidateFolderPath.StartsWith(moduleFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the authored folder depth used to prefer the most specific owning module boundary.
        /// </summary>
        /// <param name="folderPath">Normalized project-relative folder path.</param>
        /// <returns>Folder depth measured in path segments.</returns>
        static int GetFolderDepth(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                return 0;
            }

            return folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Normalizes one project-relative path to the forward-slash form used by code-module manifests.
        /// </summary>
        /// <param name="relativePath">Project-relative path to normalize.</param>
        /// <returns>Normalized manifest-style relative path.</returns>
        static string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// Builds the curated stable scene ids that the generated menu and build config should reference.
        /// </summary>
        /// <returns>Curated stable scene ids derived from the authored scene asset names.</returns>
        static string[] BuildCuratedSceneIds() {
            string[] sceneIds = new string[CuratedScenePaths.Length];
            for (int index = 0; index < CuratedScenePaths.Length; index++) {
                sceneIds[index] = SceneIdUtility.FromPath(CuratedScenePaths[index]);
            }

            return sceneIds;
        }

        /// <summary>
        /// Builds the shared baked menu definition used by both the scene writer and future editor rebuild entrypoints.
        /// </summary>
        /// <returns>Baked demo-disc menu definition.</returns>
        MenuDefinition BuildMenuDefinition() {
            return new MenuDefinition(
                string.Empty,
                string.Empty,
                "main",
                "Fonts/DemoDiscTitle.ttf",
                "Fonts/DemoDiscBody.ttf",
                new byte4(30, 17, 41, 255),
                new byte4(60, 41, 76, 232),
                new byte4(135, 94, 163, 255),
                new byte4(201, 147, 255, 255),
                new byte4(118, 219, 209, 255),
                new byte4(249, 243, 255, 255),
                new byte4(211, 198, 228, 255),
                new[] {
                    new MenuPanelDefinition(
                        "main",
                        "Main Menu",
                        "Pick a destination or peek at the menu shell.",
                        6,
                        new[] {
                            new MenuItemDefinition("main-scenes", "Select Scene", "Browse the curated demo-disc lineup.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "scene-select")),
                            new MenuItemDefinition("main-options", "Options", "Preview the reusable options shell layout.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "options"))
                        }),
                    new MenuPanelDefinition(
                        "scene-select",
                        "Select Scene",
                        "Every entry here is explicitly curated and ordered from city-side code.",
                        4,
                        CreateSceneSelectItems()),
                    new MenuPanelDefinition(
                        "options",
                        "Options",
                        "Polished shell for future settings categories.",
                        6,
                        new[] {
                            new MenuItemDefinition("options-display", "Display", "Placeholder row for future video settings.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                            new MenuItemDefinition("options-audio", "Audio", "Placeholder row for future volume settings.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                            new MenuItemDefinition("options-controls", "Controls", "Placeholder row for future input remapping.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                            new MenuItemDefinition("options-back", "Back", "Returns to the main menu.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                        })
                },
                new MenuOverlayImageDefinition("Images/Menu/helengine-logo.png", 220, 220, 36, 44),
                new MenuPlatformInfoDefinition(28, 44, 6));
        }

        /// <summary>
        /// Creates the curated scene-selection menu items baked into the demo menu scene.
        /// </summary>
        /// <returns>Curated scene-selection items.</returns>
        MenuItemDefinition[] CreateSceneSelectItems() {
            return new[] {
                new MenuItemDefinition("scene-cube-test", "Cube Test", "Minimal one-cube rendering validation scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/cube_test.helen"))),
                new MenuItemDefinition("scene-colored-cube-grid", "Colored Cube Grid", "Sixteen rotating cubes with distinct lit material colors.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/colored_cube_grid.helen"))),
                new MenuItemDefinition("scene-textured-cube-grid", "Textured Cube Grid", "Sixteen rotating cubes with distinct lit texture materials.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/textured_cube_grid.helen"))),
                new MenuItemDefinition("scene-axis-test", "Axis Test", "Three-axis rotation validation scene with a directional-light arrow.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/axis_test.helen"))),
                new MenuItemDefinition("scene-axis-test-2", "Axis Test 2", "Mirrored axis showcase that validates the right-side directional layout.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/axis_test2.helen"))),
                new MenuItemDefinition("scene-directional-shadow-plaza", "Directional Shadow Plaza", "Lighting showcase with an orbiting camera and decorative plaza geometry.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/directional_shadow_plaza.helen"))),
                new MenuItemDefinition("scene-spotlight-street-slice", "Spotlight Street Slice", "Street-lit showcase that validates spotlights and prop placement.", true, new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/rendering/spotlight_street_slice.helen"))),
                new MenuItemDefinition("scene-back", "Back", "Returns to the main menu.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
            };
        }

        /// <summary>
        /// Builds the city-side scene catalog source code.
        /// </summary>
        /// <returns>Generated scene catalog source text.</returns>
        string BuildSceneCatalogSource() {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("namespace city.menu {");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Stores the curated playable scene items shown by the first-pass demo-disc scene selector.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    public sealed class DemoDiscSceneCatalog {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Builds the ordered playable scene menu items shown by the demo-disc menu.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        /// <returns>Curated scene menu items.</returns>");
            builder.AppendLine("        public MenuItemDefinition[] CreateSceneItems() {");
            builder.AppendLine("            return new[] {");
            builder.AppendLine("                new MenuItemDefinition(\"scene-cube-test\", \"Cube Test\", \"Minimal one-cube rendering validation scene.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"cube_test\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-colored-cube-grid\", \"Colored Cube Grid\", \"Sixteen rotating cubes with distinct lit material colors.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"colored_cube_grid\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-textured-cube-grid\", \"Textured Cube Grid\", \"Sixteen rotating cubes with distinct lit texture materials.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"textured_cube_grid\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-axis-test\", \"Axis Test\", \"Three-axis rotation validation scene with a directional-light arrow.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"axis_test\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-axis-test-2\", \"Axis Test 2\", \"Mirrored axis showcase that validates the right-side directional layout.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"axis_test2\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-directional-shadow-plaza\", \"Directional Shadow Plaza\", \"Lighting showcase with an orbiting camera and decorative plaza geometry.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"directional_shadow_plaza\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-spotlight-street-slice\", \"Spotlight Street Slice\", \"Street-lit showcase that validates spotlights and prop placement.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"spotlight_street_slice\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-back\", \"Back\", \"Returns to the main menu.\", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the city-side menu theme source code.
        /// </summary>
        /// <returns>Generated menu theme source text.</returns>
        string BuildMenuThemeSource() {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("namespace city.menu {");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Stores reusable colors, font paths, and decorative artwork paths for the first-pass demo-disc menu.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    public sealed class DemoDiscMenuTheme {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the authored title font path.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public string TitleFontPath => \"Fonts/DemoDiscTitle.ttf\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the authored body font path.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public string BodyFontPath => \"Fonts/DemoDiscBody.ttf\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the decorative logo texture path.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public string LogoTexturePath => \"Images/Menu/helengine-logo.png\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the decorative logo width in authored canvas pixels.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int LogoWidth => 220;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the decorative logo height in authored canvas pixels.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int LogoHeight => 220;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the decorative logo bottom margin in authored canvas pixels.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int LogoBottomMargin => 36;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the decorative logo right margin in authored canvas pixels.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int LogoRightMargin => 44;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the top margin used by the platform-info overlay.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int PlatformInfoTopMargin => 28;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the right margin used by the platform-info overlay.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int PlatformInfoRightMargin => 44;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the vertical spacing between the platform-name and platform-version lines.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public int PlatformInfoLineSpacing => 6;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the primary lilac background color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 BackgroundColor => new byte4(30, 17, 41, 255);");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the panel surface color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 SurfaceColor => new byte4(60, 41, 76, 232);");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the panel border color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 SurfaceBorderColor => new byte4(135, 94, 163, 255);");
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the primary accent color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 AccentColor => new byte4(201, 147, 255, 255);");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the secondary accent color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 AccentSecondaryColor => new byte4(118, 219, 209, 255);");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the primary text color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 TextColor => new byte4(249, 243, 255, 255);");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the muted text color.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public byte4 MutedTextColor => new byte4(211, 198, 228, 255);");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the city-side menu-definition provider source code.
        /// </summary>
        /// <returns>Generated menu-definition provider source text.</returns>
        string BuildMenuDefinitionProviderSource() {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("namespace city.menu {");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Produces the first-pass demo-disc menu definition used by the generated city menu scene.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    public sealed class DemoDiscMenuDefinitionProvider : IMenuDefinitionProvider {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Builds the complete demo-disc menu definition.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        /// <returns>Menu definition consumed by the runtime menu host.</returns>");
            builder.AppendLine("        public MenuDefinition CreateMenuDefinition() {");
            builder.AppendLine("            DemoDiscMenuTheme theme = new DemoDiscMenuTheme();");
            builder.AppendLine("            DemoDiscSceneCatalog sceneCatalog = new DemoDiscSceneCatalog();");
            builder.AppendLine("            return new MenuDefinition(");
            builder.AppendLine("                string.Empty,");
            builder.AppendLine("                string.Empty,");
            builder.AppendLine("                \"main\",");
            builder.AppendLine("                theme.TitleFontPath,");
            builder.AppendLine("                theme.BodyFontPath,");
            builder.AppendLine("                theme.BackgroundColor,");
            builder.AppendLine("                theme.SurfaceColor,");
            builder.AppendLine("                theme.SurfaceBorderColor,");
            builder.AppendLine("                theme.AccentColor,");
            builder.AppendLine("                theme.AccentSecondaryColor,");
            builder.AppendLine("                theme.TextColor,");
            builder.AppendLine("                theme.MutedTextColor,");
            builder.AppendLine("                new[] {");
            builder.AppendLine("                    new MenuPanelDefinition(");
            builder.AppendLine("                        \"main\",");
            builder.AppendLine("                        \"Main Menu\",");
            builder.AppendLine("                        \"Pick a destination or peek at the menu shell.\",");
            builder.AppendLine("                        6,");
            builder.AppendLine("                        new[] {");
            builder.AppendLine("                            new MenuItemDefinition(\"main-scenes\", \"Select Scene\", \"Browse the curated demo-disc lineup.\", true, new MenuActionDefinition(MenuActionKind.OpenPanel, \"scene-select\")),");
            builder.AppendLine("                            new MenuItemDefinition(\"main-options\", \"Options\", \"Preview the reusable options shell layout.\", true, new MenuActionDefinition(MenuActionKind.OpenPanel, \"options\"))");
            builder.AppendLine("                        }),");
            builder.AppendLine("                    new MenuPanelDefinition(");
            builder.AppendLine("                        \"scene-select\",");
            builder.AppendLine("                        \"Select Scene\",");
            builder.AppendLine("                        \"Every entry here is explicitly curated and ordered from city-side code.\",");
            builder.AppendLine("                        4,");
            builder.AppendLine("                        sceneCatalog.CreateSceneItems()),");
            builder.AppendLine("                    new MenuPanelDefinition(");
            builder.AppendLine("                        \"options\",");
            builder.AppendLine("                        \"Options\",");
            builder.AppendLine("                        \"Polished shell for future settings categories.\",");
            builder.AppendLine("                        6,");
            builder.AppendLine("                        new[] {");
            builder.AppendLine("                            new MenuItemDefinition(\"options-display\", \"Display\", \"Placeholder row for future video settings.\", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),");
            builder.AppendLine("                            new MenuItemDefinition(\"options-audio\", \"Audio\", \"Placeholder row for future volume settings.\", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),");
            builder.AppendLine("                            new MenuItemDefinition(\"options-controls\", \"Controls\", \"Placeholder row for future input remapping.\", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),");
            builder.AppendLine("                            new MenuItemDefinition(\"options-back\", \"Back\", \"Returns to the main menu.\", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))");
            builder.AppendLine("                        })");
            builder.AppendLine("                },");
            builder.AppendLine("                new MenuOverlayImageDefinition(");
            builder.AppendLine("                    theme.LogoTexturePath,");
            builder.AppendLine("                    theme.LogoWidth,");
            builder.AppendLine("                    theme.LogoHeight,");
            builder.AppendLine("                    theme.LogoBottomMargin,");
            builder.AppendLine("                    theme.LogoRightMargin),");
            builder.AppendLine("                new MenuPlatformInfoDefinition(");
            builder.AppendLine("                    theme.PlatformInfoTopMargin,");
            builder.AppendLine("                    theme.PlatformInfoRightMargin,");
            builder.AppendLine("                    theme.PlatformInfoLineSpacing));");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the city-side platform-info overlay runtime component source code.
        /// </summary>
        /// <returns>Generated runtime component source text.</returns>
        string BuildPlatformInfoTextComponentSource() {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("namespace city.menu {");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Applies the current platform name and version to the demo-disc menu overlay text.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    public sealed class PlatformInfoTextComponent : UpdateComponent {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Stable child entity name used for the platform name text line.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        const string PlatformNameTextEntityName = \"DemoDiscPlatformInfoNameText\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Stable child entity name used for the platform version text line.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        const string PlatformVersionTextEntityName = \"DemoDiscPlatformInfoVersionText\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Tracks whether the overlay text has already been populated.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        bool Applied;");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Applies the current platform name and version to the child text entities once.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public override void Update() {");
            builder.AppendLine("            base.Update();");
            builder.AppendLine();
            builder.AppendLine("            if (Applied) {");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
            builder.AppendLine("            if (Core.Instance == null) {");
            builder.AppendLine("                throw new InvalidOperationException(\"PlatformInfoTextComponent requires a core instance.\");");
            builder.AppendLine("            } else if (Parent == null) {");
            builder.AppendLine("                throw new InvalidOperationException(\"PlatformInfoTextComponent requires a parent entity.\");");
            builder.AppendLine("            } else if (Parent.Children == null || Parent.Children.Count < 2) {");
            builder.AppendLine("                throw new InvalidOperationException(\"Platform-info overlay requires two child text entities.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            Entity nameEntity = Parent.Children[0];");
            builder.AppendLine("            Entity versionEntity = Parent.Children[1];");
            builder.AppendLine("            TextComponent nameText = FindTextComponent(nameEntity);");
            builder.AppendLine("            TextComponent versionText = FindTextComponent(versionEntity);");
            builder.AppendLine("            string platformName = Core.Instance.PlatformInfo.Name;");
            builder.AppendLine("            string platformVersion = Core.Instance.PlatformInfo.Version;");
            builder.AppendLine("            ApplyText(nameEntity, nameText, platformName, 0f);");
            builder.AppendLine("            ApplyText(versionEntity, versionText, platformVersion, nameText.Size.Y + 6f);");
            builder.AppendLine("            Applied = true;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Applies one platform-info text line to one child entity.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        /// <param name=\"entity\">Child text entity that should be updated.</param>");
            builder.AppendLine("        /// <param name=\"textComponent\">Text component that renders the value.</param>");
            builder.AppendLine("        /// <param name=\"text\">Text content to display.</param>");
            builder.AppendLine("        /// <param name=\"topOffset\">Vertical offset from the overlay container.</param>");
            builder.AppendLine("        void ApplyText(Entity entity, TextComponent textComponent, string text, float topOffset) {");
            builder.AppendLine("            if (entity == null) {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(entity));");
            builder.AppendLine("            } else if (textComponent == null) {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(textComponent));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            textComponent.Text = text;");
            builder.AppendLine("            float2 measuredSize = textComponent.Font.MeasureString(text);");
            builder.AppendLine("            textComponent.Size = new int2((int)Math.Ceiling(measuredSize.X), (int)Math.Ceiling(measuredSize.Y));");
            builder.AppendLine("            entity.LocalPosition = new float3(-textComponent.Size.X, topOffset, 0f);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Finds the text component attached to one child entity.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        /// <param name=\"entity\">Child entity whose text component should be returned.</param>");
            builder.AppendLine("        /// <returns>Attached text component.</returns>");
            builder.AppendLine("        TextComponent FindTextComponent(Entity entity) {");
            builder.AppendLine("            if (entity == null) {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(entity));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            for (int index = 0; index < entity.Components.Count; index++) {");
            builder.AppendLine("                if (entity.Components[index] is TextComponent textComponent) {");
            builder.AppendLine("                    return textComponent;");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            throw new InvalidOperationException(\"Platform-info overlay child must include a text component.\");");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}

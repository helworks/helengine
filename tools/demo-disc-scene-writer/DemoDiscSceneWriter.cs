using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.editor;
using helengine.projectfile;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Writes the city-project assets and configuration required by the first-pass demo-disc menu.
    /// </summary>
    public sealed class DemoDiscSceneWriter {
        /// <summary>
        /// Scene id used by the generated menu scene.
        /// </summary>
        const string MenuSceneId = "Scenes/DemoDiscMainMenu.helen";

        /// <summary>
        /// Curated playable scene ids packaged by the demo-disc build.
        /// </summary>
        static readonly string[] CuratedSceneIds = new[] {
            MenuSceneId,
            "NewScene.helen",
            "scenes/physics/test_scene_dynamic_stack_boxes.helen",
            "scenes/physics/test_scene_dynamic_sphere_ramp.helen",
            "scenes/physics/test_scene_character_steps.helen",
            "scenes/physics/test_scene_character_slope.helen",
            "scenes/physics/test_scene_character_moving_platform.helen",
            "scenes/physics/test_scene_kinematic_push.helen",
            "scenes/physics/test_scene_mesh_ground_stability.helen",
            "scenes/physics/test_scene_trigger_volume.helen"
        };

        /// <summary>
        /// Font writer used to build packaged menu fonts.
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

            string providerTypeName = ResolveProviderTypeName(fullProjectRootPath);
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
        }

        /// <summary>
        /// Writes the packaged title and body fonts consumed by the generated menu definition.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the city project.</param>
        void WriteMenuFonts(string assetsRootPath) {
            string fontsRootPath = Path.Combine(assetsRootPath, "Fonts");
            Directory.CreateDirectory(fontsRootPath);
            FontWriter.WriteFont(Path.Combine(fontsRootPath, "DemoDiscTitle.hefont"), "Georgia", 34f, FontStyle.Bold);
            FontWriter.WriteFont(Path.Combine(fontsRootPath, "DemoDiscBody.hefont"), "Trebuchet MS", 18f, FontStyle.Regular);
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

            string scenePath = Path.Combine(assetsRootPath, MenuSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Menu scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            MenuDefinition definition = BuildMenuDefinition();
            SceneAsset sceneAsset = SceneBuildService.BuildSceneAsset(MenuSceneId, providerTypeName, definition);

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
            windowsPlatform["selectedCodeModuleIds"] = new JsonArray("gameplay");
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
        /// Resolves the assembly-qualified menu provider type name that matches the editor script-build output.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Assembly-qualified provider type name persisted into the generated menu scene.</returns>
        string ResolveProviderTypeName(string projectRootPath) {
            string projectAssemblyName = ResolveProjectAssemblyName(projectRootPath);
            return $"city.menu.DemoDiscMenuDefinitionProvider, {projectAssemblyName}";
        }

        /// <summary>
        /// Resolves the generated script assembly name the editor will use for the supplied project.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Sanitized project assembly name used by the editor scripting solution.</returns>
        string ResolveProjectAssemblyName(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string projectFilePath = Path.Combine(projectRootPath, "project.heproj");
            if (!File.Exists(projectFilePath)) {
                throw new InvalidOperationException($"Project file was not found: {projectFilePath}");
            }

            ProjectFileReadResult readResult = new ProjectFileReader().ReadAsync(projectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded || readResult.Document == null) {
                throw new InvalidOperationException($"Project file '{projectFilePath}' could not be read for menu provider generation.");
            }
            if (string.IsNullOrWhiteSpace(readResult.Document.Name)) {
                throw new InvalidOperationException($"Project file '{projectFilePath}' must declare a project name.");
            }

            string projectAssemblyName = SanitizeProjectIdentifier(readResult.Document.Name);
            if (string.IsNullOrWhiteSpace(projectAssemblyName)) {
                throw new InvalidOperationException($"Project name '{readResult.Document.Name}' did not produce a valid script assembly name.");
            }

            return projectAssemblyName;
        }

        /// <summary>
        /// Converts one project name into the assembly identifier used by the editor-generated scripting solution.
        /// </summary>
        /// <param name="projectName">Original project display name.</param>
        /// <returns>Sanitized assembly identifier.</returns>
        string SanitizeProjectIdentifier(string projectName) {
            if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }

            StringBuilder builder = new StringBuilder(projectName.Length);
            for (int index = 0; index < projectName.Length; index++) {
                char character = projectName[index];
                if (char.IsLetterOrDigit(character)) {
                    builder.Append(character);
                } else {
                    builder.Append('_');
                }
            }

            string sanitizedValue = builder.ToString().Trim('_');
            while (sanitizedValue.Contains("__", StringComparison.Ordinal)) {
                sanitizedValue = sanitizedValue.Replace("__", "_", StringComparison.Ordinal);
            }

            return sanitizedValue;
        }

        /// <summary>
        /// Builds the shared baked menu definition used by both the scene writer and future editor rebuild entrypoints.
        /// </summary>
        /// <returns>Baked demo-disc menu definition.</returns>
        MenuDefinition BuildMenuDefinition() {
            return new MenuDefinition(
                "Helengine Demo Disc",
                "Lilac nights, bright experiments, and a little street grit.",
                "main",
                "Fonts/DemoDiscTitle.hefont",
                "Fonts/DemoDiscBody.hefont",
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
                        7,
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
                });
        }

        /// <summary>
        /// Creates the curated scene-selection menu items baked into the demo menu scene.
        /// </summary>
        /// <returns>Curated scene-selection items.</returns>
        MenuItemDefinition[] CreateSceneSelectItems() {
            return new[] {
                new MenuItemDefinition("scene-new-scene", "Neon Crossroads", "Loads the original city sandbox scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "NewScene.helen")),
                new MenuItemDefinition("scene-stack-boxes", "Stack Boxes", "Physics stress test with stacked dynamic boxes.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_dynamic_stack_boxes.helen")),
                new MenuItemDefinition("scene-ramp", "Sphere Ramp", "Dynamic sphere ramp test for broad motion and bounce.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_dynamic_sphere_ramp.helen")),
                new MenuItemDefinition("scene-steps", "Character Steps", "Character controller step traversal test.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_character_steps.helen")),
                new MenuItemDefinition("scene-slope", "Character Slope", "Character controller slope handling test.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_character_slope.helen")),
                new MenuItemDefinition("scene-platform", "Moving Platform", "Character moving-platform interaction test.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_character_moving_platform.helen")),
                new MenuItemDefinition("scene-push", "Kinematic Push", "Kinematic pusher interaction test.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_kinematic_push.helen")),
                new MenuItemDefinition("scene-ground", "Ground Stability", "Ground contact and settling stability test.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_mesh_ground_stability.helen")),
                new MenuItemDefinition("scene-trigger", "Trigger Volume", "Trigger enter and exit test scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "scenes/physics/test_scene_trigger_volume.helen")),
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
            builder.AppendLine("                new MenuItemDefinition(\"scene-new-scene\", \"Neon Crossroads\", \"Loads the original city sandbox scene.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"NewScene.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-stack-boxes\", \"Stack Boxes\", \"Physics stress test with stacked dynamic boxes.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_dynamic_stack_boxes.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-ramp\", \"Sphere Ramp\", \"Dynamic sphere ramp test for broad motion and bounce.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_dynamic_sphere_ramp.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-steps\", \"Character Steps\", \"Character controller step traversal test.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_character_steps.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-slope\", \"Character Slope\", \"Character controller slope handling test.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_character_slope.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-platform\", \"Moving Platform\", \"Character moving-platform interaction test.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_character_moving_platform.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-push\", \"Kinematic Push\", \"Kinematic pusher interaction test.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_kinematic_push.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-ground\", \"Ground Stability\", \"Ground contact and settling stability test.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_mesh_ground_stability.helen\")),");
            builder.AppendLine("                new MenuItemDefinition(\"scene-trigger\", \"Trigger Volume\", \"Trigger enter and exit test scene.\", true, new MenuActionDefinition(MenuActionKind.LoadScene, \"scenes/physics/test_scene_trigger_volume.helen\")),");
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
            builder.AppendLine("    /// Stores reusable colors and font paths for the first-pass demo-disc menu.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    public sealed class DemoDiscMenuTheme {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the packaged title font path.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public string TitleFontPath => \"Fonts/DemoDiscTitle.hefont\";");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Gets the packaged body font path.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public string BodyFontPath => \"Fonts/DemoDiscBody.hefont\";");
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
            builder.AppendLine("                \"Helengine Demo Disc\",");
            builder.AppendLine("                \"Lilac nights, bright experiments, and a little street grit.\",");
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
            builder.AppendLine("                        7,");
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
            builder.AppendLine("                });");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}

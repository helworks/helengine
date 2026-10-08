using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the graphic template catalog: the built-in kinetic typography is valid and published with slots,
    /// parameters and layouts; project <c>.hgraphic</c> files load next to the built-ins; broken definitions are rejected.
    /// </summary>
    public class GraphicTemplateCatalogTests {
        /// <summary>
        /// Visible build-owned directory for the project fixtures these tests write.
        /// </summary>
        const string FixtureRoot = "C:/dev/helworks/builds/helengine/graphics-tests";

        /// <summary>
        /// The four built-ins register and publish their contract into the capability document.
        /// </summary>
        [Fact]
        public void BuiltIns_ArePublishedWithSlotsParametersAndLayouts() {
            GraphicTemplateCatalog catalog = GraphicTemplateCatalog.CreateBuiltIn();
            MediaCapabilities capabilities = MediaCapabilities.Basic();

            catalog.Publish(capabilities);
            JsonElement json = capabilities.Describe();

            Assert.Equal(["contrast_chain", "list_build", "highlight_word", "strike_replace"], catalog.All.Select(entry => entry.Template.TemplateId));
            JsonElement chain = json.GetProperty("graphic_templates").EnumerateArray().First(item => item.GetProperty("id").GetString() == "contrast_chain");
            Assert.Equal(1, chain.GetProperty("version").GetInt32());
            Assert.Equal(["vertical", "horizontal"], chain.GetProperty("layouts").EnumerateArray().Select(item => item.GetString()));
            JsonElement items = chain.GetProperty("slots").EnumerateArray().First(slot => slot.GetProperty("name").GetString() == "items");
            Assert.Equal("text_list", items.GetProperty("kind").GetString());
            Assert.Equal(4, items.GetProperty("max_count").GetInt32());
            Assert.Equal("color", chain.GetProperty("parameters").GetProperty("accent_color").GetProperty("type").GetString());
            Assert.Equal("boolean", chain.GetProperty("parameters").GetProperty("dim_previous").GetProperty("type").GetString());
            Assert.Contains("≠", chain.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("default").GetString()));
        }

        /// <summary>
        /// A template saved into a project's assets loads after the built-ins and is marked as a project template.
        /// </summary>
        [Fact]
        public void CreateForProject_LoadsProjectTemplates() {
            string project = Project("catalog");
            GraphicTemplateAsset custom = BuiltInGraphicTemplates.ContrastChain();
            custom.TemplateId = "versus";
            custom.TemplateVersion = 3;
            GraphicTemplateFile.Save(Path.Combine(project, "assets", "graphics", "versus" + GraphicTemplateAsset.FileExtension), custom);

            GraphicTemplateCatalog catalog = GraphicTemplateCatalog.CreateForProject(project);

            GraphicTemplateCatalogEntry entry = catalog.All[^1];
            Assert.True(entry.IsProjectTemplate);
            Assert.Equal("versus", entry.Template.TemplateId);
            Assert.NotNull(catalog.Find("versus", 3));
            Assert.Null(catalog.Find("versus", 1));
            Assert.Equal(custom.Elements.Length, entry.Template.Elements.Length);
        }

        /// <summary>
        /// A project template cannot replace an engine template.
        /// </summary>
        [Fact]
        public void CreateForProject_RejectsDuplicateIds() {
            string project = Project("clash");
            GraphicTemplateFile.Save(Path.Combine(project, "assets", "clash" + GraphicTemplateAsset.FileExtension), BuiltInGraphicTemplates.ListBuild());

            Assert.Throws<InvalidDataException>(() => GraphicTemplateCatalog.CreateForProject(project));
        }

        /// <summary>
        /// Each broken definition is rejected with a validation error.
        /// </summary>
        /// <param name="mutation">Name of the mutation to apply to a valid template.</param>
        [Theory]
        [InlineData("unknown_curve")]
        [InlineData("unknown_color_parameter")]
        [InlineData("missing_items_slot")]
        [InlineData("unordered_keyframes")]
        [InlineData("opacity_out_of_range")]
        [InlineData("separator_without_slot")]
        [InlineData("reveal_on_text")]
        public void Validate_RejectsBrokenTemplates(string mutation) {
            GraphicTemplateAsset template = BuiltInGraphicTemplates.ContrastChain();
            GraphicTemplateElementAsset item = template.Elements[0];
            switch (mutation) {
                case "unknown_curve": item.Tracks[0].Keyframes[0].Curve = "bounce.v9"; break;
                case "unknown_color_parameter": template.Elements[1].ColorParameter = "missing"; break;
                case "missing_items_slot": template.Slots = template.Slots.Skip(1).ToArray(); break;
                case "unordered_keyframes": item.Tracks[0].Keyframes[1].OffsetSeconds = -1; break;
                case "opacity_out_of_range": item.Tracks[0].Keyframes[1].Value = 2; break;
                case "separator_without_slot": template.Slots = template.Slots.Take(1).ToArray(); break;
                case "reveal_on_text": item.Tracks[1].Property = GraphicAnimatedProperty.Reveal; break;
            }

            Assert.Throws<InvalidDataException>(() => GraphicTemplateValidator.Validate(template));
        }

        /// <summary>
        /// Creates an empty project directory with a project file under the build-owned fixture root.
        /// </summary>
        /// <param name="name">Fixture name.</param>
        /// <returns>Project root.</returns>
        static string Project(string name) {
            string project = Path.Combine(FixtureRoot, name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(project, "assets", "graphics"));
            File.WriteAllText(Path.Combine(project, "project.heproj"), "{}");
            return project;
        }
    }
}

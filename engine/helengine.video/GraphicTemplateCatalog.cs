using helengine.media;

namespace helengine.video {
    /// <summary>
    /// The single list of graphic templates one invocation can expand: the engine's built-in kinetic typography plus,
    /// optionally, the <c>.hgraphic</c> files of a Helengine project. The capability catalog publishes it and the edit
    /// compiler expands from it, so a template is either available everywhere or nowhere.
    /// </summary>
    public sealed class GraphicTemplateCatalog {
        /// <summary>
        /// Project directory holding the template definitions, relative to the project root.
        /// </summary>
        public const string ProjectAssetsDirectoryName = "assets";

        /// <summary>
        /// Registered entries in registration order.
        /// </summary>
        readonly List<GraphicTemplateCatalogEntry> Entries = new List<GraphicTemplateCatalogEntry>();

        /// <summary>
        /// Gets the registered entries in registration order.
        /// </summary>
        public IReadOnlyList<GraphicTemplateCatalogEntry> All {
            get {
                return Entries;
            }
        }

        /// <summary>
        /// Creates a catalog holding only the engine's built-in templates.
        /// </summary>
        /// <returns>Catalog with every built-in template.</returns>
        public static GraphicTemplateCatalog CreateBuiltIn() {
            GraphicTemplateCatalog catalog = new GraphicTemplateCatalog();
            foreach (GraphicTemplateAsset template in BuiltInGraphicTemplates.All()) {
                catalog.Add(template, false);
            }
            return catalog;
        }

        /// <summary>
        /// Creates a catalog with the built-in templates plus every template a Helengine project ships.
        /// </summary>
        /// <param name="projectDirectory">Project root containing <c>project.heproj</c> and <c>assets</c>.</param>
        /// <returns>Catalog with built-in and project templates.</returns>
        public static GraphicTemplateCatalog CreateForProject(string projectDirectory) {
            GraphicTemplateCatalog catalog = CreateBuiltIn();
            catalog.AddProject(projectDirectory);
            return catalog;
        }

        /// <summary>
        /// Registers one template definition after validating it.
        /// </summary>
        /// <param name="template">Template definition.</param>
        /// <param name="isProjectTemplate">Whether the template came from a user project.</param>
        public void Add(GraphicTemplateAsset template, bool isProjectTemplate) {
            GraphicTemplateValidator.Validate(template);
            if (Entries.Any(entry => entry.Template.TemplateId == template.TemplateId)) {
                throw new InvalidDataException($"Graphic template id '{template.TemplateId}' is registered twice; project templates cannot replace engine templates.");
            }
            Entries.Add(new GraphicTemplateCatalogEntry(template, isProjectTemplate));
        }

        /// <summary>
        /// Registers every <c>.hgraphic</c> file under a project's <c>assets</c> directory, in ordinal path order.
        /// </summary>
        /// <param name="projectDirectory">Project root containing <c>project.heproj</c>.</param>
        public void AddProject(string projectDirectory) {
            string root = Path.GetFullPath(projectDirectory);
            if (!File.Exists(Path.Combine(root, "project.heproj"))) {
                throw new DirectoryNotFoundException($"'{root}' is not a Helengine project: project.heproj is missing.");
            }
            string assets = Path.Combine(root, ProjectAssetsDirectoryName);
            if (!Directory.Exists(assets)) {
                return;
            }
            foreach (string path in Directory.GetFiles(assets, "*" + GraphicTemplateAsset.FileExtension, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)) {
                Add(GraphicTemplateFile.Load(path), true);
            }
        }

        /// <summary>
        /// Finds the template registered under an id and version.
        /// </summary>
        /// <param name="templateId">Template id.</param>
        /// <param name="version">Pinned template version.</param>
        /// <returns>Matching template, or null when no template has that id and version.</returns>
        public GraphicTemplateAsset Find(string templateId, int version) {
            return Entries.FirstOrDefault(entry => entry.Template.TemplateId == templateId && entry.Template.TemplateVersion == version)?.Template;
        }

        /// <summary>
        /// Describes every template for the capability catalog, without animation details.
        /// </summary>
        /// <returns>Descriptors in registration order.</returns>
        public List<MediaGraphicTemplateDescriptor> Describe() {
            return Entries.Select(entry => Describe(entry.Template)).ToList();
        }

        /// <summary>
        /// Publishes every template into a capability catalog, replacing any templates it listed.
        /// </summary>
        /// <param name="capabilities">Capability catalog to fill.</param>
        public void Publish(MediaCapabilities capabilities) {
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }
            capabilities.GraphicTemplates = Describe();
        }

        /// <summary>
        /// Describes one template: identity, slots, typed parameters and layout names.
        /// </summary>
        /// <param name="template">Validated template.</param>
        /// <returns>Capability descriptor.</returns>
        static MediaGraphicTemplateDescriptor Describe(GraphicTemplateAsset template) {
            MediaGraphicTemplateDescriptor descriptor = new MediaGraphicTemplateDescriptor {
                Id = template.TemplateId,
                Version = template.TemplateVersion,
                DisplayName = template.DisplayName,
                Description = template.Description,
                Layouts = template.Layouts.Select(layout => layout.Direction == GraphicLayoutDirection.Vertical ? "vertical" : "horizontal").ToList()
            };
            foreach (GraphicTemplateSlotAsset slot in template.Slots) {
                descriptor.Slots.Add(new MediaGraphicSlotDescriptor {
                    Name = slot.Name,
                    Kind = slot.Kind switch { GraphicTemplateSlotKind.TextList => "text_list", GraphicTemplateSlotKind.Text => "text", _ => "item_index" },
                    Description = slot.Description,
                    Required = slot.Required,
                    MinCount = slot.MinCount,
                    MaxCount = slot.MaxCount,
                    MaxChars = slot.MaxChars,
                    Default = slot.DefaultText ?? ""
                });
            }
            foreach (EffectParameterAsset parameter in template.Parameters) {
                descriptor.Parameters.Add(parameter.Name, VideoParameterDescriptors.Describe(parameter));
            }
            return descriptor;
        }
    }
}

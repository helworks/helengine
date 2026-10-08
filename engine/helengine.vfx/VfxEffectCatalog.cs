namespace helengine.vfx {
    /// <summary>
    /// The single list of effects one tool invocation can execute: the engine's built-in effects plus, optionally, the
    /// <c>.heffect</c> files of a Helengine project. The offline exporter, the composition compositor and the capability
    /// catalog all read from the same instance, so an effect is either available everywhere or nowhere.
    /// </summary>
    public sealed class VfxEffectCatalog {
        /// <summary>
        /// Project directory holding the effects' shaders and definitions, relative to the project root.
        /// </summary>
        public const string ProjectAssetsDirectoryName = "assets";

        /// <summary>
        /// Registered entries in registration order.
        /// </summary>
        readonly List<VfxEffectCatalogEntry> Entries = new List<VfxEffectCatalogEntry>();

        /// <summary>
        /// Gets the registered entries in registration order.
        /// </summary>
        public IReadOnlyList<VfxEffectCatalogEntry> All {
            get {
                return Entries;
            }
        }

        /// <summary>
        /// Gets every registered effect id in registration order.
        /// </summary>
        public IReadOnlyList<string> KnownIds {
            get {
                return Entries.Select(entry => entry.Effect.EffectId).ToList();
            }
        }

        /// <summary>
        /// Creates a catalog holding only the engine's built-in effects, whose shaders ship under the application directory.
        /// </summary>
        /// <returns>Catalog with every built-in effect.</returns>
        public static VfxEffectCatalog CreateBuiltIn() {
            VfxEffectCatalog catalog = new VfxEffectCatalog();
            foreach (EffectAsset effect in BuiltInVfxEffects.All()) {
                catalog.Add(effect, AppContext.BaseDirectory, false);
            }
            return catalog;
        }

        /// <summary>
        /// Creates a catalog with the built-in effects plus every effect a Helengine project ships.
        /// </summary>
        /// <param name="projectDirectory">Project root containing <c>project.heproj</c> and <c>assets</c>.</param>
        /// <returns>Catalog with built-in and project effects.</returns>
        public static VfxEffectCatalog CreateForProject(string projectDirectory) {
            VfxEffectCatalog catalog = CreateBuiltIn();
            catalog.AddProject(projectDirectory);
            return catalog;
        }

        /// <summary>
        /// Registers one effect definition after validating it.
        /// </summary>
        /// <param name="effect">Effect definition.</param>
        /// <param name="shaderRoot">Directory its pass shader paths are relative to.</param>
        /// <param name="isProjectEffect">Whether the effect came from a user project.</param>
        public void Add(EffectAsset effect, string shaderRoot, bool isProjectEffect) {
            VfxEffectValidator.Validate(effect);
            if (Entries.Any(entry => entry.Effect.EffectId == effect.EffectId)) {
                throw new InvalidDataException($"Effect id '{effect.EffectId}' is registered twice; project effects cannot replace engine effects.");
            }
            Entries.Add(new VfxEffectCatalogEntry(effect, shaderRoot, isProjectEffect));
        }

        /// <summary>
        /// Registers every <c>.heffect</c> file under a project's <c>assets</c> directory; their shader paths resolve
        /// against that same directory, matching how project shader asset ids are formed.
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
            foreach (string path in Directory.GetFiles(assets, "*" + EffectAsset.FileExtension, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)) {
                Add(VfxEffectFile.Load(path), assets, true);
            }
        }

        /// <summary>
        /// Finds the entry registered under an effect id.
        /// </summary>
        /// <param name="effectId">Effect id to look up.</param>
        /// <returns>Matching entry.</returns>
        /// <exception cref="InvalidOperationException">No effect uses that id.</exception>
        public VfxEffectCatalogEntry Resolve(string effectId) {
            VfxEffectCatalogEntry entry = Entries.FirstOrDefault(candidate => candidate.Effect.EffectId == effectId);
            if (entry == null) {
                throw new InvalidOperationException($"Unknown effect '{effectId}'. Known effects: {string.Join(", ", KnownIds)}.");
            }
            return entry;
        }

        /// <summary>
        /// Reports whether an effect id is registered.
        /// </summary>
        /// <param name="effectId">Effect id to look up.</param>
        /// <returns>True when the id resolves.</returns>
        public bool Contains(string effectId) {
            return Entries.Any(entry => entry.Effect.EffectId == effectId);
        }
    }
}

using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Verifies the single effect catalog: built-in registration, project loading and id collision rules.
    /// </summary>
    public class VfxEffectCatalogTests {
        /// <summary>
        /// The built-in catalog exposes exactly the effects the engine ships, resolving shaders from the app directory.
        /// </summary>
        [Fact]
        public void CreateBuiltIn_RegistersShippedEffects() {
            VfxEffectCatalog catalog = VfxEffectCatalog.CreateBuiltIn();
            Assert.Equal(new[] { "rainbow-expand", "rainbow-aura", "depth-composite" }, catalog.KnownIds);
            VfxEffectCatalogEntry entry = catalog.Resolve("rainbow-expand");
            Assert.False(entry.IsProjectEffect);
            Assert.Equal(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "shaders/effects/RainbowExpand.hlsl")), entry.ResolveShaderPath(entry.Effect.Passes[0]));
        }

        /// <summary>
        /// A project's .heffect files load next to the built-ins and resolve shaders inside the project's assets.
        /// </summary>
        [Fact]
        public void CreateForProject_LoadsProjectEffectsFromAssets() {
            string project = CreateProject();
            try {
                VfxEffectFile.Save(Path.Combine(project, "assets", "vfx", "blur" + EffectAsset.FileExtension), VfxEffectValidatorTests.TwoPass());

                VfxEffectCatalog catalog = VfxEffectCatalog.CreateForProject(project);

                VfxEffectCatalogEntry entry = catalog.Resolve("test-blur");
                Assert.True(entry.IsProjectEffect);
                Assert.Equal(Path.Combine(project, "assets", "shaders", "Blur.hlsl"), entry.ResolveShaderPath(entry.Effect.Passes[0]));
                Assert.Equal(4, catalog.All.Count);
            } finally {
                Directory.Delete(project, true);
            }
        }

        /// <summary>
        /// A project effect may not shadow an engine effect; compositions would otherwise change meaning per project.
        /// </summary>
        [Fact]
        public void CreateForProject_DuplicateBuiltInId_Throws() {
            string project = CreateProject();
            try {
                EffectAsset clash = VfxEffectValidatorTests.TwoPass();
                clash.EffectId = "rainbow-expand";
                VfxEffectFile.Save(Path.Combine(project, "assets", "clash" + EffectAsset.FileExtension), clash);

                Assert.Throws<InvalidDataException>(() => VfxEffectCatalog.CreateForProject(project));
            } finally {
                Directory.Delete(project, true);
            }
        }

        /// <summary>
        /// A directory without project.heproj is rejected instead of silently loading nothing.
        /// </summary>
        [Fact]
        public void CreateForProject_MissingProjectFile_Throws() {
            string directory = Directory.CreateTempSubdirectory("helengine-vfx-noproject-").FullName;
            try {
                Assert.Throws<DirectoryNotFoundException>(() => VfxEffectCatalog.CreateForProject(directory));
            } finally {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Creates a throwaway project directory with a project file and an assets folder.
        /// </summary>
        /// <returns>Absolute project directory.</returns>
        static string CreateProject() {
            string project = Directory.CreateTempSubdirectory("helengine-vfx-project-").FullName;
            File.WriteAllText(Path.Combine(project, "project.heproj"), "{}");
            Directory.CreateDirectory(Path.Combine(project, "assets", "vfx"));
            return project;
        }
    }
}

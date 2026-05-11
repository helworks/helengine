using helengine.files;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies authored city rendering scenes keep cube-grid showcase entities aligned to identity orientation.
    /// </summary>
    public sealed class CityRenderingSceneAuthoringTests {
        /// <summary>
        /// Gets the local city project path used by environment-backed rendering-scene regressions.
        /// </summary>
        const string CityProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Ensures the authored colored cube-grid scene stores identity orientation for each generated showcase cube.
        /// </summary>
        [Fact]
        public void DeserializeCityColoredCubeGridSceneAsset_AuthoredCubeOrientationsAreIdentity() {
            SceneAsset sceneAsset = ReadSceneAsset("colored_cube_grid.helen");

            AssertCubeOrientationsAreIdentity(sceneAsset, "ColoredCubeGridCube");
        }

        /// <summary>
        /// Ensures the authored textured cube-grid scene stores identity orientation for each generated showcase cube.
        /// </summary>
        [Fact]
        public void DeserializeCityTexturedCubeGridSceneAsset_AuthoredCubeOrientationsAreIdentity() {
            SceneAsset sceneAsset = ReadSceneAsset("textured_cube_grid.helen");

            AssertCubeOrientationsAreIdentity(sceneAsset, "TexturedCubeGridCube");
        }

        /// <summary>
        /// Ensures the authored spotlight street-slice scene references generated racer companion materials through their real `.helmat` assets instead of import-settings sidecars.
        /// </summary>
        [Fact]
        public void DeserializeCitySpotlightStreetSliceSceneAsset_RacerMaterialReferencesDoNotUseHelmatHassetSidecars() {
            SceneAsset sceneAsset = ReadSceneAsset("spotlight_street_slice.helen");

            Assert.DoesNotContain(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => !string.IsNullOrWhiteSpace(reference.RelativePath)
                    && reference.RelativePath.Contains(".helmat.hasset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => string.Equals(reference.RelativePath, "models/Riemers/racer/x3ds_mat_ruedas.helmat", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads one city rendering scene asset from the authored project scene folder.
        /// </summary>
        /// <param name="sceneFileName">File name of the authored rendering scene.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(CityProjectRootPath, "assets", "scenes", "rendering", sceneFileName);
            Assert.True(File.Exists(scenePath));

            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>
        /// Asserts all generated showcase cubes beneath the supplied scene share identity local orientation.
        /// </summary>
        /// <param name="sceneAsset">Scene asset whose cube entities should be inspected.</param>
        /// <param name="cubeNamePrefix">Stable name prefix used by the generated cube entities.</param>
        void AssertCubeOrientationsAreIdentity(SceneAsset sceneAsset, string cubeNamePrefix) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            } else if (string.IsNullOrWhiteSpace(cubeNamePrefix)) {
                throw new ArgumentException("Cube name prefix must be provided.", nameof(cubeNamePrefix));
            }

            List<SceneEntityAsset> cubeEntities = new List<SceneEntityAsset>();
            CollectCubeEntities(sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>(), cubeNamePrefix, cubeEntities);

            Assert.Equal(16, cubeEntities.Count);
            for (int index = 0; index < cubeEntities.Count; index++) {
                Assert.Equal(float4.Identity, cubeEntities[index].LocalOrientation);
            }
        }

        /// <summary>
        /// Collects generated showcase cube entities from the supplied scene hierarchy.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="cubeNamePrefix">Stable name prefix used by the generated cube entities.</param>
        /// <param name="results">Destination list receiving matching cube entities.</param>
        void CollectCubeEntities(SceneEntityAsset[] entities, string cubeNamePrefix, List<SceneEntityAsset> results) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(cubeNamePrefix)) {
                throw new ArgumentException("Cube name prefix must be provided.", nameof(cubeNamePrefix));
            } else if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity != null && !string.IsNullOrWhiteSpace(entity.Name) && entity.Name.StartsWith(cubeNamePrefix, StringComparison.Ordinal)) {
                    results.Add(entity);
                }

                if (entity != null) {
                    CollectCubeEntities(entity.Children ?? Array.Empty<SceneEntityAsset>(), cubeNamePrefix, results);
                }
            }
        }
    }
}

using System.IO;
using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Supplies a scene payload through the real content-manager boundary, isolating lifecycle tests from byte serialization.</summary>
    internal sealed class SceneModuleTestSceneProcessor : IContentProcessor<SceneAsset> {
        /// <summary>Module component persisted in each newly created scene.</summary>
        readonly string ModuleId;
        /// <summary>Creates a component-bearing payload processor.</summary>
        /// <param name="moduleId">Factory required by the payload.</param>
        public SceneModuleTestSceneProcessor(string moduleId) { ModuleId = moduleId; }
        /// <summary>Gets the exact scene processor output type.</summary>
        public Type OutputType => typeof(SceneAsset);
        /// <summary>Reads the test scene while retaining the production materialization pipeline.</summary>
        /// <param name="stream">Borrowed cache stream.</param>
        /// <returns>Owned fresh scene payload.</returns>
        public SceneAsset Read(Stream stream) {
            return new SceneAsset {
                RootEntities = new[] { new SceneEntityAsset {
                    Enabled = true,
                    Components = string.IsNullOrEmpty(ModuleId) ? new SceneComponentAssetRecord[0] : new[] { new SceneComponentAssetRecord { ComponentTypeId = "module." + ModuleId } }
                } }
            };
        }
        /// <summary>Returns the same owned payload through the untyped processor contract.</summary>
        /// <param name="stream">Borrowed cache stream.</param>
        /// <returns>Owned scene.</returns>
        public object ReadObject(Stream stream) { return Read(stream); }
    }
}

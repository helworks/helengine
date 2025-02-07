using Assimp.Configs;
using Assimp.Unmanaged;
using Assimp;

namespace helengine.editor.fbximporter {
    public class HelengineAssimpImporter : IModelImporter {
        public RawModelData? ImportModel(Stream stream) {
            AssimpContext importer = new AssimpContext();
            //if (configs != null) {
            //    foreach (PropertyConfig config in configs)
            //        importer.SetConfig(config);
            //}

            Scene scene = importer.ImportFileFromStream(stream, PostProcessSteps.None);

            if (scene == null) {
                return null;
            }

            //SimpleModel model = new SimpleModel();
            //model.WorldMatrix = SN.Matrix4x4.Identity;
            //if (!model.CreateVertexBuffer(scene, gd, Path.GetDirectoryName(filePath)))
            //    return null;

            //model.ComputeBoundingBox(scene);
            //model.AdjustModelScale();

            //return model;

            return null;
        }
    }
}

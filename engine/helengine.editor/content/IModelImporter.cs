namespace helengine.editor {
    public interface IModelImporter {
        RawModelData ImportModel(Stream stream);
    }
}

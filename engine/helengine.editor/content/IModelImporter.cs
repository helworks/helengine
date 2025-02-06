namespace helengine.editor {
    public interface IModelImporter {
        EditorModelData ImportModel(Stream stream);
    }
}

using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Base persisted dock-node shape used by saved workspace layouts.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(EditorWorkspaceDockLeafNodeDocument), "leaf")]
    [JsonDerivedType(typeof(EditorWorkspaceDockSplitNodeDocument), "split")]
    public abstract class EditorWorkspaceDockNodeDocument {
    }
}

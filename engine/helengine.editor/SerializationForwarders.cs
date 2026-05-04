namespace helengine.editor {
    /// <summary>
    /// Editor-local forwarding entry point for asset serialization.
    /// </summary>
    public static class AssetSerializer {
        public static void Serialize(Stream stream, Asset asset) {
            global::helengine.files.AssetSerializer.Serialize(stream, asset);
        }

        public static Asset Deserialize(Stream stream) {
            return global::helengine.files.AssetSerializer.Deserialize(stream);
        }

        public static byte[] SerializeToBytes(Asset asset) {
            return global::helengine.files.AssetSerializer.SerializeToBytes(asset);
        }

        public static Asset DeserializeFromBytes(byte[] data) {
            return global::helengine.files.AssetSerializer.DeserializeFromBytes(data);
        }
    }

    /// <summary>
    /// Editor-local forwarding entry point for packaged font serialization.
    /// </summary>
    public static class FontAssetBinarySerializer {
        public static void Serialize(Stream stream, FontAsset asset) {
            global::helengine.files.FontAssetBinarySerializer.Serialize(stream, asset);
        }

        public static FontAsset Deserialize(Stream stream) {
            return global::helengine.files.FontAssetBinarySerializer.Deserialize(stream);
        }

        public static FontAsset Deserialize(Stream stream, EngineBinaryHeader header) {
            return global::helengine.files.FontAssetBinarySerializer.Deserialize(stream, header);
        }
    }

    /// <summary>
    /// Editor-local forwarding entry point for shader package writes.
    /// </summary>
    public class ShaderModulePackageWriter {
        readonly global::helengine.files.ShaderModulePackageWriter inner = new global::helengine.files.ShaderModulePackageWriter();

        public void Write(string packagePath, ShaderModuleDefinition definition, ShaderCompileTarget target) {
            inner.Write(packagePath, definition, target);
        }
    }
}

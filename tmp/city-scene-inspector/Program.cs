using helengine;

/// <summary>
/// Dumps authored and packaged scene camera payloads for quick runtime validation.
/// </summary>
public static class Program {
    /// <summary>
    /// Entry point that inspects one or more scene files supplied on the command line.
    /// </summary>
    /// <param name="args">Absolute or relative scene file paths.</param>
    /// <returns>Zero when every file is inspected successfully.</returns>
    public static int Main(string[] args) {
        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.Length == 0) {
            Console.Error.WriteLine("Usage: city-scene-inspector <scene-path> [scene-path...]");
            return 1;
        }

        for (int index = 0; index < args.Length; index++) {
            InspectScene(Path.GetFullPath(args[index]));
        }

        return 0;
    }

    /// <summary>
    /// Deserializes one scene file and prints its entity and camera payload values.
    /// </summary>
    /// <param name="fullPath">Absolute scene file path.</param>
    static void InspectScene(string fullPath) {
        if (string.IsNullOrWhiteSpace(fullPath)) {
            throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
        }

        using FileStream stream = File.OpenRead(fullPath);
        Asset asset = AssetSerializer.Deserialize(stream);
        if (asset is not SceneAsset sceneAsset) {
            throw new InvalidOperationException($"'{fullPath}' did not deserialize into a SceneAsset.");
        }

        Console.WriteLine($"SCENE {fullPath}");
        SceneEntityAsset[] roots = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
        for (int index = 0; index < roots.Length; index++) {
            DumpEntity(roots[index], $"root[{index}]");
        }
    }

    /// <summary>
    /// Prints one serialized entity and recursively prints its children.
    /// </summary>
    /// <param name="entity">Serialized entity to dump.</param>
    /// <param name="path">Logical entity path used for output.</param>
    static void DumpEntity(SceneEntityAsset entity, string path) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        Console.WriteLine($"ENTITY {path} Name={entity.Name}");
        Console.WriteLine($"  LocalPosition={entity.LocalPosition.X},{entity.LocalPosition.Y},{entity.LocalPosition.Z}");
        Console.WriteLine($"  LocalScale={entity.LocalScale.X},{entity.LocalScale.Y},{entity.LocalScale.Z}");
        Console.WriteLine($"  LocalOrientation={entity.LocalOrientation.X},{entity.LocalOrientation.Y},{entity.LocalOrientation.Z},{entity.LocalOrientation.W}");

        SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
        for (int index = 0; index < components.Length; index++) {
            DumpComponent(components[index], $"{path}.component[{index}]");
        }

        SceneEntityAsset[] children = entity.Children ?? Array.Empty<SceneEntityAsset>();
        for (int index = 0; index < children.Length; index++) {
            DumpEntity(children[index], $"{path}.child[{index}]");
        }
    }

    /// <summary>
    /// Prints one serialized component and decodes camera payloads when present.
    /// </summary>
    /// <param name="record">Serialized component record.</param>
    /// <param name="path">Logical component path used for output.</param>
    static void DumpComponent(SceneComponentAssetRecord record, string path) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        Console.WriteLine($"  COMPONENT {path} Type={record.ComponentTypeId}");
        if (!string.Equals(record.ComponentTypeId, "helengine.CameraComponent", StringComparison.Ordinal)) {
            return;
        }

        using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
        using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

        byte version = reader.ReadByte();
        byte cameraDrawOrder = reader.ReadByte();
        ushort layerMask = reader.ReadUInt16();
        float4 viewport = new float4(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
        bool clearColorEnabled = reader.ReadByte() != 0;
        float4 clearColor = new float4(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
        bool clearDepthEnabled = reader.ReadByte() != 0;
        float clearDepth = reader.ReadSingle();
        bool clearStencilEnabled = reader.ReadByte() != 0;
        byte clearStencil = reader.ReadByte();

        Console.WriteLine($"    version={version} drawOrder={cameraDrawOrder} layerMask={layerMask}");
        Console.WriteLine($"    viewport={viewport.X},{viewport.Y},{viewport.Z},{viewport.W}");
        Console.WriteLine($"    clearColorEnabled={clearColorEnabled} clearColor={clearColor.X},{clearColor.Y},{clearColor.Z},{clearColor.W}");
        Console.WriteLine($"    clearDepthEnabled={clearDepthEnabled} clearDepth={clearDepth}");
        Console.WriteLine($"    clearStencilEnabled={clearStencilEnabled} clearStencil={clearStencil}");
    }
}

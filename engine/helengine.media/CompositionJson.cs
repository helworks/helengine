namespace helengine.media;
/// <summary>Owns the deterministic JSON protocol used between the host and media engine.</summary>
public static class CompositionJson {
    /// <summary>Strict snake-case configuration shared by serializer and parser.</summary>
    static readonly JsonSerializerOptions Options = new() {PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,WriteIndented=true,RespectRequiredConstructorParameters=true};
    /// <summary>Writes an executable composition using the versioned protocol.</summary>
    public static string Serialize(CompositionDocument document) => JsonSerializer.Serialize(document ?? throw new ArgumentNullException(nameof(document)),Options);
    /// <summary>Reads a bounded document and rejects missing protocol identity.</summary>
    public static CompositionDocument Parse(string json) {
        if (json==null || json.Length>4194304) { throw new InvalidDataException("Composition JSON must be present and at most 4 MiB."); }
        using var value=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=48});
        if (!value.RootElement.TryGetProperty("schema",out var schema) || schema.GetString()!="helengine.media.composition.v1") { throw new InvalidDataException("Unsupported composition schema."); }
        return JsonSerializer.Deserialize<CompositionDocument>(json,Options) ?? throw new InvalidDataException("Composition must be an object.");
    }
}

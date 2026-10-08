namespace helengine.media;
/// <summary>Executable data shape and range published for one effect or layer property.</summary>
public sealed class MediaParameterDescriptor {
    /// <summary>Discriminator: number, integer, boolean, enum, float2, float4, or color (RGB or RGBA in [0,1]).</summary>
    public string Type {get;set;} = "number";
    /// <summary>Explicit finite lower bound for numeric values and vector components.</summary>
    public double Minimum {get;set;} = -1000000;
    /// <summary>Explicit finite upper bound for numeric values and vector components.</summary>
    public double Maximum {get;set;} = 1000000;
    /// <summary>Named values permitted for enum parameters.</summary>
    public List<string> AllowedValues {get;set;} = [];
    /// <summary>Typed default supplied by the effect's registered implementation.</summary>
    public JsonElement DefaultValue {get;set;} = JsonSerializer.SerializeToElement(0);
    /// <summary>Human explanation usable by planning and parameter controls.</summary>
    public string Description {get;set;} = "";
    /// <summary>Checks data-only values against the registered executable shape.</summary>
    public bool Accepts(JsonElement value) {
        if(Type=="enum") {return value.ValueKind==JsonValueKind.String && AllowedValues.Contains(value.GetString());}
        if(Type=="boolean") {return value.ValueKind is JsonValueKind.True or JsonValueKind.False;}
        if(Type=="color") {return value.ValueKind==JsonValueKind.Array && value.GetArrayLength() is 3 or 4 && value.EnumerateArray().All(component=>Finite(component,0,1));}
        if(Type is "float2" or "float4") {return value.ValueKind==JsonValueKind.Array && value.GetArrayLength()==(Type=="float2"?2:4) && value.EnumerateArray().All(component=>Finite(component,Minimum,Maximum));}
        if(!Finite(value,Minimum,Maximum)) {return false;}
        return Type=="number" || Type=="integer" && value.GetDouble()==Math.Truncate(value.GetDouble());
    }
    /// <summary>Checks that one JSON value is a finite number inside an inclusive range.</summary>
    static bool Finite(JsonElement value,double minimum,double maximum) => value.ValueKind==JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number) && number>=minimum && number<=maximum;
}

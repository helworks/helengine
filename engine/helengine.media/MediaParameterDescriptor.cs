namespace helengine.media;
/// <summary>Executable data shape and range published for one effect or layer property.</summary>
public sealed class MediaParameterDescriptor {
    /// <summary>Number, integer, enum or RGB color discriminator.</summary>
    public string Type {get;set;} = "number";
    /// <summary>Explicit finite lower bound for numeric values.</summary>
    public double Minimum {get;set;} = -1000000;
    /// <summary>Explicit finite upper bound for numeric values.</summary>
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
        if(Type=="color") {return value.ValueKind==JsonValueKind.Array && value.GetArrayLength()==3 && value.EnumerateArray().All(component=>component.ValueKind==JsonValueKind.Number && component.TryGetDouble(out double number) && double.IsFinite(number) && number>=0 && number<=1);}
        if(value.ValueKind!=JsonValueKind.Number || !value.TryGetDouble(out double scalar) || !double.IsFinite(scalar) || scalar<Minimum || scalar>Maximum) {return false;}
        return Type=="number" || Type=="integer" && scalar==Math.Truncate(scalar);
    }
}

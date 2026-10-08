using System.Text.Json;
using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Maps engine parameter declarations (<see cref="EffectParameterAsset"/>) to the data-only shapes published in the
    /// capability catalog, so effect and graphic template parameters are described and checked the same way.
    /// </summary>
    public static class VideoParameterDescriptors {
        /// <summary>
        /// Maps one declared parameter to its JSON data shape, range and typed default.
        /// </summary>
        /// <param name="parameter">Declared parameter.</param>
        /// <returns>Catalog descriptor.</returns>
        public static MediaParameterDescriptor Describe(EffectParameterAsset parameter) {
            if (parameter == null) {
                throw new ArgumentNullException(nameof(parameter));
            }
            MediaParameterDescriptor result = new MediaParameterDescriptor { Description = parameter.Description, Minimum = parameter.Minimum, Maximum = parameter.Maximum };
            float4 value = parameter.DefaultValue;
            if (parameter.Type == EffectParameterType.Enum) {
                result.Type = "enum";
                result.AllowedValues = parameter.AllowedValues.ToList();
                result.DefaultValue = JsonSerializer.SerializeToElement(parameter.AllowedValues[(int)value.X]);
            } else if (parameter.Type == EffectParameterType.Bool) {
                result.Type = "boolean";
                result.DefaultValue = JsonSerializer.SerializeToElement(value.X != 0);
            } else if (parameter.Type == EffectParameterType.Color) {
                result.Type = "color";
                result.Minimum = 0;
                result.Maximum = 1;
                result.DefaultValue = JsonSerializer.SerializeToElement(value.W == 1 ? new double[] { value.X, value.Y, value.Z } : new double[] { value.X, value.Y, value.Z, value.W });
            } else if (parameter.Type == EffectParameterType.Float2) {
                result.Type = "float2";
                result.DefaultValue = JsonSerializer.SerializeToElement(new double[] { value.X, value.Y });
            } else if (parameter.Type == EffectParameterType.Float4) {
                result.Type = "float4";
                result.DefaultValue = JsonSerializer.SerializeToElement(new double[] { value.X, value.Y, value.Z, value.W });
            } else {
                result.Type = parameter.Type == EffectParameterType.Integer ? "integer" : "number";
                result.DefaultValue = JsonSerializer.SerializeToElement((double)value.X);
            }
            return result;
        }
    }
}

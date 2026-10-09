using System.Text.Json;

namespace helengine.timeline {
    /// <summary>
    /// The properties of one JSON object of the timeline form, checked once for duplicates and unknown names, with typed
    /// accessors that report problems at the property's JSON path. A property set to <c>null</c> counts as omitted, so
    /// optional fields take their defaults.
    /// </summary>
    sealed class TimelineJsonFields {
        /// <summary>
        /// Present, non-null properties by name.
        /// </summary>
        readonly Dictionary<string, JsonElement> Values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        /// <summary>
        /// Reads an object's properties, rejecting non-objects, repeated names and names outside the allowed set.
        /// </summary>
        /// <param name="element">Element that must be an object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <param name="allowed">Property names the object may use.</param>
        /// <param name="what">What the object is, for messages, such as <c>a transform clip</c>.</param>
        public TimelineJsonFields(JsonElement element, string path, string[] allowed, string what) {
            Path = path;
            if (element.ValueKind != JsonValueKind.Object) {
                throw Fail(path, "Expected " + what + " object, found " + Describe(element) + ".");
            }
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject()) {
                string propertyPath = TimelinePath.Member(path, property.Name);
                if (!seen.Add(property.Name)) {
                    throw Fail(propertyPath, "The property '" + property.Name + "' appears twice.");
                }
                if (Array.IndexOf(allowed, property.Name) < 0) {
                    throw Fail(propertyPath, "Unknown property '" + property.Name + "' for " + what + "; allowed: " + string.Join(", ", allowed) + ".");
                }
                if (property.Value.ValueKind != JsonValueKind.Null) {
                    Values.Add(property.Name, property.Value);
                }
            }
        }

        /// <summary>
        /// Gets the JSON path of the object.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Checks whether a property is present and not null.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>True when the property has a value.</returns>
        public bool Has(string name) {
            return Values.ContainsKey(name);
        }

        /// <summary>
        /// Returns the names of the present properties, for kind-specific checks.
        /// </summary>
        /// <returns>Present property names.</returns>
        public IEnumerable<string> Names() {
            return Values.Keys;
        }

        /// <summary>
        /// Returns a present property's raw value.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The value.</returns>
        public JsonElement Required(string name) {
            JsonElement value;
            if (!Values.TryGetValue(name, out value)) {
                throw Fail(TimelinePath.Member(Path, name), "The property '" + name + "' is required.");
            }
            return value;
        }

        /// <summary>
        /// Reads a required number.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The number.</returns>
        public double RequiredNumber(string name) {
            return ToNumber(Required(name), TimelinePath.Member(Path, name));
        }

        /// <summary>
        /// Reads an optional number.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value used when the property is omitted or null.</param>
        /// <returns>The number or the fallback.</returns>
        public double OptionalNumber(string name, double fallback) {
            if (!Has(name)) {
                return fallback;
            }
            return RequiredNumber(name);
        }

        /// <summary>
        /// Reads an optional integer.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value used when the property is omitted or null.</param>
        /// <returns>The integer or the fallback.</returns>
        public int OptionalInteger(string name, int fallback) {
            if (!Has(name)) {
                return fallback;
            }
            JsonElement value = Required(name);
            int result;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out result)) {
                throw Fail(TimelinePath.Member(Path, name), "Expected a whole number, found " + Describe(value) + ".");
            }
            return result;
        }

        /// <summary>
        /// Reads a required string.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The string.</returns>
        public string RequiredString(string name) {
            return ToText(Required(name), TimelinePath.Member(Path, name));
        }

        /// <summary>
        /// Reads an optional string.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The string, or empty when omitted or null.</returns>
        public string OptionalString(string name) {
            if (!Has(name)) {
                return string.Empty;
            }
            return RequiredString(name);
        }

        /// <summary>
        /// Returns the elements of an optional array.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The elements; empty when omitted or null.</returns>
        public List<JsonElement> OptionalArray(string name) {
            List<JsonElement> items = new List<JsonElement>();
            if (!Has(name)) {
                return items;
            }
            JsonElement value = Required(name);
            if (value.ValueKind != JsonValueKind.Array) {
                throw Fail(TimelinePath.Member(Path, name), "Expected a list, found " + Describe(value) + ".");
            }
            foreach (JsonElement item in value.EnumerateArray()) {
                items.Add(item);
            }
            return items;
        }

        /// <summary>
        /// Converts an element to a number.
        /// </summary>
        /// <param name="value">Element that must be a number.</param>
        /// <param name="path">JSON path for messages.</param>
        /// <returns>The number.</returns>
        public static double ToNumber(JsonElement value, string path) {
            if (value.ValueKind != JsonValueKind.Number) {
                throw Fail(path, "Expected a number, found " + Describe(value) + ".");
            }
            return value.GetDouble();
        }

        /// <summary>
        /// Converts an element to a string.
        /// </summary>
        /// <param name="value">Element that must be a string.</param>
        /// <param name="path">JSON path for messages.</param>
        /// <returns>The string.</returns>
        public static string ToText(JsonElement value, string path) {
            if (value.ValueKind != JsonValueKind.String) {
                throw Fail(path, "Expected a string, found " + Describe(value) + ".");
            }
            return value.GetString();
        }

        /// <summary>
        /// Builds the exception for one structural problem.
        /// </summary>
        /// <param name="path">JSON path of the problem.</param>
        /// <param name="message">Readable explanation.</param>
        /// <returns>The exception to throw.</returns>
        public static TimelineFormatException Fail(string path, string message) {
            return new TimelineFormatException(new TimelineDiagnostic[] { new TimelineDiagnostic(path, message) });
        }

        /// <summary>
        /// Names the JSON kind of an element for messages.
        /// </summary>
        /// <param name="value">Element to describe.</param>
        /// <returns>Text such as <c>a string</c> or <c>a list</c>.</returns>
        public static string Describe(JsonElement value) {
            if (value.ValueKind == JsonValueKind.Object) {
                return "an object";
            } else if (value.ValueKind == JsonValueKind.Array) {
                return "a list";
            } else if (value.ValueKind == JsonValueKind.String) {
                return "a string";
            } else if (value.ValueKind == JsonValueKind.Number) {
                return "a number";
            } else if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) {
                return "a boolean";
            }
            return "null";
        }
    }
}

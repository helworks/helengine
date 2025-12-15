using System.Text;
using System.Text.Json;

namespace helengine.editor.launcher.Services;

static class JsonFormatting {
    public static string SerializeWithIndent<T>(T value, JsonSerializerOptions options) {
        string json = JsonSerializer.Serialize(value, options);
        return ReindentJson(json);
    }

    public static string ReindentJson(string json) {
        var builder = new StringBuilder(json.Length);
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < json.Length; i++) {
            char ch = json[i];

            if (!inString && ch == '\n') {
                builder.Append(ch);

                int spaceStart = i + 1;
                int spaces = 0;
                while (spaceStart + spaces < json.Length && json[spaceStart + spaces] == ' ') {
                    spaces++;
                }

                int indentLevel = spaces / 2;
                int remainder = spaces % 2;
                builder.Append(' ', indentLevel * 4 + remainder);
                i = spaceStart + spaces - 1;
                escape = false;
                continue;
            }

            builder.Append(ch);

            if (escape) {
                escape = false;
                continue;
            }

            if (ch == '\\' && inString) {
                escape = true;
                continue;
            }

            if (ch == '"') {
                inString = !inString;
            }
        }

        return builder.ToString();
    }
}

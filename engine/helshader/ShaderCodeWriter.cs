namespace helshader {
    /// <summary>
    /// Utility for building indented code blocks.
    /// </summary>
    public class ShaderCodeWriter {
        /// <summary>
        /// Stores the current indentation depth.
        /// </summary>
        int indentLevel;

        /// <summary>
        /// Stores the generated lines.
        /// </summary>
        readonly List<string> lines;

        /// <summary>
        /// Initializes a new code writer.
        /// </summary>
        public ShaderCodeWriter() {
            lines = new List<string>();
        }

        /// <summary>
        /// Appends a line with the current indentation.
        /// </summary>
        /// <param name="line">Line content.</param>
        public void WriteLine(string line) {
            string indent = new string(' ', indentLevel * 4);
            lines.Add($"{indent}{line}");
        }

        /// <summary>
        /// Increases the indentation level.
        /// </summary>
        public void IncreaseIndent() {
            indentLevel++;
        }

        /// <summary>
        /// Decreases the indentation level.
        /// </summary>
        public void DecreaseIndent() {
            if (indentLevel == 0) {
                throw new InvalidOperationException("Indent level cannot be negative.");
            }

            indentLevel--;
        }

        /// <summary>
        /// Gets the generated code as a single string.
        /// </summary>
        /// <returns>Generated code.</returns>
        public string GetText() {
            return string.Join(Environment.NewLine, lines);
        }
    }
}

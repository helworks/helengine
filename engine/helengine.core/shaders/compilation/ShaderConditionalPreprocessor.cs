using System.Text;

namespace helengine {
    /// <summary>
    /// Applies a minimal subset of shader preprocessor conditionals so binding parsing sees only active resource declarations.
    /// </summary>
    public static class ShaderConditionalPreprocessor {
        /// <summary>
        /// Preprocesses shader source using compile-time defines and a minimal conditional-directive subset.
        /// </summary>
        /// <param name="source">Shader source text to preprocess.</param>
        /// <param name="defines">Compile-time defines available to conditional branches.</param>
        /// <returns>Preprocessed shader source with inactive branches removed while preserving line structure.</returns>
        public static string Preprocess(string source, IReadOnlyList<ShaderDefine> defines) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            Dictionary<string, string> defineLookup = BuildDefineLookup(defines);
            Stack<ShaderConditionalFrame> frames = new Stack<ShaderConditionalFrame>();
            StringBuilder builder = new StringBuilder(source.Length);
            using (StringReader reader = new StringReader(source)) {
                string line = reader.ReadLine();
                while (line != null) {
                    ProcessLine(line, defineLookup, frames, builder);
                    line = reader.ReadLine();
                }
            }

            if (frames.Count > 0) {
                throw new InvalidOperationException("Shader source ended before all conditional preprocessor blocks were closed.");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Processes one source line and appends the appropriate output to the preprocessed source builder.
        /// </summary>
        /// <param name="line">Source line being processed.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        /// <param name="builder">Builder receiving preprocessed output.</param>
        static void ProcessLine(
            string line,
            Dictionary<string, string> defineLookup,
            Stack<ShaderConditionalFrame> frames,
            StringBuilder builder) {
            if (line == null) {
                throw new ArgumentNullException(nameof(line));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            string trimmedLine = line.TrimStart();
            if (!trimmedLine.StartsWith("#", StringComparison.Ordinal)) {
                AppendSourceLine(builder, line, IsCurrentBranchIncluded(frames));
                return;
            }

            if (TryProcessConditionalDirective(trimmedLine, defineLookup, frames)) {
                builder.AppendLine();
                return;
            }

            if (TryProcessDefineDirective(trimmedLine, defineLookup, frames)) {
                builder.AppendLine();
                return;
            }

            builder.AppendLine();
        }

        /// <summary>
        /// Appends one source line when the active conditional state includes it, otherwise emits an empty placeholder line.
        /// </summary>
        /// <param name="builder">Builder receiving preprocessed output.</param>
        /// <param name="line">Source line to conditionally append.</param>
        /// <param name="includeLine">Whether the line should be included in the preprocessed output.</param>
        static void AppendSourceLine(StringBuilder builder, string line, bool includeLine) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            if (line == null) {
                throw new ArgumentNullException(nameof(line));
            }

            if (includeLine) {
                builder.AppendLine(line);
                return;
            }

            builder.AppendLine();
        }

        /// <summary>
        /// Attempts to process one conditional-preprocessor directive.
        /// </summary>
        /// <param name="trimmedLine">Trimmed source line beginning with <c>#</c>.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        /// <returns>True when the line contained a supported conditional directive; otherwise false.</returns>
        static bool TryProcessConditionalDirective(
            string trimmedLine,
            Dictionary<string, string> defineLookup,
            Stack<ShaderConditionalFrame> frames) {
            if (trimmedLine == null) {
                throw new ArgumentNullException(nameof(trimmedLine));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            }

            if (trimmedLine.StartsWith("#ifdef", StringComparison.Ordinal)) {
                string identifier = GetDirectiveArgument(trimmedLine, "#ifdef");
                bool branchIncluded = IsCurrentBranchIncluded(frames) && IsDefineEnabled(identifier, defineLookup);
                frames.Push(new ShaderConditionalFrame(IsCurrentBranchIncluded(frames), branchIncluded, branchIncluded));
                return true;
            } else if (trimmedLine.StartsWith("#ifndef", StringComparison.Ordinal)) {
                string identifier = GetDirectiveArgument(trimmedLine, "#ifndef");
                bool branchIncluded = IsCurrentBranchIncluded(frames) && !IsDefineEnabled(identifier, defineLookup);
                frames.Push(new ShaderConditionalFrame(IsCurrentBranchIncluded(frames), branchIncluded, branchIncluded));
                return true;
            } else if (trimmedLine.StartsWith("#if", StringComparison.Ordinal)) {
                string expression = GetDirectiveArgument(trimmedLine, "#if");
                bool branchIncluded = IsCurrentBranchIncluded(frames) && EvaluateExpression(expression, defineLookup);
                frames.Push(new ShaderConditionalFrame(IsCurrentBranchIncluded(frames), branchIncluded, branchIncluded));
                return true;
            } else if (trimmedLine.StartsWith("#elif", StringComparison.Ordinal)) {
                string expression = GetDirectiveArgument(trimmedLine, "#elif");
                ApplyElseIfDirective(expression, defineLookup, frames);
                return true;
            } else if (trimmedLine.StartsWith("#else", StringComparison.Ordinal)) {
                ApplyElseDirective(frames);
                return true;
            } else if (trimmedLine.StartsWith("#endif", StringComparison.Ordinal)) {
                ApplyEndIfDirective(frames);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to process one define-management directive for the active branch.
        /// </summary>
        /// <param name="trimmedLine">Trimmed source line beginning with <c>#</c>.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        /// <returns>True when the line contained a supported define-management directive; otherwise false.</returns>
        static bool TryProcessDefineDirective(
            string trimmedLine,
            Dictionary<string, string> defineLookup,
            Stack<ShaderConditionalFrame> frames) {
            if (trimmedLine == null) {
                throw new ArgumentNullException(nameof(trimmedLine));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            }

            if (!IsCurrentBranchIncluded(frames)) {
                return trimmedLine.StartsWith("#define", StringComparison.Ordinal) ||
                       trimmedLine.StartsWith("#undef", StringComparison.Ordinal);
            }

            if (trimmedLine.StartsWith("#define", StringComparison.Ordinal)) {
                ApplyDefineDirective(trimmedLine, defineLookup);
                return true;
            } else if (trimmedLine.StartsWith("#undef", StringComparison.Ordinal)) {
                ApplyUndefDirective(trimmedLine, defineLookup);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies one <c>#elif</c> directive to the current conditional frame.
        /// </summary>
        /// <param name="expression">Expression to evaluate for the new branch.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        static void ApplyElseIfDirective(
            string expression,
            Dictionary<string, string> defineLookup,
            Stack<ShaderConditionalFrame> frames) {
            if (string.IsNullOrWhiteSpace(expression)) {
                throw new ArgumentException("Conditional expression must be provided.", nameof(expression));
            }

            ShaderConditionalFrame frame = GetCurrentFrame(frames);
            if (frame.HasElseBranch) {
                throw new InvalidOperationException("Shader source cannot use #elif after #else in the same conditional block.");
            }

            bool branchIncluded = frame.ParentIncluded && !frame.BranchMatched && EvaluateExpression(expression, defineLookup);
            frame.CurrentIncluded = branchIncluded;
            if (branchIncluded) {
                frame.BranchMatched = true;
            }
        }

        /// <summary>
        /// Applies one <c>#else</c> directive to the current conditional frame.
        /// </summary>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        static void ApplyElseDirective(Stack<ShaderConditionalFrame> frames) {
            ShaderConditionalFrame frame = GetCurrentFrame(frames);
            if (frame.HasElseBranch) {
                throw new InvalidOperationException("Shader source cannot contain more than one #else branch in the same conditional block.");
            }

            bool branchIncluded = frame.ParentIncluded && !frame.BranchMatched;
            frame.CurrentIncluded = branchIncluded;
            frame.BranchMatched = true;
            frame.HasElseBranch = true;
        }

        /// <summary>
        /// Applies one <c>#endif</c> directive to the current conditional frame.
        /// </summary>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        static void ApplyEndIfDirective(Stack<ShaderConditionalFrame> frames) {
            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            } else if (frames.Count == 0) {
                throw new InvalidOperationException("Shader source contains #endif without a matching opening conditional directive.");
            }

            frames.Pop();
        }

        /// <summary>
        /// Applies one <c>#define</c> directive to the active define lookup.
        /// </summary>
        /// <param name="trimmedLine">Trimmed source line containing the directive.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        static void ApplyDefineDirective(string trimmedLine, Dictionary<string, string> defineLookup) {
            if (trimmedLine == null) {
                throw new ArgumentNullException(nameof(trimmedLine));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            string remainder = GetDirectiveArgument(trimmedLine, "#define");
            string[] parts = remainder.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) {
                throw new InvalidOperationException("Shader source contains #define without an identifier.");
            }

            string value = parts.Length > 1 ? parts[1].Trim() : "1";
            defineLookup[parts[0]] = value;
        }

        /// <summary>
        /// Applies one <c>#undef</c> directive to the active define lookup.
        /// </summary>
        /// <param name="trimmedLine">Trimmed source line containing the directive.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        static void ApplyUndefDirective(string trimmedLine, Dictionary<string, string> defineLookup) {
            if (trimmedLine == null) {
                throw new ArgumentNullException(nameof(trimmedLine));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            string identifier = GetDirectiveArgument(trimmedLine, "#undef");
            defineLookup.Remove(identifier);
        }

        /// <summary>
        /// Returns the argument text that follows one directive keyword.
        /// </summary>
        /// <param name="trimmedLine">Trimmed source line beginning with the directive keyword.</param>
        /// <param name="directive">Directive keyword to remove from the line.</param>
        /// <returns>Argument text following the directive keyword.</returns>
        static string GetDirectiveArgument(string trimmedLine, string directive) {
            if (string.IsNullOrWhiteSpace(trimmedLine)) {
                throw new ArgumentException("Directive line must be provided.", nameof(trimmedLine));
            }

            if (string.IsNullOrWhiteSpace(directive)) {
                throw new ArgumentException("Directive keyword must be provided.", nameof(directive));
            }

            string argument = trimmedLine.Substring(directive.Length).Trim();
            if (string.IsNullOrWhiteSpace(argument)) {
                throw new InvalidOperationException($"Shader source directive '{directive}' is missing its required argument.");
            }

            return argument;
        }

        /// <summary>
        /// Returns the current conditional frame.
        /// </summary>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        /// <returns>Current conditional frame.</returns>
        static ShaderConditionalFrame GetCurrentFrame(Stack<ShaderConditionalFrame> frames) {
            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            } else if (frames.Count == 0) {
                throw new InvalidOperationException("Shader source contains a conditional branch directive without a matching opening conditional directive.");
            }

            return frames.Peek();
        }

        /// <summary>
        /// Evaluates one minimal conditional expression used by shader preprocessor branches.
        /// </summary>
        /// <param name="expression">Expression to evaluate.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <returns>True when the expression evaluates to a non-zero or enabled state; otherwise false.</returns>
        static bool EvaluateExpression(string expression, Dictionary<string, string> defineLookup) {
            if (string.IsNullOrWhiteSpace(expression)) {
                throw new ArgumentException("Conditional expression must be provided.", nameof(expression));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            string trimmedExpression = expression.Trim();
            if (trimmedExpression.StartsWith("defined(", StringComparison.Ordinal) &&
                trimmedExpression.EndsWith(")", StringComparison.Ordinal)) {
                string identifier = trimmedExpression.Substring("defined(".Length, trimmedExpression.Length - "defined(".Length - 1).Trim();
                return IsDefineEnabled(identifier, defineLookup);
            } else if (trimmedExpression.StartsWith("!", StringComparison.Ordinal)) {
                string nestedExpression = trimmedExpression.Substring(1).Trim();
                return !EvaluateExpression(nestedExpression, defineLookup);
            } else if (string.Equals(trimmedExpression, "0", StringComparison.Ordinal)) {
                return false;
            } else if (string.Equals(trimmedExpression, "1", StringComparison.Ordinal)) {
                return true;
            }

            return IsDefineEnabled(trimmedExpression, defineLookup);
        }

        /// <summary>
        /// Determines whether one define is currently enabled.
        /// </summary>
        /// <param name="identifier">Define name to evaluate.</param>
        /// <param name="defineLookup">Lookup of currently active define values.</param>
        /// <returns>True when the define exists and is not set to zero; otherwise false.</returns>
        static bool IsDefineEnabled(string identifier, Dictionary<string, string> defineLookup) {
            if (string.IsNullOrWhiteSpace(identifier)) {
                throw new ArgumentException("Define identifier must be provided.", nameof(identifier));
            }

            if (defineLookup == null) {
                throw new ArgumentNullException(nameof(defineLookup));
            }

            if (!defineLookup.TryGetValue(identifier, out string value)) {
                return false;
            } else if (string.IsNullOrWhiteSpace(value)) {
                return true;
            } else if (string.Equals(value, "0", StringComparison.Ordinal)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the current nested conditional state includes source lines.
        /// </summary>
        /// <param name="frames">Conditional-frame stack tracking active branches.</param>
        /// <returns>True when source lines should currently be emitted.</returns>
        static bool IsCurrentBranchIncluded(Stack<ShaderConditionalFrame> frames) {
            if (frames == null) {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0) {
                return true;
            }

            return frames.Peek().CurrentIncluded;
        }

        /// <summary>
        /// Builds a mutable lookup of define values from the compile-time define list.
        /// </summary>
        /// <param name="defines">Compile-time defines supplied to shader compilation.</param>
        /// <returns>Mutable lookup of define values keyed by identifier.</returns>
        static Dictionary<string, string> BuildDefineLookup(IReadOnlyList<ShaderDefine> defines) {
            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int defineIndex = 0; defineIndex < defines.Count; defineIndex++) {
                ShaderDefine define = defines[defineIndex];
                if (define == null) {
                    throw new InvalidOperationException("Shader define lists must not contain null entries.");
                }

                lookup[define.Name] = define.Value;
            }

            return lookup;
        }
    }
}

using System.Text.RegularExpressions;

namespace helengine {
    /// <summary>
    /// Parses a minimal subset of HLSL resource declarations so runtime shader assets can expose material bindings without backend reflection.
    /// </summary>
    public static class HlslShaderBindingParser {
        /// <summary>
        /// Matches block comments that should be removed before parsing declarations.
        /// </summary>
        static readonly Regex BlockCommentPattern = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
        /// <summary>
        /// Matches single-line comments that should be removed before parsing declarations.
        /// </summary>
        static readonly Regex LineCommentPattern = new Regex(@"//.*?$", RegexOptions.Compiled | RegexOptions.Multiline);
        /// <summary>
        /// Matches constant-buffer declarations with explicit register bindings.
        /// </summary>
        static readonly Regex ConstantBufferPattern = new Regex(
            @"\bcbuffer\s+(?<name>[A-Za-z_]\w*)\s*:\s*register\s*\(\s*b(?<slot>\d+)(?:\s*,\s*space(?<space>\d+))?\s*\)\s*\{(?<body>.*?)\}\s*;?",
            RegexOptions.Compiled | RegexOptions.Singleline);
        /// <summary>
        /// Matches texture, sampler, and buffer resource declarations with explicit register bindings.
        /// </summary>
        static readonly Regex ResourcePattern = new Regex(
            @"^\s*(?<type>[A-Za-z_]\w*(?:<[^;>]+>)?)\s+(?<name>[A-Za-z_]\w*)\s*:\s*register\s*\(\s*(?<register>[bstu])(?<slot>\d+)(?:\s*,\s*space(?<space>\d+))?\s*\)\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline);
        /// <summary>
        /// Matches one constant-buffer member declaration.
        /// </summary>
        static readonly Regex ConstantBufferMemberPattern = new Regex(
            @"(?<type>[A-Za-z_]\w*(?:<[^;>]+>)?)\s+(?<name>[A-Za-z_]\w*)(?:\s*\[\s*(?<count>\d+)\s*\])?\s*;",
            RegexOptions.Compiled);
        /// <summary>
        /// Matches matrix type names such as float4x4.
        /// </summary>
        static readonly Regex MatrixTypePattern = new Regex(
            @"^(?<base>[A-Za-z_]\w*)(?<rows>\d)x(?<columns>\d)$",
            RegexOptions.Compiled);
        /// <summary>
        /// Size in bytes of one HLSL constant-buffer register.
        /// </summary>
        const int RegisterSizeInBytes = 16;

        /// <summary>
        /// Parses shader bindings from HLSL source text.
        /// </summary>
        /// <param name="source">HLSL source text to inspect.</param>
        /// <param name="bindingPolicy">Binding policy used to normalize slots.</param>
        /// <returns>Array of inferred shader bindings.</returns>
        public static ShaderBinding[] ParseBindings(string source, ShaderBindingPolicy bindingPolicy) {
            return ParseBindings(source, bindingPolicy, Array.Empty<ShaderDefine>());
        }

        /// <summary>
        /// Parses shader bindings from HLSL source text while respecting the provided compile-time defines.
        /// </summary>
        /// <param name="source">HLSL source text to inspect.</param>
        /// <param name="bindingPolicy">Binding policy used to normalize slots.</param>
        /// <param name="defines">Compile-time defines that control active conditional branches.</param>
        /// <returns>Array of inferred shader bindings.</returns>
        public static ShaderBinding[] ParseBindings(
            string source,
            ShaderBindingPolicy bindingPolicy,
            IReadOnlyList<ShaderDefine> defines) {
            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Shader source must be provided.", nameof(source));
            }

            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            string preprocessedSource = ShaderConditionalPreprocessor.Preprocess(source, defines);
            string normalizedSource = StripComments(preprocessedSource);
            List<ShaderBinding> bindings = new List<ShaderBinding>();
            AddConstantBufferBindings(normalizedSource, bindingPolicy, bindings);
            AddResourceBindings(normalizedSource, bindingPolicy, bindings);
            return bindings.ToArray();
        }

        /// <summary>
        /// Removes line and block comments so declaration parsing sees only source tokens.
        /// </summary>
        /// <param name="source">Source text whose comments should be removed.</param>
        /// <returns>Source text without comments.</returns>
        static string StripComments(string source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            string withoutBlockComments = BlockCommentPattern.Replace(source, string.Empty);
            return LineCommentPattern.Replace(withoutBlockComments, string.Empty);
        }

        /// <summary>
        /// Adds inferred constant-buffer bindings from the source text.
        /// </summary>
        /// <param name="source">Normalized source text without comments.</param>
        /// <param name="bindingPolicy">Binding policy used to normalize slots.</param>
        /// <param name="bindings">Binding list being built.</param>
        static void AddConstantBufferBindings(
            string source,
            ShaderBindingPolicy bindingPolicy,
            [NativeNoEscape] List<ShaderBinding> bindings) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            if (bindings == null) {
                throw new ArgumentNullException(nameof(bindings));
            }

            MatchCollection matches = ConstantBufferPattern.Matches(source);
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
                Match match = matches[matchIndex];
                if (!match.Success) {
                    continue;
                }

                string name = match.Groups["name"].Value;
                int registerIndex = ParseRequiredInt(match.Groups["slot"].Value, "constant-buffer register");
                int set = ParseOptionalInt(match.Groups["space"].Value, bindingPolicy.DefaultSpace);
                int slot = bindingPolicy.GetSlot(ShaderResourceType.ConstantBuffer, registerIndex);
                string body = match.Groups["body"].Value;
                ShaderConstantMember[] members = ParseConstantBufferMembers(body);
                int size = ComputeConstantBufferSize(members);
                bindings.Add(new ShaderBinding(name, ShaderResourceType.ConstantBuffer, set, slot, size, members));
            }
        }

        /// <summary>
        /// Adds inferred texture, sampler, and buffer bindings from the source text.
        /// </summary>
        /// <param name="source">Normalized source text without comments.</param>
        /// <param name="bindingPolicy">Binding policy used to normalize slots.</param>
        /// <param name="bindings">Binding list being built.</param>
        static void AddResourceBindings(
            string source,
            ShaderBindingPolicy bindingPolicy,
            [NativeNoEscape] List<ShaderBinding> bindings) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            if (bindings == null) {
                throw new ArgumentNullException(nameof(bindings));
            }

            MatchCollection matches = ResourcePattern.Matches(source);
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
                Match match = matches[matchIndex];
                if (!match.Success) {
                    continue;
                }

                string typeText = match.Groups["type"].Value;
                ShaderResourceType resourceType = ResolveResourceType(typeText, match.Groups["register"].Value);
                string name = match.Groups["name"].Value;
                int registerIndex = ParseRequiredInt(match.Groups["slot"].Value, "resource register");
                int set = ParseOptionalInt(match.Groups["space"].Value, bindingPolicy.DefaultSpace);
                int slot = bindingPolicy.GetSlot(resourceType, registerIndex);
                bindings.Add(new ShaderBinding(name, resourceType, set, slot, 0, Array.Empty<ShaderConstantMember>()));
            }
        }

        /// <summary>
        /// Parses the members declared within one constant buffer body.
        /// </summary>
        /// <param name="body">Constant buffer body text.</param>
        /// <returns>Array of parsed constant-buffer members.</returns>
        static ShaderConstantMember[] ParseConstantBufferMembers(string body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            List<ShaderConstantMember> members = new List<ShaderConstantMember>();
            MatchCollection matches = ConstantBufferMemberPattern.Matches(body);
            int currentOffset = 0;
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
                Match match = matches[matchIndex];
                if (!match.Success) {
                    continue;
                }

                string type = match.Groups["type"].Value;
                string name = match.Groups["name"].Value;
                int arrayCount = ParseOptionalInt(match.Groups["count"].Value, 0);
                int memberSize = ComputeMemberStorageSize(type, arrayCount);
                int memberOffset = ComputeMemberOffset(currentOffset, type, arrayCount, memberSize);
                members.Add(new ShaderConstantMember(name, type, memberOffset, memberSize));
                currentOffset = memberOffset + memberSize;
            }

            return members.ToArray();
        }

        /// <summary>
        /// Computes the final byte size of one constant buffer from its parsed members.
        /// </summary>
        /// <param name="members">Parsed constant-buffer members.</param>
        /// <returns>Aligned constant-buffer size in bytes.</returns>
        static int ComputeConstantBufferSize(IReadOnlyList<ShaderConstantMember> members) {
            if (members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            if (members.Count == 0) {
                return 0;
            }

            ShaderConstantMember lastMember = members[members.Count - 1];
            int endOffset = lastMember.Offset + lastMember.Size;
            return AlignToRegister(endOffset);
        }

        /// <summary>
        /// Computes the byte offset of one constant-buffer member using standard HLSL constant-buffer packing rules.
        /// </summary>
        /// <param name="currentOffset">Current end offset of the buffer.</param>
        /// <param name="type">Member type text.</param>
        /// <param name="arrayCount">Array length when the member is an array; otherwise zero.</param>
        /// <param name="memberSize">Resolved storage size of the member.</param>
        /// <returns>Byte offset for the member.</returns>
        static int ComputeMemberOffset(int currentOffset, string type, int arrayCount, int memberSize) {
            if (currentOffset < 0) {
                throw new ArgumentOutOfRangeException(nameof(currentOffset), "Current offset cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }

            if (arrayCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(arrayCount), "Array count cannot be negative.");
            }

            if (memberSize < 0) {
                throw new ArgumentOutOfRangeException(nameof(memberSize), "Member size cannot be negative.");
            }

            if (RequiresRegisterAlignment(type, arrayCount)) {
                return AlignToRegister(currentOffset);
            }

            int registerOffset = currentOffset % RegisterSizeInBytes;
            if (registerOffset + memberSize > RegisterSizeInBytes) {
                return AlignToRegister(currentOffset);
            }

            return currentOffset;
        }

        /// <summary>
        /// Determines whether one constant-buffer member must start on a fresh 16-byte register boundary.
        /// </summary>
        /// <param name="type">Member type text.</param>
        /// <param name="arrayCount">Array length when the member is an array; otherwise zero.</param>
        /// <returns>True when the member must begin on a register boundary.</returns>
        static bool RequiresRegisterAlignment(string type, int arrayCount) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }

            if (arrayCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(arrayCount), "Array count cannot be negative.");
            }

            if (arrayCount > 0) {
                return true;
            }

            return IsMatrixType(type);
        }

        /// <summary>
        /// Computes the stored byte size of one constant-buffer member.
        /// </summary>
        /// <param name="type">Member type text.</param>
        /// <param name="arrayCount">Array length when the member is an array; otherwise zero.</param>
        /// <returns>Stored byte size of the member.</returns>
        static int ComputeMemberStorageSize(string type, int arrayCount) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }

            if (arrayCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(arrayCount), "Array count cannot be negative.");
            }

            if (arrayCount > 0) {
                return ComputeArrayStorageSize(type, arrayCount);
            } else if (IsMatrixType(type)) {
                return ComputeMatrixStorageSize(type);
            }

            return ComputeScalarOrVectorStorageSize(type);
        }

        /// <summary>
        /// Computes the byte size of one array member according to HLSL constant-buffer packing rules.
        /// </summary>
        /// <param name="type">Element type text.</param>
        /// <param name="arrayCount">Element count in the array.</param>
        /// <returns>Total stored byte size of the array.</returns>
        static int ComputeArrayStorageSize(string type, int arrayCount) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Element type must be provided.", nameof(type));
            }

            if (arrayCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(arrayCount), "Array count must be greater than zero.");
            }

            if (IsMatrixType(type)) {
                return ComputeMatrixStorageSize(type) * arrayCount;
            }

            return RegisterSizeInBytes * arrayCount;
        }

        /// <summary>
        /// Computes the byte size of one matrix member according to HLSL constant-buffer packing rules.
        /// </summary>
        /// <param name="type">Matrix type text.</param>
        /// <returns>Stored byte size of the matrix.</returns>
        static int ComputeMatrixStorageSize(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Matrix type must be provided.", nameof(type));
            }

            if (string.Equals(type, "matrix", StringComparison.Ordinal)) {
                return RegisterSizeInBytes * 4;
            }

            Match match = MatrixTypePattern.Match(type);
            if (!match.Success) {
                throw new InvalidOperationException($"Unsupported HLSL matrix type '{type}'.");
            }

            int columns = ParseRequiredInt(match.Groups["columns"].Value, "matrix column count");
            return RegisterSizeInBytes * columns;
        }

        /// <summary>
        /// Computes the byte size of one scalar or vector member.
        /// </summary>
        /// <param name="type">Scalar or vector type text.</param>
        /// <returns>Stored byte size of the member.</returns>
        static int ComputeScalarOrVectorStorageSize(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }

            string normalizedType = type.Trim();
            if (string.Equals(normalizedType, "matrix", StringComparison.Ordinal)) {
                return RegisterSizeInBytes * 4;
            }

            if (TryParseVectorComponentCount(normalizedType, out int componentCount)) {
                return ResolveScalarTypeSize(normalizedType) * componentCount;
            }

            return ResolveScalarTypeSize(normalizedType);
        }

        /// <summary>
        /// Resolves the byte size of one scalar component type.
        /// </summary>
        /// <param name="type">Scalar or vector type text.</param>
        /// <returns>Byte size of one scalar component.</returns>
        static int ResolveScalarTypeSize(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Scalar type must be provided.", nameof(type));
            }

            string baseType = ExtractBaseType(type);
            switch (baseType) {
                case "bool":
                case "int":
                case "uint":
                case "float":
                case "half":
                case "min16float":
                case "min10float":
                case "min16int":
                case "min12int":
                case "min16uint":
                    return 4;
                default:
                    throw new InvalidOperationException($"Unsupported HLSL scalar type '{type}'.");
            }
        }

        /// <summary>
        /// Extracts the non-numeric prefix from one scalar, vector, or matrix type.
        /// </summary>
        /// <param name="type">Type text to analyze.</param>
        /// <returns>Base type prefix without numeric suffixes.</returns>
        static string ExtractBaseType(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Type must be provided.", nameof(type));
            }

            int numericIndex = type.Length;
            for (int characterIndex = 0; characterIndex < type.Length; characterIndex++) {
                if (char.IsDigit(type[characterIndex])) {
                    numericIndex = characterIndex;
                    break;
                }
            }

            return type.Substring(0, numericIndex);
        }

        /// <summary>
        /// Determines whether one type represents an HLSL matrix.
        /// </summary>
        /// <param name="type">Type text to evaluate.</param>
        /// <returns>True when the type represents a matrix.</returns>
        static bool IsMatrixType(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Type must be provided.", nameof(type));
            }

            if (string.Equals(type, "matrix", StringComparison.Ordinal)) {
                return true;
            }

            return MatrixTypePattern.IsMatch(type);
        }

        /// <summary>
        /// Tries to parse the vector component count from one HLSL scalar or vector type.
        /// </summary>
        /// <param name="type">Type text to analyze.</param>
        /// <param name="componentCount">Resolved component count when the type is a vector.</param>
        /// <returns>True when the type is a vector; otherwise false.</returns>
        static bool TryParseVectorComponentCount(string type, out int componentCount) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Type must be provided.", nameof(type));
            }

            Match match = MatrixTypePattern.Match(type);
            if (match.Success) {
                componentCount = 0;
                return false;
            }

            int trailingDigitIndex = type.Length - 1;
            if (trailingDigitIndex < 0 || !char.IsDigit(type[trailingDigitIndex])) {
                componentCount = 1;
                return false;
            }

            componentCount = ParseRequiredInt(type.Substring(trailingDigitIndex, 1), "vector component count");
            return true;
        }

        /// <summary>
        /// Resolves an HLSL resource declaration into the engine resource type enumeration.
        /// </summary>
        /// <param name="typeText">Declared HLSL type text.</param>
        /// <param name="registerClass">Register class used by the declaration.</param>
        /// <returns>Resolved engine resource type.</returns>
        static ShaderResourceType ResolveResourceType(string typeText, string registerClass) {
            if (string.IsNullOrWhiteSpace(typeText)) {
                throw new ArgumentException("Resource type text must be provided.", nameof(typeText));
            }

            if (string.IsNullOrWhiteSpace(registerClass)) {
                throw new ArgumentException("Register class must be provided.", nameof(registerClass));
            }

            string normalizedType = typeText.Trim();
            if (string.Equals(registerClass, "s", StringComparison.Ordinal)) {
                return ShaderResourceType.Sampler;
            } else if (normalizedType.StartsWith("TextureCube", StringComparison.Ordinal)) {
                return ShaderResourceType.TextureCube;
            } else if (normalizedType.StartsWith("Texture2D", StringComparison.Ordinal)) {
                return ShaderResourceType.Texture2D;
            } else if (normalizedType.StartsWith("RWTexture2D", StringComparison.Ordinal)) {
                return ShaderResourceType.StorageTexture2D;
            } else if (normalizedType.StartsWith("RWStructuredBuffer", StringComparison.Ordinal) ||
                       normalizedType.StartsWith("RWBuffer", StringComparison.Ordinal) ||
                       string.Equals(normalizedType, "ByteAddressBuffer", StringComparison.Ordinal) ||
                       string.Equals(normalizedType, "RWByteAddressBuffer", StringComparison.Ordinal) ||
                       normalizedType.StartsWith("AppendStructuredBuffer", StringComparison.Ordinal) ||
                       normalizedType.StartsWith("ConsumeStructuredBuffer", StringComparison.Ordinal)) {
                return ShaderResourceType.StorageBuffer;
            } else if (normalizedType.StartsWith("StructuredBuffer", StringComparison.Ordinal) ||
                       normalizedType.StartsWith("Buffer", StringComparison.Ordinal)) {
                return ShaderResourceType.Buffer;
            }

            throw new InvalidOperationException($"Unsupported HLSL resource type '{typeText}'.");
        }

        /// <summary>
        /// Parses a required integer token from source text.
        /// </summary>
        /// <param name="text">Integer text to parse.</param>
        /// <param name="label">Display label used in failure diagnostics.</param>
        /// <returns>Parsed integer value.</returns>
        static int ParseRequiredInt(string text, string label) {
            if (string.IsNullOrWhiteSpace(text)) {
                throw new ArgumentException("Integer text must be provided.", nameof(text));
            }

            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Label must be provided.", nameof(label));
            }

            if (int.TryParse(text, out int value)) {
                return value;
            }

            throw new InvalidOperationException($"The {label} value '{text}' is not a valid integer.");
        }

        /// <summary>
        /// Parses an optional integer token from source text, returning the provided fallback when the token is absent.
        /// </summary>
        /// <param name="text">Optional integer text to parse.</param>
        /// <param name="fallbackValue">Fallback value used when the text is absent.</param>
        /// <returns>Parsed integer value or the fallback.</returns>
        static int ParseOptionalInt(string text, int fallbackValue) {
            if (string.IsNullOrWhiteSpace(text)) {
                return fallbackValue;
            }

            return ParseRequiredInt(text, "optional integer");
        }

        /// <summary>
        /// Aligns one byte offset up to the next HLSL constant-buffer register boundary.
        /// </summary>
        /// <param name="offset">Offset to align.</param>
        /// <returns>Aligned offset.</returns>
        static int AlignToRegister(int offset) {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            }

            int remainder = offset % RegisterSizeInBytes;
            if (remainder == 0) {
                return offset;
            }

            return offset + (RegisterSizeInBytes - remainder);
        }
    }
}

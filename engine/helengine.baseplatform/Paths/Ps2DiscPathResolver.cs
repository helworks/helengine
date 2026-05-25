namespace helengine.baseplatform.Paths;

/// <summary>
/// Resolves deterministic PS2-safe disc and runtime paths from logical cooked asset paths.
/// </summary>
public static class Ps2DiscPathResolver {
    /// <summary>
    /// Maximum proven-safe PS2 runtime path length including the leading slash and the trailing `;1` version suffix.
    /// </summary>
    const int MaxPhysicalRuntimePathLength = 32;

    /// <summary>
    /// Runtime path overhead added around every staged disc-relative path when it becomes a rooted PS2 search path.
    /// </summary>
    const int PhysicalRuntimePathAdornmentLength = 3;

    /// <summary>
    /// Stable runtime root emitted by PS2 player builds when opening packaged content from the disc.
    /// </summary>
    const string RuntimeRootPrefix = "cdrom0:\\";

    /// <summary>
    /// Stable runtime version suffix required by PS2 CD file lookup.
    /// </summary>
    const string RuntimeVersionSuffix = ";1";

    /// <summary>
    /// Stable directory token used when deep logical paths must collapse into a shorter deterministic alias.
    /// </summary>
    const string LongPathAliasDirectoryToken = "L";

    /// <summary>
    /// Stable cooked scene directory token preserved when deep scene assets collapse into a short deterministic alias.
    /// </summary>
    const string SceneAliasDirectoryToken = "SCENES";

    /// <summary>
    /// Stable shortest-form HEL asset directory token preserved when deep HEL assets collapse into a short deterministic alias.
    /// </summary>
    const string HelAssetAliasDirectoryToken = "H";

    /// <summary>
    /// Stable shortest-form imported asset directory token preserved when extensionless cooked imported assets collapse into a short deterministic alias.
    /// </summary>
    const string ImportedAssetAliasDirectoryToken = "I";

    /// <summary>
    /// Number of alias-stem characters used to shard dense HEL alias buckets across multiple small directories.
    /// </summary>
    const int HelAssetAliasShardLength = 1;

    /// <summary>
    /// Number of alias-stem characters used to shard dense imported asset alias buckets across multiple small directories.
    /// </summary>
    const int ImportedAssetAliasShardLength = 1;

    /// <summary>
    /// Number of alias-stem characters used to shard dense generic long-path alias buckets across multiple small directories.
    /// </summary>
    const int LongPathAliasShardLength = 1;

    /// <summary>
    /// Resolves one logical cooked relative path into a PS2-safe physical disc relative path.
    /// </summary>
    /// <param name="logicalRelativePath">Logical cooked relative path that identifies one staged asset.</param>
    /// <returns>PS2-safe physical relative path using uppercase 8.3-compatible segments.</returns>
    public static string ResolveDiscRelativePath(string logicalRelativePath) {
        if (string.IsNullOrWhiteSpace(logicalRelativePath)) {
            throw new ArgumentException("Logical relative path must be provided.", nameof(logicalRelativePath));
        }

        string normalizedPath = logicalRelativePath.Replace('\\', '/');
        if (IsExtensionlessImportedLogicalPath(normalizedPath)) {
            return ResolveExtensionlessImportedPathAlias(normalizedPath);
        }

        string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] resolvedSegments = new string[segments.Length];
        for (int index = 0; index < segments.Length; index++) {
            bool isLastSegment = index == segments.Length - 1;
            resolvedSegments[index] = isLastSegment
                ? ResolveFileName(segments[index])
                : ResolveComponentToken(segments[index], 8);
        }

        string resolvedPath = string.Join(Path.DirectorySeparatorChar, resolvedSegments);
        if (GetRuntimePhysicalPathLength(resolvedPath) <= MaxPhysicalRuntimePathLength) {
            return resolvedPath;
        }

        return ResolveLongPathAlias(normalizedPath, resolvedSegments, segments[^1]);
    }

    /// <summary>
    /// Resolves one logical cooked relative path into the rooted runtime path consumed by the PS2 player.
    /// </summary>
    /// <param name="logicalRelativePath">Logical cooked relative path that identifies one staged asset.</param>
    /// <returns>Rooted PS2 runtime path including the disc root and version suffix.</returns>
    public static string ResolveRuntimePath(string logicalRelativePath) {
        string discRelativePath = ResolveDiscRelativePath(logicalRelativePath).Replace('/', '\\');
        return RuntimeRootPrefix + discRelativePath + RuntimeVersionSuffix;
    }

    /// <summary>
    /// Resolves one directory or file stem token into an uppercase PS2-safe component token.
    /// </summary>
    /// <param name="token">Logical directory name or file stem token.</param>
    /// <param name="maxLength">Maximum physical token length.</param>
    /// <returns>Sanitized uppercase token that fits the supplied length budget.</returns>
    static string ResolveComponentToken(string token, int maxLength) {
        string sanitizedToken = SanitizeToken(token);
        if (sanitizedToken.Length <= maxLength) {
            return sanitizedToken;
        }

        int hashLength = Math.Min(6, maxLength - 1);
        int prefixLength = maxLength - hashLength;
        return sanitizedToken.Substring(0, prefixLength) + ComputeHash(token).Substring(0, hashLength);
    }

    /// <summary>
    /// Resolves one logical file name into an uppercase PS2-safe 8.3 filename.
    /// </summary>
    /// <param name="fileName">Logical file name including its extension.</param>
    /// <returns>Sanitized PS2-safe file name.</returns>
    static string ResolveFileName(string fileName) {
        int extensionSeparatorIndex = fileName.LastIndexOf('.');
        if (extensionSeparatorIndex <= 0) {
            return ResolveComponentToken(fileName, 8);
        }

        string stem = fileName[..extensionSeparatorIndex];
        string extension = fileName[(extensionSeparatorIndex + 1)..];
        string resolvedExtension = ResolveExtension(extension);
        if (string.IsNullOrWhiteSpace(resolvedExtension)) {
            return ResolveComponentToken(stem, 8);
        }

        return ResolveComponentToken(stem, 8) + "." + resolvedExtension;
    }

    /// <summary>
    /// Resolves one deep logical path into a shorter deterministic alias that still preserves the top-level root and file extension.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <param name="resolvedSegments">PS2-safe path segments before the full-path budget check.</param>
    /// <param name="fileName">Original logical file name including its extension.</param>
    /// <returns>Collapsed deterministic physical disc-relative path.</returns>
    static string ResolveLongPathAlias(string normalizedLogicalPath, string[] resolvedSegments, string fileName) {
        if (IsDeepSceneLogicalPath(normalizedLogicalPath)) {
            return ResolveDeepScenePathAlias(normalizedLogicalPath, fileName);
        }
        if (IsDeepHelLogicalPath(fileName)) {
            return ResolveDeepHelPathAlias(normalizedLogicalPath, fileName);
        }

        string aliasRoot = resolvedSegments.Length > 0
            ? resolvedSegments[0]
            : ResolveComponentToken("ROOT", 8);
        string resolvedExtension = ResolveResolvedExtension(fileName);
        string aliasFileStem = ResolveNumericLeadingAliasStem(normalizedLogicalPath);
        string aliasShardDirectory = ResolveLongPathAliasShardDirectory(aliasFileStem);
        string aliasFileName = string.IsNullOrWhiteSpace(resolvedExtension)
            ? aliasFileStem
            : aliasFileStem + "." + resolvedExtension;
        string aliasedPath = string.Join(
            Path.DirectorySeparatorChar,
            new[] {
                aliasRoot,
                LongPathAliasDirectoryToken,
                aliasShardDirectory,
                aliasFileName
            });
        if (GetRuntimePhysicalPathLength(aliasedPath) > MaxPhysicalRuntimePathLength) {
            throw new InvalidOperationException($"The logical PS2 asset path '{normalizedLogicalPath}' could not be shortened to a safe physical runtime path.");
        }

        return aliasedPath;
    }

    /// <summary>
    /// Resolves one deep HEL logical path into a short deterministic `COOKED\H` alias.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical HEL path using forward slashes.</param>
    /// <param name="fileName">Original logical file name including its extension.</param>
    /// <returns>Collapsed deterministic HEL physical disc-relative path.</returns>
    static string ResolveDeepHelPathAlias(string normalizedLogicalPath, string fileName) {
        string resolvedExtension = ResolveResolvedExtension(fileName);
        string aliasFileStem = ResolveHelAliasStem(normalizedLogicalPath);
        string aliasShardDirectory = ResolveHelAliasShardDirectory(aliasFileStem);
        string aliasFileName = string.IsNullOrWhiteSpace(resolvedExtension)
            ? aliasFileStem
            : aliasFileStem + "." + resolvedExtension;
        string aliasedPath = string.Join(
            Path.DirectorySeparatorChar,
            new[] {
                ResolveComponentToken("COOKED", 8),
                HelAssetAliasDirectoryToken,
                aliasShardDirectory,
                aliasFileName
            });
        if (GetRuntimePhysicalPathLength(aliasedPath) > MaxPhysicalRuntimePathLength) {
            throw new InvalidOperationException($"The logical PS2 HEL asset path '{normalizedLogicalPath}' could not be shortened to a safe physical runtime path.");
        }

        return aliasedPath;
    }

    /// <summary>
    /// Resolves one extensionless imported cooked asset path into a short deterministic `COOKED\I` alias with a real PS2-safe extension.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical imported asset path using forward slashes.</param>
    /// <returns>Collapsed deterministic imported asset physical disc-relative path.</returns>
    static string ResolveExtensionlessImportedPathAlias(string normalizedLogicalPath) {
        string aliasFileStem = ResolveImportedAliasStem(normalizedLogicalPath);
        string aliasShardDirectory = ResolveImportedAliasShardDirectory(aliasFileStem);
        string aliasFileName = aliasFileStem + ".HAS";
        string aliasedPath = string.Join(
            Path.DirectorySeparatorChar,
            new[] {
                ResolveComponentToken("COOKED", 8),
                ImportedAssetAliasDirectoryToken,
                aliasShardDirectory,
                aliasFileName
            });
        if (GetRuntimePhysicalPathLength(aliasedPath) > MaxPhysicalRuntimePathLength) {
            throw new InvalidOperationException($"The logical PS2 imported asset path '{normalizedLogicalPath}' could not be shortened to a safe physical runtime path.");
        }

        return aliasedPath;
    }

    /// <summary>
    /// Resolves one deep scene logical path into a short deterministic `COOKED\SCENES` alias.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical scene path using forward slashes.</param>
    /// <param name="fileName">Original logical file name including its extension.</param>
    /// <returns>Collapsed deterministic scene physical disc-relative path.</returns>
    static string ResolveDeepScenePathAlias(string normalizedLogicalPath, string fileName) {
        string resolvedExtension = ResolveResolvedExtension(fileName);
        string aliasFileStem = ComputeHash(normalizedLogicalPath);
        string aliasFileName = string.IsNullOrWhiteSpace(resolvedExtension)
            ? aliasFileStem
            : aliasFileStem + "." + resolvedExtension;
        string aliasedPath = string.Join(
            Path.DirectorySeparatorChar,
            new[] {
                ResolveComponentToken("COOKED", 8),
                SceneAliasDirectoryToken,
                aliasFileName
            });
        if (GetRuntimePhysicalPathLength(aliasedPath) > MaxPhysicalRuntimePathLength) {
            throw new InvalidOperationException($"The logical PS2 scene path '{normalizedLogicalPath}' could not be shortened to a safe physical runtime path.");
        }

        return aliasedPath;
    }

    /// <summary>
    /// Resolves the extension that should survive a full-path alias fallback.
    /// </summary>
    /// <param name="fileName">Original logical file name including its extension.</param>
    /// <returns>Sanitized PS2-safe 3-character extension or an empty string when the logical file name has none.</returns>
    static string ResolveResolvedExtension(string fileName) {
        int extensionSeparatorIndex = fileName.LastIndexOf('.');
        if (extensionSeparatorIndex <= 0) {
            return string.Empty;
        }

        string extension = fileName[(extensionSeparatorIndex + 1)..];
        return ResolveExtension(extension);
    }

    /// <summary>
    /// Resolves one deterministic generic alias stem whose first character is alphabetic so PS2 runtime CD file lookup can open long-path aliases reliably.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <returns>8-character deterministic alias stem whose first character is alphabetic.</returns>
    static string ResolveNumericLeadingAliasStem(string normalizedLogicalPath) {
        string hash = ComputeHash(normalizedLogicalPath);
        return "A" + hash[1..];
    }

    /// <summary>
    /// Resolves one deterministic alias stem whose first character is a fixed PS2-safe prefix and whose remaining characters come from the stable path hash.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <param name="prefix">Single-character uppercase PS2-safe prefix.</param>
    /// <returns>8-character deterministic alias stem with the requested prefix.</returns>
    static string ResolvePrefixedAliasStem(string normalizedLogicalPath, string prefix) {
        if (string.IsNullOrWhiteSpace(prefix)) {
            throw new ArgumentException("Alias prefix must be provided.", nameof(prefix));
        }

        string sanitizedPrefix = SanitizeToken(prefix);
        if (sanitizedPrefix.Length != 1) {
            throw new InvalidOperationException($"Expected a single PS2-safe alias prefix, but got '{prefix}'.");
        }

        string hash = ComputeHash(normalizedLogicalPath);
        return sanitizedPrefix + hash[1..];
    }

    /// <summary>
    /// Resolves one deterministic HEL alias stem using the fixed `H1` prefix that stays inside the safe PS2 filename class proven by runtime probes.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <returns>8-character deterministic HEL alias stem.</returns>
    static string ResolveHelAliasStem(string normalizedLogicalPath) {
        string hash = ComputeHash(normalizedLogicalPath);
        return "H1" + hash[2..];
    }

    /// <summary>
    /// Resolves one deterministic imported asset alias stem using the fixed `I1` prefix so extensionless imported cooked assets use an explicit PS2-safe filename class.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <returns>8-character deterministic imported asset alias stem.</returns>
    static string ResolveImportedAliasStem(string normalizedLogicalPath) {
        string hash = ComputeHash(normalizedLogicalPath);
        return "I1" + hash[2..];
    }

    /// <summary>
    /// Resolves the shard directory for one HEL alias stem so large HEL buckets do not require multi-sector ISO directory scans at runtime.
    /// </summary>
    /// <param name="aliasFileStem">Deterministic HEL alias stem that will be emitted as the physical file name.</param>
    /// <returns>Stable shard directory token derived from the HEL alias stem.</returns>
    static string ResolveHelAliasShardDirectory(string aliasFileStem) {
        if (string.IsNullOrWhiteSpace(aliasFileStem)) {
            throw new ArgumentException("HEL alias file stem must be provided.", nameof(aliasFileStem));
        }
        if (aliasFileStem.Length < 2 + HelAssetAliasShardLength) {
            throw new InvalidOperationException($"HEL alias file stem '{aliasFileStem}' is too short to derive a shard directory.");
        }

        return aliasFileStem.Substring(2, HelAssetAliasShardLength);
    }

    /// <summary>
    /// Resolves the shard directory for one imported asset alias stem so dense imported buckets do not require broad directory scans at runtime.
    /// </summary>
    /// <param name="aliasFileStem">Deterministic imported asset alias stem that will be emitted as the physical file name.</param>
    /// <returns>Stable shard directory token derived from the imported asset alias stem.</returns>
    static string ResolveImportedAliasShardDirectory(string aliasFileStem) {
        if (string.IsNullOrWhiteSpace(aliasFileStem)) {
            throw new ArgumentException("Imported asset alias file stem must be provided.", nameof(aliasFileStem));
        }
        if (aliasFileStem.Length < 2 + ImportedAssetAliasShardLength) {
            throw new InvalidOperationException($"Imported asset alias file stem '{aliasFileStem}' is too short to derive a shard directory.");
        }

        return aliasFileStem.Substring(2, ImportedAssetAliasShardLength);
    }

    /// <summary>
    /// Resolves the shard directory for one generic long-path alias stem so dense `L` buckets do not require multi-sector ISO directory scans at runtime.
    /// </summary>
    /// <param name="aliasFileStem">Deterministic generic alias stem that will be emitted as the physical file name.</param>
    /// <returns>Stable shard directory token derived from the generic alias stem.</returns>
    static string ResolveLongPathAliasShardDirectory(string aliasFileStem) {
        if (string.IsNullOrWhiteSpace(aliasFileStem)) {
            throw new ArgumentException("Long-path alias file stem must be provided.", nameof(aliasFileStem));
        }
        if (aliasFileStem.Length < 1 + LongPathAliasShardLength) {
            throw new InvalidOperationException($"Long-path alias file stem '{aliasFileStem}' is too short to derive a shard directory.");
        }

        return aliasFileStem.Substring(1, LongPathAliasShardLength);
    }

    /// <summary>
    /// Resolves one logical file extension into an uppercase PS2-safe 3-character extension.
    /// </summary>
    /// <param name="extension">Logical extension without the leading dot.</param>
    /// <returns>Sanitized PS2-safe extension.</returns>
    static string ResolveExtension(string extension) {
        string sanitizedExtension = SanitizeToken(extension.TrimStart('.'));
        if (string.IsNullOrWhiteSpace(sanitizedExtension)) {
            return string.Empty;
        }
        if (sanitizedExtension.Length <= 3) {
            return sanitizedExtension;
        }

        return sanitizedExtension[..3];
    }

    /// <summary>
    /// Sanitizes one logical token into uppercase PS2-safe characters.
    /// </summary>
    /// <param name="token">Logical token to sanitize.</param>
    /// <returns>Uppercase token containing only letters, digits, or underscores.</returns>
    static string SanitizeToken(string token) {
        if (string.IsNullOrWhiteSpace(token)) {
            return "X";
        }

        char[] characters = token.ToUpperInvariant().ToCharArray();
        for (int index = 0; index < characters.Length; index++) {
            char currentCharacter = characters[index];
            bool allowedCharacter = currentCharacter is >= 'A' and <= 'Z'
                || currentCharacter is >= '0' and <= '9'
                || currentCharacter == '_';
            if (!allowedCharacter) {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    /// <summary>
    /// Computes one stable uppercase hexadecimal hash used to shorten oversized PS2 path tokens.
    /// </summary>
    /// <param name="token">Logical token that contributes to the hash.</param>
    /// <returns>Stable uppercase hexadecimal hash.</returns>
    static string ComputeHash(string token) {
        uint hash = 2166136261u;
        for (int index = 0; index < token.Length; index++) {
            hash ^= token[index];
            hash *= 16777619u;
        }

        return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns whether one logical path addresses a deep cooked scene asset whose full physical path must stay in the scene namespace.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <returns>True when the logical path targets a deep cooked scene asset; otherwise false.</returns>
    static bool IsDeepSceneLogicalPath(string normalizedLogicalPath) {
        if (string.IsNullOrWhiteSpace(normalizedLogicalPath)) {
            return false;
        }

        return normalizedLogicalPath.StartsWith("cooked/scenes/", StringComparison.OrdinalIgnoreCase)
            && normalizedLogicalPath.Count(character => character == '/') >= 3;
    }

    /// <summary>
    /// Returns whether one logical file name should collapse into the dedicated HEL asset namespace.
    /// </summary>
    /// <param name="fileName">Original logical file name including its extension.</param>
    /// <returns>True when the logical file name targets a HEL asset; otherwise false.</returns>
    static bool IsDeepHelLogicalPath(string fileName) {
        if (string.IsNullOrWhiteSpace(fileName)) {
            return false;
        }

        return string.Equals(ResolveResolvedExtension(fileName), "HEL", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether one logical path targets an extensionless imported cooked asset that should avoid the flat `COOKED\IMPORTED` namespace.
    /// </summary>
    /// <param name="normalizedLogicalPath">Normalized logical path using forward slashes.</param>
    /// <returns>True when the logical path targets an extensionless imported cooked asset; otherwise false.</returns>
    static bool IsExtensionlessImportedLogicalPath(string normalizedLogicalPath) {
        if (string.IsNullOrWhiteSpace(normalizedLogicalPath)) {
            return false;
        }

        if (!normalizedLogicalPath.StartsWith("cooked/imported/", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        int lastSlashIndex = normalizedLogicalPath.LastIndexOf('/');
        if (lastSlashIndex < 0 || lastSlashIndex == normalizedLogicalPath.Length - 1) {
            return false;
        }

        string fileName = normalizedLogicalPath[(lastSlashIndex + 1)..];
        return !fileName.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes the final rooted PS2 runtime path length for one disc-relative path.
    /// </summary>
    /// <param name="physicalRelativePath">Physical disc-relative path to measure.</param>
    /// <returns>Total rooted runtime path length including the leading slash and the trailing `;1` suffix.</returns>
    static int GetRuntimePhysicalPathLength(string physicalRelativePath) {
        if (string.IsNullOrWhiteSpace(physicalRelativePath)) {
            throw new ArgumentException("Physical relative path must be provided.", nameof(physicalRelativePath));
        }

        return physicalRelativePath.Replace('/', '\\').Length + PhysicalRuntimePathAdornmentLength;
    }
}

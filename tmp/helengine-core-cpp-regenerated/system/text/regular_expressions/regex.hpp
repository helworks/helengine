#pragma once

#include <cstdint>
#include <regex>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

/// <summary>
/// Represents managed regex option flags needed by the transpiled engine parser surface.
/// </summary>
enum class RegexOptions : uint32_t {
    None = 0,
    IgnoreCase = 1 << 0,
    Multiline = 1 << 1,
    Singleline = 1 << 2,
    Compiled = 1 << 3
};

/// <summary>
/// Combines two regex option values.
/// </summary>
/// <param name="left">Left option value.</param>
/// <param name="right">Right option value.</param>
/// <returns>The bitwise union of the supplied option values.</returns>
inline RegexOptions operator|(RegexOptions left, RegexOptions right) {
    return static_cast<RegexOptions>(static_cast<uint32_t>(left) | static_cast<uint32_t>(right));
}

/// <summary>
/// Tests whether the supplied option value contains all flags from the expected mask.
/// </summary>
/// <param name="value">Current option flags.</param>
/// <param name="expected">Mask to test.</param>
/// <returns><c>true</c> when all expected flags are present; otherwise, <c>false</c>.</returns>
inline bool HasRegexOption(RegexOptions value, RegexOptions expected) {
    return (static_cast<uint32_t>(value) & static_cast<uint32_t>(expected)) == static_cast<uint32_t>(expected);
}

/// <summary>
/// Represents one regex capture group value.
/// </summary>
class Group {
public:
    std::string Value;

    /// <summary>
    /// Initializes an empty group.
    /// </summary>
    Group()
        : Value() {
    }

    /// <summary>
    /// Initializes a group with the captured text.
    /// </summary>
    /// <param name="value">Captured text value.</param>
    explicit Group(std::string value)
        : Value(std::move(value)) {
    }
};

/// <summary>
/// Provides named lookup for captured regex groups.
/// </summary>
class GroupCollection {
    std::unordered_map<std::string, Group> groups;
    Group emptyGroup;

public:
    /// <summary>
    /// Initializes an empty group collection.
    /// </summary>
    GroupCollection()
        : groups(), emptyGroup() {
    }

    /// <summary>
    /// Stores one named group capture.
    /// </summary>
    /// <param name="name">Stable group name.</param>
    /// <param name="value">Captured group value.</param>
    void Set(const std::string& name, const std::string& value) {
        groups[name] = Group(value);
    }

    /// <summary>
    /// Resolves one named group capture.
    /// </summary>
    /// <param name="name">Group name to resolve.</param>
    /// <returns>A stable reference to the matching group when present; otherwise, the shared empty group.</returns>
    const Group& operator[](const std::string& name) const {
        std::unordered_map<std::string, Group>::const_iterator iterator = groups.find(name);
        if (iterator == groups.end()) {
            return emptyGroup;
        }

        return iterator->second;
    }
};

/// <summary>
/// Represents one regex match result.
/// </summary>
class Match {
public:
    GroupCollection Groups;
    bool Success;

    /// <summary>
    /// Initializes an empty unsuccessful match.
    /// </summary>
    Match()
        : Groups(), Success(false) {
    }
};

/// <summary>
/// Represents a materialized collection of regex matches.
/// </summary>
class MatchCollection {
    std::vector<Match> matches;

public:
    int32_t Count;

    /// <summary>
    /// Initializes an empty match collection.
    /// </summary>
    MatchCollection()
        : matches(), Count(0) {
    }

    /// <summary>
    /// Appends one match to the collection.
    /// </summary>
    /// <param name="match">Match to append.</param>
    void Add(const Match& match) {
        matches.push_back(match);
        Count = static_cast<int32_t>(matches.size());
    }

    /// <summary>
    /// Resolves one match by zero-based index.
    /// </summary>
    /// <param name="index">Zero-based match index.</param>
    /// <returns>The resolved match value.</returns>
    Match operator[](int32_t index) const {
        return matches[static_cast<std::size_t>(index)];
    }
};

/// <summary>
/// Provides a lightweight regex wrapper with named-group support for transpiled managed parsing code.
/// </summary>
class Regex {
    std::regex compiledPattern;
    std::vector<std::pair<std::string, int32_t>> namedGroupIndexes;

public:
    /// <summary>
    /// Initializes an empty regex value.
    /// </summary>
    Regex()
        : compiledPattern(), namedGroupIndexes() {
    }

    /// <summary>
    /// Initializes the regex from the supplied managed-style pattern and option flags.
    /// </summary>
    /// <param name="pattern">Managed-style regex pattern.</param>
    /// <param name="options">Managed-style option flags.</param>
    explicit Regex(const std::string& pattern, RegexOptions options = RegexOptions::None) {
        std::string normalizedPattern = NormalizePattern(pattern, options, namedGroupIndexes);
        compiledPattern = std::regex(normalizedPattern, BuildFlags(options));
    }

    /// <summary>
    /// Tests whether the pattern matches any part of the supplied input.
    /// </summary>
    /// <param name="input">Source text to inspect.</param>
    /// <returns><c>true</c> when the pattern matches; otherwise, <c>false</c>.</returns>
    bool IsMatch(const std::string& input) const {
        return std::regex_search(input, compiledPattern);
    }

    /// <summary>
    /// Returns the first match within the supplied input.
    /// </summary>
    /// <param name="input">Source text to inspect.</param>
    /// <returns>The first materialized match result.</returns>
    auto MatchOne(const std::string& input) const -> ::Match {
        std::smatch searchResult;
        if (!std::regex_search(input, searchResult, compiledPattern)) {
            return ::Match();
        }

        return CreateMatch(searchResult);
    }

    /// <summary>
    /// Returns the first match within the supplied input.
    /// </summary>
    /// <param name="input">Source text to inspect.</param>
    /// <returns>The first materialized match result.</returns>
    auto Match(const std::string& input) const -> ::Match {
        return MatchOne(input);
    }

    /// <summary>
    /// Returns every match within the supplied input.
    /// </summary>
    /// <param name="input">Source text to inspect.</param>
    /// <returns>The materialized match collection.</returns>
    MatchCollection Matches(const std::string& input) const {
        MatchCollection collection;

        for (std::sregex_iterator iterator(input.begin(), input.end(), compiledPattern), end; iterator != end; ++iterator) {
            collection.Add(CreateMatch(*iterator));
        }

        return collection;
    }

    /// <summary>
    /// Replaces every match in the supplied input with the replacement text.
    /// </summary>
    /// <param name="input">Source text to transform.</param>
    /// <param name="replacement">Replacement text applied to each match.</param>
    /// <returns>The transformed string.</returns>
    std::string Replace(const std::string& input, const std::string& replacement) const {
        return std::regex_replace(input, compiledPattern, replacement);
    }

private:
    /// <summary>
    /// Builds the native regex flags for the supplied managed option flags.
    /// </summary>
    /// <param name="options">Managed option flags.</param>
    /// <returns>The native regex flag set.</returns>
    static std::regex_constants::syntax_option_type BuildFlags(RegexOptions options) {
        std::regex_constants::syntax_option_type flags = std::regex_constants::ECMAScript;

        if (HasRegexOption(options, RegexOptions::IgnoreCase)) {
            flags |= std::regex_constants::icase;
        }

#if defined(__cpp_lib_regex) || defined(_LIBCPP_VERSION) || defined(__GLIBCXX__)
#ifdef __cpp_lib_regex
        if (HasRegexOption(options, RegexOptions::Multiline)) {
            flags |= std::regex_constants::multiline;
        }
#endif
#endif

        return flags;
    }

    /// <summary>
    /// Normalizes managed pattern syntax into a form supported by the native regex engine and records named capture indexes.
    /// </summary>
    /// <param name="pattern">Managed pattern to normalize.</param>
    /// <param name="options">Managed option flags.</param>
    /// <param name="namedIndexes">Collection that receives named capture indexes in evaluation order.</param>
    /// <returns>The normalized native regex pattern.</returns>
    static std::string NormalizePattern(
        const std::string& pattern,
        RegexOptions options,
        std::vector<std::pair<std::string, int32_t>>& namedIndexes) {
        std::string normalizedPattern;
        normalizedPattern.reserve(pattern.size());

        bool insideCharacterClass = false;
        int32_t captureIndex = 0;

        for (std::size_t index = 0; index < pattern.size(); index++) {
            char current = pattern[index];

            if (current == '\\' && index + 1 < pattern.size()) {
                normalizedPattern.push_back(current);
                normalizedPattern.push_back(pattern[++index]);
                continue;
            }

            if (current == '[') {
                insideCharacterClass = true;
                normalizedPattern.push_back(current);
                continue;
            }

            if (current == ']') {
                insideCharacterClass = false;
                normalizedPattern.push_back(current);
                continue;
            }

            if (!insideCharacterClass && current == '.' && HasRegexOption(options, RegexOptions::Singleline)) {
                normalizedPattern.append("[\\s\\S]");
                continue;
            }

            if (!insideCharacterClass && current == '(' && index + 2 < pattern.size() && pattern[index + 1] == '?') {
                if (pattern[index + 2] == ':') {
                    captureIndex++;
                    normalizedPattern.push_back('(');
                    index += 2;
                    continue;
                }

                if (pattern[index + 2] == '<') {
                    std::size_t nameEnd = pattern.find('>', index + 3);
                    if (nameEnd != std::string::npos) {
                        captureIndex++;
                        namedIndexes.emplace_back(pattern.substr(index + 3, nameEnd - (index + 3)), captureIndex);
                        normalizedPattern.push_back('(');
                        index = nameEnd;
                        continue;
                    }
                }
            }

            if (!insideCharacterClass && current == '(') {
                captureIndex++;
            }

            normalizedPattern.push_back(current);
        }

        return normalizedPattern;
    }

    /// <summary>
    /// Materializes one managed-style match from a native search result.
    /// </summary>
    /// <param name="searchResult">Native search result.</param>
    /// <returns>The materialized match value.</returns>
    auto CreateMatch(const std::smatch& searchResult) const -> ::Match {
        ::Match match;
        match.Success = searchResult.ready() && !searchResult.empty();

        if (!match.Success) {
            return match;
        }

        for (const std::pair<std::string, int32_t>& namedGroup : namedGroupIndexes) {
            if (namedGroup.second >= 0 &&
                namedGroup.second < static_cast<int32_t>(searchResult.size()) &&
                searchResult[static_cast<std::size_t>(namedGroup.second)].matched) {
                match.Groups.Set(namedGroup.first, searchResult[static_cast<std::size_t>(namedGroup.second)].str());
            }
        }

        return match;
    }
};

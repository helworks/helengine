#pragma once

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdint>
#include <stdexcept>
#include <string>
#include <type_traits>
#include <vector>

#include "array.hpp"

/// <summary>
/// Defines the comparison modes required by transpiled managed string helpers.
/// </summary>
enum class StringComparison {
    Ordinal,
    OrdinalIgnoreCase
};

enum class StringSplitOptions {
    None,
    RemoveEmptyEntries
};

/// <summary>
/// Provides lightweight managed-style static string helpers required by transpiled code paths.
/// </summary>
class String {
public:
    inline static const std::string Empty = std::string();

    /// <summary>
    /// Determines whether the supplied string is empty.
    /// </summary>
    /// <param name="value">String value to inspect.</param>
    /// <returns>True when the string contains no characters; otherwise false.</returns>
    static bool IsNullOrEmpty(const std::string& value) {
        return value.empty();
    }

    /// <summary>
    /// Determines whether the supplied string is null, empty, or consists only of whitespace characters.
    /// </summary>
    /// <param name="value">String value to inspect.</param>
    /// <returns>True when the string is empty or whitespace-only; otherwise false.</returns>
    static bool IsNullOrWhiteSpace(const std::string& value) {
        if (value.empty()) {
            return true;
        }

        for (unsigned char character : value) {
            if (!std::isspace(character)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two strings using the requested comparison mode.
    /// </summary>
    /// <param name="left">Left-hand string.</param>
    /// <param name="right">Right-hand string.</param>
    /// <param name="comparison">Comparison mode.</param>
    /// <returns>True when both strings are equal for the selected comparison.</returns>
    static bool Equals(const std::string& left, const std::string& right, StringComparison comparison = StringComparison::Ordinal) {
        if (comparison == StringComparison::OrdinalIgnoreCase) {
            return ToLowerInvariant(left) == ToLowerInvariant(right);
        }

        return left == right;
    }

    /// <summary>
    /// Determines whether a string starts with the supplied prefix.
    /// </summary>
    /// <param name="value">String to inspect.</param>
    /// <param name="prefix">Prefix to compare.</param>
    /// <param name="comparison">Comparison mode.</param>
    /// <returns>True when the string starts with the prefix; otherwise false.</returns>
    static bool StartsWith(const std::string& value, const std::string& prefix, StringComparison comparison = StringComparison::Ordinal) {
        if (prefix.size() > value.size()) {
            return false;
        }

        return Equals(value.substr(0, prefix.size()), prefix, comparison);
    }

    /// <summary>
    /// Determines whether a string starts with the supplied character.
    /// </summary>
    /// <param name="value">String to inspect.</param>
    /// <param name="prefix">Prefix character to compare.</param>
    /// <returns>True when the string starts with the character; otherwise false.</returns>
    static bool StartsWith(const std::string& value, char prefix) {
        return !value.empty() && value.front() == prefix;
    }

    /// <summary>
    /// Determines whether a string ends with the supplied suffix.
    /// </summary>
    /// <param name="value">String to inspect.</param>
    /// <param name="suffix">Suffix to compare.</param>
    /// <param name="comparison">Comparison mode.</param>
    /// <returns>True when the string ends with the suffix; otherwise false.</returns>
    static bool EndsWith(const std::string& value, const std::string& suffix, StringComparison comparison = StringComparison::Ordinal) {
        if (suffix.size() > value.size()) {
            return false;
        }

        return Equals(value.substr(value.size() - suffix.size()), suffix, comparison);
    }

    /// <summary>
    /// Determines whether a string ends with the supplied character.
    /// </summary>
    /// <param name="value">String to inspect.</param>
    /// <param name="suffix">Suffix character to compare.</param>
    /// <returns>True when the string ends with the character; otherwise false.</returns>
    static bool EndsWith(const std::string& value, char suffix) {
        return !value.empty() && value.back() == suffix;
    }

    /// <summary>
    /// Trims leading and trailing ASCII whitespace characters from a string.
    /// </summary>
    /// <param name="value">String to trim.</param>
    /// <returns>Trimmed string copy.</returns>
    static std::string Trim(const std::string& value) {
        size_t start = 0;
        while (start < value.size() && std::isspace(static_cast<unsigned char>(value[start]))) {
            start++;
        }

        size_t end = value.size();
        while (end > start && std::isspace(static_cast<unsigned char>(value[end - 1]))) {
            end--;
        }

        return value.substr(start, end - start);
    }

    static std::string TrimStart(const std::string& value) {
        size_t start = 0;
        while (start < value.size() && std::isspace(static_cast<unsigned char>(value[start]))) {
            start++;
        }

        return value.substr(start);
    }

    /// <summary>
    /// Extracts a substring from the specified start index to the end of the string.
    /// </summary>
    /// <param name="value">Source string.</param>
    /// <returns>Original string copy.</returns>
    static std::string Substring(const std::string& value) {
        return value;
    }

    /// <summary>
    /// Extracts a substring from the specified start index to the end of the string.
    /// </summary>
    /// <param name="value">Source string.</param>
    /// <param name="startIndex">Zero-based start index.</param>
    /// <returns>Substring copy.</returns>
    static std::string Substring(const std::string& value, int32_t startIndex) {
        if (startIndex < 0 || static_cast<size_t>(startIndex) > value.size()) {
            throw std::out_of_range("startIndex");
        }

        return value.substr(static_cast<size_t>(startIndex));
    }

    /// <summary>
    /// Extracts a substring with the specified length.
    /// </summary>
    /// <param name="value">Source string.</param>
    /// <param name="startIndex">Zero-based start index.</param>
    /// <param name="length">Requested substring length.</param>
    /// <returns>Substring copy.</returns>
    static std::string Substring(const std::string& value, int32_t startIndex, int32_t length) {
        if (startIndex < 0 || length < 0 || static_cast<size_t>(startIndex) > value.size()) {
            throw std::out_of_range("startIndex");
        }

        size_t safeStartIndex = static_cast<size_t>(startIndex);
        if (safeStartIndex + static_cast<size_t>(length) > value.size()) {
            throw std::out_of_range("length");
        }

        return value.substr(safeStartIndex, static_cast<size_t>(length));
    }

    /// <summary>
    /// Determines whether a character is an ASCII digit.
    /// </summary>
    /// <param name="value">Character to inspect.</param>
    /// <returns>True when the character is a digit; otherwise false.</returns>
    static bool IsDigit(char value) {
        return std::isdigit(static_cast<unsigned char>(value)) != 0;
    }

    /// <summary>
    /// Converts a string to lowercase using invariant ASCII casing rules.
    /// </summary>
    /// <param name="value">String to transform.</param>
    /// <returns>Lowercase string copy.</returns>
    static std::string ToLowerInvariant(const std::string& value) {
        std::string lowered = value;
        std::transform(lowered.begin(), lowered.end(), lowered.begin(), [](unsigned char character) {
            return static_cast<char>(std::tolower(character));
        });
        return lowered;
    }

    static char ToUpper(char value) {
        return static_cast<char>(std::toupper(static_cast<unsigned char>(value)));
    }

    static std::string Remove(const std::string& value, int32_t startIndex) {
        if (startIndex < 0 || static_cast<size_t>(startIndex) > value.size()) {
            throw std::out_of_range("startIndex");
        }

        return value.substr(0, static_cast<size_t>(startIndex));
    }

    static std::string Remove(const std::string& value, int32_t startIndex, int32_t count) {
        if (startIndex < 0 || count < 0 || static_cast<size_t>(startIndex) > value.size()) {
            throw std::out_of_range("startIndex");
        }

        size_t safeStartIndex = static_cast<size_t>(startIndex);
        size_t safeCount = static_cast<size_t>(count);
        if (safeStartIndex + safeCount > value.size()) {
            throw std::out_of_range("count");
        }

        return value.substr(0, safeStartIndex) + value.substr(safeStartIndex + safeCount);
    }

    static std::string Insert(const std::string& value, int32_t startIndex, const std::string& insertion) {
        if (startIndex < 0 || static_cast<size_t>(startIndex) > value.size()) {
            throw std::out_of_range("startIndex");
        }

        size_t safeStartIndex = static_cast<size_t>(startIndex);
        return value.substr(0, safeStartIndex) + insertion + value.substr(safeStartIndex);
    }

    /// <summary>
    /// Concatenates string values from a collection using the supplied separator.
    /// </summary>
    /// <typeparam name="TCollection">Iterable collection type that yields string-compatible values.</typeparam>
    /// <param name="separator">Separator inserted between adjacent values.</param>
    /// <param name="values">Collection to concatenate.</param>
    /// <returns>The concatenated string.</returns>
    template <typename TCollection>
    static std::string Join(const std::string& separator, const TCollection* values) {
        if (values == nullptr) {
            return std::string();
        }

        std::string result;
        bool isFirst = true;

        for (const auto& value : *values) {
            if (!isFirst) {
                result += separator;
            }

            result += value;
            isFirst = false;
        }

        return result;
    }

    template <typename... TValues>
    static std::string Join(const std::string& separator, const TValues&... values) {
        std::string result;
        bool isFirst = true;

        auto appendValue = [&](const auto& currentValue) {
            if (!isFirst) {
                result += separator;
            }

            result += ToJoinString(currentValue);
            isFirst = false;
        };

        (appendValue(values), ...);
        return result;
    }

    template <typename TCollection>
    static auto Join(const std::string& separator, const TCollection* values) -> decltype(values->begin(), values->end(), std::string()) {
        if (values == nullptr) {
            return std::string();
        }

        std::string result;
        bool isFirst = true;
        for (const auto& currentValue : *values) {
            if (!isFirst) {
                result += separator;
            }

            result += ToJoinString(currentValue);
            isFirst = false;
        }

        return result;
    }

    static std::string JoinArray(const std::string& separator, const Array<std::string>* values) {
        if (values == nullptr || values->Length <= 0) {
            return std::string();
        }

        std::string result;
        for (int32_t index = 0; index < values->Length; index++) {
            if (index > 0) {
                result += separator;
            }

            result += (*values)[index];
        }

        return result;
    }

    template <typename... TValues>
    static std::string Concat(const TValues&... values) {
        std::string result;
        auto appendValue = [&](const auto& currentValue) {
            result += ToJoinString(currentValue);
        };

        (appendValue(values), ...);
        return result;
    }

    static Array<std::string>* Split(const std::string& value, const Array<char>* separators, int32_t count, StringSplitOptions options) {
        std::vector<std::string> parts;
        size_t segmentStart = 0;
        int32_t remainingParts = count <= 0 ? INT32_MAX : count;

        while (segmentStart <= value.size()) {
            if (remainingParts == 1) {
                std::string finalPart = value.substr(segmentStart);
                if (!(options == StringSplitOptions::RemoveEmptyEntries && finalPart.empty())) {
                    parts.push_back(finalPart);
                }

                break;
            }

            size_t separatorIndex = FindNextSeparator(value, separators, segmentStart);
            std::string part = separatorIndex == std::string::npos
                ? value.substr(segmentStart)
                : value.substr(segmentStart, separatorIndex - segmentStart);

            if (!(options == StringSplitOptions::RemoveEmptyEntries && part.empty())) {
                parts.push_back(part);
                remainingParts--;
            }

            if (separatorIndex == std::string::npos) {
                break;
            }

            segmentStart = separatorIndex + 1;
        }

        Array<std::string>* result = new Array<std::string>(static_cast<int32_t>(parts.size()));
        for (int32_t index = 0; index < result->Length; index++) {
            (*result)[index] = parts[static_cast<size_t>(index)];
        }

        return result;
    }

private:
    static size_t FindNextSeparator(const std::string& value, const Array<char>* separators, size_t startIndex) {
        if (separators == nullptr || separators->Length <= 0) {
            return value.find_first_of(" \t\r\n", startIndex);
        }

        for (size_t index = startIndex; index < value.size(); index++) {
            for (int32_t separatorIndex = 0; separatorIndex < separators->Length; separatorIndex++) {
                if (value[index] == (*separators)[separatorIndex]) {
                    return index;
                }
            }
        }

        return std::string::npos;
    }

    static std::string ToJoinString(const std::string& value) {
        return value;
    }

    static std::string ToJoinString(const char* value) {
        return value == nullptr ? std::string() : std::string(value);
    }

    static std::string ToJoinString(char value) {
        return std::string(1, value);
    }

    template <typename TValue>
    static std::enable_if_t<std::is_arithmetic_v<TValue>, std::string> ToJoinString(const TValue& value) {
        if constexpr (std::is_same_v<TValue, bool>) {
            return value ? "True" : "False";
        }

        return std::to_string(value);
    }

    template <typename TValue>
    static std::string ToJoinString(const TValue& value) {
        return value;
    }
};

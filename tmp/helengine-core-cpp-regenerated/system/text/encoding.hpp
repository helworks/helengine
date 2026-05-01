#pragma once

#include <string>
#include "../../runtime/array.hpp"

/// <summary>
/// Represents the minimal managed Encoding surface required by transpiled UTF-8 oriented code paths.
/// </summary>
class Encoding {
public:
    /// <summary>
    /// Gets the shared UTF-8 encoding marker used by transpiled readers and writers.
    /// </summary>
    static const Encoding UTF8;

    /// <summary>
    /// Encodes a UTF-8 string into a managed byte-array wrapper.
    /// </summary>
    static Array<uint8_t>* GetBytes(const Encoding&, const std::string& value) {
        Array<uint8_t>* bytes = new Array<uint8_t>(static_cast<int32_t>(value.size()));
        for (int32_t index = 0; index < bytes->Length; index++) {
            (*bytes)[index] = static_cast<uint8_t>(value[static_cast<size_t>(index)]);
        }

        return bytes;
    }

    /// <summary>
    /// Decodes a managed byte-array wrapper as a UTF-8 string.
    /// </summary>
    static std::string GetString(const Encoding&, Array<uint8_t>* value) {
        if (value == nullptr || value->Length == 0) {
            return std::string();
        }

        return std::string(reinterpret_cast<const char*>(value->Data), static_cast<size_t>(value->Length));
    }
};

inline const Encoding Encoding::UTF8 = Encoding();

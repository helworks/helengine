#pragma once

#include <cstdint>
#include <cstring>

/// <summary>
/// Provides the minimal managed BitConverter surface required by transpiled serializer code.
/// </summary>
class BitConverter {
public:
    /// <summary>
    /// Reinterprets the supplied integer bits as a single-precision floating point value.
    /// </summary>
    static float Int32BitsToSingle(int32_t value) {
        float result = 0.0f;
        std::memcpy(&result, &value, sizeof(result));
        return result;
    }

    /// <summary>
    /// Reinterprets the supplied single-precision floating point value as a 32-bit integer bit pattern.
    /// </summary>
    static int32_t SingleToInt32Bits(float value) {
        int32_t result = 0;
        std::memcpy(&result, &value, sizeof(result));
        return result;
    }
};

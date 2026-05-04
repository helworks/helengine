#pragma once

#include "../../../runtime/array.hpp"

#include <cstdint>

class SHA256 {
public:
    static Array<uint8_t>* HashData(Array<uint8_t>* data) {
        Array<uint8_t>* hash = new Array<uint8_t>(32);
        uint64_t state = 1469598103934665603ull;

        if (data != nullptr) {
            for (int32_t index = 0; index < data->Length; index++) {
                state ^= (*data)[index];
                state *= 1099511628211ull;
            }
        }

        for (int32_t index = 0; index < hash->Length; index++) {
            state ^= (state >> 32);
            state *= 0x9E3779B185EBCA87ull + static_cast<uint64_t>(index);
            (*hash)[index] = static_cast<uint8_t>((state >> ((index % 8) * 8)) & 0xFF);
        }

        return hash;
    }
};
